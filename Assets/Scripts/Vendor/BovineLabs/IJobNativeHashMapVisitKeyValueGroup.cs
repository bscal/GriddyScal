namespace BovineLabs.Common.Collections
{
    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// Job that visits each key value pair in a <see cref="NativeHashMap{TKey,TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The key type of the hash map.</typeparam>
    /// <typeparam name="TValue">The value type of the hash map.</typeparam>
    [JobProducerType(typeof(JobNativeHashMapVisitKeyValueGroup.NativeHashMapVisitKeyValueJobStruct<,,>))]
    public interface IJobNativeHashMapVisitKeyValueGroup<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        void Execute(NativeSlice<TKey> keys, NativeSlice<TValue> values);
    }

    /// <summary>
    /// Extension methods for <see cref="IJobNativeHashMapVisitKeyValueGroup{TKey,TValue}"/>.
    /// </summary>
    public static class JobNativeHashMapVisitKeyValueGroup
    {
        /// <summary>
        /// Schedule a <see cref="IJobNativeHashMapVisitKeyValueGroup{TKey,TValue}"/> job.
        /// </summary>
        /// <param name="jobData">The job.</param>
        /// <param name="hashMap">The hash map.</param>
        /// <param name="minIndicesPerJobCount">Min indices per job count.</param>
        /// <param name="dependsOn">The job handle dependency.</param>
        /// <typeparam name="TJob">The type of the job.</typeparam>
        /// <typeparam name="TKey">The type of the key in the hash map.</typeparam>
        /// <typeparam name="TValue">The type of the value in the hash map.</typeparam>
        /// <returns>The handle to job.</returns>
        public static unsafe JobHandle Schedule<TJob, TKey, TValue>(
            this TJob jobData,
            NativeHashMap<TKey, TValue> hashMap,
            int minIndicesPerJobCount,
            JobHandle dependsOn = default)
            where TJob : struct, IJobNativeHashMapVisitKeyValueGroup<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            var imposter = (NativeHashMapImposter<TKey, TValue>)hashMap;

            var fullData = new NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue>
            {
                HashMap = imposter,
                JobData = jobData,
                IndicesPerJobCount = minIndicesPerJobCount,
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref fullData),
                NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue>.Initialize(),
                dependsOn,
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(
                ref scheduleParams,
                imposter.Buffer->BucketCapacityMask + 1,
                minIndicesPerJobCount);
        }

        /// <summary>
        /// The job execution struct;
        /// </summary>
        /// <typeparam name="TJob">The type of the job.</typeparam>
        /// <typeparam name="TKey">The type of the key in the hash map.</typeparam>
        /// <typeparam name="TValue">The type of the value in the hash map.</typeparam>
        internal struct NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue>
            where TJob : struct, IJobNativeHashMapVisitKeyValueGroup<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            [ReadOnly]
            public NativeHashMapImposter<TKey, TValue> HashMap;

            public TJob JobData;

            public int IndicesPerJobCount;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr JobReflectionData;

            internal static IntPtr Initialize()
            {
                if (JobReflectionData == IntPtr.Zero)
                {
                    JobReflectionData = JobsUtility.CreateJobReflectionData(
                        typeof(NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue>),
                        typeof(TJob),
                        (ExecuteJobFunction)Execute);
                }

                return JobReflectionData;
            }

            private delegate void ExecuteJobFunction(
                ref NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            // ReSharper disable once MemberCanBePrivate.Global - Required by Burst
            public static unsafe void Execute(
                ref NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                var keyArray = new NativeArray<TKey>(fullData.IndicesPerJobCount, Allocator.Temp);
                var valueArray = new NativeArray<TValue>(fullData.IndicesPerJobCount, Allocator.Temp);

                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                    {
                        return;
                    }

                    var buckets = (int*)fullData.HashMap.Buffer->Buckets;
                    var keys = fullData.HashMap.Buffer->Keys;
                    var values = fullData.HashMap.Buffer->Values;

                    var jobData = fullData.JobData;

                    var index = 0;

                    for (int i = begin; i < end; i++)
                    {
                        int entryIndex = buckets[i];

                        if (entryIndex != -1)
                        {
                            var key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);
                            var value = UnsafeUtility.ReadArrayElement<TValue>(values, entryIndex);

                            keyArray[index] = key;
                            valueArray[index] = value;
                            index++;
                        }
                    }

                    if (index > 0)
                    {
                        jobData.Execute(keyArray.Slice(0, index), valueArray.Slice(0, index));
                    }

                }
            }
        }
    }
}
