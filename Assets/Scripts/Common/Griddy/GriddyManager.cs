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
    public class GriddyManager : MonoBehaviour
    {
        public const int MAX_INSTANCED_MESHES = 1023;

        public int3 ChunkDimensions;
        public int Size;

        public Mesh Mesh;
        public Material Material;

        public BlobHashMap<FixedString32, CellObjectState> CellStatesByName;
        public BlobHashMap<int, CellObjectState> CellStatesById;

        public Dictionary<int3, Matrix4x4[]> ChunkMatricies;
        public Dictionary<int3, Cell> Cells;
        //public Dictionary<int3, GriddyChunk> Chunks;
        //public NativeHashMap<int3, Cell> Cells;

        private void Start()
        {
            Size = ChunkDimensions.x * ChunkDimensions.y * ChunkDimensions.z;
            Mesh = TileMeshCreator.Create2DTileMeshXZ(1, 1, 1, 1);
            Cells = new Dictionary<int3, Cell>();
            ChunkMatricies = new Dictionary<int3, Matrix4x4[]>();

            LoadChunk(new int3(0, 0, 0));
            LoadChunk(new int3(0, 0, 1));
            LoadChunk(new int3(1, 0, 0));
            LoadChunk(new int3(1, 0, 1));
        }

        public void LoadChunk(int3 chunkPos)
        {
            ChunkMatricies.Add(chunkPos, new Matrix4x4[Size]);
            GriddyChunk chunk = new(chunkPos, ChunkDimensions, Size);
            //Chunks.Add(chunkPos, chunk);
            chunk.CreateCells(this);
        }

        public void Update()
        {
            foreach (var matricies in ChunkMatricies)
            {
                int count = matricies.Value.Length;
                int batches = Mathf.CeilToInt((float)count / (float)MAX_INSTANCED_MESHES);
                for (int i = 0; i < batches; i++)
                {
                    int batchCount = Mathf.Min(MAX_INSTANCED_MESHES, count - (MAX_INSTANCED_MESHES * i));
                    int start = Mathf.Max(0, (i - 1) * MAX_INSTANCED_MESHES);
                    Graphics.DrawMeshInstanced(Mesh, 0, Material, matricies.Value, batchCount);
                }
            }
        }

        public int3 CellPosToChunkPos(int3 cellPos) => new(cellPos.x / 16, cellPos.y / 16, cellPos.z);

    }
}

public class TileMeshCreator
{
    public static Mesh Create2DTileMeshXY(int sizeX, int sizeY, int width, int height)
    {
        Mesh mesh = new();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int size = sizeX * sizeY;
        Vector3[] vertices = new Vector3[4 * size];
        Vector2[] uvs = new Vector2[4 * size];
        Vector4[] colors = new Vector4[4 * size];
        int[] triangles = new int[6 * size];

        int iVertCount = 0;
        int iIndexCount = 0;
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int xx = x * height;
                int yy = y * width;
                vertices[iVertCount + 0] = new Vector3(xx, yy);
                vertices[iVertCount + 1] = new Vector3(xx, yy + height);
                vertices[iVertCount + 2] = new Vector3(xx + width, yy + height);
                vertices[iVertCount + 3] = new Vector3(xx + width, yy);

                uvs[iVertCount + 0] = new Vector2(0, 0);
                uvs[iVertCount + 1] = new Vector2(0, 1);
                uvs[iVertCount + 2] = new Vector2(1, 1);
                uvs[iVertCount + 3] = new Vector2(1, 0);

                colors[iVertCount + 0] = Vector4.one;
                colors[iVertCount + 1] = Vector4.one;
                colors[iVertCount + 2] = Vector4.one;
                colors[iVertCount + 3] = Vector4.one;

                triangles[iIndexCount + 0] += (iVertCount + 0);
                triangles[iIndexCount + 1] += (iVertCount + 1);
                triangles[iIndexCount + 2] += (iVertCount + 2);
                triangles[iIndexCount + 3] += (iVertCount + 0);
                triangles[iIndexCount + 4] += (iVertCount + 2);
                triangles[iIndexCount + 5] += (iVertCount + 3);

                iVertCount += 4;
                iIndexCount += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }

    public static Mesh Create2DTileMeshXZ(int sizeX, int sizeZ, int width, int height)
    {
        Mesh mesh = new();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int size = sizeX * sizeZ;
        Vector3[] vertices = new Vector3[4 * size];
        Vector2[] uvs = new Vector2[4 * size];
        Vector4[] colors = new Vector4[4 * size];
        int[] triangles = new int[6 * size];

        int iVertCount = 0;
        int iIndexCount = 0;
        for (int z = 0; z < sizeZ; z++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int xx = x * width;
                int zz = z * height;
                vertices[iVertCount + 0] = new Vector3(xx, 0, zz);
                vertices[iVertCount + 1] = new Vector3(xx, 0, zz + height);
                vertices[iVertCount + 2] = new Vector3(xx + width, 0, zz + height);
                vertices[iVertCount + 3] = new Vector3(xx + width, 0, zz);

                uvs[iVertCount + 0] = new Vector2(0, 0);
                uvs[iVertCount + 1] = new Vector2(0, 1);
                uvs[iVertCount + 2] = new Vector2(1, 1);
                uvs[iVertCount + 3] = new Vector2(1, 0);

                colors[iVertCount + 0] = Vector4.one;
                colors[iVertCount + 1] = Vector4.one;
                colors[iVertCount + 2] = Vector4.one;
                colors[iVertCount + 3] = Vector4.one;

                triangles[iIndexCount + 0] += (iVertCount + 0);
                triangles[iIndexCount + 1] += (iVertCount + 1);
                triangles[iIndexCount + 2] += (iVertCount + 2);
                triangles[iIndexCount + 3] += (iVertCount + 0);
                triangles[iIndexCount + 4] += (iVertCount + 2);
                triangles[iIndexCount + 5] += (iVertCount + 3);

                iVertCount += 4;
                iIndexCount += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }
}