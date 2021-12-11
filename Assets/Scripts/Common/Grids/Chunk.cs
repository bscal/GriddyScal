using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Common.Grids
{

    public struct ChunkData
    {
        public ChunkState State;
        public long ChunkKey;
        public NativeArray<CellStateData> Cells;
    }

    public class Chunk
    {
        public ChunkState State;
        public int x, y, Width, Height;
        public bool IsDirty;

        public NativeArray<float4> Colors;
        public NativeArray<CellStateData> Cells;

        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public Chunk()
        { 
            int size = Width * Height;
            Colors = new(size * 4, Allocator.Persistent);
            Cells = new(size, Allocator.Persistent);
        }

        public Chunk(int x, int y, int w, int h) : this()
        {
            this.x = x;
            this.y = y;
            this.Width = w;
            this.Height = h;
        }

        ~Chunk()
        {
            Colors.Dispose();
            Cells.Dispose();
        }

        public void Create(GameObject gameObject, Mesh mesh, Material material)
        {
            gameObject.name = $"Chunk({x}, {y})";
            gameObject.transform.position = new Vector3(x, y);
            GameObject = gameObject;

            MeshFilter = gameObject.AddComponent<MeshFilter>();
            MeshFilter.mesh = mesh;

            MeshRenderer = gameObject.AddComponent<MeshRenderer>();
            MeshRenderer.sharedMaterial = material;

            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new(Width, Height, .01f);
        }

        public void Update()
        {
            if (IsDirty)
            {
                IsDirty = false;

                MeshFilter.mesh.SetUVs(1, Colors);

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

        public long GetKey() => (long)y << 32 | (long)x;



    }

    public enum ChunkState : byte
    {
        UNLOADED = 0,
        LOADED = 1,
        PERMANENTLY_LOADED = 2,
        FROZEN = 3,
    }
}