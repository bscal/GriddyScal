using Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;
using Unity.Core;
using UnityEngine;
using BlobHashMaps;

namespace Griddy
{

    public class GriddyChunk
    {
        public int3 ChunkPos;
        public int3 CellStartPos;
        public Cell[] Cells;
        public NativeArray<Cell> NativeCells;

        public GriddyChunk(int3 pos, int3 chunkDimensions, int size)
        {
            ChunkPos = pos;
            CellStartPos = new(pos.x * chunkDimensions.x, pos.y * chunkDimensions.y, pos.z * chunkDimensions.z);
            //Cells = new Cell[size];
            //NativeCells = new NativeArray<Cell>(size, Allocator.Persistent);
        }

        public void CreateCells(GriddyManager manager)
        {
            int i = 0;
            for (int z = CellStartPos.z; z < CellStartPos.z + manager.ChunkDimensions.z; z++)
            {
                for (int y = CellStartPos.y; y < CellStartPos.y + manager.ChunkDimensions.y; y++)
                {
                    for (int x = CellStartPos.x; x < CellStartPos.x + manager.ChunkDimensions.x; x++)
                    {
                        Vector3 translation = new(x, y, z);
                        int3 pos = new(x, y, z);
                        Cell cell = new();
                        cell.Position = pos;
                        //Cells[i] = cell;
                        //NativeCells[i] = cell;
                        manager.Cells.Add(cell.Position, cell);
                        Matrix4x4 matrix = Matrix4x4.identity;
                        matrix.SetTRS(translation, Quaternion.identity, Vector3.one);
                        manager.ChunkMatricies[ChunkPos][i] = matrix;
                        i++;
                    }
                }
            }
        }

        public void CopyFrom()
        {
            NativeCells.CopyFrom(Cells);
        }

        public void CopyTo()
        {
            NativeCells.CopyTo(Cells);
        }

        ~GriddyChunk()
        {
            if (NativeCells.IsCreated)
                NativeCells.Dispose();
        }
    }

}
