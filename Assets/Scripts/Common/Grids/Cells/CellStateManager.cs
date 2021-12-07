using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using BlobHashMaps;

namespace Common.Grids.Cells
{
    public class CellStateManager : MonoBehaviour
    {
        public static CellStateManager Instance { get; private set; }

        public const ushort AIR = 0;
        public const ushort BLANK = 1;
        public const ushort CLEAR = 2;

        public CellStateScriptableObject[] Cells;

        public CellStateScriptableObject Air => Cells[AIR];
        public CellStateScriptableObject Blank => Cells[BLANK];
        public CellStateScriptableObject Clear => Cells[CLEAR];

        public BlobAssetReference<CellStatesBlobAsset> CellStatesBlobReference { get; private set; }
        public BlobAssetReference<CellStatesBlobHashMap> CellStatesBlobMap { get; private set; }

        public CellStateData GetDefaultState(ushort index) => Cells[index].GetDefaultState();

        private void Awake()
        {
            Instance = this;

            BlobBuilder blobArrayBuilder = new(Allocator.Temp);
            BlobBuilder blobMapBuilder = new(Allocator.Temp);

            ref CellStatesBlobAsset cellStatesBlobAsset = ref blobArrayBuilder.ConstructRoot<CellStatesBlobAsset>();
            var array = blobArrayBuilder.Allocate(ref cellStatesBlobAsset.CellStates, Cells.Length);

            ref var cellStatesHashMap = ref blobMapBuilder.ConstructRoot<CellStatesBlobHashMap>();
            var map = blobMapBuilder.AllocateHashMap(ref cellStatesHashMap.CellStates, Cells.Length);

            for (int i = 0; i < Cells.Length; i++)
            {
                var state = Cells[i].GetDefaultState();
                array[i] = state;
                map.Add(Cells[i].NamespacedKey.Value, state);
            }

            CellStatesBlobReference = blobArrayBuilder.CreateBlobAssetReference<CellStatesBlobAsset>(Allocator.Persistent);
            blobArrayBuilder.Dispose();

            CellStatesBlobMap = blobMapBuilder.CreateBlobAssetReference<CellStatesBlobHashMap>(Allocator.Persistent);
            blobMapBuilder.Dispose();
        }
    }

    public struct CellStatesBlobAsset
    {
        public BlobArray<CellStateData> CellStates;
    }

    public struct CellStatesBlobHashMap
    {
        public BlobHashMap<FixedString64, CellStateData> CellStates;
    }
}
