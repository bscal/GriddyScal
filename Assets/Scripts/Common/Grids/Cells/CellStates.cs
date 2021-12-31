using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Common.Grids.Cells
{
    public class CellStates : MonoBehaviour
    {
        private unsafe void* m_StateRegistryPointer;

        public CellStatesRegistry StateRegistry;

        private unsafe void Awake()
        {
            UnsafeUtility.CopyStructureToPtr(ref StateRegistry, m_StateRegistryPointer);
        }

        public CellStateScriptableObject Air;
        public CellStateScriptableObject FreshWater;
        public CellStateScriptableObject Sand;
        public CellStateScriptableObject Stone;
    }

    [SerializeField]
    public struct CellStatesRegistry
    {
        public CellStateScriptableObject Air;
        public CellStateScriptableObject FreshWater;
        public CellStateScriptableObject Sand;
        public CellStateScriptableObject Stone;
    }

}