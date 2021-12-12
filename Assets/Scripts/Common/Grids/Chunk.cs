using System.Collections;
using Unity.Collections;
using Unity.Jobs;
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

    public static class ChunkManager
    {
        public static bool UpdatevhunkStaes()
        {


            return true;
        }
    }

    public class Chunk
    {
        public ChunkState State;
        public int x, y, Width, Height;
        public bool IsDirty;

        public NativeArray<float4> Colors;
        public NativeArray<CellStateData> Cells;
        public NativeArray<CellStateData> NewCells;
        public NativeArray<float> Mass;
        public NativeArray<float> NewMass;

        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public int Count;
        public float MaxMass = 1.0f;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        public TileMap2DArray Grid;
        private bool m_FallingLeft;

        public Chunk()
        { 
            int size = Width * Height;
            Colors = new(size * 4, Allocator.Persistent);
            Cells = new(size, Allocator.Persistent);
            NewCells = new(size, Allocator.Persistent);
            Mass = new(size, Allocator.Persistent);
            NewMass = new(size, Allocator.Persistent);
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
            NewCells.Dispose();
            Mass.Dispose();
            NewMass.Dispose();
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

        public void Process()
        {
        }

        public void UpdateMesh()
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


    public struct UpdateChunkJob : IJob
    {
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public int MapWidth;
        [ReadOnly] public int MapHeight;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public NativeHashMap<long, ChunkData> ChunkMap;
        [ReadOnly] public NativeArray<CellStateData> Cells;
        public NativeArray<CellStateData> NewCells;
        [ReadOnly] public NativeArray<float> Mass;
        public NativeArray<float> NewMass;

        public void Execute()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = GetCellId(x, y);
                    float remainingMass = Mass[index];
                    if (remainingMass <= 0) return;
                    if (Cells[index].IsSolid) return;
                    float flow;
                    // Down
                    int downId = GetCellId(x, y - 1);
                    if (InBounds(x, y - 1) && !Cells[downId].IsSolid)
                    {
                        flow = GetStableMass(remainingMass + Mass[downId]) - Mass[downId];
                        if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                        flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                        NewMass[index] -= flow;
                        NewMass[downId] += flow;
                        remainingMass -= flow;
                    }
                    if (remainingMass <= 0)
                        return;

                    // Left
                    int leftId = GetCellId(x - 1, y);
                    if (InBounds(x - 1, y) && !Cells[leftId].IsSolid)
                    {
                        flow = (Mass[index] - Mass[leftId]) / 4;
                        if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                        flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                        NewMass[index] -= flow;
                        NewMass[leftId] += flow;
                        remainingMass -= flow;
                    }

                    if (remainingMass <= 0)
                        return;
                    // Right
                    int rightId = GetCellId(x + 1, y);
                    if (InBounds(x + 1, y) && !Cells[rightId].IsSolid)
                    {
                        flow = (Mass[index] - Mass[rightId]) / 4;
                        if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                        flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                        NewMass[index] -= flow;
                        NewMass[rightId] += flow;
                        remainingMass -= flow;

                    }

                    if (remainingMass <= 0)
                        return;

                    // Up
                    int upId = GetCellId(x, y + 1);
                    if (InBounds(x, y + 1) && !Cells[upId].IsSolid)
                    {
                        flow = remainingMass - GetStableMass(remainingMass + Mass[upId]);
                        if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                        flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                        NewMass[index] -= flow;
                        NewMass[upId] += flow;
                    }

                }
            }

            
        }

        private float GetStableMass(float totalMass)
        {
            // All water goes to lower cell
            if (totalMass <= 1) return 1;
            else if (totalMass < 2 * MaxMass + MaxCompression) return (MaxMassSqr + totalMass * MaxCompression) / (MaxMass + MaxCompression);
            else return (totalMass + MaxCompression) / 2;
        }

        public bool InBounds(int x, int y)
        {
            return x > -1 && y > -1 && x < Width && y < Height;
        }

        public int GetCellId(int x, int y)
        {
            x = math.clamp(x, 0, Width - 1);
            y = math.clamp(y, 0, Height - 1);
            return x + y * Width;
        }

        public CellStateData GetCellState(int cellX, int cellY)
        {
            int x = cellX % MapWidth;
            int y = cellY / MapHeight;
            long key = XYToKey(x, y);
            bool found = ChunkMap.TryGetValue(key, out ChunkData chunkData);

            int xx = x % Width;
            int yy = y / Height;
            return chunkData.Cells[xx + yy * Width];
        }

        public void SetCellState(int cellX, int cellY, CellStateData data)
        {
            int x = cellX % MapWidth;
            int y = cellY / MapHeight;
            long key = XYToKey(x, y);
            bool found = ChunkMap.TryGetValue(key, out ChunkData chunkData);

            int xx = x % Width;
            int yy = y / Height;
            chunkData.Cells[xx + yy * Width] = data;
        }

        public long XYToKey(int x, int y) => (long)y << 32 | (long)x;

        public Vector2Int KeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));
    }
}