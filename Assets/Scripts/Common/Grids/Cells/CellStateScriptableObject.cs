using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;
using Common.Utils;

namespace Common.Grids
{
    [CreateAssetMenu(fileName = "Cell", menuName = "Game/CellState")]
    public class CellStateScriptableObject : ScriptableObject
    {
        public string Name;
        public NamespacedKey NamespacedKey;
        public Color Color;

        public bool IsSolid;

        public CellStateData GetDefaultState()
        {
            return new CellStateData()
            {
                CellColor = Utils.Utils.ColorToFloat4(Color),
                IsSolid = IsSolid,
            };
        }
    }

    public struct CellStateData : IEquatable<CellStateData>
    {
        public float4 CellColor;
        public bool IsSolid;

        public bool Equals(CellStateData other) => NamespacedKey.Value == other.NamespacedKey.Value;
    }
}