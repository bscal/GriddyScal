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
    [JobProducerType(typeof(JobNativeHashMapVisitKeyValue.NativeHashMapVisitKeyValueJobStruct<,,>))]
    public interface IJobNativeHashMapVisitKeyValue<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        void ExecuteNext(TKey key, TValue value);
    }

    /// <summary>
    /// Extension methods for <see cref="IJobNativeHashMapVisitKeyValue{TKey,TValue}"/>.
    /// </summary>
    public static class JobNativeHashMapVisitKeyValue
    {
        /// <summary>
        /// Schedule a <see cref="IJobNativeHashMapVisitKeyValue{TKey,TValue}"/> job.
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
            where TJob : struct, IJobNativeHashMapVisitKeyValue<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            var imposter = (NativeHashMapImposter<TKey, TValue>)hashMap;

            var fullData = new NativeHashMapVisitKeyValueJobStruct<TJob, TKey, TValue>
            {
                HashMap = imposter,
                JobData = jobData,
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
            where TJob : struct, IJobNativeHashMapVisitKeyValue<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            [ReadOnly]
            public NativeHashMapImposter<TKey, TValue> HashMap;

            public TJob JobData;

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
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                    {
                        return;
                    }

                    var buckets = (int*)fullData.HashMap.Buffer->Buckets;
                    var bucketNext = (int*)fullData.HashMap.Buffer->Next;
                    var keys = fullData.HashMap.Buffer->Keys;
                    var values = fullData.HashMap.Buffer->Values;

                    for (int i = begin; i < end; i++)
                    {
                        int entryIndex = buckets[i];

                        while (entryIndex != -1)
                        {
                            var key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);
                            var value = UnsafeUtility.ReadArrayElement<TValue>(values, entryIndex);
                            fullData.JobData.ExecuteNext(key, value);

                            // TODO is this needed for a non multi map?
                            entryIndex = bucketNext[entryIndex];
                        }
                    }
                }
            }
        }
    }
}
