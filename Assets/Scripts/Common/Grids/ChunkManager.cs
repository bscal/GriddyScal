using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System;

namespace Common.Grids
{

    public struct ChunkSection
    {
        public int StartingIndex, EndingIndex;
        public NativeSlice<CellStateData> Cells;
    }

    public struct ChunkMap
    {
        public int ChunkCount;
        public NativeArray<CellStateData> Cells;
        public NativeHashMap<long, ChunkSection> Chunks;
    }

    [Serializable]
    public class ChunkMesh
    {
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
    }

    public class ChunkManager : MonoBehaviour
    {
        public ChunkMap Chunks;
        public Dictionary<long, ChunkMesh> ChunkMeshes;

        public void GetTile(int tileX, int tileY)
        {

        }

        public void LoadChunk()
        {
            
        }

        public long GetChunkId(int x, int y) => (0xffL & y << 32) | x;

    }
}