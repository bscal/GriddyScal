using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;
using Common.Grids.Cells;

namespace Common.Grids
{

    public struct ChunkSection
    {
        public int ChunkSize, StartingIndex, EndingIndex;
        public bool IsDirty, IsActive;

        public int GetIndex(int x, int y) => StartingIndex + x + y * ChunkSize;
    }

    public struct ChunkMap
    {
        public int2 MapSize;
        public int ChunkSize;
        public int CellCount;
        public NativeArray<CellStateData> Cells;
        public NativeHashMap<long, ChunkSection> Chunks;

        public CellStateData GetState(int cellX, int cellY) => Cells[cellX + cellY * MapSize.x];

        public void SetState(int cellX, int cellY, CellStateData state)
        {
            Cells[cellX + cellY * MapSize.x] = state;
            var key = Int2ToChunkKey(CellToChunkCoords(cellX, cellY));
            if (Chunks.TryGetValue(key, out ChunkSection chunkSection))
                chunkSection.IsDirty = true;
        }

        public ChunkSection GetChunkSection(int cellX, int cellY) => Chunks[XYToChunkKey(cellX / ChunkSize, cellY / ChunkSize)];

        public ChunkSection CreateChunkSection(int chunkX, int chunkY)
        {
            var indexes = FindChunkIndexes(chunkX, chunkY);
            var chunkSection = new ChunkSection
            {
                ChunkSize = ChunkSize,
                StartingIndex = indexes.x,
                EndingIndex = indexes.y
            };
            Chunks.Add(XYToChunkKey(chunkX, chunkY), chunkSection);
            return chunkSection;
        }

        public int2 CellToChunkCoords(int cellX, int cellY) => new(cellX / ChunkSize, cellY / ChunkSize);

        public int2 FindChunkIndexes(int chunkX, int chunkY) => new(chunkX * ChunkSize, chunkY * ChunkSize);

        public long Int2ToChunkKey(int2 coords) => (long)coords.y << 32 | (long)coords.x;

