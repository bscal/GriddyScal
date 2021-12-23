using Common.Grids.Cells;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.Grids
{

    public struct ChunkSection
    {
        public int ChunkSize;
        public int2 StartPos;
        public bool IsDirty, IsActive;
    }

    public struct ChunkMap
    {
        public int2 MapSize;
        public int2 MapSizeInChunks;
        public int ChunksPerPlayer;
        public int ChunkSize;
        public int ChunkCellCount;
        public int MapCellCount;
        public NativeArray<CellStateData> Cells;
        public NativeHashMap<long, ChunkSection> Chunks;

        public ChunkMap(int2 mapSize, int2 mapSizeInChunks, int chunksPerPlayer, int chunkSize, int chunkCellCount, int mapCellCount)
        {
            MapSize = mapSize;
            MapSizeInChunks = mapSizeInChunks;
            ChunksPerPlayer = chunksPerPlayer;
            ChunkSize = chunkSize;
            ChunkCellCount = chunkCellCount;
            MapCellCount = mapCellCount;
            Cells = new NativeArray<CellStateData>(mapCellCount, Allocator.Persistent);
            Chunks = new NativeHashMap<long, ChunkSection>(ChunksPerPlayer, Allocator.Persistent);
        }

        public void Cleanup()
        {
            Cells.Dispose();
            Chunks.Dispose();
        }

        public void LoadChunk(int chunkX, int chunkY)
        {
            if (ChunkInBounds(chunkX, chunkY)) return;
            long key = XYToChunkKey(chunkX, chunkY);
            if (Chunks.ContainsKey(key)) return;
            var chunkSection = new ChunkSection
            {
                ChunkSize = ChunkSize,
                StartPos = FindChunkIndexes(chunkX, chunkY),
                IsActive = true,
                IsDirty = true,
            };
            Chunks.Add(key, chunkSection);
        }

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
            long key = XYToChunkKey(chunkX, chunkY);
            var indexes = FindChunkIndexes(chunkX, chunkY);
            var chunkSection = new ChunkSection
            {
                ChunkSize = ChunkSize,
                StartPos = FindChunkIndexes(chunkX, chunkY)
            };
            Chunks.Add(key, chunkSection);
            return chunkSection;
        }

        public int2 CellToChunkCoords(int cellX, int cellY) => new(cellX / ChunkSize, cellY / ChunkSize);

        public int2 FindChunkIndexes(int chunkX, int chunkY) => new(chunkX * ChunkSize, chunkY * ChunkSize);

        public long Int2ToChunkKey(int2 coords) => (long)coords.y << 32 | (long)coords.x;

        public long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public int2 ChunkKeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));

        public bool ChunkInBounds(int chunkX, int chunkY)
        {
            return chunkX < 0 || chunkY < 0 || chunkX >= MapSizeInChunks.x || chunkY >= MapSizeInChunks.y;
        }
    }

    [Serializable]
    public class ChunkMesh
    {
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public BoxCollider Collider;
    }

    public class ChunkManager : MonoBehaviour
    {
        public Material Material;
        public int2 TextureSize;

        public int2 MapSize;
        public int ChunkSize;
        public int Size;
        public ChunkMap ChunkMap;
        public Dictionary<long, ChunkMesh> ChunkMeshes;

        public GameObject ChunkPrefab;

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

        public Vector2Int TileSize = new(1, 1);

        private void Awake()
        {
            MaxMassSqr = MaxMass * MaxMass;
            Size = MapSize.x * MapSize.y;
            int chunksPerPlayer = 1 + 8 + 16;
            int chunkCellCount = ChunkSize * ChunkSize;
            int2 chunkMapSize = new(MapSize.x / ChunkSize, MapSize.y / ChunkSize);
            Vector2Int ChunkSizes = new(ChunkSize, ChunkSize);
            ChunkMap = new ChunkMap(MapSize, chunkMapSize, chunksPerPlayer, ChunkSize, chunkCellCount, Size);


            uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var m_Random = new Unity.Mathematics.Random(seed);

            ChunkMeshes = new Dictionary<long, ChunkMesh>();
            NewCells = new NativeArray<CellStateData>(Size, Allocator.Persistent);
            Mass = new NativeArray<float>(Size, Allocator.Persistent);
            NewMass = new NativeArray<float>(Size, Allocator.Persistent);

            Debug.Log($"Generating TileMap[{MapSize.x}, {MapSize.y}]. Seed: {seed} | ChunkSize: {ChunkSize}");

            Mesh mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.MarkDynamic();
          

            NativeArray<Vector3> vertices = new NativeArray<Vector3>(chunkCellCount * 4, Allocator.TempJob);
            GenVerticiesJob verticiesJob = new GenVerticiesJob
            {
                MapSize = ChunkSizes,
                TileSize = TileSize,
                Verticies = vertices
            };
            var verticesHandle = verticiesJob.Schedule();

            NativeArray<Vector3> uvs = new NativeArray<Vector3>(chunkCellCount * 4, Allocator.TempJob);
            GenTextureUVJob uvJob = new GenTextureUVJob
            {
                Seed = m_Random,
                MapSize = ChunkSizes,
                UVs = uvs
            };
            var uvHandle = uvJob.Schedule();

            NativeArray<Vector4> colors = new(chunkCellCount * 4, Allocator.TempJob);
            GenColorUVs cJob = new()
            {
                MapSize = ChunkSizes,
                UVs = colors,
            };
            var cHandler = cJob.Schedule();

            NativeArray<int> triangles = new NativeArray<int>(chunkCellCount * 6, Allocator.TempJob);
            GenTrianglesJob trianglesJob = new GenTrianglesJob
            {
                MapSize = ChunkSizes,
                Triangles = triangles
            };
            var trianglesHandle = trianglesJob.Schedule();

            // Wait for Verticies to Finish because cant set uvs without them
            verticesHandle.Complete();
            mesh.vertices = verticiesJob.Verticies.ToArray();

            trianglesHandle.Complete();
            mesh.triangles = trianglesJob.Triangles.ToArray();

            uvHandle.Complete();
            mesh.SetUVs(0, uvJob.UVs);

            cHandler.Complete();
            mesh.SetColors(colors);

            // free
            vertices.Dispose();
            uvs.Dispose();
            colors.Dispose();
            triangles.Dispose();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            for (int y = 0; y < chunkMapSize.y; y++)
            {
                for (int x = 0; x < chunkMapSize.x; x++)
                {
                    var go = Instantiate(ChunkPrefab, gameObject.transform);

                    go.name = $"Chunk({x}, {y})";
                    go.transform.position = new Vector3(x * ChunkSize, y * ChunkSize);

                    var meshFilter = go.AddComponent<MeshFilter>();
                    meshFilter.mesh = mesh;

                    var meshRenderer = go.AddComponent<MeshRenderer>();
                    meshRenderer.material = Material;
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;

                    var collider = go.AddComponent<BoxCollider>();
                    collider.size = new(ChunkSize, ChunkSize, .01f);

                    ChunkMesh chunkMesh = new()
                    {
                        MeshFilter = meshFilter,
                        MeshRenderer = meshRenderer,
                        Collider = collider,
                    };
                    ChunkMeshes.Add(XYToChunkKey(x, y), chunkMesh);
                }
            }
            gameObject.SetActive(true);
        }

        private void Start()
        {
            for (int i = 0; i < Size; i++)
            {
                ChunkMap.Cells[i] = CellStateManager.Instance.Air.GetDefaultState();
            }
        }
        private void OnDestroy()
        {
            NewCells.Dispose();
            Mass.Dispose();
            NewMass.Dispose();
            ChunkMap.Cleanup();
        }

        public void Update()
        {
            HandleInputs();

            FallingLeft = !FallingLeft;

            ChunkMap.Cells.CopyTo(NewCells);
            Mass.CopyTo(NewMass);

            JobHandle lastHandle = new();
            NativeArray<JobHandle> handles = new(ChunkMap.Chunks.Capacity, Allocator.Temp);
            var keyvalues = ChunkMap.Chunks.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keyvalues.Length; i++)
            {
                ChunkMap.Chunks.TryGetValue(keyvalues[i], out var chunkSection);
                if (true)
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
                        CellStatesById = CellStateManager.Instance.CellStatesBlobIdMap,
                        CellStatesByName = CellStateManager.Instance.CellStatesBlobMap,
                        Air = CellStateManager.Instance.Air.GetDefaultState(),
                        ChunkKey = keyvalues[i],
                        ChunkSection = chunkSection,
                    };
                    lastHandle = job.ScheduleParallel(ChunkSize * ChunkSize, 1, lastHandle);
                    handles[i] = lastHandle;
                }
            }
            JobHandle.CompleteAll(handles);
            handles.Dispose();

            NewCells.CopyTo(ChunkMap.Cells);
            NewMass.CopyTo(Mass);
            
            for (int i = 0; i < keyvalues.Length; i++)
            {
                ChunkMap.Chunks.TryGetValue(keyvalues[i], out var chunkSection);
                if (chunkSection.IsDirty)
                {
                    chunkSection.IsDirty = false;
                    var mesh = ChunkMeshes[keyvalues[i]].MeshFilter.mesh;
                    var colors = mesh.colors;
                    int vertexIndex = 0;

                    for (int y = chunkSection.StartPos.y; y < chunkSection.StartPos.y + ChunkSize; y++)
                    {
                        for (int x = chunkSection.StartPos.x; x < chunkSection.StartPos.x + ChunkSize; x++)
                        {
                            int mapIndex = x + y * MapSize.x;
                            var state = ChunkMap.Cells[mapIndex];
                            if (!state.IsSolid)
                            {
                                if (Mass[mapIndex] >= MinMass)
                                {
                                    SetState(mapIndex, CellStateManager.Instance.Cells[1].GetDefaultState());
                                }
                                else
                                {
                                    SetState(mapIndex, CellStateManager.Instance.Cells[0].GetDefaultState());
                                }
                            }
                            else
                                Mass[mapIndex] = 0f;

                            // Get any updated values
                            state = ChunkMap.Cells[mapIndex];
                            if (state.Equals(CellStateManager.Instance.Cells[1].GetDefaultState()))
                                SetColor(vertexIndex, Color.Lerp(Color.cyan, Color.blue, Mass[mapIndex]), colors);
                            else
                                SetColor(vertexIndex, Float4ToColor(state.CellColor), colors);
                            vertexIndex += 4;
                        }
                    }
                    mesh.colors = colors;
                }
            }
            keyvalues.Dispose();
        }

        public Color Float4ToColor(float4 f4)
        {
            return new Color
            {
                r = f4.x,
                g = f4.y,
                b = f4.z,
                a = f4.w
            };
        }

        public void SetColor(int i, Color color, in Color[] colors)
        {
            colors[i + 0] = color;
            colors[i + 1] = color;
            colors[i + 2] = color;
            colors[i + 3] = color;
        }

        public void GetTile(int tileX, int tileY)
        {

        }

        public void LoadChunk()
        {

        }

        public int GetCellId(int x, int y)
        {
            x = Mathf.Clamp(x, 0, MapSize.x - 1);
            y = Mathf.Clamp(y, 0, MapSize.y - 1);
            return x + y * MapSize.x;
        }

        public static long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public static Vector2Int ChunkKeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));

        private void HandleInputs()
        {
            bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            bool leftClicked = Mouse.current.leftButton.isPressed;
            bool middleClicked = Mouse.current.middleButton.wasPressedThisFrame;
            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100.0f))
            {
                var diff = hit.point - transform.position;
                int x = (int)diff.x;
                int y = (int)diff.y;
                int chunkX = x / ChunkSize;
                int chunkY = y / ChunkSize;

                ChunkMap.LoadChunk(chunkX, chunkY);

                ChunkMap.LoadChunk(chunkX, chunkY + 1);
                ChunkMap.LoadChunk(chunkX + 1, chunkY + 1);
                ChunkMap.LoadChunk(chunkX + 1, chunkY);
                ChunkMap.LoadChunk(chunkX + 1, chunkY - 1);

                ChunkMap.LoadChunk(chunkX, chunkY - 1);
                ChunkMap.LoadChunk(chunkX - 1, chunkY - 1);
                ChunkMap.LoadChunk(chunkX - 1, chunkY);
                ChunkMap.LoadChunk(chunkX - 1, chunkY + 1);

                if (!rightClicked && !leftClicked && !middleClicked) return;
                bool shiftHeld = Keyboard.current.leftShiftKey.isPressed;
                if (rightClicked)
                {
                    if (shiftHeld)
                        SetState(x, y, CellStateManager.Instance.CellStatesBlobMap.Value.CellStates["default:air"]);
                    else
                        SetState(x, y, CellStateManager.Instance.CellStatesBlobMap.Value.CellStates["default:stone"]);
                }
                else if (leftClicked)
                {
                    if (shiftHeld)
                        SetState(x, y, CellStateManager.Instance.CellStatesBlobMap.Value.CellStates["default:sand"]);
                    else
                    {
                        SetState(x, y, CellStateManager.Instance.CellStatesBlobMap.Value.CellStates["default:fresh_water"]);
                        SetMass(x, y, .5f);
                    }
                }
                else if (middleClicked)
                {
                    //MarkAsInfiniteSource(x, y);
                }
            }
        }

        public void SetMass(int x, int y, float v)
        {
            var id = GetCellId(x, y);
            Mass[id] = Mathf.Clamp(Mass[id] + v, MinMass, MaxMass);
        }

        public void AddMass(int x, int y, float v)
        {
            int id = GetCellId(x, y);
            float mass = Mathf.Clamp(Mass[id] + v, MinMass, MaxMass);
            Mass[id] = mass;
        }

        public void SetState(int x, int y, CellStateData state, bool updateState = true)
        {
            int id = GetCellId(x, y);
            ChunkMap.Cells[id] = state;
            long key = ChunkMap.Int2ToChunkKey(ChunkMap.CellToChunkCoords(x, y));
            var chunk = ChunkMap.Chunks[key];
            chunk.IsDirty = true;
            ChunkMap.Chunks[key] = chunk;
        }

        public void SetState(int index, CellStateData state, bool updateState = true)
        {
            ChunkMap.Cells[index] = state;
        }

    }

    [BurstCompile]
    public struct ChunkCellsUpdateJob : IJobFor
    {
        [NativeDisableParallelForRestriction] [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public ChunkSection ChunkSection;
        [ReadOnly] public long ChunkKey;

        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public bool FallLeft;

        [ReadOnly] public BlobAssetReference<CellStateIdMap> CellStatesById;
        [ReadOnly] public BlobAssetReference<CellStatesBlobHashMap> CellStatesByName;

        [ReadOnly] public CellStateData Air;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<CellStateData> NewCells;

        [ReadOnly] public NativeArray<float> Mass;
        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;

        private bool m_HasChanged;

        public void Execute(int i)
        {
            int cx = i % ChunkMap.ChunkSize;
            int cy = i / ChunkMap.ChunkSize;
            int index = GetCellId(ChunkSection.StartPos.x + cx, ChunkSection.StartPos.y + cy);
            int x = index % ChunkMap.MapSize.x;
            int y = index / ChunkMap.MapSize.y;

            var state = ChunkMap.Cells[index];
            var stateSand = CellStatesByName.Value.CellStates["default:sand"];
            var stateWater = CellStatesByName.Value.CellStates["default:fresh_water"];
            if (state.Equals(stateSand))
            {
                SimulateSand(x, y, index);
            }
            else if (state.Equals(stateWater))
            {
                SimulateFluid(x, y, index);
            }

            if (m_HasChanged)
            {
                ChunkMap.Chunks.TryGetValue(ChunkKey, out ChunkSection chunk);
                chunk.IsDirty = m_HasChanged;
            }
        }

        private void SimulateFluid(int x, int y, int index)
        {
            float remainingMass = Mass[index];
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