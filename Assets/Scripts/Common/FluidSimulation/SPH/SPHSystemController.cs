using Unity.Entities;
using UnityEngine;

public class SPHSystemController : MonoBehaviour
{
    public static GameObject ParticlePrefab;

    [Header("Particle Object")]
    [SerializeField]
    private GameObject m_ParticlePrefab;
    /*    public float ParticleRadius;

        [Header("SPHSystem Settings")]
        public int Dimensions;
        public float Mass;
        public float Viscostiy;
        public float RestDensity;
        public float Damping;
        public Vector3 Gravity;
        public float TimeStep;*/

    private SPHSystem m_SPHSystem;

    private void Awake()
    {
        ParticlePrefab = m_ParticlePrefab;
    }

    private void Start()
    {
        m_SPHSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<SPHSystem>();
    }

    private void Update()
    {
        if (m_SPHSystem != null && m_SPHSystem.Enabled)
        {
            m_SPHSystem.Update();
        }
    }
}
