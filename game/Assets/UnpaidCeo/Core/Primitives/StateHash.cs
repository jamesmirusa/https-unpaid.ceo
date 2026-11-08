using System.Security.Cryptography;
using System.Text;

namespace UnpaidCeo.Core.Primitives
{
    /// <summary>SHA-256 (lowercase hex) of the canonical JSON form of an object.</summary>
    public static class StateHash
    {
        public static string Compute(object o)
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(o));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
