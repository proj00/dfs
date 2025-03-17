using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public static class HashUtils
    {
        public static string GetHash(byte[] data)
        {
            var hash = SHA3_512.HashData(data);
            var hex = Convert.ToHexStringLower(hash);

            return hex;
        }

        public static string CombineHashes(string[] hashes)
        {
            var data = hashes.Select(hash => Convert.FromHexString(hash));
            var total = new List<byte>();

            foreach (var h in data)
            {
                total.AddRange(h);
            }

            return GetHash([.. total]);
        }
    }
}
