using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Mostly uses https://github.com/Gornhoth/Unity-Smoothed-Particle-Hydrodynamics for help. I originally tried to base off of their repo
/// and https://github.com/MangoSister/SPHFluid, https://lucasschuermann.com/writing/implementing-sph-in-2d repos and create my somewhat own system.
/// However after many hours of failure and not being able to figure out what I was doing wrong I decided to directly take from them and just start
/// from there onto my own. Hopefully I can learn from that better. 
/// 
/// I "think" what I did was change the Delta Time to be too fast? I kept running into issues with pretty much every iteration i tried where
/// collisions causes particles to basically explode into insane speeds. I am not 100% sure but turning the dt up in this version gave me a very 
/// similiar result.
/// 
/// Uses GameObject prefab converted to Entity to display particles
/// </summary>

[DisableAutoCreation]
public class SPHSystem : SystemBase
{
    public float radius = 1f;

    public int dimensions = 50;
    public int numberOfParticles = 10000;
    public int maximumParticlesPerCell = 18; // Optimisation rule of thumb: (numberOfParticles^(1/3)) * 0.8
    public float mass = 1f;
    public float gasConstant = 2000.0f;
    public float restDensity = 2f;
    public float viscosityCoefficient = 0.25f;
    public float3 gravity = new(0.0f, -9.81f, 0.0f);
    public float damping = -0.5f;
    public float dt = 0.0008f;

    public Mesh particleMesh;
    public float particleRenderSize = 40f;
    public Material material;

    [Tooltip("The absolute accumulated simulation steps")]
    public int elapsedSimulationSteps;

    private NativeArray<Particle> m_Particles;
    private NativeMultiHashMap<int, int> m_HashGrid;
    private NativeArray<int> m_NeighbourTracker;
    private NativeArray<int> m_NeighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
    private NativeArray<float> m_Densities;
    private NativeArray<float> m_Pressures;
    private NativeArray<float3> m_Forces;
    private NativeArray<float3> m_Velocities;

    private float m_Radius2;
    private float m_Radius3;
    private float m_Radius4;
    private float m_Radius5;
    private float m_Mass2;

    private ComputeBuffer _particleColorPositionBuffer;
    private ComputeBuffer _argsBuffer;
    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private readonly int m_BatchCount = 1;

    [StructLayout(LayoutKind.Sequential, Size = sizeof(float) * 3 + sizeof(float) * 4)]
    private struct Particle : IComponentData
    {
        public float3 Position;
        public float4 Color;
    }

    protected override void OnCreate()
    {
        Application.targetFrameRate = -1;
        //var stuff = EntityManager.GetSharedComponentData<RenderMesh>(GetSingletonEntity<ParticlePrefabTag>());
        //particleMesh = stuff.mesh;
        //material = stuff.material;
        m_Radius2 = radius * radius;
        m_Radius3 = m_Radius2 * radius;
        m_Radius4 = m_Radius3 * radius;
        m_Radius5 = m_Radius4 * radius;
        m_Mass2 = mass * mass;

        RespawnParticles();
        InitNeighbourHashing();
        //InitComputeBuffers();
    }

    protected override void OnStopRunning()
    {
        EntityManager.CompleteAllJobs();
        ReleaseNative();
    }

    private void ReleaseNative()
    {
        //_particles.Dispose();
        m_HashGrid.Dispose();
        m_NeighbourList.Dispose();
        m_NeighbourTracker.Dispose();
        m_Densities.Dispose();
        m_Pressures.Dispose();
        m_Forces.Dispose();
        m_Velocities.Dispose();

        //_particleColorPositionBuffer.Dispose();
        //_argsBuffer.Dispose();
    }

