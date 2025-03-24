using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Digests;
using System.Diagnostics.CodeAnalysis;

namespace common
{
    public static class HashUtils
    {
        private static ByteString GetFinal(Sha3Digest digest)
        {
            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            return ByteString.CopyFrom(hash);
        }

        public static ByteString GetHash(byte[] data)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(data);
            return GetFinal(digest);
        }

        public static ByteString GetHash(ReadOnlySpan<byte> data)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(data);
            return GetFinal(digest);
        }

        public static ByteString GetHash(Fs.FileSystemObject obj)
        {
            var digest = new Sha3Digest(512);
            digest.BlockUpdate(obj.ToByteArray());
            return GetFinal(digest);
        }

        public static ByteString CombineHashes(ByteString[] hashes)
        {
            var total = new List<byte>();

            foreach (var h in hashes)
            {
                total.AddRange(h);
            }

            return GetHash([.. total]);
        }

        public class ByteStringComparer : IEqualityComparer<ByteString>, IComparer<ByteString>
        {
            public int Compare(ByteString? x, ByteString? y)
            {
                if (x == null || y == null || x.Length != y.Length)
                {
                    throw new ArgumentNullException();
                }

                for (int i = 0; i < x.Length; i++)
                {
                    var comp = x[i].CompareTo(y[i]);
                    if (comp != 0)
                    {
                        return comp;
                    }
                }

                return 0;
            }

            public bool Equals(ByteString? x, ByteString? y)
            {
                return Compare(x, y) == 0;
            }

            public int GetHashCode([DisallowNull] ByteString obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
