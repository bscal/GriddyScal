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

        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }

    [Serializable]
    public class NamespacedKey : IEquatable<NamespacedKey>, ISerializationCallbackReceiver
    {
        public const string DEFAULT_NAMESPACE = "default";

        public string Namespace;
        public string Id;
        public string Value { get; private set; }

        public NamespacedKey(string id)
        {
            Namespace = DEFAULT_NAMESPACE;
            id = id.ToLower();
            Id = id;
            Value = Namespace + ":" + Id;
        }

        public NamespacedKey(string keyNamespace, string id)
        {
            if (keyNamespace == null || keyNamespace.Length == 0)
                Namespace = DEFAULT_NAMESPACE;
            else
                Namespace = keyNamespace;
            id = id.ToLower();
            Id = id;
            Value = Namespace + ":" + Id;
        }

        private NamespacedKey(string[] keyArray)
        {
            Namespace = keyArray[0];
            Id = keyArray[1].ToLower();
            Value = Namespace + ":" + Id;
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

        public override string ToString() => Value;

        public bool Equals(NamespacedKey other) => this.Namespace == other.Namespace && this.Id == other.Id;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (Namespace == null || Namespace.Length == 0)
                Namespace = DEFAULT_NAMESPACE;
            Id = Id.ToLower();
            Value = Namespace + ":" + Id;
        }

        public override bool Equals(object obj)
        {
            return obj is NamespacedKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Namespace, Id);
        }

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
