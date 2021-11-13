using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Transforms;

namespace Common.FluidSimulation.SPH
{
    class SPHSystems : SystemBase
    {
        EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = this.GetEntityQuery(typeof(SPHParticle), typeof(Translation));
        }

        protected override void OnUpdate()
        {
            SPHJob job = new() {
                VIEW_WIDTH = 64,
                VIEW_HEIGHT = 64f,
                VIEW_DEPTH = 64,
                ParticleHandle = this.GetComponentTypeHandle<SPHParticle>(false),
                TranslationHandle = this.GetComponentTypeHandle<Translation>(false),
            };
            this.Dependency = job.ScheduleParallel(m_Query, 1, this.Dependency);
        }
    }
}
