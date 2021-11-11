using Common.Entities.Components;
using Common.Entities.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Common.FluidSimulation
{
    [RequireComponent(typeof(TileMap2DArray))]
    public class FluidSimulation : MonoBehaviour
    {

        [SerializeField]
        public TileMap2DArray TileMap;

        private Controls m_Controls;

        private void Awake()
        {
            m_Controls.Enable();

            var FluidSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<FluidSystems>();
            FluidSystem.TileMap = TileMap;
        }

        private void Start()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            EntityArchetype archetype = entityManager.CreateArchetype(
                typeof(TileVelocityComponent),
                typeof(TileDensityComponent));

            NativeArray<Entity> entityArray = new(TileMap.Size, Allocator.Temp);
            entityManager.CreateEntity(archetype, entityArray);

            for (int i = 0; i < entityArray.Length; i++)
            {
                entityManager.SetComponentData(entityArray[i], new TileVelocityComponent { Velocity = 1 });
                entityManager.SetComponentData(entityArray[i], new TileDensityComponent { Density = 1 });
            }

            entityArray.Dispose();
        }

        private void Update()
        {
            
        }

    }
}