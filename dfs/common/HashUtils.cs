﻿using Google.Protobuf;
using Node;
using Org.BouncyCastle.Crypto.Digests;

namespace common
{
    public static partial class HashUtils
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
            ArgumentNullException.ThrowIfNull(obj);
            var digest = new Sha3Digest(512);
            var buffer = new byte[2 * obj.CalculateSize()];
            using var stream = new CodedOutputStream(buffer);
            stream.Deterministic = true;
            obj.WriteTo(stream);
            stream.Flush();
            digest.BlockUpdate(buffer, 0, (int)stream.Position);
            return GetFinal(digest);
        }

        public static ByteString CombineHashes(ByteString[] hashes)
        {
            ArgumentNullException.ThrowIfNull(hashes);

            var total = new List<byte>();

            foreach (var h in hashes)
            {
                total.AddRange(h);
            }

            return GetHash([.. total]);
        }

        public static ByteString ConcatHashes(ByteString[] hashes)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            var total = new List<byte>();

            foreach (var h in hashes)
            {
                total.AddRange(h);
            }

            return ByteString.CopyFrom([.. total]);
        }

        public static ByteString GetChunkHash(FileChunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            return ConcatHashes([chunk.FileHash, chunk.Hash]);
        }
    }
}
