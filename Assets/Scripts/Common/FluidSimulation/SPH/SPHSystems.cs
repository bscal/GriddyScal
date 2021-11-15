using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Jobs;

namespace Common.FluidSimulation.SPH
{
    class SPHSystems : SystemBase
    {
        EntityQuery m_Query;

        public float radius = 1f;
        public int dimensions = 10;
        public int numberOfParticles = 1000;
        public int maximumParticlesPerCell = 18;

        public NativeArray<SPHParticle> m_Particles;
        public NativeArray<Translation> m_Locations;

        protected override void OnCreate()
        {
            m_Query = this.GetEntityQuery(typeof(SPHParticle), typeof(Translation));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            m_Particles.Dispose();
            m_Locations.Dispose();
        }

        protected override void OnUpdate()
        {
            //var grid = new NativeMultiHashMap<int, int>(numberOfParticles, Allocator.Persistent);
            var neighbourList = new NativeArray<int>(numberOfParticles * maximumParticlesPerCell * 8, Allocator.Persistent);
            var neighbourTracker = new NativeArray<int>(numberOfParticles, Allocator.Persistent);
            m_Locations = m_Query.ToComponentDataArray<Translation>(Allocator.Persistent);
            m_Particles = m_Query.ToComponentDataArray<SPHParticle>(Allocator.Persistent);


            NativeArray<Translation> translations = new(m_Locations.Length, Allocator.TempJob);
            translations.CopyFrom(m_Locations);
            NativeMultiHashMap<int, int> hashGrid = new NativeMultiHashMap<int, int>(1000, Allocator.Persistent);
            SPHJobPopulateHashMap popJob = new() {
                cellSize = radius * 2,
                dimensions = dimensions,
                hashGrid = hashGrid,
                positions = translations
            };
            JobHandle popHandle = popJob.Schedule();
            popHandle.Complete();
            translations.CopyTo(m_Locations);
            translations.Dispose();


            NativeArray<Translation> translations1 = new(m_Locations.Length, Allocator.TempJob);
            translations1.CopyFrom(m_Locations);
            SPHJobFindNeighbors neighborsJob = new() {
                cellSize = radius * 2,
                radiusSquared = radius * radius,
                maximumParticlesPerCell = maximumParticlesPerCell,
                dimensions = dimensions,
                readonlyLocations = translations1,
                neighbourList = neighbourList,
                neighbourTracker = neighbourTracker,
                hashGrid = hashGrid
            };
            JobHandle neighborsHandle = new JobHandle();
            JobHandle h = neighborsJob.ScheduleParallel(m_Locations.Length, 1, neighborsHandle);
            h.Complete();
            
            translations1.CopyTo(m_Locations);
            translations1.Dispose();


            NativeArray<SPHParticle> particles2 = new(m_Particles.Length, Allocator.TempJob);
            particles2.CopyFrom(m_Particles);
            NativeArray<Translation> translations2 = new(m_Locations.Length, Allocator.TempJob);
            translations2.CopyFrom(m_Locations);
            SPHJobV2 job = new() {
                radiusSquared = radius * radius,
                cellSize = radius * 2,
                dimensions = dimensions,
                maximumParticlesPerCell = maximumParticlesPerCell,
                neighbourList = neighbourList,
                neighbourTracker = neighbourTracker,
                translations = translations2,
                particles = particles2,
                hashGrid = hashGrid,
            };
            //this.Dependency = job.ScheduleParallel(m_Query, 1, this.Dependency);
            JobHandle jobHandle = new JobHandle();
            JobHandle jobH = job.ScheduleParallel(m_Locations.Length, 1, jobHandle);
            jobH.Complete();
            particles2.CopyTo(m_Particles);
            translations2.CopyTo(m_Locations);
            particles2.Dispose();
            translations2.Dispose();
            hashGrid.Dispose();
            neighbourList.Dispose();
            neighbourTracker.Dispose();
        }

        public static Vector3Int GetCell(Vector3 position, float cellSize)
        {
            return new Vector3Int((int)(position.x / cellSize), (int)(position.y / cellSize), (int)(position.z / cellSize));
        }

        public static int Hash(Vector3Int cell, int dimensions)
        {
            return cell.x + dimensions * (cell.y + dimensions * cell.z);
        }
    }
}
