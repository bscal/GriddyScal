using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.FluidSimulation.Cellular_Automata
{
    [DisableAutoCreation]
    public class CAFluidSystem : SystemBase
    {
        public int Width, Height;
        public float MaxMass = 1.0f;
        public float MinMass = 0.0001f;
        public float MaxCompression = 0.02f;
        public float MinFlow = .01f;
        public float MaxSpeed = 1f;

        public NativeArray<float> m_Mass;
        public NativeArray<float> m_NewMass;
        public NativeArray<int> m_States;

        protected override void OnStartRunning()
        {
            Debug.Log("STARTED");
            int size = Width * Height;
            m_Mass = new NativeArray<float>(size, Allocator.Persistent);
            m_NewMass = new NativeArray<float>(size, Allocator.Persistent);
            m_States = new NativeArray<int>(size, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_Mass.Dispose();
            m_NewMass.Dispose();
            m_States.Dispose();
        }

        protected override void OnUpdate()
        {
            SimulateCompressionJob simulateCompressionJob = new SimulateCompressionJob
            {
                Width = Width,
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
                States = m_States
            };
            JobHandle updateHandle = updateStates.ScheduleParallel(m_States.Length, 64, this.Dependency);
            updateHandle.Complete();
        }
    }

    public struct UpdateStates : IJobFor
    {
        [ReadOnly] public float MinMass;
        public NativeArray<float> Mass;
        public NativeArray<int> States;

        public void Execute(int index)
        {
            if (States[index] == CellState.STATE_GROUND) return;
            else if (States[index] == CellState.STATE_INFINITE)
            {
                Mass[index] = 1;
                return;
            }
            States[index] = (Mass[index] > MinMass) ? CellState.STATE_WATER : CellState.STATE_AIR;
        }
    }

    public struct SimulateCompressionJob : IJobFor
    {
        [ReadOnly] public int Width;
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public NativeArray<float> Mass;
        [ReadOnly] public NativeArray<int> States;

        [WriteOnly] public NativeArray<float> NewMass;

        public void Execute(int index)
        {
            if (States[index] == CellState.STATE_GROUND) return;

            float remainingMass = Mass[index];
            if (remainingMass <= 0) return;

            float flow;
            // Down
            int downId = index - Width;
            if (States[downId] != CellState.STATE_GROUND)
            {
                flow = GetStableMass(remainingMass + Mass[downId]) - Mass[downId];
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = Mathf.Clamp(flow, 0, Mathf.Min(MaxSpeed, remainingMass));

                NewMass[index] -= flow;
                NewMass[downId] += flow;
                remainingMass -= flow;
            }

            if (remainingMass <= 0) return;

            // Left
            int leftId = index - 1;
            if (States[leftId] != CellState.STATE_GROUND)
            {
                flow = (Mass[index] - Mass[leftId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = Mathf.Clamp(flow, 0, remainingMass);

                NewMass[index] -= flow;
                NewMass[leftId] += flow;
                remainingMass -= flow;
            }


            if (remainingMass <= 0) return ;

            // Right
            int rightId = index + 1;
            if (States[rightId] != CellState.STATE_GROUND)
            {
                flow = (Mass[index] - Mass[rightId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = Mathf.Clamp(flow, 0, remainingMass);

              NewMass[index] -= flow;
                NewMass[rightId] += flow;
                remainingMass -= flow;
            }

            if (remainingMass <= 0) return;

            // Up
            int upId = index + Width;
            if (States[upId] != CellState.STATE_GROUND)
            {
                flow = remainingMass - GetStableMass(remainingMass + Mass[upId]);
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = Mathf.Clamp(flow, 0, Mathf.Min(MaxSpeed, remainingMass));

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
    }

    [RequireComponent(typeof(TileMap2DArray))]
    public class CellularAutomataFluidControllerV2 : MonoBehaviour
    {

        public TileMap2DArray Grid;

        private CAFluidSystem m_CAFluidSystem;

        private void Awake()
        {
            m_CAFluidSystem = World.DefaultGameObjectInjectionWorld.CreateSystem<CAFluidSystem>();
            m_CAFluidSystem.Width = Grid.MapSize.x;
            m_CAFluidSystem.Height = Grid.MapSize.y;
        }

        // Update is called once per frame
        void Update()
        {
            if (m_CAFluidSystem.Enabled)
            {
                m_CAFluidSystem.Update();
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