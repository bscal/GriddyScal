using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Common.Utils
{
    public static class Utils
    {
        public static float4 ColorToFloat4(Color color) => new(color.r, color.g, color.b, color.a);
    }

    public struct NamespacedKey : IEquatable<NamespacedKey>
    {
        public const string DEFAULT_NAMESPACE = "default";

        public string Namespace;
        public string Id;

        public NamespacedKey(string id)
        {
            Namespace = DEFAULT_NAMESPACE;
            Id = id;
        }

        public NamespacedKey(string keyNamespace, string id)
        {
            if (keyNamespace == null || keyNamespace.Length == 0)
                Namespace = DEFAULT_NAMESPACE;
            else
                Namespace = keyNamespace;
            Id = id;
        }

        private NamespacedKey(string[] keyArray)
        {
            Namespace = keyArray[0];
            Id = keyArray[1];
        }

        public static NamespacedKey FromString(string value)
        {
            var split = value.Split(':');
            if (split.Length == 2) return new NamespacedKey(split);
#if DEBUG
            Debug.LogError("Unable to create NamespacedKey from String: " + value);
#endif
            return new NamespacedKey("INVALID", value);
        }

        public override string ToString() => Namespace + ":" + Id;

        public bool Equals(NamespacedKey other) => this.Namespace == other.Namespace && this.Id == other.Id;

        public static bool operator ==(NamespacedKey l, NamespacedKey r)
        {
            return l.Equals(r);
        }

        public static bool operator !=(NamespacedKey l, NamespacedKey r)
        {
            return !l.Equals(r);
        }
    }
}
