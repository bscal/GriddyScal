/*
 * Written by Bart van de Sande
 * https://github.com/bartofzo/BlobHashMaps
 * https://bartvandesande.nl
 */

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#define BLOBHASHMAP_SAFE
#endif

using Common.Grids;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace BlobHashMaps
{
    /// <summary>
    /// Extension methods for BlobBuilder to allocate BlobHashMaps with
    /// </summary>
    public static class CellStateDataBlobBuilderExtensions
    {
        // 16384 is somewhat arbitrary but tests have shown that for small enough capacities this will
        // be a bit faster while still not allocating loads of memory
        private const int UseBucketCapacityRatioOfThreeUpTo = 16384;
        
        /// <summary>
        /// Allocates a BlobHashMap and copies all key value pairs from the source NativeHashMap
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="source">Source hashmap to copy keys and values from</param>
        public static void CellStateDataConstructHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobHashMap<TKey> blobHashMap, ref NativeHashMap<TKey, CellStateData> source)
            where TKey : struct, IEquatable<TKey>
        {
            int count = source.Count();
            var kv = source.GetKeyValueArrays(Allocator.Temp);
            var hashMapBuilder = builder.CellStateDataAllocateHashMap(ref blobHashMap, count);
                
            for (int i = 0; i < kv.Length; i++)
                hashMapBuilder.Add(kv.Keys[i], kv.Values[i]);
        }
        
        /// <summary>
        /// Allocates a BlobHashMap and copies all key value pairs from the source dictionary
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="source">Source hashmap to copy keys and values from</param>
        public static void CellStateDataConstructHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobHashMap<TKey> blobHashMap, Dictionary<TKey, CellStateData> source)
            where TKey : struct, IEquatable<TKey>
        {
            int count = source.Count;
            int ratio = count <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2;
            
            var hashMapBuilder = builder.CellStateDataAllocateHashMap(ref blobHashMap, source.Count, ratio);
            foreach (var kv in source)
                hashMapBuilder.Add(kv.Key, kv.Value);
        }
        
        /// <summary>
        /// Allocates a BlobHashMap and returns a builder than can be used to add values manually
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated hashmap. This value cannot be changed after allocation</param>
        /// <returns>Builder that can be ued to add values to the hashmap</returns>
        public static CellStateDataBlobBuilderHashMap<TKey> CellStateDataAllocateHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobHashMap<TKey> blobHashMap, int capacity)
            where TKey : struct, IEquatable<TKey>
        {
            return CellStateDataAllocateHashMap(ref builder, ref blobHashMap, capacity, capacity <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2);
        }
        
        /// <summary>
        /// Allocates a BlobHashMap and returns a builder than can be used to add values manually
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated hashmap. This value cannot be changed after allocation</param>
        /// <param name="bucketCapacityRatio">
        /// Bucket capacity ratio to use when allocating the hashmap.
        /// A higher value may result in less collisions and slightly better performance, but memory consumption increases exponentially.
        /// </param>
        /// <returns>Builder that can be ued to add values to the hashmap</returns>
        public static CellStateDataBlobBuilderHashMap<TKey> CellStateDataAllocateHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobHashMap<TKey> blobHashMap, 
            int capacity, int bucketCapacityRatio)
            where TKey : struct, IEquatable<TKey>
        {
            var hashmapBuilder = new CellStateDataBlobBuilderHashMap<TKey>(capacity, bucketCapacityRatio, ref builder, ref blobHashMap.data);
            return hashmapBuilder;
        }
        
        /// <summary>
        /// Allocates a BlobHashMap and copies all key value pairs from the source NativeHashMap
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobMultiHashMap field</param>
        /// <param name="source">Source multihashmap to copy keys and values from</param>
        public static void CellStateDataConstructMultiHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobMultiHashMap<TKey> blobMultiHashMap, ref NativeMultiHashMap<TKey, CellStateData> source)
            where TKey : struct, IEquatable<TKey>
        {
            int count = source.Count();

            var kv = source.GetKeyValueArrays(Allocator.Temp);
            var hashMapBuilder = builder.CellStateDataAllocateMultiHashMap(ref blobMultiHashMap, count);
                
            for (int i = 0; i < kv.Length; i++)
                hashMapBuilder.Add(kv.Keys[i], kv.Values[i]);
        }
        
        /// <summary>
        /// Allocates a BlobMultiHashMap and returns a builder than can be used to add values manually
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated multihashmap. This value cannot be changed after allocation</param>
        /// <returns>Builder that can be ued to add values to the multihashmap</returns>
        public static CellStateDataBlobBuilderMultiHashMap<TKey> CellStateDataAllocateMultiHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobMultiHashMap<TKey> blobMultiHashMap, int capacity)
            where TKey : struct, IEquatable<TKey>
        {
            var hashmapBuilder = new CellStateDataBlobBuilderMultiHashMap<TKey>(capacity, capacity <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2, ref builder, ref blobMultiHashMap.data);
            return hashmapBuilder;
        }
        
        /// <summary>
        /// Allocates a BlobMultiHashMap and returns a builder than can be used to add values manually
        /// </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated multihashmap. This value cannot be changed after allocation</param>
        /// <param name="bucketCapacityRatio">
        /// Bucket capacity ratio to use when allocating the hashmap.
        /// A higher value may result in less collisions and slightly better performance, but memory consumption increases exponentially.
        /// </param>
        /// <returns>Builder that can be ued to add values to the multihashmap</returns>
        public static CellStateDataBlobBuilderMultiHashMap<TKey> CellStateDataAllocateMultiHashMap<TKey>(
            this ref BlobBuilder builder, ref CellStateDataBlobMultiHashMap<TKey> blobMultiHashMap, 
            int capacity, int bucketCapacityRatio)
            where TKey : struct, IEquatable<TKey>
        {
            var hashmapBuilder = new CellStateDataBlobBuilderMultiHashMap<TKey>(capacity, bucketCapacityRatio, ref builder, ref blobMultiHashMap.data);
            return hashmapBuilder;
        }
    }
}