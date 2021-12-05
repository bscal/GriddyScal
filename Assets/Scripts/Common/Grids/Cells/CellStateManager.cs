using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using BlobHashMaps;

namespace Common.Grids.Cells
{
    public class CellStateManager : MonoBehaviour, ISerializationCallbackReceiver
    {
        public static CellStateManager Instance { get; private set; }

        public const ushort AIR = 0;
        public const ushort BLANK = 1;
        public const ushort CLEAR = 2;

        public ushort Stone = 3;
        public ushort Fresh_Water = 4;
        public ushort Sand = 5;
        public ushort Wood = 6;

        public CellStateScriptableObject[] Cells;

        public CellStateScriptableObject Air => Cells[AIR];
        public CellStateScriptableObject Blank => Cells[BLANK];
        public CellStateScriptableObject Clear => Cells[CLEAR];

        public BlobAssetReference<CellStatesBlobAsset> CellStatesBlobReference { get; private set; }

        public CellStateData GetDefaultState(ushort index) => Cells[index].GetDefaultState();

        private void Awake()
        {
            Instance = this;
        }

        public void OnAfterDeserialize()
        {
            using BlobBuilder blobBuilder = new();

            ref CellStatesBlobAsset cellStatesBlobAsset = ref blobBuilder.ConstructRoot<CellStatesBlobAsset>();
            var array = blobBuilder.Allocate(ref cellStatesBlobAsset.CellStates, Cells.Length);

            for (int i = 0; i < Cells.Length; i++)
            {
                array[i] = Cells[i].GetDefaultState();
            }
            CellStatesBlobReference = blobBuilder.CreateBlobAssetReference<CellStatesBlobAsset>(Allocator.Persistent);

        }

        public void OnBeforeSerialize()
        {
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
