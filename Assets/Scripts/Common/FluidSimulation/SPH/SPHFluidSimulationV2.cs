using Common.FluidSimulation.SPH;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Common.FluidSimulation
{
    public class SPHFluidSimulationV2 : MonoBehaviour
    {

        public Vector3Int Size;
        public float Spacing;
        public float Height;
        public int ParticleCount;
        public GameObject FluidObjectPrefab;

        private Entity m_PrefabEntity;

        void Start()
        {
            EntityManager eManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            

            //NativeArray<Entity> entities = new(ParticleCount, Allocator.Persistent);
            // convertion
            BlobAssetStore blobAssetStore = new BlobAssetStore();
            m_PrefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(FluidObjectPrefab,
                GameObjectConversionSettings.FromWorld(eManager.World, blobAssetStore));
            blobAssetStore.Dispose();

            eManager.AddComponent(m_PrefabEntity, typeof(SPHParticle));

            int particlesPerDimension = Mathf.CeilToInt(Mathf.Pow(ParticleCount, 1f / 3f));

            int counter = 0;
            while (counter < ParticleCount)
            {
                for (int x = 0; x < particlesPerDimension; x++)
                    for (int y = 0; y < particlesPerDimension; y++)
                        for (int z = 0; z < particlesPerDimension; z++)
                        {
                            float dimensions = 100;
                            float3 startPos = new float3(dimensions - 1, dimensions - 1, dimensions - 1) - new float3(x / 2f, y / 2f, z / 2f) - new float3(UnityEngine.Random.Range(0f, 0.01f), UnityEngine.Random.Range(0f, 0.01f), UnityEngine.Random.Range(0f, 0.01f));
                            Entity ent = eManager.Instantiate(m_PrefabEntity);
                            eManager.SetComponentData(ent, new Translation() {
                                Value = startPos
                            });
                            eManager.SetComponentData(m_PrefabEntity, new SPHParticle() {
                                velocity = float3.zero,
                                force = float3.zero,
                                density = -1,
                                pressure = 0f
                            });
                            if (++counter == ParticleCount)
                            {
                                return;
                            }
                        }
            }
            //entities.Dispose();
        }
    }

    struct SPHJobPopulateHashMap : IJob
    {
        [ReadOnly] public float cellSize;
        [ReadOnly] public int dimensions;
        [ReadOnly] public NativeArray<Translation> positions;
        [WriteOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.

        public void Execute()
        {
            for (int i = 0; i < positions.Length; i++)
            {
                hashGrid.Add(Hash(GetCell(positions[i].Value, cellSize), dimensions), i);
            }
        }

        public static int3 GetCell(float3 position, float cellSize)
        {
            return new int3((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(int3 cell, int dimensions)
        {
            return cell.x + dimensions * (cell.y + dimensions * cell.z);
        }
    }

    struct SPHJobFindNeighbors : IJobFor
    {
        [ReadOnly] public int dimensions;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float radiusSquared;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        //public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public NativeArray<Translation> readonlyLocations;
        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourTracker;
        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'

        public void Execute(int index)
        {
            neighbourTracker[index] = 0;
            var cell = GetCell(readonlyLocations[index].Value, cellSize);
            var cells = GetNearbyKeys(cell, readonlyLocations[index].Value);

            for (int j = 0; j < cells.Length; j++)
            {
                if (!hashGrid.ContainsKey(cells[j])) continue;
                var neighbourCell = hashGrid.GetValuesForKey(cells[j]);
                int counter = 0;
                foreach (var potentialNeighbour in neighbourCell)
                {
                    if (potentialNeighbour == index) continue;
                    if (Vector3.Magnitude(readonlyLocations[potentialNeighbour].Value - readonlyLocations[index].Value) < .5f) // Using squared length instead of magnitude for performance
                    {
                        neighbourList[index * maximumParticlesPerCell * 8 + neighbourTracker[index]++] = potentialNeighbour;
                        if (++counter == maximumParticlesPerCell) break;    // Prevent potential UB in neighbourList by only allowing maximumParticlesPerCell neighbours from one cell.
                    }
                }
            }
        }

        private NativeArray<int> GetNearbyKeys(int3 originIndex, Vector3 position)
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
                nearbyKeys[i] = Hash(new int3(nearbyBucketIndicesX[i], nearbyBucketIndicesY[i], nearbyBucketIndicesZ[i]), dimensions);
            }

            return nearbyKeys;
        }
        public static int3 GetCell(float3 position, float cellSize)
        {
            return new int3((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(int3 cell, int dimensions)
        {
            return cell.x + dimensions * (cell.y + dimensions * cell.z);
        }

    }

    struct SPHJobV2 : IJobFor
    {
        // solver parameters
        static readonly float3 Gravity = new(0, -9.81f, 0.0f);
        const float REST_DENS = 2f;  // rest density
        const float GAS_CONST = 2000f; // const for equation of state
        const float H = .5f;           // kernel radius
        const float H2 = H * H;        // radius^2 for optimization
        const float H3 = H2 * H;
        const float H4 = H3 * H;
        const float H5 = H4 * H;
        const float HR3 = 1 / (H * H2);
        const float HR6 = HR3 * HR3;
        const float HR9 = HR6 * HR3;
        const float MASS = 1f;       // assume all particles have the same mass
        const float MASS2 = MASS * MASS;
        const float VISC = .25f;       // viscosity constant
        const float DT = 0.008f;       // integration timestep
        const float BOUND_DAMPING = -0.5f;

        //public ComponentTypeHandle<SPHParticle> ParticleHandle;
        //public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float radiusSquared;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public int dimensions;

        [ReadOnly] public NativeArray<int> neighbourTracker;
        [ReadOnly] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Translation> translations;
        [ReadOnly] public NativeArray<SPHParticle> particles;

        [WriteOnly] public NativeArray<SPHParticle> writeParticles;
        [WriteOnly] public NativeArray<Translation> writeTranslations;

        public void Execute(int i)
        {
            float3 origin = translations[i].Value;
            float sum = 0f;
            for (int j = 0; j < neighbourTracker[i]; j++)
            {
                int neighbourIndex = neighbourList[i * maximumParticlesPerCell * 8 + j];
                float3 distanceSquared = origin - translations[neighbourIndex].Value;
                sum += MASS * KernelPoly6(distanceSquared);
            }

            var density = sum;
            var pressures = 0f;
            if (density < REST_DENS)
            {
                pressures = 0;
                density = 1 / REST_DENS;
            }
            else
            {
                pressures = GAS_CONST * (density - REST_DENS);
                density = 1 / density;
            }

            writeParticles[i] = new SPHParticle {
                velocity = particles[i].velocity,
                density = density,
                force = particles[i].force,
                pressure = pressures
            };
        }

        // Kernel by Müller et al.
        private float StdKernel(float distanceSquared)
        {
            // Doyub Kim
            float x = 1.0f - distanceSquared / H2;
            return 315f / (64f * math.PI * H3) * x * x * x;
        }

        float KernelPoly6(float3 r)
        {
            float sqrDiff = (H2 - math.dot(r, r));
            if (sqrDiff < 0)
                return 0;
            return 1.566681471061f * HR9 * sqrDiff * sqrDiff * sqrDiff;
        }
    }

    // ************************************************
    struct SPHForce : IJobFor
    {
        // solver parameters
        static readonly float3 Gravity = new(0, -9.81f, 0.0f);
        const float REST_DENS = 2f;  // rest density
        const float GAS_CONST = 2000f; // const for equation of state
        const float H = .5f;           // kernel radius
        const float H2 = H * H;        // radius^2 for optimization
        const float H3 = H2 * H;
        const float H4 = H3 * H;
        const float H5 = H4 * H;
        const float HR3 = 1 / (H * H2);
        const float HR6 = HR3 * HR3;
        const float HR9 = HR6 * HR3;
        const float MASS = 1f;       // assume all particles have the same mass
        const float MASS2 = MASS * MASS;
        const float VISC = .25f;       // viscosity constant
        const float DT = 0.008f;       // integration timestep
        const float BOUND_DAMPING = -0.5f;

        //public ComponentTypeHandle<SPHParticle> ParticleHandle;
        //public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float radiusSquared;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public int dimensions;

        [ReadOnly] public NativeArray<int> neighbourTracker;
        [ReadOnly] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Translation> translations;
        [ReadOnly] public NativeArray<SPHParticle> particles;

        [WriteOnly] public NativeArray<SPHParticle> writeParticles;
        [WriteOnly] public NativeArray<Translation> writeTranslations;

        public void Execute(int i)
        {
            var particleDensity2 = particles[i].density * particles[i].density;

            var force = float3.zero;
            for (int j = 0; j < neighbourTracker[i]; j++)
            {
                int neighbourIndex = neighbourList[i * maximumParticlesPerCell * 8 + j];
                float distance = math.distance(translations[i].Value, translations[neighbourIndex].Value);
                var direction = (translations[i].Value - translations[neighbourIndex].Value);
                // 7. Compute pressure gradient force (Doyub Kim page 136)
                force += (-0.5f) * MASS * (particles[i].pressure + particles[neighbourIndex].pressure * (particles[neighbourIndex].density)) * GradKernelSpiky(direction);   // Kim
                                                                                                                                                                             // 8. Compute the viscosity force
                float3 v = MASS * (particles[i].velocity - particles[neighbourIndex].velocity) * particles[neighbourIndex].density * LaplacianKernelViscosity(direction);    // Kim
                v *= VISC;
                force += v;
            }

            // Gravity
            force += Gravity;

            writeParticles[i] = new SPHParticle {
                velocity = particles[i].velocity,
                density = particles[i].density,
                force = force,
                pressure = particles[i].pressure
            };
        }

        float3 GradKernelSpiky(float3 r)
        {
            float mag = Vector3.Magnitude(r);
            float diff = (H - mag);
            if (diff < 0 || mag <= 0)
                return new float3(0, 0, 0);
            r *= (1 / mag);
            return -14.3239448783f * HR6 * diff * diff * r;
        }

        float LaplacianKernelViscosity(float3 r)
        {
            float mag = Vector3.Magnitude(r);
            float diff = H - mag;
            if (diff < 0 || mag <= 0)
                return 0;
            return 14.3239448783f * HR6 * diff;
        }

        private float SpikyKernelFirstDerivative(float distance)
        {
            float x = 1.0f - distance / H;
            return -45.0f / (math.PI * H4) * x * x;
        }

        // Doyub Kim page 130
        private float SpikyKernelSecondDerivative(float distance)
        {
            // Btw, it derives 'distance' not 'radius' (h)
            float x = 1.0f - distance / H;
            return 90f / (math.PI * H5) * x;
        }

        // // Doyub Kim page 130
        private float3 SpikyKernelGradient(float distance, float3 directionFromCenter)
        {
            return SpikyKernelFirstDerivative(distance) * directionFromCenter;
        }
    }

    // **********************************************
    struct SPHTranslate : IJobFor
    {
        // solver parameters
        static readonly float3 Gravity = new(0, -9.81f, 0.0f);
        const float REST_DENS = 2f;  // rest density
        const float GAS_CONST = 2000f; // const for equation of state
        const float H = .5f;           // kernel radius
        const float H2 = H * H;        // radius^2 for optimization
        const float H3 = H2 * H;
        const float H4 = H3 * H;
        const float H5 = H4 * H;
        const float MASS = 1f;       // assume all particles have the same mass
        const float MASS2 = MASS * MASS;
        const float VISC = .25f;       // viscosity constant
        const float DT = 0.008f;       // integration timestep
        const float BOUND_DAMPING = -0.5f;

        //public ComponentTypeHandle<SPHParticle> ParticleHandle;
        //public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float radiusSquared;
        [ReadOnly] public int maximumParticlesPerCell;
        [ReadOnly] public int dimensions;

        [ReadOnly] public NativeArray<int> neighbourTracker;
        [ReadOnly] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Translation> translations;
        [ReadOnly] public NativeArray<SPHParticle> particles;

        [WriteOnly] public NativeArray<SPHParticle> writeParticles;
        [WriteOnly] public NativeArray<Translation> writeTranslations;

        public void Execute(int i)
        {
            var pi = particles[i];
            var ti = translations[i];

            // forward Euler integration
            ti.Value += DT * pi.velocity;
            pi.velocity += DT * ((pi.force + pi.pressure) / MASS);


            // enforce boundary conditions
            // enforce boundary conditions
            if (ti.Value.x - float.Epsilon < 0.0f)
            {
                pi.velocity.x *= BOUND_DAMPING;
                ti.Value.x = float.Epsilon;
            }
            else if (ti.Value.x + float.Epsilon > dimensions - 1f)
            {
                pi.velocity.x *= BOUND_DAMPING;
                ti.Value.x = dimensions - 1 - float.Epsilon;
            }

            if (ti.Value.y - float.Epsilon < 0.0f)
            {
                pi.velocity.y *= BOUND_DAMPING;
                ti.Value.y = float.Epsilon;
            }
            else if (ti.Value.y + float.Epsilon > dimensions - 1f)
            {
                pi.velocity.y *= BOUND_DAMPING;
                ti.Value.y = dimensions - 1 - float.Epsilon;
            }

            if (ti.Value.z - float.Epsilon < 0.0f)
            {
                pi.velocity.z *= BOUND_DAMPING;
                ti.Value.z = float.Epsilon;
            }
            else if (ti.Value.z + float.Epsilon > dimensions - 1f)
            {
                pi.velocity.z *= BOUND_DAMPING;
                ti.Value.z = dimensions - 1 - float.Epsilon;
            }
            writeParticles[i] = new SPHParticle {
                velocity = pi.velocity,
                density = pi.density,
                force = pi.force,
                pressure = pi.pressure
            };
            writeTranslations[i] = new Translation { Value = ti.Value };
        }
    }

}
