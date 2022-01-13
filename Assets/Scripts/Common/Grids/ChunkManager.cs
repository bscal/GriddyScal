using BovineLabs.Common.Collections;
using Common.Grids.Cells;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common.Grids
{

    public struct ChunkSection
    {
        public int2 StartPos;
        public ushort ChunkSize;
        public bool IsActive;
    }

    /// <summary>
    /// A NativeArray implementation designed to be stored in NativeCollections.
    /// I tried to include most of the saftely features.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// 
    public unsafe struct ChunkArrayData<T> where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal unsafe void* m_Data;
        internal int m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule]
        //internal DisposeSentinel m_DisposeSentinel;
        static int s_StaticSafetyId;

        [BurstDiscard]
        static void AssignStaticSafetyId(ref AtomicSafetyHandle safty)
        {
            // static safety IDs are unique per-type, and should only be initialized the first time an instance of
            // the type is created.
            if (s_StaticSafetyId == 0)
            {
                s_StaticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<ChunkArrayData<T>>();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref safty, s_StaticSafetyId);
        }
#endif

        public unsafe ChunkArrayData(int length)
        {
            Allocate(length, true, Allocator.Persistent, out this);
        }

        public unsafe ChunkArrayData(T[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            Allocate(array.Length, false, Allocator.Persistent, out this);
            Copy(array, this);
        }

        public unsafe ChunkArrayData(NativeArray<T> array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            Allocate(array.Length, false, Allocator.Persistent, out this);
            Copy(array, this);
        }

        public unsafe NativeArray<T> ToNativeArray(Allocator allocator)
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_Data, m_Length, allocator);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
            return array;
        }

        public unsafe static void Copy(NativeArray<T> src, ChunkArrayData<T> dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(src));
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.Length, dst.m_Length);
            CheckCopyArguments(src.Length, 0, dst.m_Length, 0, src.Length);
            UnsafeUtility.MemCpy(dst.m_Data, src.GetUnsafeReadOnlyPtr(), src.Length * UnsafeUtility.SizeOf<T>());
        }

        public unsafe static void Copy(ChunkArrayData<T> src, ChunkArrayData<T> dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.m_Length, dst.m_Length);
            CheckCopyArguments(src.m_Length, 0, dst.m_Length, 0, src.m_Length);
            UnsafeUtility.MemCpy(dst.m_Data, src.m_Data, src.m_Length * UnsafeUtility.SizeOf<T>());
        }

        private void Copy(T[] src, ChunkArrayData<T> dst)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.Length, m_Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public unsafe static void Copy(T[] src, int srcIndex, ChunkArrayData<T> dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (src == null)
            {
                throw new ArgumentNullException("src");
            }

            CheckCopyArguments(src.Length, srcIndex, dst.m_Length, dstIndex, length);
            GCHandle gCHandle = GCHandle.Alloc(src, GCHandleType.Pinned);
            IntPtr value = gCHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((byte*)dst.m_Data + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)(void*)value + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
            gCHandle.Free();
        }

        private unsafe static void Allocate(int length, bool clearMemory, Allocator allocator, out ChunkArrayData<T> chunkArrayData)
        {
            long totalSize = (long)UnsafeUtility.SizeOf<T>() * (long)length;
            CheckAllocateArguments(length, allocator, totalSize);
            chunkArrayData = default(ChunkArrayData<T>);
            chunkArrayData.m_Data = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
            if (clearMemory)
                UnsafeUtility.MemClear(chunkArrayData.m_Data, totalSize);
            chunkArrayData.m_Length = length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            chunkArrayData.m_MinIndex = 0;
            chunkArrayData.m_MaxIndex = length - 1;
            chunkArrayData.m_Safety = AtomicSafetyHandle.Create();
            //DisposeSentinel.Create(out chunkArrayData.m_Safety, out chunkArrayData.m_DisposeSentinel, 1, allocator);
            AssignStaticSafetyId(ref chunkArrayData.m_Safety);
#endif
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

            UnsafeUtility.Free(m_Data, Allocator.Persistent);
            m_Data = null;
            m_Length = 0;
        }

        public unsafe T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                CheckElementReadAccess(index);
#endif
                return UnsafeUtility.ReadArrayElement<T>(m_Data, index);
            }
            [WriteAccessRequired]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                CheckElementWriteAccess(index);
