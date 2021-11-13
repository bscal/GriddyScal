using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;

namespace Common.FluidSimulation.SPH
{
    public struct SPHParticle : IComponentData
    {
        public float3 velocity;
        public float3 force;
        public float density;
        public float pressure;
    }
}