        public long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public int2 ChunkKeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));
    }

    [Serializable]
    public class ChunkMesh
    {
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public ChunkMesh(MeshFilter meshFilter, MeshRenderer meshRenderer)
        {
            MeshFilter = meshFilter;
            MeshRenderer = meshRenderer;
        }
    }

    public class ChunkManager : MonoBehaviour
    {
        public ChunkMap ChunkMap;
        public Dictionary<long, ChunkMesh> ChunkMeshes;

        public float MaxMass = 1.0f;
        public float MaxMassSqr = 0;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;
        public bool FallingLeft;

        public NativeArray<CellStateData> NewCells;
        public NativeArray<float> Mass;
        public NativeArray<float> NewMass;

        private void Awake()
        {
            MaxMassSqr = MaxMass * MaxMass;
        }

        public void Update()
        {
            FallingLeft = !FallingLeft;

            ChunkMap.Cells.CopyTo(NewCells);
            Mass.CopyTo(NewMass);

            NativeArray<JobHandle> handles = new(ChunkMap.Chunks.Capacity, Allocator.Temp);
            int index = 0;
            foreach (var pair in ChunkMap.Chunks)
            {
                if (pair.Value.IsActive)
                {
                    ChunkCellsUpdateJob job = new ChunkCellsUpdateJob
                    {
                        ChunkMap = ChunkMap,
                        MaxCompression = MaxCompression,
                        MaxMass = MaxMass,
                        MinMass = MinMass,
                        MaxMassSqr = MaxMassSqr,
                        MaxSpeed = MaxSpeed,
                        MinFlow = MinFlow,
                        FallLeft = FallingLeft,
                        NewCells = NewCells,
                        Mass = Mass,
                        NewMass = NewMass,
                        CellStates = CellStateManager.Instance.CellStatesBlobIdMap,
                    };
                    handles[index++] = job.ScheduleParallel(ChunkMap.CellCount, 32, new JobHandle());
                }
            }
            JobHandle.CompleteAll(handles);
            handles.Dispose();

            ChunkMap.Cells.CopyFrom(NewCells);
            Mass.CopyFrom(NewMass);
        }

        public void GetTile(int tileX, int tileY)
        {

        }

        public void LoadChunk()
        {
            
        }

        public static long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public static Vector2Int ChunkKeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));

    }


    public struct ChunkCellsUpdateJob : IJobFor
    {
        [NativeDisableParallelForRestriction] [ReadOnly] public ChunkMap ChunkMap;

        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public bool FallLeft;

        [ReadOnly] public BlobAssetReference<CellStateIdMap> CellStates;

        [ReadOnly] public CellStateData Sand;
        [ReadOnly] public CellStateData Air;
        [ReadOnly] public CellStateData Water;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<CellStateData> NewCells;

        [ReadOnly] public NativeArray<float> Mass;
        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;

        private bool m_HasChanged;

        public void Execute(int i)
        {
            int x = i % ChunkMap.MapSize.x;
            int y = i / ChunkMap.MapSize.y;
            int index = GetCellId(x, y);
            int chunkX = x / ChunkMap.ChunkSize;
            int chunkY = y / ChunkMap.ChunkSize;
            long chunkKey = XYToKey(chunkX, chunkY);

            var chunk = ChunkMap.Chunks[chunkKey];

            if (ChunkMap.Cells[index].Equals(Sand))
            {
                SimulateSand(x, y, index);
            }
            else if (ChunkMap.Cells[index].Equals(Water))
            {
                SimulateFluid(x, y, index, index);
            }

            if (m_HasChanged)
            {
                chunk.IsDirty = m_HasChanged;
                ChunkMap.Chunks[chunkKey] = chunk;
            }
        }

        private void SimulateFluid(int x, int y, int index, int localIndex)
        {
            float remainingMass = Mass[localIndex];
            if (remainingMass <= 0) return;
            float flow;
            // Down
            int downId = GetCellId(x, y - 1);
            if (InBounds(x, y - 1) && !ChunkMap.Cells[downId].IsSolid)
            {
                flow = GetStableMass(remainingMass + Mass[downId]) - Mass[downId];
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[downId] += flow;
                remainingMass -= flow;
                m_HasChanged = true;
            }
            if (remainingMass <= 0)
                return;

            // Left
            int leftId = GetCellId(x - 1, y);
            if (InBounds(x - 1, y) && !ChunkMap.Cells[leftId].IsSolid)
            {
                flow = (Mass[index] - Mass[leftId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[leftId] += flow;
                remainingMass -= flow;
                m_HasChanged = true;
            }

            if (remainingMass <= 0)
                return;
            // Right
            int rightId = GetCellId(x + 1, y);
            if (InBounds(x + 1, y) && !ChunkMap.Cells[rightId].IsSolid)
            {
                flow = (Mass[index] - Mass[rightId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[rightId] += flow;
                remainingMass -= flow;
                m_HasChanged = true;
            }

            if (remainingMass <= 0)
                return;

            // Up
            int upId = GetCellId(x, y + 1);
            if (InBounds(x, y + 1) && !ChunkMap.Cells[upId].IsSolid)
            {
                flow = remainingMass - GetStableMass(remainingMass + Mass[upId]);
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[upId] += flow;
                m_HasChanged = true;
            }
        }

        private void SimulateSand(int x, int y, int index)
        {
            if (!InBounds(x, y - 1)) return;

            int down = GetCellId(x, y - 1);
            if (!ChunkMap.Cells[down].IsSolid)
            {
                // Handle downwards movement
                NewCells[down] = ChunkMap.Cells[index];
                NewCells[index] = Air;
                m_HasChanged = true;
            }
            else
            {
                if (FallLeft)
                {
                    int left = GetCellId(x - 1, y - 1);
                    if (InBounds(x - 1, y - 1) && !ChunkMap.Cells[left].IsSolid)
                    {
                        // Handle leftward movement
                        NewCells[left] = ChunkMap.Cells[index];
                        NewCells[index] = Air;
                        m_HasChanged = true;
                    }
                }
                else
                {
                    int right = GetCellId(x + 1, y - 1);
                    if (InBounds(x + 1, y - 1) && !ChunkMap.Cells[right].IsSolid)
                    {
                        // Handle rightward movement
                        NewCells[right] = ChunkMap.Cells[index];
                        NewCells[index] = Air;
                        m_HasChanged = true;
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

        private bool InBounds(int x, int y)
        {
            return x > -1 && y > -1 && x < ChunkMap.MapSize.x && y < ChunkMap.MapSize.y;
        }

        private int GetCellId(int x, int y)
        {
            x = math.clamp(x, 0, ChunkMap.MapSize.x - 1);
            y = math.clamp(y, 0, ChunkMap.MapSize.y - 1);
            return x + y * ChunkMap.MapSize.x;
        }

        private long XYToKey(int x, int y) => (long)y << 32 | (long)x;

        private Vector2Int KeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));
    }
}