    #region Initialisation
    private void RespawnParticles()
    {
        //_particles = new NativeArray<Particle>(numberOfParticles, Allocator.Persistent);
        m_Densities = new NativeArray<float>(numberOfParticles, Allocator.Persistent);
        m_Pressures = new NativeArray<float>(numberOfParticles, Allocator.Persistent);
        m_Forces = new NativeArray<float3>(numberOfParticles, Allocator.Persistent);
        m_Velocities = new NativeArray<float3>(numberOfParticles, Allocator.Persistent);

        int particlesPerDimension = (int)math.ceil(math.pow(numberOfParticles, 1f / 3f));

        EntityManager eManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // convertion
        BlobAssetStore blobAssetStore = new();
        Entity m_PrefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(SPHSystemController.ParticlePrefab,
            GameObjectConversionSettings.FromWorld(eManager.World, blobAssetStore));
        blobAssetStore.Dispose();

        eManager.AddComponent(m_PrefabEntity, typeof(Particle));

        int counter = 0;
        while (counter < numberOfParticles)
        {
            for (int x = 0; x < particlesPerDimension; x++)
                for (int y = 0; y < particlesPerDimension; y++)
                    for (int z = 0; z < particlesPerDimension; z++)
                    {
                        float3 startPos = new float3(dimensions - 1, dimensions - 1, dimensions - 1) - new float3(x / 2f, y / 2f, z / 2f)  /*- new Vector3(Random.Range(0f, 0.01f), Random.Range(0f, 0.01f), Random.Range(0f, 0.01f))*/;
                        Entity ent = eManager.Instantiate(m_PrefabEntity);
                        eManager.SetComponentData(ent, new Particle()
                        {
                            Position = startPos,
                            Color = new float4(1, 1, 1, 1)
                        });
                        // _particles[counter] = particle;
                        m_Densities[counter] = -1f;
                        m_Pressures[counter] = 0.0f;
                        m_Forces[counter] = float3.zero;
                        m_Velocities[counter] = float3.zero;
                        if (++counter == numberOfParticles)
                        {
                            return;
                        }
                    }
        }

    }

    private void InitNeighbourHashing()
    {
        m_NeighbourList = new NativeArray<int>(numberOfParticles * maximumParticlesPerCell * 8, Allocator.Persistent); // 8 because we consider 8 cells
        m_NeighbourTracker = new NativeArray<int>(numberOfParticles, Allocator.Persistent);
        SpatialHashing.CellSize = radius * 2; // Setting cell-size h to particle diameter.
        SpatialHashing.Dimensions = dimensions;
        m_HashGrid = new NativeMultiHashMap<int, int>(numberOfParticles, Allocator.Persistent);
    }

