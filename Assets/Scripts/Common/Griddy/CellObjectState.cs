using Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;

namespace Griddy
{
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
}
