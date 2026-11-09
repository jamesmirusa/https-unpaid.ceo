using System.Text;

namespace UnpaidCeo.Core.Primitives
{
    /// <summary>Small stable hashes for seeds: SplitMix64 for numbers, FNV-1a (64-bit) for text ids.</summary>
    public static class Hashes
    {
        public static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        public static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong h = 0xCBF29CE484222325UL;
                foreach (var b in Encoding.UTF8.GetBytes(text ?? "")) { h ^= b; h *= 0x100000001B3UL; }
                return h;
            }
        }
    }
}
