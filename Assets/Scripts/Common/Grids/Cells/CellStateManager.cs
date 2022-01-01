using BlobHashMaps;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Common.Grids.Cells
{
    public class CellStateManager : MonoBehaviour
    {
        public static CellStateManager Instance { get; private set; }


        [Header("Individual States")]
        [SerializeField]
        public CellStates States;

        [Header("State List")]
        [SerializeField]
        public CellStateScriptableObject[] Cells;

        public BlobAssetReference<CellStateRegistryMap> CellStatesBlobMap { get; private set; }

        private void Awake()
        {
            Instance = this;

            BlobBuilder blobStatesBuilder = new(Allocator.Temp);
            ref CellStates states = ref blobStatesBuilder.ConstructRoot<CellStates>();

            BlobBuilder blobMapBuilder = new(Allocator.Temp);
            ref var cellStatesHashMap = ref blobMapBuilder.ConstructRoot<CellStateRegistryMap>();
            var map = blobMapBuilder.AllocateHashMap(ref cellStatesHashMap.CellStates, Cells.Length);

            for (int i = 0; i < Cells.Length; i++)
            {
                var state = Cells[i];
                //var hash = UnityEngine.Hash128.Compute(state.NamespacedKey.Value);
                map.Add(state.NamespacedKey.Value, state.GetDefaultState());
            }

            CellStatesBlobMap = blobMapBuilder.CreateBlobAssetReference<CellStateRegistryMap>(Allocator.Persistent);
            blobMapBuilder.Dispose();
        }
    }

    [Serializable]
    public struct CellStates
    {
        public CellStateScriptableObject Air;
        public CellStateScriptableObject FreshWater;
        public CellStateScriptableObject Sand;
        public CellStateScriptableObject Stone;
    }

    public struct CellStatesBlob
    {

    }

    public struct CellStateRegistryMap
    {
        public BlobHashMap<FixedString32, CellStateData> CellStates;
    }
}
