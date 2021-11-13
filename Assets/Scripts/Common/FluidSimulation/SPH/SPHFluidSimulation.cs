using Common.FluidSimulation.SPH;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace Common.FluidSimulation
{
    public class SPHFluidSimulation : MonoBehaviour
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
            eManager.SetComponentData(m_PrefabEntity, new SPHParticle() {
                velocity = new float3(1, 0, 0),
                force = float3.zero,
                density = 0,
            });
            

            int x = 0, y = 0, z = 0;
            for (int i = 0; i < ParticleCount; i++)
            {
                Entity ent = eManager.Instantiate(m_PrefabEntity);
                eManager.SetComponentData(ent, new Translation() {
                    Value = new(x * Spacing, y * Spacing + Height, z * Spacing)
                });
                
                x++;
                if (x > Size.x)
                {
                    x = 0;
                    z++;
                    if (z > Size.z)
                    {
                        z = 0;
                        y++;
                    }
                }
            }

            //entities.Dispose();
        }

        void Update()
        {
            ComputeDensityPresure();
            ComputeForces();
            Integerate();
        }

        void Integerate()
        {
        }

        void ComputeDensityPresure()
        {
        }

        void ComputeForces()
        {
        }


    }

    struct SPHJob : IJobEntityBatch
    {
        // solver parameters
        static readonly float3 G = new(0f, -10f, 0f);   // external (gravitational) forces
        const float REST_DENS = 300f;  // rest density
        const float GAS_CONST = 2000f; // const for equation of state
        const float H = 1f;           // kernel radius
        const float HSQ = H * H;        // radius^2 for optimization
        const float MASS = 2.5f;        // assume all particles have the same mass
        const float VISC = 200f;       // viscosity constant
        const float DT = 0.007f;       // integration timestep

        // smoothing kernels defined in Müller and their gradients
        // adapted to 2D per "SPH Based Shallow Water Simulation" by Solenthaler et al.
        static readonly float POLY6 = 4f / (math.PI * math.pow(H, 8f));
        static readonly float SPIKY_GRAD = -10f / (math.PI * math.pow(H, 5f));
        static readonly float VISC_LAP = 40f / (math.PI * math.pow(H, 5f));

        // simulation parameters
        const float EPS = H; // boundary epsilon
        const float BOUND_DAMPING = -0.5f;

        [ReadOnly] public float VIEW_WIDTH;
        [ReadOnly] public float VIEW_HEIGHT;
        [ReadOnly] public float VIEW_DEPTH;

        public ComponentTypeHandle<SPHParticle> ParticleHandle;
        public ComponentTypeHandle<Translation> TranslationHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            NativeArray<SPHParticle> particles = batchInChunk.GetNativeArray(ParticleHandle);
            NativeArray<Translation> translations = batchInChunk.GetNativeArray(TranslationHandle);
            

            // presure
            for (int i = 0; i < particles.Length; i++)
            {
                var pi = particles[i];
                var ti = translations[i];

                pi.density = 0;
                for (int j = 0; j < particles.Length; j++)
                {
                    var pj = particles[j];
                    var tj = translations[j];
                    float r2 = math.distancesq(tj.Value, ti.Value);
                    if (r2 < HSQ)
                    {
                        pi.density += MASS * POLY6 * math.pow(HSQ - r2, 3f);
                    }
                }
                pi.pressure = GAS_CONST * (pi.density - REST_DENS);
                particles[i] = new SPHParticle {
                    velocity = pi.velocity,
                    density = pi.density,
                    force = pi.force,
                    pressure = pi.pressure
                };
            }

            // forces
            for (int i = 0; i < particles.Length; i++)
            {
                float3 fpress = float3.zero;
                float3 fvisc = float3.zero;
                var pi = particles[i];
                var ti = translations[i];
                for (int j = 0; j < particles.Length; j++)
                {
                    if (i == j) continue;

                    var pj = particles[j];
                    var tj = translations[j];

                    float3 rij = tj.Value - ti.Value;
                    float r = math.distance(tj.Value, ti.Value);
                    if (r < H)
                    {
                        //var dir = rij / r;
                        //fpress -= (MASS * MASS) * (pi.pressure / (pi.density * pi.density) + pj.pressure / (pj.density * pj.density))
                        //    * (SpikyKernalFirst(r) * dir);
                        //
                        //fvisc += VISC * (MASS * MASS) * (pj.velocity - pi.velocity) / pj.density * SpikeyKernalSecond(r);

                        fpress += -math.normalize(rij)
                            * MASS * (pi.pressure + pj.pressure) / (2f * pj.density)
                            * SPIKY_GRAD * math.pow(H - r, 3);
                        
                        fvisc += VISC * MASS * (pj.velocity - pi.velocity) / pj.density * VISC_LAP * (H - r);
                    }
                }
                float3 fgrav = G * MASS / pi.density;
                pi.force = fpress + fvisc + fgrav;
                particles[i] = new SPHParticle {
                    velocity = pi.velocity,
                    density = pi.density,
                    force = pi.force,
                    pressure = pi.pressure
                };
            }

            //integerate
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];
                var t = translations[i];
                
                // forward Euler integration
                p.velocity += DT * p.force / p.density;
                t.Value += DT * p.velocity;

                // enforce boundary conditions
                if (t.Value.x - EPS < 0f)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    t.Value.x = EPS;
                }
                if (t.Value.x + EPS > VIEW_WIDTH)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    t.Value.x = VIEW_WIDTH - EPS;
                }
                if (t.Value.y - EPS < 0f)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    t.Value.y = EPS;
                }
                if (t.Value.y + EPS > VIEW_HEIGHT)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    t.Value.y = VIEW_HEIGHT - EPS;
                }
                if (t.Value.z - EPS < 0f)
                {
                    p.velocity.z *= BOUND_DAMPING;
                    t.Value.z = EPS;
                }
                if (t.Value.z + EPS > VIEW_DEPTH)
                {
                    p.velocity.z *= BOUND_DAMPING;
                    t.Value.z = VIEW_DEPTH - EPS;
                }
                particles[i] = new SPHParticle {
                    velocity = p.velocity,
                    density = p.density,
                    force = p.force,
                    pressure = p.pressure
                };
                translations[i] = new Translation {Value = t.Value };
            }
        }

        private float SpikyKernalFirst(float dist)
        {
            float x = 1.0f - dist / H;
            return -45.0f / (Mathf.PI * (H * H * H * H)) * x * x;
        }

        private float SpikeyKernalSecond(float dist)
        {
            float x = 1.0f - dist / H;
            return 90f / (Mathf.PI * (H * H * H * H * H)) * x;
        }
    }
}