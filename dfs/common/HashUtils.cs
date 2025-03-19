using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Digests;

namespace common
{
    public static class HashUtils
    {
        private static byte[] GetFinal(Sha3Digest digest)
        {
            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            return hash;
        }

        public static string GetHash(byte[] data)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(data);
            var hex = Convert.ToHexStringLower(GetFinal(digest));

            return hex;
        }

        public static string GetHash(ReadOnlySpan<byte> data)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(data);
            var hex = Convert.ToHexStringLower(GetFinal(digest));

            return hex;
        }

        public static string GetHash(Fs.FileSystemObject obj)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(obj.ToByteArray());
            var hex = Convert.ToHexStringLower(GetFinal(digest));

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
