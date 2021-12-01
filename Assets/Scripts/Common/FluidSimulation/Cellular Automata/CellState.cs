using System;
using Unity.Collections;
using UnityEngine;

namespace Common.FluidSimulation.Cellular_Automata
{
    public class CellRegistry
    {
        public static readonly CellRegistry INSTANCE = new();

        public event Action<CellRegistry> LoadStates;

        public readonly NativeList<CellStateV2> States;
        public readonly NativeHashMap<NamespacedKey, CellStateV2> StatesMap;

        public readonly CellStateV2 AIR;
        public readonly CellStateV2 STONE;
        public readonly CellStateV2 WOOD;
        public readonly CellStateV2 FRESH_WATER;
        public readonly CellStateV2 SAND;

        private ushort m_InternalIndex;

        public CellRegistry()
        {
            States = new(Allocator.Persistent);

            AIR = Register("AIR", false);
            STONE = Register("STONE", true);
            WOOD = Register("WOOD", true);
            FRESH_WATER = Register("FRESH_WATER", false);
            SAND = Register("SAND", true);

            LoadStates?.Invoke(this);
        }

        ~CellRegistry()
        {
            States.Dispose();
        }

        public CellStateV2 Register(string key, bool isSolid)
        {
            var state = new CellStateV2
            {
                StateId = m_InternalIndex++,
                IsSolid = isSolid,
            };
            States.Add(state);
            StatesMap.Add(new NamespacedKey(key), state);
            return state;
        }

        public CellStateV2 Get(CellStateV2 state)
        {
            return States[state.StateId];
        }
    }

    public readonly struct NamespacedKey : IEquatable<NamespacedKey>
    {
        public const string DEFAULT_NAMESPACE = "default";

        public readonly string Namespace;
        public readonly string Id;

        public NamespacedKey(string id)
        {
            Namespace = DEFAULT_NAMESPACE;
            Id = id;
        }

        public NamespacedKey(string keyNamespace, string id)
        {
            Namespace = keyNamespace;
            Id = id; ;
        }

        private NamespacedKey(string[] keyArray)
        {
            Namespace = keyArray[0];
            Id = keyArray[1];
        }

        public static NamespacedKey FromString(string value)
        {
            var split = value.Split(':');
            if (split.Length == 2) return new NamespacedKey(split);
#if DEBUG
            Debug.LogError("Unable to create NamespacedKey from String: " + value);
#endif
            return new NamespacedKey("INVALID", value);
        }

        public override string ToString() => Namespace + ":" + Id;

        public bool Equals(NamespacedKey other) => this.Namespace == other.Namespace && this.Id == other.Id;
    }

    public struct CellStateV2 : IEquatable<CellStateV2>
    {
        public ushort StateId;
        public bool IsSolid;


        public bool IsAir => StateId == 0;

        public bool Equals(CellStateV2 other) => StateId == other.StateId;
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