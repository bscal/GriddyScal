using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.FluidSimulation.Cellular_Automata
{
    [DisableAutoCreation]
    public class CAFluidSystem : SystemBase
    {
        public TileMap2DArray Grid;
        public float MaxMass = 1.0f;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        public NativeArray<float> m_Mass;
        public NativeArray<float> m_NewMass;
        public NativeArray<int> m_States;
        public NativeArray<float4> m_Colors;

        protected override void OnStartRunning()
        {
            m_Mass = new NativeArray<float>(Grid.Size, Allocator.Persistent);
            m_NewMass = new NativeArray<float>(Grid.Size, Allocator.Persistent);
            m_States = new NativeArray<int>(Grid.Size, Allocator.Persistent);
            m_Colors = new NativeArray<float4>(Grid.Size * 4, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_Mass.Dispose();
            m_NewMass.Dispose();
            m_States.Dispose();
            m_Colors.Dispose();
        }

        protected override void OnUpdate()
        {
            m_NewMass.CopyFrom(m_Mass);

            SimulateCompressionJob simulateCompressionJob = new SimulateCompressionJob
            {
                Width = Grid.MapSize.x,
                Height = Grid.MapSize.y,
                MaxMass = MaxMass,
                MinMass = MinMass,
                MaxCompression = MaxCompression,
                MinFlow = MinFlow,
                MaxSpeed = MaxSpeed,
                MaxMassSqr = MaxMass * MaxMass,
                Mass = m_Mass,
                NewMass = m_NewMass,
                States = m_States,
            };
            JobHandle simulationHandle = simulateCompressionJob.ScheduleParallel(m_States.Length, 1, this.Dependency);
            simulationHandle.Complete();

            m_NewMass.CopyTo(m_Mass);

            UpdateStates updateStates = new UpdateStates()
            {
                MinMass = MinMass,
                Mass = m_Mass,
                States = m_States,
                Colors = m_Colors,
            };

            JobHandle updateHandle = updateStates.ScheduleParallel(m_States.Length, 64, this.Dependency);
            updateHandle.Complete();
            Grid.SetMeshColors(m_Colors.Reinterpret<Vector4>().ToArray());
        }
    }

    public struct UpdateStates : IJobFor
    {
        [ReadOnly] public static readonly float4 BLACK = new(0f, 0f, 0f, 1f);
        [ReadOnly] public static readonly float4 WHITE = new(1f, 1f, 1f, 1f);
        [ReadOnly] public static readonly float4 CYAN = new(0f, 1f, 1f, 1f);
        [ReadOnly] public static readonly float4 BLUE = new(0f, 0f, 1f, 1f);

        [ReadOnly] public float MinMass;
        public NativeArray<float> Mass;
        public NativeArray<int> States;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float4> Colors;

        public void Execute(int index)
        {
            int state = States[index];
            if (state != CellState.STATE_GROUND)
            {
                if (Mass[index] >= MinMass)
                {
                    States[index] = CellState.STATE_WATER;
                }   
                else
                {
                    //Mass[index] = 0;
                    States[index] = CellState.STATE_AIR;
                }
            }

            if (state == CellState.STATE_GROUND) SetColor(index, BLACK);
            else if (state == CellState.STATE_AIR) SetColor(index, WHITE);
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

    public struct SimulateCompressionJob : IJobFor
    {
        [ReadOnly] public int Width, Height;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public NativeArray<float> Mass;
        [ReadOnly] public NativeArray<int> States;

        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;

        public void Execute(int i)
        {
            int x = i % Width;
            int y = i / Width;
            int index = GetCellId(x, y);
            float remainingMass = Mass[index];
            if (remainingMass <= 0) return;
            if (States[index] == CellState.STATE_GROUND) return;
            float flow;
            // Down
            int downId = GetCellId(x, y - 1);
            if (InBounds(x, y - 1) && States[downId] != CellState.STATE_GROUND)
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
            if (InBounds(x - 1, y) && States[leftId] != CellState.STATE_GROUND)
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
            if (InBounds(x + 1, y) && States[rightId] != CellState.STATE_GROUND)
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
            if (InBounds(x, y + 1) && States[upId] != CellState.STATE_GROUND)
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

    [RequireComponent(typeof(TileMap2DArray))]
    public class CellularAutomataFluidControllerV2 : MonoBehaviour
    {

        public TileMap2DArray Grid;

        private CAFluidSystem m_CAFluidSystem;

        private void Awake()
        {
            m_CAFluidSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<CAFluidSystem>();
            m_CAFluidSystem.Grid = Grid;
        }

        // Update is called once per frame
        void Update()
        {
            HandleInputs();
            if (m_CAFluidSystem.Enabled)
            {
                m_CAFluidSystem.Update();
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
                Debug.Log($"mass = {m_CAFluidSystem.m_Mass[x + y * Grid.MapSize.x]}");
                if (rightClicked)
                {
                    SetState(x, y, CellState.STATE_GROUND);
                }
                else if (leftClicked)
                {
                    SetState(x, y, CellState.STATE_WATER);
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
            m_CAFluidSystem.m_States[GetCellId(x, y)] = CellState.STATE_INFINITE;
        }

        public void SetMass(int x, int y, float v)
        {
            var id = GetCellId(x, y);
            m_CAFluidSystem.m_Mass[id] = Mathf.Clamp(m_CAFluidSystem.m_Mass[id] + v, m_CAFluidSystem.MinMass, m_CAFluidSystem.MaxMass);
        }

        public void AddMass(int x, int y, float v)
        {
            int id = GetCellId(x, y);
            float mass = Mathf.Clamp(m_CAFluidSystem.m_Mass[id] + v, m_CAFluidSystem.MinMass, m_CAFluidSystem.MaxMass);
            m_CAFluidSystem.m_Mass[id] = mass;
        }

        public void SetState(int x, int y, int state, bool updateState = true)
        {
            int id = GetCellId(x, y);
            m_CAFluidSystem.m_States[id] = state;
        }

        public int GetCellId(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Grid.MapSize.x - 1);
            y = Mathf.Clamp(y, 0, Grid.MapSize.y - 1);
            return x + y * Grid.MapSize.x;
        }
    }

    public struct CellState
    {
        public const int STATE_NONE = -1;
        public const int STATE_AIR = 0;
        public const int STATE_GROUND = 1;
        public const int STATE_WATER = 2;
        public const int STATE_INFINITE = 3;

        public int State;
    }
}