using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Entities;

namespace Common.FluidSimulation.Components
{
    public struct TileComponent : IComponentData
    {
        public int x, y;
    }

    public struct TileState : IComponentData
    {
        public ushort Front, Back;
    }

    public struct TileFluid : IComponentData
    {
        public float Mass, NewMass;
    }

    public struct TileSand : IComponentData
    {

    }
}
