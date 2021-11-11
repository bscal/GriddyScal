using System.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Common.Entities.Components
{
    public struct TileVelocityComponent : IComponentData
    {
        public float3 Velocity;
        public float3 PrevVelocity;
    }

    public struct TileDensityComponent : IComponentData
    {
        public float Density;
        public float PrevDensity;
    }

    public struct TileComponent : IComponentData
    {
        public int id;
        public readonly int x, y;
    }

    public struct TileWorldComponent : ISharedComponentData
    {
        public int2 Size;
    }
}