#endif
                UnsafeUtility.WriteArrayElement(m_Data, index, value);
            }
        }

        public bool IsCreated
        {
            get { return m_Data != null; }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyLengths(int srcLength, int dstLength)
        {
            if (srcLength != dstLength)
            {
                throw new ArgumentException("source and destination length must be the same");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyArguments(int srcLength, int srcIndex, int dstLength, int dstIndex, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "length must be equal or greater than zero.");
            }

            if (srcIndex < 0 || srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
            {
                throw new ArgumentOutOfRangeException("srcIndex", "srcIndex is outside the range of valid indexes for the source NativeArray.");
            }

            if (dstIndex < 0 || dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
            {
                throw new ArgumentOutOfRangeException("dstIndex", "dstIndex is outside the range of valid indexes for the destination NativeArray.");
            }

            if (srcIndex + length > srcLength)
            {
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", "length");
            }

            if (srcIndex + length < 0)
            {
                throw new ArgumentException("srcIndex + length causes an integer overflow");
            }

            if (dstIndex + length > dstLength)
            {
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", "length");
            }

            if (dstIndex + length < 0)
            {
                throw new ArgumentException("dstIndex + length causes an integer overflow");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckAllocateArguments(int length, Allocator allocator, long totalSize)
        {
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
            //if (!UnsafeUtility.IsBlittable<T>())
            //throw new ArgumentException(string.Format("{0} used in ChunkNativeData<{0}> must be blittable", typeof(T)));
            //IsUnmanagedAndThrow();
        }

        [BurstDiscard]
        internal static void IsUnmanagedAndThrow()
        {
            if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            {
                throw new InvalidOperationException($"{typeof(T)} used must be unmanaged (contain no managed types) and cannot itself be a native container type.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementReadAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementWriteAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                FailOutOfRangeError(index);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void FailOutOfRangeError(int index)
        {
            if (index < m_Length && (m_MinIndex != 0 || m_MaxIndex != m_Length - 1))
            {
                throw new IndexOutOfRangeException($"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\n" + "ReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
            }

            throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
        }
    }

    public unsafe struct ChunkCellData
    {
        public void* data;
        //public void* newData;

        internal int m_Length;
        internal AtomicSafetyHandle m_Safety;

        public unsafe bool IsNullPtr => data == null;

        public unsafe CellStateData this[int index]
        {
            get
            {
                CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<CellStateData>(data, index);
            }
            [WriteAccessRequired]
            set
            {
                CheckElementWriteAccess(index);
                UnsafeUtility.WriteArrayElement(data, index, value);
            }
        }

        [WriteAccessRequired]
        public unsafe void WriteNewState(int index, CellStateData state)
        {
            CheckElementWriteAccess(index);
            //UnsafeUtility.WriteArrayElement(newData, index, state);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementReadAccess(int index)
        {
            if (index < 0 || index > m_Length - 1)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckElementWriteAccess(int index)
        {
            if (index < 0 || index > m_Length - 1)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }

    public struct ChunkedCellData
    {
        public ChunkArrayData<CellStateData> Cells;
        public ChunkArrayData<CellStateData> NewCells;
        public ChunkArrayData<float> Masses;
        public ChunkArrayData<float> NewMasses;
        public ChunkArrayData<float4> Colors;

        public ChunkedCellData(int length)
        {
            Cells = new(length);
            NewCells = new(length);
            Masses = new(length);
            NewMasses = new(length);
            Colors = new(length * 4);
        }

        public void Dispose()
        {
            Cells.Dispose();
            NewCells.Dispose();
            Masses.Dispose();
            NewMasses.Dispose();
        }

        public void Serialize()
        {

        }

        public void Deserialize()
        {

        }
    }

    public struct ChunkMap
    {
        public int2 MapSize;
        public int2 MapSizeInChunks;
        public int ChunksPerPlayer;
        public int ChunkSize;
        public int ChunkCellCount;
        public int MapCellCount;
        public NativeHashMap<long, ChunkedCellData> CellData;

        public ChunkMap(int2 mapSize, int2 mapSizeInChunks, int chunksPerPlayer, int chunkSize, int chunkCellCount, int mapCellCount)
        {
            MapSize = mapSize;
            MapSizeInChunks = mapSizeInChunks;
            ChunksPerPlayer = chunksPerPlayer;
            ChunkSize = chunkSize;
            ChunkCellCount = chunkCellCount;
            MapCellCount = mapCellCount;
            CellData = new NativeHashMap<long, ChunkedCellData>(ChunksPerPlayer, Allocator.Persistent);
        }

        public void Cleanup()
        {
            foreach (var chunk in CellData)
            {
                UnloadChunkData(chunk.Key);
            }
            CellData.Dispose();
        }

        public unsafe void LoadChunkData(long key)
        {
            var chunkData = new ChunkedCellData(ChunkCellCount);

            var defaultState = CellStateManager.Instance.States.Air.GetDefaultState();
            for (int i = 0; i < ChunkCellCount; i++)
            {
                chunkData.Cells[i] = defaultState;
                chunkData.NewCells[i] = defaultState;
            }

            CellData[key] = chunkData;
        }

        public unsafe void UnloadChunkData(long key)
        {
            var data = CellData[key];
            data.Dispose();
        }

        public void LoadChunk(int chunkX, int chunkY)
        {
            if (ChunkInBounds(chunkX, chunkY)) return;
            long key = XYToChunkKey(chunkX, chunkY);
            if (CellData.ContainsKey(key)) return;
            LoadChunkData(key);
        }

        public void UnloadChunk(int chunkX, int chunkY)
        {
            if (ChunkInBounds(chunkX, chunkY)) return;
            long key = XYToChunkKey(chunkX, chunkY);
            if (!CellData.ContainsKey(key)) return;
            UnloadChunkData(key);
        }

        public CellStateData ReadState(long key, int index)
        {
            var data = TryGetCachedState(key);
            return data.Cells[index];
        }

        public ChunkedCellData TryGetCachedState(long key)
        {
            var data = CellData[key];
            return data;
        }

        public void WriteState(long key, int index, CellStateData state, bool update)
        {
            var data = TryGetCachedState(key);
            data.NewCells[index] = state;
        }

        public CellStateData GetState(long chunkKey, int cellX, int cellY)
        {
            int2 cellCoords = WorldCellToChunkCell(cellX, cellY);
            return ReadState(chunkKey, cellCoords.x + cellCoords.y * ChunkSize);
        }

        public CellStateData GetState(long chunkKey, int index)
        {
            int x = index % ChunkSize;
            int y = index % ChunkSize;
            return ReadState(chunkKey, x + y * ChunkSize);
        }

        public CellStateData GetState(int cellX, int cellY)
        {
            long chunkKey = Int2ToChunkKey(CellToChunkCoords(cellX, cellY));
            return GetState(chunkKey, cellX, cellY);
        }

        public CellStateData GetState(int index)
        {
            int x = index % MapSize.x;
            int y = index / MapSize.y;
            long chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            return GetState(chunkKey, x, y);
        }

        public void SetState(long chunkKey, int index, CellStateData state, bool updateChunk)
        {
            int x = index % MapSize.x;
            int y = index / MapSize.y;
            SetState(chunkKey, x, y, state, updateChunk);
        }

        public void SetState(long chunkKey, int cellX, int cellY, CellStateData state, bool updateChunk)
        {
            int2 cellCoords = WorldCellToChunkCell(cellX, cellY);
            WriteState(chunkKey, cellCoords.x + cellCoords.y * ChunkSize, state, updateChunk);
        }

        public void SetState(int index, CellStateData state, bool updateChunk = true)
        {
            int x = index % MapSize.x;
            int y = index / MapSize.y;
            long chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            SetState(chunkKey, x, y, state, updateChunk);
        }

        public void SetState(int cellX, int cellY, CellStateData state, bool updateChunk = true)
        {
            var key = Int2ToChunkKey(CellToChunkCoords(cellX, cellY));
            SetState(key, cellX, cellY, state, updateChunk);
        }

        public unsafe void CopyNewToCells()
        {
            // TODO
        }

        public unsafe void CopyCellToNew()
        {
            // TODO
        }

        public int2 CellToChunkCoords(int cellX, int cellY) => new(cellX / ChunkSize, cellY / ChunkSize);

        public int2 WorldCellToChunkCell(int cellX, int cellY) => new(cellX % ChunkSize, cellY % ChunkSize);

        public int2 FindChunkIndexes(int chunkX, int chunkY) => new(chunkX * ChunkSize, chunkY * ChunkSize);

        public long Int2ToChunkKey(int2 coords) => (long)coords.y << 32 | (long)coords.x;

        public long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public int2 ChunkKeyToXY(long key)
        {
            return new((int)(key & 0xffL), (int)(key >> 32 & 0xffL));
        }

        public int ChunkCellCoordToIndex(int chunkX, int chunkY) => chunkX + chunkY * ChunkSize;

        public int ChunkCellCoordToIndex(int2 chunkCoords) => ChunkCellCoordToIndex(chunkCoords.x, chunkCoords.y);

        public bool ChunkInBounds(int chunkX, int chunkY)
        {
            return chunkX < 0 || chunkY < 0 || chunkX >= MapSizeInChunks.x || chunkY >= MapSizeInChunks.y;
        }

        internal unsafe NativeArray<float4> GetColors(long updatedChunk)
        {
            var colors = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4>(CellData[updatedChunk].Colors.m_Data, CellData[updatedChunk].Colors.m_Length, Allocator.None);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref colors, AtomicSafetyHandle.Create());
            return colors;
        }

        internal void SetStateOverride(int x, int y, CellStateData state)
        {
            var chunkCellCoords = WorldCellToChunkCell(x, y);
            var chunkCellIndex = ChunkCellCoordToIndex(chunkCellCoords.x, chunkCellCoords.y);
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            data.Cells[chunkCellIndex] = state;
            data.NewCells[chunkCellIndex] = state;
        }

        internal void SetMassOverride(int x, int y, float mass)
        {
            var chunkCellCoords = WorldCellToChunkCell(x, y);
            var chunkCellIndex = ChunkCellCoordToIndex(chunkCellCoords.x, chunkCellCoords.y);
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            data.Masses[chunkCellIndex] = mass;
            data.NewMasses[chunkCellIndex] = mass;
        }

        internal void SetMass(int x, int y, float mass)
        {
            var chunkCellCoords = WorldCellToChunkCell(x, y);
            var chunkCellIndex = ChunkCellCoordToIndex(chunkCellCoords.x, chunkCellCoords.y);
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            data.NewMasses[chunkCellIndex] = mass;
        }

        internal float GetMass(int x, int y)
        {
            var chunkCellCoords = WorldCellToChunkCell(x, y);
            var chunkCellIndex = ChunkCellCoordToIndex(chunkCellCoords.x, chunkCellCoords.y);
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            return data.Masses[chunkCellIndex];
        }

        internal float GetNewMass(int x, int y)
        {
            var chunkCellCoords = WorldCellToChunkCell(x, y);
            var chunkCellIndex = ChunkCellCoordToIndex(chunkCellCoords.x, chunkCellCoords.y);
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            return data.NewMasses[chunkCellIndex];
        }

        internal ChunkedCellData GetData(int x, int y)
        {
            var chunkKey = Int2ToChunkKey(CellToChunkCoords(x, y));
            CellData.TryGetValue(chunkKey, out var data);
            return data;
        }

        internal ChunkedCellData GetData(long chunkKey) => CellData[chunkKey];

        public bool TryGetData(long chunkKey, out ChunkedCellData data) => CellData.TryGetValue(chunkKey, out data);
    }

    [Serializable]
    public class ChunkMesh
    {
        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public BoxCollider Collider;
    }

    public class ChunkManager : MonoBehaviour
    {
        [Header("Texture Info")]
        public Material Material;
        public int2 TextureSize;

        [Header("World Map Info")]
        public int2 MapSize;
        public int ChunkSize;
        public bool UseCustomSeed;
        public uint Seed;

        [Header("Map Tile Data")]
        public Vector2Int TileSize = new(1, 1);

        [Header("Chunk Prefab")]
        public GameObject ChunkPrefab;

        [Header("ChunkMap Data")]
        public int ChunkLoadRadius = 2;

        [Header("Fluid Simulation Variables")]
        public float MaxMass = 1.0f;
        public float MaxMassSqr = 0;
        public float MinMass = 0.005f;
        public float MaxCompression = 0.02f;
        public float MinFlow = 0.01f;
        public float MaxSpeed = 1f;

        protected ChunkMap m_ChunkMap;
        protected Dictionary<long, ChunkMesh> m_ChunkMeshes;
        protected List<ChunkMesh> m_ChunkObjects;
        protected Mesh m_Mesh;
        protected int m_Size;
        protected Unity.Mathematics.Random m_Random;

        private bool m_FallingLeft;


        private void Awake()
        {
            MaxMassSqr = MaxMass * MaxMass;
            m_Size = MapSize.x * MapSize.y;
            int chunksPerPlayer = 1 + 8 + 16;
            int chunkCellCount = ChunkSize * ChunkSize;
            int2 chunkMapSize = new(MapSize.x / ChunkSize, MapSize.y / ChunkSize);
            Vector2Int ChunkSizes = new(ChunkSize, ChunkSize);
            m_ChunkMap = new ChunkMap(MapSize, chunkMapSize, chunksPerPlayer, ChunkSize, chunkCellCount, m_Size);
            m_ChunkObjects = new(chunksPerPlayer);


            uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            m_Random = new Unity.Mathematics.Random(seed);

            m_ChunkMeshes = new Dictionary<long, ChunkMesh>();

            UnityEngine.Debug.Log($"Generating TileMap[{MapSize.x}, {MapSize.y}]. Seed: {seed} | ChunkSize: {ChunkSize}");

            m_Mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            m_Mesh.MarkDynamic();


            NativeArray<Vector3> vertices = new NativeArray<Vector3>(chunkCellCount * 4, Allocator.TempJob);
            GenVerticiesJob verticiesJob = new GenVerticiesJob
            {
                MapSize = ChunkSizes,
                TileSize = TileSize,
                Verticies = vertices
            };
            var verticesHandle = verticiesJob.Schedule();

            NativeArray<Vector3> uvs = new NativeArray<Vector3>(chunkCellCount * 4, Allocator.TempJob);
            GenTextureUVJob uvJob = new GenTextureUVJob
            {
                Seed = m_Random,
                MapSize = ChunkSizes,
                UVs = uvs
            };
            var uvHandle = uvJob.Schedule();

            NativeArray<Vector4> colors = new(chunkCellCount * 4, Allocator.TempJob);
            GenColorUVs cJob = new()
            {
                MapSize = ChunkSizes,
                UVs = colors,
            };
            var cHandler = cJob.Schedule();

            NativeArray<int> triangles = new NativeArray<int>(chunkCellCount * 6, Allocator.TempJob);
            GenTrianglesJob trianglesJob = new GenTrianglesJob
            {
                MapSize = ChunkSizes,
                Triangles = triangles
            };
            var trianglesHandle = trianglesJob.Schedule();

            // Wait for Verticies to Finish because cant set uvs without them
            verticesHandle.Complete();
            m_Mesh.vertices = verticiesJob.Verticies.ToArray();

            trianglesHandle.Complete();
            m_Mesh.triangles = trianglesJob.Triangles.ToArray();

            uvHandle.Complete();
            m_Mesh.SetUVs(0, uvJob.UVs);

            cHandler.Complete();
            m_Mesh.SetColors(colors);

            // free
            vertices.Dispose();
            uvs.Dispose();
            colors.Dispose();
            triangles.Dispose();

            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();

            for (int y = 0; y < chunkMapSize.y; y++)
            {
                for (int x = 0; x < chunkMapSize.x; x++)
                {
                    var go = Instantiate(ChunkPrefab, gameObject.transform);

                    go.name = $"Chunk({x}, {y})";
                    go.transform.position = new Vector3(x * ChunkSize, y * ChunkSize);

                    var meshFilter = go.AddComponent<MeshFilter>();
                    meshFilter.mesh = m_Mesh;

                    var meshRenderer = go.AddComponent<MeshRenderer>();
                    meshRenderer.material = Material;
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;

                    var collider = go.AddComponent<BoxCollider>();
                    collider.size = new(ChunkSize, ChunkSize, .01f);

                    ChunkMesh chunkMesh = new()
                    {
                        GameObject = go,
                        MeshFilter = meshFilter,
                        MeshRenderer = meshRenderer,
                        Collider = collider,
                    };
                    m_ChunkMeshes.Add(XYToChunkKey(x, y), chunkMesh);
                }
            }
            gameObject.SetActive(true);
        }

        private void Start()
        {
        }

        private void OnDestroy()
        {
            m_ChunkMap.Cleanup();
        }

        public void Update()
        {
            HandleInputs();

            m_FallingLeft = !m_FallingLeft;

            var updated = new NativeHashSet<long>(m_ChunkMap.CellData.Capacity, Allocator.TempJob);
            ChunkUpdateCells updateCellsJob = new()
            {
                ChunkMap = m_ChunkMap,
                MaxCompression = MaxCompression,
                MaxMass = MaxMass,
                MinMass = MinMass,
                MaxMassSqr = MaxMassSqr,
                MaxSpeed = MaxSpeed,
                MinFlow = MinFlow,
                FallLeft = m_FallingLeft,
                CellStateMap = CellStateManager.Instance.CellStatesBlobMap,
                Air = CellStateManager.Instance.States.Air.GetDefaultState(),
                Updated = updated.AsParallelWriter(),
            };
            JobHandle updateCellsHandle = updateCellsJob.Schedule(m_ChunkMap.CellData, 1);
            updateCellsHandle.Complete();

            ChunkUpdateState updateStateJob = new()
            {
                ChunkMap = m_ChunkMap,
                MapSize = MapSize,
                MinMass = MinMass,
                CellStateMap = CellStateManager.Instance.CellStatesBlobMap,
            };
            JobHandle updateStateHandle = updateStateJob.Schedule(m_ChunkMap.CellData, 1);
            updateStateHandle.Complete();

            foreach (var updatedChunk in updated)
            {
                m_ChunkMeshes[updatedChunk].MeshFilter.mesh.SetColors(m_ChunkMap.GetColors(updatedChunk));
            }
            updated.Dispose();
        }

        public ChunkMesh CreateChunkObject()
        {
            var go = Instantiate(ChunkPrefab, gameObject.transform);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.mesh = m_Mesh;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = Material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new(ChunkSize, ChunkSize, .01f);

            ChunkMesh chunkMesh = new()
            {
                GameObject = go,
                MeshFilter = meshFilter,
                MeshRenderer = meshRenderer,
                Collider = collider,
            };

            return chunkMesh;
        }

        public ChunkMesh GetPooledChunkedObject()
        {

            ChunkMesh go;
            if (m_ChunkObjects.Count > 0)
            {
                go = m_ChunkObjects[m_ChunkObjects.Count - 1];
                m_ChunkObjects.RemoveAt(m_ChunkObjects.Count - 1);
            }
            else
            {
                go = CreateChunkObject();
            }
            return go;
        }

        public void ReturnPooledChunkedObject(int chunkX, int chunkY)
        {
            long key = XYToChunkKey(chunkX, chunkY);
            if (m_ChunkMeshes.TryGetValue(key, out var chunkMesh))
            {
                m_ChunkMeshes.Remove(key);
                m_ChunkObjects.Add(chunkMesh);
            }
        }

        public void InitChunkObject(ChunkMesh chunkMesh, int chunkX, int chunkY)
        {
            chunkMesh.GameObject.name = $"Chunk({chunkX},{chunkY})";
            chunkMesh.GameObject.transform.position = new Vector3(chunkX * ChunkSize, chunkY * ChunkSize);
            m_ChunkMeshes.Add(XYToChunkKey(chunkX, chunkY), chunkMesh);
        }

        public Color Float4ToColor(float4 f4)
        {
            return new Color
            {
                r = f4.x,
                g = f4.y,
                b = f4.z,
                a = f4.w
            };
        }

        public void SetColor(int i, Color color, in Color[] colors)
        {
            colors[i + 0] = color;
            colors[i + 1] = color;
            colors[i + 2] = color;
            colors[i + 3] = color;
        }

        public int GetCellId(int x, int y)
        {
            x = Mathf.Clamp(x, 0, MapSize.x - 1);
            y = Mathf.Clamp(y, 0, MapSize.y - 1);
            return x + y * MapSize.x;
        }

        public static long XYToChunkKey(int x, int y) => (long)y << 32 | (long)x;

        public static Vector2Int ChunkKeyToXY(long key) => new((int)(key & 0xff), (int)(key >> 32));

        private void HandleInputs()
        {
            bool rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            bool leftClicked = Mouse.current.leftButton.isPressed;
            bool middleClicked = Mouse.current.middleButton.wasPressedThisFrame;
            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100.0f))
            {
                var diff = hit.point - transform.position;
                int x = (int)diff.x;
                int y = (int)diff.y;
                int chunkX = x / ChunkSize;
                int chunkY = y / ChunkSize;

                m_ChunkMap.LoadChunk(chunkX, chunkY);

                m_ChunkMap.LoadChunk(chunkX, chunkY + 1);
                m_ChunkMap.LoadChunk(chunkX + 1, chunkY + 1);
                m_ChunkMap.LoadChunk(chunkX + 1, chunkY);
                m_ChunkMap.LoadChunk(chunkX + 1, chunkY - 1);

                m_ChunkMap.LoadChunk(chunkX, chunkY - 1);
                m_ChunkMap.LoadChunk(chunkX - 1, chunkY - 1);
                m_ChunkMap.LoadChunk(chunkX - 1, chunkY);
                m_ChunkMap.LoadChunk(chunkX - 1, chunkY + 1);

                if (!rightClicked && !leftClicked && !middleClicked) return;
                bool shiftHeld = Keyboard.current.leftShiftKey.isPressed;
                if (rightClicked)
                {
                    if (shiftHeld)
                        m_ChunkMap.SetStateOverride(x, y, CellStateManager.Instance.States.Air.GetDefaultState());
                    else
                        m_ChunkMap.SetStateOverride(x, y, CellStateManager.Instance.States.Stone.GetDefaultState());
                }
                else if (leftClicked)
                {
                    if (shiftHeld)
                        m_ChunkMap.SetStateOverride(x, y, CellStateManager.Instance.States.Sand.GetDefaultState());
                    else
                    {
                        m_ChunkMap.SetStateOverride(x, y, CellStateManager.Instance.States.FreshWater.GetDefaultState());
                        m_ChunkMap.SetMassOverride(x, y, .5f);
                    }
                }
                else if (middleClicked)
                {
                    //MarkAsInfiniteSource(x, y);
                }
            }
        }
    }

    [BurstCompile]
    public struct ChunkCellsUpdateJob : IJobFor
    {
        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public ChunkSection ChunkSection;
        [ReadOnly] public long ChunkKey;

        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;
        [ReadOnly] public bool FallLeft;

        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStateMap;

        [ReadOnly] public CellStateData Air;

        [ReadOnly] public NativeArray<float> Mass;
        [NativeDisableParallelForRestriction] public NativeArray<float> NewMass;
        public NativeHashSet<long>.ParallelWriter updated;

        private CellStateData State;

        public void Execute(int i)
        {
            int cx = i % ChunkMap.ChunkSize;
            int cy = i / ChunkMap.ChunkSize;
            int index = GetCellId(ChunkSection.StartPos.x + cx, ChunkSection.StartPos.y + cy);
            int x = index % ChunkMap.MapSize.x;
            int y = index / ChunkMap.MapSize.y;

            State = ChunkMap.GetState(ChunkKey, index);

            var stateSand = CellStateMap.Value.CellStates["default:sand"];
            var stateWater = CellStateMap.Value.CellStates["default:fresh_water"];
            if (State.Equals(stateSand))
            {
                SimulateSand(x, y, index);
            }
            else if (State.Equals(stateWater))
            {
                SimulateFluid(x, y, index);
            }
        }

        private void SimulateFluid(int x, int y, int index)
        {
            float remainingMass = Mass[index];
            if (remainingMass < MinMass) return;
            updated.Add(ChunkKey);
            float flow;
            // Down
            int downId = GetCellId(x, y - 1);
            if (InBounds(x, y - 1) && !ChunkMap.GetState(ChunkKey, downId).IsSolid)
            {
                flow = GetStableMass(remainingMass + Mass[downId]) - Mass[downId];
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[downId] += flow;
                remainingMass -= flow;
            }
            if (remainingMass <= 0)
                return;

            // Left
            int leftId = GetCellId(x - 1, y);
            if (InBounds(x - 1, y) && !ChunkMap.GetState(ChunkKey, leftId).IsSolid)
            {
                flow = (Mass[index] - Mass[leftId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[leftId] += flow;
                remainingMass -= flow;
            }

            if (remainingMass <= 0)
                return;
            // Right
            int rightId = GetCellId(x + 1, y);
            if (InBounds(x + 1, y) && !ChunkMap.GetState(ChunkKey, rightId).IsSolid)
            {
                flow = (Mass[index] - Mass[rightId]) / 4;
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[rightId] += flow;
                remainingMass -= flow;
            }

            if (remainingMass <= 0)
                return;

            // Up
            int upId = GetCellId(x, y + 1);
            if (InBounds(x, y + 1) && !ChunkMap.GetState(ChunkKey, upId).IsSolid)
            {
                flow = remainingMass - GetStableMass(remainingMass + Mass[upId]);
                if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                NewMass[index] -= flow;
                NewMass[upId] += flow;
            }
        }

        private void SimulateSand(int x, int y, int index)
        {
            if (!InBounds(x, y - 1)) return;

            if (!ChunkMap.GetState(x, y - 1).IsSolid)
            {
                // Handle downwards movement
                ChunkMap.SetState(x, y - 1, State, true);
                ChunkMap.SetState(ChunkKey, index, Air, true);
                updated.Add(ChunkKey);
            }
            else
            {
                if (FallLeft)
                {
                    if (InBounds(x - 1, y - 1) && !ChunkMap.GetState(x - 1, y - 1).IsSolid)
                    {
                        // Handle leftward movement
                        ChunkMap.SetState(x - 1, y - 1, State, true);
                        ChunkMap.SetState(ChunkKey, index, Air, true);
                        updated.Add(ChunkKey);
                    }
                }
                else
                {
                    if (InBounds(x + 1, y - 1) && !ChunkMap.GetState(x + 1, y - 1).IsSolid)
                    {
                        // Handle rightward movement
                        ChunkMap.SetState(x + 1, y - 1, State, true);
                        ChunkMap.SetState(ChunkKey, index, Air, true);
                        updated.Add(ChunkKey);
                    }
                }
            }
        }

        public void SetState(int x, int y, CellStateData state)
        {
            ChunkMap.SetState(x, y, state, false);
        }

        public void SetState(long key, int index, CellStateData state)
        {
            ChunkMap.SetState(key, index, state, false);
        }

        private float GetStableMass(float totalMass)
        {
            // All water goes to lower cell
            if (totalMass <= 1) return 1;
            else if (totalMass < 2 * MaxMass + MaxCompression) return (MaxMassSqr + totalMass * MaxCompression) / (MaxMass + MaxCompression);
            else return (totalMass + MaxCompression) / 2;
        }

        private bool InBounds(int x, int y)
        {
            return x > -1 && y > -1 && x < ChunkMap.MapSize.x && y < ChunkMap.MapSize.y;
        }

        private int GetCellId(int x, int y)
        {
            x = math.clamp(x, 0, ChunkMap.MapSize.x - 1);
            y = math.clamp(y, 0, ChunkMap.MapSize.y - 1);
            return x + y * ChunkMap.MapSize.x;
        }
    }

    public class ChunkSerializer
    {

        // GameDir/Saves/WorldName/Chunks/{saved data}


        public const string RootPath = "GameDir/Saves";
        public const string ChunkSaveDir = "Chunks";

        public readonly string WorldSaveDir;
        public readonly string FullSavePath;

        public GameObject ChunkPrefab;
        public ChunkManager ChunkManager;

        public ChunkSerializer(string worldName)
        {
            WorldSaveDir = worldName;
            FullSavePath = Path.Combine(Application.dataPath, RootPath, WorldSaveDir, ChunkSaveDir);
        }

        public string GetChunkPath(int chunkX, int chunkY)
        {
            return Path.Combine(FullSavePath, $"{chunkX},{chunkY}");
        }

        public bool LoadChunk(int chunkX, int chunkY)
        {
            return true;
        }

        public bool SaveChunk(int chunkX, int chunkY)
        {
            return true;
        }

        public void SerializeGameObject(string path, GameObject chunkObject)
        {

        }

    }

    [BurstCompile]
    public struct ChunkUpdateCells : IJobNativeHashMapVisitKeyValue<long, ChunkedCellData>
    {
        [ReadOnly] public ChunkMap ChunkMap;

        // Tiles/Cells
        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStateMap;
        [ReadOnly] public CellStateData Air;

        [WriteOnly] public NativeHashSet<long>.ParallelWriter Updated;

        // Fluid Simulation Variables
        [ReadOnly] public float MaxMass;
        [ReadOnly] public float MinMass;
        [ReadOnly] public float MaxCompression;
        [ReadOnly] public float MinFlow;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float MaxMassSqr;

        // Sand Simulation Variables
        [ReadOnly] public bool FallLeft;

        // Job cached varialbes
        private CellStateData State;
        private int2 ChunkCoords;

        public void ExecuteNext(long key, ChunkedCellData value)
        {
            ChunkCoords = ChunkMap.ChunkKeyToXY(key);
            for (int y = 0; y < ChunkMap.ChunkSize; y++)
            {
                for (int x = 0; x < ChunkMap.ChunkSize; x++)
                {
                    int i = x + y * ChunkMap.ChunkSize;
                    State = value.Cells[i];

                    var stateSand = CellStateMap.Value.CellStates["default:sand"];
                    var stateWater = CellStateMap.Value.CellStates["default:fresh_water"];
                    if (State.Equals(stateSand))
                    {
                        SimulateSand(x, y, i, key, ref value);
                    }
                    else if (State.Equals(stateWater))
                    {
                        SimulateFluid(x, y, i, key, ref value);
                    }
                }
            }
        }

        private void SimulateFluid(int x, int y, int index, long ChunkKey, ref ChunkedCellData value)
        {
            float remainingMass = value.Masses[index];
            if (remainingMass < MinMass) return;

            Updated.Add(ChunkKey);
            float flow;

            // Down
            int2 downCoords = new(x, y - 1);
            if (GetNextChunk(downCoords, ref value, out var downData))
            {
                int i = GetIndexWrapped(downCoords);
                if (!downData.Cells[i].IsSolid)
                {
                    flow = GetStableMass(remainingMass + downData.Masses[i]) - downData.Masses[i];
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                    value.NewMasses[index] = value.NewMasses[index] - flow;
                    downData.NewMasses[i] = downData.NewMasses[i] + flow;
                    remainingMass -= flow;
                }
                if (remainingMass <= 0) return;
            }

            // Left
            int2 leftCoords = new(x - 1, y);
            if (GetNextChunk(leftCoords, ref value, out var leftData))
            {
                int i = GetIndexWrapped(leftCoords);
                if (!leftData.Cells[i].IsSolid)
                {
                    flow = (value.Masses[index] - leftData.Masses[i]) / 4;
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                    value.NewMasses[index] = value.NewMasses[index] - flow;
                    leftData.NewMasses[i] = leftData.NewMasses[i] + flow;
                    remainingMass -= flow;
                }
                if (remainingMass <= 0) return;
            }

            // Right
            int2 rightCoords = new(x + 1, y);
            if (GetNextChunk(rightCoords, ref value, out var rightData))
            {
                int i = GetIndexWrapped(rightCoords);
                if (!rightData.Cells[i].IsSolid)
                {
                    flow = (value.Masses[index] - rightData.Masses[i]) / 4;
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                    value.NewMasses[index] = value.NewMasses[index] - flow;
                    //SetMass(x + 1, y, GetNewMass(x + 1, y) + flow);
                    rightData.NewMasses[i] = rightData.NewMasses[i] + flow;
                    remainingMass -= flow;
                }
                if (remainingMass <= 0) return;
            }

            // Up
            int2 upCoords = new(x, y + 1);
            if (GetNextChunk(upCoords, ref value, out var upData))
            {
                int i = GetIndexWrapped(upCoords);
                if (!upData.Cells[i].IsSolid)
                {
                    flow = remainingMass - GetStableMass(remainingMass + upData.Masses[i]);
                    if (flow > MinFlow) flow *= 0.5f; // Leads to smoother flow
                    flow = math.clamp(flow, 0, math.min(MaxSpeed, remainingMass));
                    value.NewMasses[index] = value.NewMasses[index] - flow;
                    upData.NewMasses[i] = upData.NewMasses[i] + flow;
                }
            }
        }

        private void SimulateSand(int x, int y, int index, long ChunkKey, ref ChunkedCellData value)
        {
            Updated.Add(ChunkKey);
            int2 downCoord = new(x, y - 1);
            if (GetNextChunk(downCoord, ref value, out ChunkedCellData downData))
            {
                int downIndex = GetIndexWrapped(downCoord);
                if (!downData.Cells[downIndex].IsSolid)
                {
                    downData.NewCells[downIndex] = State;
                    value.NewCells[index] = Air;
                    return;
                }
            }
            if (FallLeft)
            {
                int2 leftCoord = new(x - 1, y - 1);
                if (GetNextChunk(leftCoord, ref value, out ChunkedCellData leftData))
                {
                    int leftIndex = GetIndexWrapped(leftCoord);
                    if (!leftData.Cells[leftIndex].IsSolid)
                    {
                        leftData.NewCells[leftIndex] = State;
                        value.NewCells[index] = Air;
                        return;
                    }
                }
            }
            else
            {
                int2 rightCoord = new(x + 1, y - 1);
                if (GetNextChunk(rightCoord, ref value, out ChunkedCellData rightData))
                {
                    int rightIndex = GetIndexWrapped(rightCoord);
                    if (!rightData.Cells[rightIndex].IsSolid)
                    {
                        rightData.NewCells[rightIndex] = State;
                        value.NewCells[index] = Air;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Wraps cellCoords xy to always be inside a chunks bounds.
        /// </summary>
        private int GetIndexWrapped(int2 cellCoords)
        {
            if (cellCoords.x < 0)
                cellCoords.x = ChunkMap.ChunkSize - 1;
            else if (cellCoords.x >= ChunkMap.ChunkSize)
                cellCoords.x = 0;
            if (cellCoords.y < 0)
                cellCoords.y = ChunkMap.ChunkSize - 1;
            else if (cellCoords.y >= ChunkMap.ChunkSize)
                cellCoords.y = 0;
            return cellCoords.x + cellCoords.y * ChunkMap.ChunkSize;
        }

        /// <summary>
        /// Used to get Chunk's ChunkedCellData from cellCoord.<br></br>
        /// This is useful when getting neighboring cells that are inside of another chunk.<br></br>
        /// If cellCoords is inside current chunk then the 'out' data will be current chunks data,
        /// otherwise 'out' data will be fetched (possibly loaded*) from ChunkMap.
        /// </summary>
        /// <param name="cellCoords">XY position of the cell</param>
        /// <param name="currentChunkData">The current ChunkedCellData used if cellCoords is inside the current Chunk</param>
        /// <param name="data">ChunkedCellData to use for fetching whatever you need</param>
        /// <param name="markDirty">Will mark the chunk as dirty</param>
        /// <returns>If the Chunk was found (either current chunk or by loading), false if does not exist</returns>
        private bool GetNextChunk(int2 cellCoords, ref ChunkedCellData currentChunkData, out ChunkedCellData data, bool markDirty = true)
        {
            if (IsCellInsideChunk(cellCoords))
            {
                data = currentChunkData;
                return true;
            }
            int2 chunkCoords = GetNewChunkKey(cellCoords);
            if (!IsChunkInsideWorldBounds(chunkCoords))
            {
                data = currentChunkData;
                return false;
            }
            long chunkKey = ChunkMap.Int2ToChunkKey(chunkCoords);
            bool wasFound = ChunkMap.TryGetData(chunkKey, out data);
            if (wasFound && markDirty) Updated.Add(chunkKey);
            return wasFound;
        }

        private bool SetCellInNextChunk(int2 cellCoords, ref ChunkedCellData currentChunkData, ref CellStateData state)
        {
            int index = GetIndexWrapped(cellCoords);
            if (IsCellInsideChunk(cellCoords))
            {
                currentChunkData.NewCells[index] = state;
                return true;
            }
            int2 chunkCoords = GetNewChunkKey(cellCoords);
            if (!IsChunkInsideWorldBounds(chunkCoords))
            {
                return false;
            }
            long chunkKey = ChunkMap.Int2ToChunkKey(chunkCoords);
            bool wasFound = ChunkMap.TryGetData(chunkKey, out ChunkedCellData data);
            if (wasFound) data.NewCells[index] = state;
            return wasFound;
        }

        private bool IsCellInsideChunk(int2 cellCoords)
        {
            return cellCoords.x > -1 && cellCoords.x < ChunkMap.ChunkSize && cellCoords.y > -1 && cellCoords.y < ChunkMap.ChunkSize;
        }

        private bool IsChunkInsideWorldBounds(int2 chunkCoords)
        {
            return chunkCoords.x > -1 && chunkCoords.x <= ChunkMap.MapSizeInChunks.x && chunkCoords.y > -1 && chunkCoords.y <= ChunkMap.MapSizeInChunks.y;
        }

        /// <summary>
        /// Moves current ChunkCoords by cellCoords -> chunk coords offset.
        /// </summary>
        private int2 GetNewChunkKey(int2 cellCoord)
        {
            int2 newChunkCoord = ChunkCoords.xy;
            if (cellCoord.x < 0 || cellCoord.x >= ChunkMap.ChunkSize)
                newChunkCoord.x += cellCoord.x / ChunkMap.ChunkSize + (cellCoord.x < 0 ? -1 : 0);
            if (cellCoord.y < 0 || cellCoord.y >= ChunkMap.ChunkSize)
                newChunkCoord.y += cellCoord.y / ChunkMap.ChunkSize + (cellCoord.y < 0 ? -1 : 0);
            return newChunkCoord;// math.clamp(newChunkCoord, int2.zero, new(ChunkMap.ChunkSize, ChunkMap.ChunkSize));
        }

        private float GetStableMass(float totalMass)
        {
            // All water goes to lower cell
            if (totalMass <= 1) return 1;
            else if (totalMass < 2 * MaxMass + MaxCompression) return (MaxMassSqr + totalMass * MaxCompression) / (MaxMass + MaxCompression);
            else return (totalMass + MaxCompression) / 2;
        }
    }

    /// <summary>
    /// Updates the states for each cell in a chunk.
    /// This is done after the chunk updates/simulates states.
    /// NewCells will transfer to Cells, Colors and Textures will be updated
    /// </summary>
    [BurstCompile]
    public struct ChunkUpdateState : IJobNativeHashMapVisitKeyValue<long, ChunkedCellData>
    {
        [ReadOnly] private readonly static float4 WATER_BLUE = new(0, 0, 1, 1);
        [ReadOnly] private readonly static float4 WATER_CYAN = new(0, 1, 1, 1);

        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public int2 MapSize;
        [ReadOnly] public float MinMass;

        [ReadOnly] public BlobAssetReference<CellStateRegistryMap> CellStateMap;

        public void ExecuteNext(long key, ChunkedCellData value)
        {
            var air = CellStateMap.Value.CellStates["default:air"];
            var water = CellStateMap.Value.CellStates["default:fresh_water"];

            int vertexIndex = 0;
            for (int i = 0; i < ChunkMap.ChunkCellCount; i++)
            {
                var state = value.NewCells[i];
                float mass = value.NewMasses[i];

                // Any state processing
                if (!state.IsSolid)
                {
                    if (mass >= MinMass)
                    {
                        state = water;
                    }
                    else
                    {
                        state = air;
                    }
                }
                else
                    mass = 0.0f;

                // Update new states to states
                value.Cells[i] = state;
                value.Masses[i] = mass;

                if (state.Equals(water))
                    SetColor(vertexIndex, math.lerp(WATER_CYAN, WATER_BLUE, mass), ref value);
                else
                    SetColor(vertexIndex, state.CellColor, ref value);
                vertexIndex += 4;
            }
        }

        /// <summary>
        /// Sets the colors for each vertex
        /// </summary>
        private void SetColor(int vertexIndex, float4 color, ref ChunkedCellData value)
        {
            value.Colors[vertexIndex + 0] = color;
            value.Colors[vertexIndex + 1] = color;
            value.Colors[vertexIndex + 2] = color;
            value.Colors[vertexIndex + 3] = color;
        }

    }
}