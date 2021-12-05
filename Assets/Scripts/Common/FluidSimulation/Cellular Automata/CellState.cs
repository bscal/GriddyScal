using System;
using Unity.Collections;
using UnityEngine;

namespace Common.FluidSimulation.Cellular_Automata
{
    public class CellRegistry
    {
        //public static readonly CellRegistry INSTANCE = new();

        public event Action<CellRegistry> LoadStates;

        public readonly NativeList<TileState> States;

        public readonly TileState AIR;
        public readonly TileState STONE;
        public readonly TileState WOOD;
        public readonly TileState FRESH_WATER;
        public readonly TileState SAND;

        private ushort m_InternalIndex;

        public CellRegistry()
        {
            States = new(Allocator.Persistent);

            AIR = Register(false);
            STONE = Register(true);
            WOOD = Register(true);
            FRESH_WATER = Register(false);
            SAND = Register(true);

            LoadStates?.Invoke(this);
        }

        ~CellRegistry()
        {
            States.Dispose();
        }

        public TileState Register(bool isSolid)
        {
            var state = new TileState
            {
                StateId = m_InternalIndex++,
                IsSolid = isSolid,
            };
            States.Add(state);
            return state;
        }

        public TileState Get(TileState state)
        {
            return States[state.StateId];
        }
    }

    public struct TileState : IEquatable<TileState>
    {
        public ushort StateId;
        public bool IsSolid;

        public bool IsAir => StateId == 0;

        public bool Equals(TileState other) => StateId == other.StateId;
    }

    public struct CellState
    {
        public const ushort STATE_AIR = 0;
        public const ushort STATE_GROUND = 1;
        public const ushort STATE_WATER = 2;
        public const ushort STATE_INFINITE = 3;
        public const ushort STATE_SAND = 4;
    }
}