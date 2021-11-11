using Common.Entities.Components;
using Unity.Entities;

namespace Common.Entities.Systems
{
    [DisableAutoCreation]
    public class FluidSystems : SystemBase
    {
        public TileMap2DArray TileMap;

        protected override void OnUpdate()
        {
        }
    }
}