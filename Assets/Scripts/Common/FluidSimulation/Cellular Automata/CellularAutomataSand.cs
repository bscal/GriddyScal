using Common.Grids;
using Common.Grids.Cells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.FluidSimulation.Cellular_Automata
{

    [RequireComponent(typeof(TileMap2DArray))]
    public class CellularAutomataSand : MonoBehaviour
    {
        public TileMap2DArray Grid;

        public TilePhysicsSystem m_TilePhysicsSystem;
        private CellStateManager m_CellStateManagerRef;

        public NativeArray<CellStateData> States;
        public NativeArray<CellStateData> NewStates;

        public static readonly int FRESH_WATER = Utils.Utils.GetStableHashCode("default:fresh_water");
        public static readonly int SAND = Utils.Utils.GetStableHashCode("default:sand");
        public static readonly int AIR = Utils.Utils.GetStableHashCode("default:air");

        private void Awake()
        {
            States = new NativeArray<CellStateData>(Grid.Size, Allocator.Persistent);
            NewStates = new NativeArray<CellStateData>(Grid.Size, Allocator.Persistent);

            m_TilePhysicsSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<TilePhysicsSystem>();
            m_TilePhysicsSystem.Grid = Grid;
            m_TilePhysicsSystem.GridStates = this;
        }

        private void Start()
        {
            m_CellStateManagerRef = CellStateManager.Instance;
        }

        private void OnDestroy()
        {
            States.Dispose();
            NewStates.Dispose();
        }

        // Update is called once per frame
        void Update()
        {
            HandleInputs();
            if (m_TilePhysicsSystem.Enabled)
            {
                m_TilePhysicsSystem.Update();
            }
        }

        private void HandleInputs()
        {
            bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            bool leftClicked = Mouse.current.leftButton.isPressed;
            bool middleClicked = Mouse.current.middleButton.wasPressedThisFrame;

            if (!rightClicked && !leftClicked && !middleClicked) return;

            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100.0f))
            {
                var diff = hit.point - transform.position;
                int x = (int)diff.x;
                int y = (int)diff.y;

                bool shiftHeld = Keyboard.current.leftShiftKey.isPressed;


                if (rightClicked)
                {
                    if (shiftHeld)
                        SetState(x, y, CellStateManager.Instance.States.Air.GetDefaultState());
                    else
                        SetState(x, y, CellStateManager.Instance.States.Stone.GetDefaultState());
                }
                else if (leftClicked)
                {
                    if (shiftHeld)
                        SetState(x, y, CellStateManager.Instance.States.Sand.GetDefaultState());
                    else
                    {
                        SetState(x, y, CellStateManager.Instance.States.FreshWater.GetDefaultState());
                        SetMass(x, y, .5f);
                    }
                }
                else if (middleClicked)
                {
                    MarkAsInfiniteSource(x, y);
                }
            }
        }

        public void MarkAsInfiniteSource(int x, int y)
        {
        }

        public void SetMass(int x, int y, float v)
        {
            var id = GetCellId(x, y);
            m_TilePhysicsSystem.Mass[id] = Mathf.Clamp(m_TilePhysicsSystem.Mass[id] + v, m_TilePhysicsSystem.MinMass, m_TilePhysicsSystem.MaxMass);
        }

        public void AddMass(int x, int y, float v)
        {
            int id = GetCellId(x, y);
            float mass = Mathf.Clamp(m_TilePhysicsSystem.Mass[id] + v, m_TilePhysicsSystem.MinMass, m_TilePhysicsSystem.MaxMass);
            m_TilePhysicsSystem.Mass[id] = mass;
        }

        public void SetState(int x, int y, CellStateData state, bool updateState = true)
        {
            int id = GetCellId(x, y);
            States[id] = state;
        }

        public int GetCellId(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Grid.MapSize.x - 1);
            y = Mathf.Clamp(y, 0, Grid.MapSize.y - 1);
            return x + y * Grid.MapSize.x;
        }
    }

    [DisableAutoCreation]
    public class TilePhysicsSystem : SystemBase
    {
        public TileMap2DArray Grid;
        public CellularAutomataSand GridStates;
        public int Count;
        public float MaxMass = 1.0f;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        public NativeArray<float> Mass;
        public NativeArray<float> NewMass;

        private bool m_FallingLeft;

        protected override void OnStartRunning()
        {
            Count = Grid.Size;
            Mass = new NativeArray<float>(Grid.Size, Allocator.Persistent);
            NewMass = new NativeArray<float>(Grid.Size, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            Mass.Dispose();
            NewMass.Dispose();
        }

        protected override void OnUpdate()
        {
            m_FallingLeft = !m_FallingLeft;
            NewMass.CopyFrom(Mass);
            GridStates.NewStates.CopyFrom(GridStates.States);

            /*            SimulationJob simulateCompressionJob = new()
                        {
                            Width = Grid.MapSize.x,
                            Height = Grid.MapSize.y,
                            MaxMass = MaxMass,
                            MinMass = MinMass,
                            MaxCompression = MaxCompression,
                            MinFlow = MinFlow,
                            MaxSpeed = MaxSpeed,
                            MaxMassSqr = MaxMass * MaxMass,
                            Mass = Mass,
                            NewMass = NewMass,
                            States = GridStates.States,
                        };
                        JobHandle simulationHandle = simulateCompressionJob.ScheduleParallel(Count, 1, this.Dependency);
                        simulationHandle.Complete();

                        NewMass.CopyTo(Mass);

                        GridStates.NewStates.CopyFrom(GridStates.States);

                        SandSimulationJob sandSimJob = new()
                        {
                            Width = Grid.MapSize.x,
                            Height = Grid.MapSize.y,
                            FallLeft = (m_FallingLeft = !m_FallingLeft),
                            States = GridStates.States,
                            NewStates = GridStates.NewStates,
                            CellStates = CellStateManager.Instance.CellStatesBlobIdMap,
                            AIR = CellularAutomataSand.AIR,
                            SAND = CellularAutomataSand.SAND
                        };
                        JobHandle sandSimHandle = sandSimJob.ScheduleParallel(Count, 1, this.Dependency);
                        sandSimHandle.Complete();

                        GridStates.States.CopyFrom(GridStates.NewStates);

                        UpdateTileStates updateStates = new()
                        {
                            MinMass = MinMass,
                            Mass = Mass,
                            States = GridStates.States,
                            Colors = Grid.Colors,
                            CellStates = CellStateManager.Instance.CellStatesBlobIdMap,
                            AIR = CellularAutomataSand.AIR,
                            FRESH_WATER = CellularAutomataSand.FRESH_WATER

                        };
                        JobHandle updateHandle = updateStates.ScheduleParallel(Count, 1, this.Dependency);
                        updateHandle.Complete();*/


            UpdateChunkJob updateJob = new UpdateChunkJob
            {
                ChunkSize = Grid.ChunkSize,
                MapWidth = Grid.MapSize.x,
                MapHeight = Grid.MapSize.y,
                MaxCompression = MaxCompression,
                MaxMass = MaxMass,
                MinMass = MinMass,
                MaxMassSqr = MaxMass * MaxMass,
                MaxSpeed = MaxSpeed,
                MinFlow = MinFlow,
                FallLeft = m_FallingLeft,
                Cells = GridStates.States,
                NewCells = GridStates.NewStates,
                Mass = Mass,
                NewMass = NewMass,
                CellStates = CellStateManager.Instance.CellStatesBlobMap,
                Chunks = Grid.NativeChunkMap,
            };
            JobHandle handle = updateJob.Schedule(Grid.Size, 1);
            handle.Complete();

            NewMass.CopyTo(Mass);
            GridStates.NewStates.CopyTo(GridStates.States);
        }
    }


    [BurstCompile]
    public struct UpdateTileStates : IJobFor
    {
        [ReadOnly] public static readonly float4 CYAN = new(0f, 1f, 1f, 1f);
        [ReadOnly] public static readonly float4 BLUE = new(0f, 0f, 1f, 1f);

        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStates;
        [ReadOnly] public float MinMass;
        public NativeArray<float> Mass;
        public NativeArray<CellStateData> States;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float4> Colors;

        [ReadOnly] public CellStateData Air;

        public void Execute(int index)
        {
            var state = States[index];
            if (!state.IsSolid)
            {
                if (Mass[index] >= MinMass)
                {
                    States[index] = CellStates.Value.CellStates["default:fresh_water"];
                }
                else
                {
                    States[index] = Air;
                }
            }
            else
                Mass[index] = 0f;

            // Get any updated values
            state = States[index];
            if (state.Equals(CellStates.Value.CellStates["default:fresh_water"]))
                SetColor(index, math.lerp(CYAN, BLUE, Mass[index]));
            else
                SetColor(index, state.CellColor);
        }

        private void SetColor(int index, float4 color)
        {
            int offset = index * 4;
            Colors[offset + 0] = color;
            Colors[offset + 1] = color;
            Colors[offset + 2] = color;
            Colors[offset + 3] = color;
        }
    }

    [BurstCompile]
    public struct SandSimulationJob : IJobFor
    {
        [ReadOnly] public int Width, Height;
        [ReadOnly] public bool FallLeft;
        [ReadOnly] public NativeArray<CellStateData> States;
        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStates;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<CellStateData> NewStates;


        [ReadOnly] public CellStateData Air;

        public void Execute(int index)
        {
            int x = index % Width;
            int y = index / Width;
            // If downwards falling blocks (sand) are at the bottom of map, they have nowhere to go.
            if (!InBounds(x, y - 1) || !States[index].Equals(CellStates.Value.CellStates["default:sand"])) return;

            int down = GetCellId(x, y - 1);
            if (!States[down].IsSolid)
            {
                // Handle downwards movement
                NewStates[down] = States[index];
                NewStates[index] = Air;
            }
            else
            {
                if (FallLeft)
                {
                    int left = GetCellId(x - 1, y - 1);
                    if (InBounds(x - 1, y - 1) && !States[left].IsSolid)
                    {
                        // Handle leftward movement
                        NewStates[left] = States[index];
                        NewStates[index] = Air;
                    }
                }
                else
                {
                    int right = GetCellId(x + 1, y - 1);
                    if (InBounds(x + 1, y - 1) && !States[right].IsSolid)
                    {
                        // Handle rightward movement
                        NewStates[right] = States[index];
                        NewStates[index] = Air;
                    }
                }
            }
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
    }

    [BurstCompile]
    public struct SimulationJob : IJobFor
    {
        [ReadOnly] public int Width, Height;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public NativeArray<CellStateData> States;
        [ReadOnly] public NativeArray<float> Mass;

        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;

        public void Execute(int i)
        {
            int x = i % Width;
            int y = i / Width;
            int index = GetCellId(x, y);
            float remainingMass = Mass[index];
            if (remainingMass <= 0) return;
            if (States[index].IsSolid) return;
            float flow;
            // Down
            int downId = GetCellId(x, y - 1);
            if (InBounds(x, y - 1) && !States[downId].IsSolid)
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
            if (InBounds(x - 1, y) && !States[leftId].IsSolid)
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
            if (InBounds(x + 1, y) && !States[rightId].IsSolid)
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
            if (InBounds(x, y + 1) && !States[upId].IsSolid)
            {
                flow = remainingMass - GetStableMass(remainingMass + Mass[upId]);
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[upId] += flow;
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
    }

    [BurstCompile]
    public struct UpdateChunkJob : IJobParallelFor
    {
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int MapWidth;
        [ReadOnly] public int MapHeight;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public bool FallLeft;
        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStates;
        [ReadOnly] public CellStateData Sand;
        [ReadOnly] public CellStateData Air;
        [ReadOnly] public CellStateData Water;
        [NativeDisableParallelForRestriction] public NativeHashMap<long, ChunkData> Chunks;

        [ReadOnly] public NativeArray<CellStateData> Cells;
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<CellStateData> NewCells;

        [ReadOnly] public NativeArray<float> Mass;
        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;

        private bool m_HasChanged;

        public void Execute(int i)
        {
            int x = i % MapWidth;
            int y = i / MapHeight;
            int index = GetCellId(x, y);
            int chunkX = x / ChunkSize;
            int chunkY = y / ChunkSize;
            long chunkKey = XYToKey(chunkX, chunkY);

            var chunk = Chunks[chunkKey];
            if (chunk.State != ChunkState.LOADED) return;

            if (Cells[index].Equals(Sand))
            {
                SimulateSand(x, y, index);
            }
            else if (Cells[index].Equals(Water))
            {
                SimulateFluid(x, y, index, index);
            }

            if (m_HasChanged)
            {
                chunk.IsDirty = m_HasChanged;
                Chunks[chunkKey] = chunk;
            }
        }

        private void SimulateFluid(int x, int y, int index, int localIndex)
        {
            float remainingMass = Mass[localIndex];
            if (remainingMass <= 0) return;
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
                m_HasChanged = true;
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
                m_HasChanged = true;
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
                m_HasChanged = true;
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
                m_HasChanged = true;
            }
        }

        private void SimulateSand(int x, int y, int index)
        {
            if (!InBounds(x, y - 1)) return;

            int down = GetCellId(x, y - 1);
            if (!Cells[down].IsSolid)
            {
                // Handle downwards movement
                NewCells[down] = Cells[index];
                NewCells[index] = Air;
                m_HasChanged = true;
            }
            else
            {
                if (FallLeft)
                {
                    int left = GetCellId(x - 1, y - 1);
                    if (InBounds(x - 1, y - 1) && !Cells[left].IsSolid)
                    {
                        // Handle leftward movement
                        NewCells[left] = Cells[index];
                        NewCells[index] = Air;
                        m_HasChanged = true;
                    }
                }
                else
                {
                    int right = GetCellId(x + 1, y - 1);
                    if (InBounds(x + 1, y - 1) && !Cells[right].IsSolid)
                    {
                        // Handle rightward movement
                        NewCells[right] = Cells[index];
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
            return x > -1 && y > -1 && x < MapWidth && y < MapHeight;
        }

        private int GetCellId(int x, int y)
        {
            x = math.clamp(x, 0, MapWidth - 1);
            y = math.clamp(y, 0, MapHeight - 1);
            return x + y * MapWidth;
        }

        private long XYToKey(int x, int y) => (long)y << 32 | (long)x;

        private Vector2Int KeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));
    }
}
