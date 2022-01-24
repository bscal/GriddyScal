using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;
using Common.Utils;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace Common.Grids
{
    [CreateAssetMenu(fileName = "Cell", menuName = "Game/CellState")]
    public class CellStateScriptableObject : ScriptableObject, ISerializationCallbackReceiver
    {
        public string Name;
        public NamespacedKey NamespacedKey;
        public int NamespaceHash;
        public Color Color;

        public bool IsSolid;

        public CellStateData GetDefaultState()
        {
            return new CellStateData()
            {
                Id = NamespaceHash,
                CellColor = Utils.Utils.ColorToFloat4(Color),
                IsSolid = IsSolid,
            };
        }

        public void OnAfterDeserialize()
        {
            NamespaceHash = NamespacedKey.Value.GetStableHashCode();
        }

        public void OnBeforeSerialize()
        {
            
        }
    }

    public struct CellState2
    {
        public int Id;
        public float4 CellColor;
        public bool IsSolid;
    }

    [MessagePackObject]
    public struct CellStateData : IEquatable<CellStateData>
    {
        [Key(0)]
        public int Id;
        [Key(1)]
        public float4 CellColor;
        [Key(2)]
        public bool IsSolid;

        public bool Equals(CellStateData other) => Id == other.Id;
    }
}