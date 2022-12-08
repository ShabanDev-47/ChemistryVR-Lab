using System.Runtime.InteropServices;
using UnityEngine;

namespace com.zibra.liquid.Utilities
{
    // Basically CRC128
    [StructLayout(LayoutKind.Sequential)]
    public struct ZibraHash128
    {
        ulong m0;
        ulong m1;

        const ulong polynomial0 = 0x8000000000000000ul;
        const ulong polynomial1 = 0x0000000000000003ul;

        public void Init()
        {
            m0 = 0;
            m1 = 0;
        }

        public void Append(bool input)
        {
            ulong bit = (input ? 1ul : 0ul) ^ (m1 & 1ul);

            // right shifting 128 bit hash
            m1 >>= 1;
            m1 += (m0 & 1) * 0x8000000000000000ul;
            m0 >>= 1;

            m0 ^= bit * polynomial0;
            m1 ^= bit * polynomial1;
        }

        public void Append(int input)
        {
            for (int i = 0; i < sizeof(int) * 8; i++)
            {
                Append((input & 1) != 0);
                input >>= 1;
            }
        }

        public void Append(Color32[] input)
        {
            foreach (var color in input)
            {
                Append(color.r);
                Append(color.g);
                Append(color.b);
                Append(color.a);
            }
        }

        public static bool operator ==(ZibraHash128 hash1, ZibraHash128 hash2)
        {
            return (hash1.m0 == hash2.m0 && hash1.m1 == hash2.m1);
        }

        public static bool operator !=(ZibraHash128 hash1, ZibraHash128 hash2)
        {
            return (hash1.m0 != hash2.m0 || hash1.m1 != hash2.m1);
        }

        public static bool operator<(ZibraHash128 hash1, ZibraHash128 hash2)
        {
            return (hash1.m0 < hash2.m0 || (hash1.m0 == hash2.m0 && hash1.m1 < hash2.m1));
        }

        public static bool operator>(ZibraHash128 hash1, ZibraHash128 hash2)
        {
            return (hash1.m0 > hash2.m0 || (hash1.m0 == hash2.m0 && hash1.m1 > hash2.m1));
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !typeof(ZibraHash128).Equals(obj.GetType()))
            {
                return false;
            }

            ZibraHash128 p = (ZibraHash128)obj;
            return this == p;
        }

        public override int GetHashCode()
        {
            const uint MAX_INT = 0xFFFFFFFF;

            ulong hash = ((m0 >> 16) & MAX_INT) ^ (m0 & MAX_INT) ^ ((m1 >> 16) & MAX_INT) ^ (m1 & MAX_INT);

            return (int)hash;
        }
    }
}