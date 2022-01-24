using Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;
using Unity.Core;
using UnityEngine;
using BlobHashMaps;

namespace Griddy
{
    public class GriddyManager<T> : MonoBehaviour where T : struct
    {

        public BlobHashMap<FixedString32, CellObjectState> CellStatesByName;
        public BlobHashMap<int, CellObjectState> CellStatesById;

        public Dictionary<long, GriddyChunk<T>> Chunks;
        public NativeHashMap<int3, Cell> Cells;

    }

    public class GriddyChunk<T> where T : struct
    {
        public T[] Cells;
        public NativeArray<T> NativeCells;

        public GriddyChunk(int size)
        {
            Cells = new T[size];
            NativeCells = new NativeArray<T>(size, Allocator.Persistent);
        }

        public void CopyFrom()
        {
            NativeCells.CopyFrom(Cells);
        }

        public void CopyTo()
        {
            NativeCells.CopyTo(Cells);
        }

        ~GriddyChunk()
        {
            if (NativeCells.IsCreated)
                NativeCells.Dispose();
        }
    }

    public struct CellObjectState
    {
        public NamespacedKey NamespacedKey;
        public int StateId;
        public int TextureId;
        public int DefaultTint;
        public NativeHashMap<FixedString32, NativeObject> DefaultProperties;

        public Cell GetDefaultState(int3 position)
        {
            return new Cell()
            {
                StateId = StateId,
                Position = position,
                Tint = DefaultTint,
                Properties = DefaultProperties,
            };
        }
    }

    public struct Cell
    {
        public int StateId;
        public int3 Position;
        public int Tint;
        public NativeHashMap<FixedString32, NativeObject> Properties;

        public unsafe void GetProperty<T>(ref FixedString32 key, out T value) where T : struct
        {
            if (Properties.TryGetValue(key, out NativeObject valuePtr))
            {
                UnsafeUtility.CopyPtrToStructure<T>(valuePtr.Value, out value);
            }
            else
                value = default;
        }

        public unsafe void SetProperty<T>(ref FixedString32 key, T value, Allocator allocator) where T : struct
        {
            int sizeOf = UnsafeUtility.SizeOf<T>();
            void* ptr = UnsafeUtility.Malloc(sizeOf, UnsafeUtility.AlignOf<T>(), allocator);
            UnsafeUtility.CopyStructureToPtr(ref value, ptr);
            NativeObject valuePtr = new()
            {
                Value = ptr,
                SizeOf = sizeOf,
                Allocator = allocator,
            };
            Properties[key] = valuePtr;
        }

        public unsafe bool ContainsProperty<T>(ref FixedString32 key) where T : struct
        {
            return Properties.ContainsKey(key);
        }

        public unsafe void ClearProperty<T>(ref FixedString32 key) where T : struct
        {
            if (Properties.TryGetValue(key, out NativeObject valuePtr))
            {
                UnsafeUtility.Free(valuePtr.Value, valuePtr.Allocator);
                Properties.Remove(key);
            }
        }
    }

    public unsafe struct NativeObject
    {
        public void* Value;
        public int SizeOf;
        //public int TypeKey; // Maybe some type registry to add security for types?
        public Allocator Allocator;

        public bool IsValid<T>() where T : struct
        {
            return UnsafeUtility.SizeOf<T>() == SizeOf;
        }
    }

}