    void InitComputeBuffers()
    {
        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint) numberOfParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        _particleColorPositionBuffer = new ComputeBuffer(numberOfParticles, sizeof(float) * (3 + 4));
        _particleColorPositionBuffer.SetData(m_Particles);
    }

    #endregion

    [BurstCompile]
    private struct RecalculateHashGrid : IJobParallelFor
    {
        public NativeMultiHashMap<int, int>.ParallelWriter hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Particle> particles;

        [ReadOnly] public int dimensions;
        [ReadOnly] public float cellSize;

        public void Execute(int index)
        {
            hashGrid.Add(SpatialHashing.Hash(SpatialHashing.GetCell(particles[index].Position, cellSize), dimensions), index);
        }
    }

    [BurstCompile]
    private struct BuildNeighbourList : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourTracker;
        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Particle> particles;

        [ReadOnly] public float cellSize;
        [ReadOnly] public float radiusSquared;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public int dimensions;

        public void Execute(int index)
        {
            neighbourTracker[index] = 0;
            var cell = SpatialHashing.GetCell(particles[index].Position, cellSize);
            var cells = GetNearbyKeys(cell, particles[index].Position);

            for (int j = 0; j < cells.Length; j++)
            {
                if (!hashGrid.ContainsKey(cells[j])) continue;
                var neighbourCell = hashGrid.GetValuesForKey(cells[j]);
                int counter = 0;
                foreach (var potentialNeighbour in neighbourCell)
                {
                    if (potentialNeighbour == index) continue;
                    if (math.distancesq(particles[potentialNeighbour].Position, particles[index].Position) < radiusSquared) // Using squared length instead of magnitude for performance
                    {
                        neighbourList[index * maximumParticlesPerCell * 8 + neighbourTracker[index]++] = potentialNeighbour;
                        if (++counter == maximumParticlesPerCell) break;    // Prevent potential UB in neighbourList by only allowing maximumParticlesPerCell neighbours from one cell.
                    }
                }
            }
        }

        private NativeArray<int> GetNearbyKeys(int3 originIndex, float3 position)
        {
            NativeArray<int> nearbyBucketIndicesX = new NativeArray<int>(8, Allocator.Temp);
            NativeArray<int> nearbyBucketIndicesY = new NativeArray<int>(8, Allocator.Temp);
            NativeArray<int> nearbyBucketIndicesZ = new NativeArray<int>(8, Allocator.Temp);
            for (int i = 0; i < 8; i++)
            {
                nearbyBucketIndicesX[i] = originIndex.x;
                nearbyBucketIndicesY[i] = originIndex.y;
                nearbyBucketIndicesZ[i] = originIndex.z;
            }

            if ((originIndex.x + 0.5f) * cellSize <= position.x)
            {
                nearbyBucketIndicesX[4] += 1;
                nearbyBucketIndicesX[5] += 1;
                nearbyBucketIndicesX[6] += 1;
                nearbyBucketIndicesX[7] += 1;
            }
            else
            {
                nearbyBucketIndicesX[4] -= 1;
                nearbyBucketIndicesX[5] -= 1;
                nearbyBucketIndicesX[6] -= 1;
                nearbyBucketIndicesX[7] -= 1;
            }

            if ((originIndex.y + 0.5f) * cellSize <= position.y)
            {
                nearbyBucketIndicesY[2] += 1;
                nearbyBucketIndicesY[3] += 1;
                nearbyBucketIndicesY[6] += 1;
                nearbyBucketIndicesY[7] += 1;
            }
            else
            {
                nearbyBucketIndicesY[2] -= 1;
                nearbyBucketIndicesY[3] -= 1;
                nearbyBucketIndicesY[6] -= 1;
                nearbyBucketIndicesY[7] -= 1;
            }

            if ((originIndex.z + 0.5f) * cellSize <= position.z)
            {
                nearbyBucketIndicesZ[1] += 1;
                nearbyBucketIndicesZ[3] += 1;
                nearbyBucketIndicesZ[5] += 1;
                nearbyBucketIndicesZ[7] += 1;
            }
            else
            {
                nearbyBucketIndicesZ[1] -= 1;
                nearbyBucketIndicesZ[3] -= 1;
                nearbyBucketIndicesZ[5] -= 1;
                nearbyBucketIndicesZ[7] -= 1;
            }

            NativeArray<int> nearbyKeys = new NativeArray<int>(8, Allocator.Temp);
            for (int i = 0; i < 8; i++)
            {
                nearbyKeys[i] = SpatialHashing.Hash(new int3(nearbyBucketIndicesX[i], nearbyBucketIndicesY[i], nearbyBucketIndicesZ[i]), dimensions);
            }

            return nearbyKeys;
        }
    }

    [BurstCompile]
    private struct ComputeDensityPressure : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> neighbourTracker;
        [ReadOnly] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeArray<Particle> particles;
        [NativeDisableParallelForRestriction] public NativeArray<float> densities;
        [NativeDisableParallelForRestriction] public NativeArray<float> pressures;

        [ReadOnly] public float mass;
        [ReadOnly] public float gasConstant;
        [ReadOnly] public float restDensity;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public float radius2;
        [ReadOnly] public float radius3;

        public void Execute(int index)
        {
            // Doyub Kim 121, 122, 123
            // 5. Compute densities
            float3 origin = particles[index].Position;
            float sum = 0f;
            for (int j = 0; j < neighbourTracker[index]; j++)
            {
                int neighbourIndex = neighbourList[index * maximumParticlesPerCell * 8 + j];
                float distanceSquared = math.distancesq(origin, particles[neighbourIndex].Position);
                sum += StdKernel(distanceSquared);
            }

            densities[index] = sum * mass + 0.000001f;

            // 6. Compute pressure based on density
            pressures[index] = gasConstant * (densities[index] - restDensity); // as described in Müller et al Equation 12
        }

        // Kernel by Müller et al.
        private float StdKernel(float distanceSquared)
        {
            // Doyub Kim
            float x = 1.0f - distanceSquared / radius2;
            return 315f / (64f * math.PI * radius3) * x * x * x;
        }
    }

    [BurstCompile]
    private struct ComputeForces : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> neighbourTracker;
        [ReadOnly] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeArray<Particle> particles;
        [NativeDisableParallelForRestriction] public NativeArray<float3> forces;
        [ReadOnly] public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> densities;
        [ReadOnly] public NativeArray<float> pressures;

        [ReadOnly] public float mass2;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public float viscosityCoefficient;
        [ReadOnly] public float3 gravity;
        [ReadOnly] public float radius;
        [ReadOnly] public float radius4;
        [ReadOnly] public float radius5;

        public void Execute(int index)
        {
            forces[index] = float3.zero;
            var particleDensity2 = densities[index] * densities[index];
            for (int j = 0; j < neighbourTracker[index]; j++)
            {
                int neighbourIndex = neighbourList[index * maximumParticlesPerCell * 8 + j];
                float distance = math.distance(particles[index].Position, particles[neighbourIndex].Position);
                if (distance > 0.0f)
                {
                    var direction = (particles[index].Position - particles[neighbourIndex].Position) / distance;
                    // 7. Compute pressure gradient force (Doyub Kim page 136)
                    forces[index] -= mass2 * (pressures[index] / particleDensity2 + pressures[neighbourIndex] / (densities[neighbourIndex] * densities[neighbourIndex])) * SpikyKernelGradient(distance, direction);   // Kim
                    // 8. Compute the viscosity force
                    forces[index] += viscosityCoefficient * mass2 * (velocities[neighbourIndex] - velocities[index]) / densities[neighbourIndex] * SpikyKernelSecondDerivative(distance);    // Kim
                }
            }

            // Gravity
            forces[index] += gravity;
        }

        // Doyub Kim page 130
        private float SpikyKernelFirstDerivative(float distance)
        {
            float x = 1.0f - distance / radius;
            return -45.0f / (math.PI * radius4) * x * x;
        }

        // Doyub Kim page 130
        private float SpikyKernelSecondDerivative(float distance)
        {
            // Btw, it derives 'distance' not 'radius' (h)
            float x = 1.0f - distance / radius;
            return 90f / (math.PI * radius5) * x;
        }

        // // Doyub Kim page 130
        private float3 SpikyKernelGradient(float distance, float3 directionFromCenter)
        {
            return SpikyKernelFirstDerivative(distance) * directionFromCenter;
        }
    }

    [BurstCompile]
    private struct Integrate : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<Particle> particles;
        [ReadOnly] public NativeArray<float3> forces;
        [NativeDisableParallelForRestriction] public NativeArray<float3> velocities;

        [ReadOnly] public float mass;
        [ReadOnly] public float damping;
        [ReadOnly] public float dt;
        [ReadOnly] public int dimensions;

        public void Execute(int index)
        {
            var particle = particles[index];
            // forward Euler integration
            velocities[index] += dt * forces[index] / mass;
            particle.Position += dt * velocities[index];
            particles[index] = particle;

            particle = particles[index];
            var velocity = velocities[index];

            // enforce boundary conditions
            if (particles[index].Position.x - float.Epsilon < 0.0f)
            {
                velocity.x *= damping;
                particle.Position.x = float.Epsilon;
            }
            else if (particles[index].Position.x + float.Epsilon > dimensions - 1f)
            {
                velocity.x *= damping;
                particle.Position.x = dimensions - 1 - float.Epsilon;
            }

            if (particles[index].Position.y - float.Epsilon < 0.0f)
            {
                velocity.y *= damping;
                particle.Position.y = float.Epsilon;
            }
            else if (particles[index].Position.y + float.Epsilon > dimensions - 1f)
            {
                velocity.y *= damping;
                particle.Position.y = dimensions - 1 - float.Epsilon;
            }

            if (particles[index].Position.z - float.Epsilon < 0.0f)
            {
                velocity.z *= damping;
                particle.Position.z = float.Epsilon;
            }
            else if (particles[index].Position.z + float.Epsilon > dimensions - 1f)
            {
                velocity.z *= damping;
                particle.Position.z = dimensions - 1 - float.Epsilon;
            }

            velocities[index] = velocity;
            particles[index] = particle;
        }
    }

    protected override void OnUpdate()
    {
        m_Particles = this.GetEntityQuery(typeof(Particle)).ToComponentDataArray<Particle>(Allocator.Persistent);
        // Calculate hash of all particles and build neighboring list.
        // 1. Clear HashGrid
        m_HashGrid.Clear();
        // 2. Recalculate hashes of each particle.
        RecalculateHashGrid recalculateHashGridJob = new RecalculateHashGrid
        {
            particles = m_Particles,
            hashGrid = m_HashGrid.AsParallelWriter(),
            dimensions = SpatialHashing.Dimensions,
            cellSize = SpatialHashing.CellSize
        };
        JobHandle fillHashGridJobHandle = recalculateHashGridJob.Schedule(numberOfParticles, m_BatchCount);
        fillHashGridJobHandle.Complete();
        // 3. For each particle go through all their 8 neighbouring cells.
        //    Check each particle in those neighbouring cells for interference radius r and store the interfering ones inside the particles neighbour list.
        BuildNeighbourList buildNeighbourListJob = new BuildNeighbourList
        {
            particles = m_Particles,
            hashGrid = m_HashGrid,
            dimensions = SpatialHashing.Dimensions,
            cellSize = SpatialHashing.CellSize,
            radiusSquared = radius * radius,
            maximumParticlesPerCell = maximumParticlesPerCell,
            neighbourList = m_NeighbourList,
            neighbourTracker = m_NeighbourTracker
        };
        JobHandle buildNeighbourListJobHandle = buildNeighbourListJob.Schedule(numberOfParticles, m_BatchCount);
        buildNeighbourListJobHandle.Complete();
        // 4. The Neighbouring-list should be n-particles big, each index containing a list of each particles neighbours in radius r.

        // 5. Compute density pressure
        ComputeDensityPressure computeDensityPressureJob = new ComputeDensityPressure
        {
            neighbourTracker = m_NeighbourTracker,
            neighbourList = m_NeighbourList,
            particles = m_Particles,
            densities = m_Densities,
            pressures = m_Pressures,
            mass = mass,
            gasConstant = gasConstant,
            restDensity = restDensity,
            maximumParticlesPerCell = maximumParticlesPerCell,
            radius2 = m_Radius2,
            radius3 = m_Radius3
        };
        JobHandle computeDensityPressureJobHandle = computeDensityPressureJob.Schedule(numberOfParticles, m_BatchCount);
        computeDensityPressureJobHandle.Complete();

        ComputeForces computeForcesJob = new ComputeForces
        {
            neighbourTracker = m_NeighbourTracker,
            neighbourList = m_NeighbourList,
            particles = m_Particles,
            forces = m_Forces,
            velocities = m_Velocities,
            densities = m_Densities,
            pressures = m_Pressures,
            mass2 = m_Mass2,
            maximumParticlesPerCell = maximumParticlesPerCell,
            viscosityCoefficient = viscosityCoefficient,
            gravity = gravity,
            radius = radius,
            radius4 = m_Radius4,
            radius5 = m_Radius5
        };
        JobHandle computeForcesJobHandle = computeForcesJob.Schedule(numberOfParticles, m_BatchCount);
        computeForcesJobHandle.Complete();

        Integrate integrateJob = new Integrate
        {
            particles = m_Particles,
            forces = m_Forces,
            velocities = m_Velocities,
            mass = mass,
            damping = damping,
            dt = dt,
            dimensions = dimensions
        };
        JobHandle integrateJobHandle = integrateJob.Schedule(numberOfParticles, m_BatchCount);
        integrateJobHandle.Complete();

        //_particleColorPositionBuffer.SetData(_particles);
        //material.SetFloat(SizeProperty, particleRenderSize);
        //material.SetBuffer(ParticlesBufferProperty, _particleColorPositionBuffer);
        //Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), _argsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.On);
        elapsedSimulationSteps++;

        this.GetEntityQuery(typeof(Particle)).CopyFromComponentDataArray(m_Particles);

        JobHandle dummyHandle = new();
        JobHandle updateTranslationsJob = Entities.WithAll<Particle, Translation>().ForEach((ref Particle particle, ref Translation translation) =>
        {
            translation.Value = particle.Position;
        }).Schedule(dummyHandle);
        updateTranslationsJob.Complete();

        m_Particles.Dispose();
    }

    public static class SpatialHashing
    {
        public static float CellSize = 1f;
        public static int Dimensions = 10;

        public static int3 GetCell(float3 position, float cellSize)
        {
            return new int3((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(int3 cell, int dimensions)
        {
            return cell.x + dimensions * (cell.y + dimensions * cell.z);
        }
    }



}

