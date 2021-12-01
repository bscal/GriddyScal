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

        private TilePhysicsSystem m_TilePhysicsSystem;

        public NativeArray<CellStateV2> States;
        public NativeArray<float4> Colors;

        private void Awake()
        {
            States = new NativeArray<CellStateV2>(Grid.Size, Allocator.Persistent);
            Colors = new NativeArray<float4>(Grid.Size * 4, Allocator.Persistent);

            m_TilePhysicsSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<TilePhysicsSystem>();
            m_TilePhysicsSystem.Grid = Grid;
        }

        private void OnDestroy()
        {
            States.Dispose();
            Colors.Dispose();
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
                Debug.Log($"{x}, {y}");
                Debug.Log($"mass = {m_TilePhysicsSystem.Mass[x + y * Grid.MapSize.x]}");
                if (rightClicked)
                {
                    SetState(x, y, CellRegistry.INSTANCE.STONE);
                }
                else if (leftClicked)
                {
                    SetState(x, y, CellRegistry.INSTANCE.FRESH_WATER);
                    SetMass(x, y, .5f);
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

        public void SetState(int x, int y, CellStateV2 state, bool updateState = true)
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
        public float MaxMass = 1.0f;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        public NativeArray<float> Mass;
        public NativeArray<float> NewMass;

        protected override void OnStartRunning()
        {
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
            NewMass.CopyFrom(Mass);

            SimulationJob simulateCompressionJob = new()
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
            JobHandle simulationHandle = simulateCompressionJob.ScheduleParallel(Mass.Length, 1, this.Dependency);
            simulationHandle.Complete();

            NewMass.CopyTo(Mass);

            UpdateTileStates updateStates = new()
            {
                MinMass = MinMass,
                Mass = Mass,
                States = GridStates.States,
                Colors = GridStates.Colors,
            };

            JobHandle updateHandle = updateStates.ScheduleParallel(Mass.Length, 64, this.Dependency);
            updateHandle.Complete();
            Grid.SetMeshColors(GridStates.Colors.Reinterpret<Vector4>().ToArray());
        }
    }



    public struct UpdateTileStates : IJobFor
    {
        [ReadOnly] static readonly NamespacedKey AIR = new NamespacedKey("AIR");
        [ReadOnly] static readonly NamespacedKey STONE = new NamespacedKey("STONE");
        [ReadOnly] static readonly NamespacedKey FRESH_WATER = new NamespacedKey("FRESH_WATER");

        [ReadOnly] public static readonly float4 BLACK = new(0f, 0f, 0f, 1f);
        [ReadOnly] public static readonly float4 WHITE = new(1f, 1f, 1f, 1f);
        [ReadOnly] public static readonly float4 CYAN = new(0f, 1f, 1f, 1f);
        [ReadOnly] public static readonly float4 BLUE = new(0f, 0f, 1f, 1f);

        [ReadOnly] public float MinMass;
        [ReadOnly] public NativeHashMap<NamespacedKey, CellStateV2> StatesMap;
        public NativeArray<float> Mass;
        public NativeArray<CellStateV2> States;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float4> Colors;

        public void Execute(int index)
        {
            var state = States[index];
            if (!state.IsSolid)
            {
                if (Mass[index] >= MinMass)
                {
                    States[index] = StatesMap[FRESH_WATER];
                }
                else
                {
                    States[index] = StatesMap[AIR];
                }
            }

            if (state.Equals(StatesMap[STONE])) SetColor(index, BLACK);
            else if (state.Equals(StatesMap[AIR])) SetColor(index, WHITE);
            else SetColor(index, math.lerp(CYAN, BLUE, Mass[index]));
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

    public struct SandSimulationJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public int Width, Height;
        [ReadOnly] public bool FallLeft;
        [ReadOnly] public NativeArray<ushort> States;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<ushort> NewStates;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery)
        {
            throw new System.NotImplementedException();
        }

        public void Execute(int i)
        {
            int x = i % Width;
            int y = i / Width;
            int index = GetCellId(x, y);
            // If downwards falling blocks (sand) are at the bottom of map, they have nowhere to go.
            if (InBounds(x, y - 1)) return;

            int down = GetCellId(x, y - 1);


            if (InBounds(x, y - 1))

            if (FallLeft)
            {
                int left = GetCellId(x - 1, y);
            }
            
            else
            {
                int right = GetCellId(x + 1, y);
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

    public struct SimulationJob : IJobFor
    {
        [ReadOnly] public int Width, Height;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public NativeArray<CellStateV2> States;
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
            if (InBounds(x, y - 1) && States[downId].IsSolid)
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
            if (InBounds(x - 1, y) && States[leftId].IsSolid)
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
            if (InBounds(x + 1, y) && States[rightId].IsSolid)
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
            if (InBounds(x, y + 1) && States[upId].IsSolid)
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
}
