using System.Collections;
using Unity.Collections;
using UnityEngine;

namespace Common.Grids
{

    public class Chunk
    {
        public ChunkState State;
        public int x, y;
        public byte Width, height;
        public bool IsDirty;

        public NativeArray<CellStateData> Cells;

        public void Update()
        {
            if (IsDirty)
            {
                IsDirty = false;

                // TODO
                // Culling? Update UVs
                // Update Cells.
    
                // TODO create a manager? to pass Cells array to
                // This will replace the UpdateSystem.
                
                // Wait possible multithread chunk updating??
                // System that Calls update on each chunk thats multithreaded
                // and waits for all chunks to update?

            }
        }

        public void Serialize()
        {

        }

        public void Deserialize()
        {

        }



    }

    public enum ChunkState : byte
    {
        UNLOADED = 0,
        LOADED = 1,
        PERMANENTLY_LOADED = 2,
        FROZEN = 3,
    }
}