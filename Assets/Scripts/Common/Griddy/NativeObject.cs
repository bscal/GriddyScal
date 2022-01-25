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
