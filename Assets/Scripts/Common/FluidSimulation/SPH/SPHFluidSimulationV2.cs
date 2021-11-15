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
                            float dimensions = 10;
                            Vector3 startPos = new Vector3(dimensions - 1, dimensions - 1, dimensions - 1) - new Vector3(x / 2f, y / 2f, z / 2f)  /*- new Vector3(Random.Range(0f, 0.01f), Random.Range(0f, 0.01f), Random.Range(0f, 0.01f))*/;
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

        public static Vector3Int GetCell(Vector3 position, float cellSize)
        {
            return new Vector3Int((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(Vector3Int cell, int dimensions)
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
            var ti = readonlyLocations[index];
            neighbourTracker[index] = 0;
            var cell = GetCell(ti.Value, cellSize);
            var cells = GetNearbyKeys(cell, ti.Value);
            for (int j = 0; j < cells.Length; j++)
            {
                //var pj = particles[i];

                if (!hashGrid.ContainsKey(cells[j])) continue;
                var neighbourCell = hashGrid.GetValuesForKey(cells[j]);
                int counter = 0;
                foreach (var potentialNeighbour in neighbourCell)
                {
                    if (potentialNeighbour == index) continue;
                    if (Vector3.SqrMagnitude(readonlyLocations[potentialNeighbour].Value - ti.Value) < radiusSquared) // Using squared length instead of magnitude for performance
                    {
                        neighbourList[index * maximumParticlesPerCell * 8 + neighbourTracker[index]++] = potentialNeighbour;
                        if (++counter == maximumParticlesPerCell) break;    // Prevent potential UB in neighbourList by only allowing maximumParticlesPerCell neighbours from one cell.
                    }
                }
            }
        }

        private NativeArray<int> GetNearbyKeys(Vector3Int originIndex, Vector3 position)
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
                nearbyKeys[i] = Hash(new Vector3Int(nearbyBucketIndicesX[i], nearbyBucketIndicesY[i], nearbyBucketIndicesZ[i]), dimensions);
            }

            return nearbyKeys;
        }
        private static Vector3Int GetCell(Vector3 position, float cellSize)
        {
            return new Vector3Int((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }
        private static int Hash(Vector3Int cell, int dimensions)
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
        const float H = 1f;           // kernel radius
        const float H2 = H * H;        // radius^2 for optimization
        const float H3 = H2 * H;
        const float H4 = H3 * H;
        const float H5 = H4 * H;
        const float MASS = 1f;        // assume all particles have the same mass
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

        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourTracker;
        [NativeDisableParallelForRestriction] public NativeArray<int> neighbourList; // Stores all neighbours of a particle aligned at 'particleIndex * maximumParticlesPerCell * 8'
        [ReadOnly] public NativeMultiHashMap<int, int> hashGrid; // Hash of cell to particle indices.
        [ReadOnly] public NativeArray<Translation> translations;
        [ReadOnly] public NativeArray<SPHParticle> particles;

        public NativeArray<SPHParticle> tResult;
        public NativeArray<SPHParticle> pResult;
        public NativeArray<int> indexResults;

        public void Execute(int i)
        {
            //NativeArray<SPHParticle> particles = batchInChunk.GetNativeArray(ParticleHandle);
            //NativeArray<Translation> translations = batchInChunk.GetNativeArray(TranslationHandle);

            // presure
            //for (int i = 0; i < particles.Length; i++)
            //{
                var pi = particles[i];
                var ti = translations[i];

                float density = 0;
                for (int j = 0; j < neighbourTracker[i]; j++)
                {
                    int neighbourIndex = neighbourList[i * maximumParticlesPerCell * 8 + j];
                    var r2 = Vector3.SqrMagnitude(ti.Value - translations[neighbourIndex].Value);
                    density += StdKernel(r2);
                }

                pi.density = density * MASS + 0.000001f;
                pi.pressure = GAS_CONST * (pi.density - REST_DENS);
                particles[i] = new SPHParticle {
                    velocity = pi.velocity,
                    density = pi.density,
                    force = pi.force,
                    pressure = pi.pressure
                };
            //}

            // forces
            // (int i = 0; i < particles.Length; i++)
            //{
                pi = particles[i];
                ti = translations[i];
                var denisty2 = pi.density * pi.density;
                pi.force = float3.zero;
                for (int j = 0; j < neighbourTracker[i]; j++)
                {
                    int neighbourIndex = neighbourList[i * maximumParticlesPerCell * 8 + j];
                    var pj = particles[neighbourIndex];
                    var tj = translations[neighbourIndex];
                    float distance = Vector3.Magnitude(ti.Value - tj.Value);
                    if (distance > 0.0f)
                    {
                        var dir = (ti.Value - tj.Value) / distance;
                        pi.force -= MASS2 * (pi.pressure / denisty2 + pj.pressure / (pj.density * pj.density)) * SpikyKernelGradient(distance, dir);   // Kim
                        pi.force += VISC * MASS2 * (pj.velocity - pi.velocity) / pj.density * SpikeyKernalSecond(distance);    // Kim
                    }
                }
                pi.force += Gravity;
                particles[i] = new SPHParticle {
                    velocity = pi.velocity,
                    density = pi.density,
                    force = pi.force,
                };
            //}

            //integerate
            //for (int i = 0; i < particles.Length; i++)
            //{
                var p = particles[i];
                var t = translations[i];

                // forward Euler integration
                p.velocity += DT * p.force / MASS;
                t.Value += DT * p.velocity;

                // enforce boundary conditions
                // enforce boundary conditions
                if (t.Value.x - float.Epsilon < 0.0f)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    t.Value.x = float.Epsilon;
                }
                else if (t.Value.x + float.Epsilon > dimensions - 1f)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    t.Value.x = dimensions - 1 - float.Epsilon;
                }

                if (t.Value.y - float.Epsilon < 0.0f)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    t.Value.y = float.Epsilon;
                }
                else if (t.Value.y + float.Epsilon > dimensions - 1f)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    t.Value.y = dimensions - 1 - float.Epsilon;
                }

                if (t.Value.z - float.Epsilon < 0.0f)
                {
                    p.velocity.z *= BOUND_DAMPING;
                    t.Value.z = float.Epsilon;
                }
                else if (t.Value.z + float.Epsilon > dimensions - 1f)
                {
                    p.velocity.z *= BOUND_DAMPING;
                    t.Value.z = dimensions - 1 - float.Epsilon;
                }
                particles[i] = new SPHParticle {
                    velocity = p.velocity,
                    density = p.density,
                    force = p.force,
                    pressure = p.pressure
                };
                translations[i] = new Translation { Value = t.Value };
            //}
        }

        // Kernel by Müller et al.
        private float StdKernel(float distanceSquared)
        {
            // Doyub Kim
            float x = 1.0f - distanceSquared / H2;
            return 315f / (64f * math.PI * H3) * x * x * x;
        }

        private float SpikyKernalFirst(float dist)
        {
            float x = 1.0f - dist / H;
            return -45.0f / (math.PI * H4) * x * x;
        }

        private float SpikeyKernalSecond(float dist)
        {
            float x = 1.0f - dist / H;
            return 90f / (math.PI * H5) * x;
        }


        // // Doyub Kim page 130
        private float3 SpikyKernelGradient(float distance, float3 directionFromCenter)
        {
            return SpikyKernalFirst(distance) * directionFromCenter;
        }

        private NativeArray<int> GetNearbyKeys(Vector3Int originIndex, Vector3 position)
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
                nearbyKeys[i] = Hash(new Vector3Int(nearbyBucketIndicesX[i], nearbyBucketIndicesY[i], nearbyBucketIndicesZ[i]), dimensions);
            }

            return nearbyKeys;
        }

        public static Vector3Int GetCell(Vector3 position, float cellSize)
        {
            return new Vector3Int((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(Vector3Int cell, int dimensions)
        {
            return cell.x + dimensions * (cell.y + dimensions * cell.z);
        }
    }
}
