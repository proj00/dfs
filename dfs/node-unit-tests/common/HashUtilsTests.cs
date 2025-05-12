using common;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using unit_tests.node;

namespace unit_tests.common
{
    class HashUtilsTests
    {
        private static readonly Bogus.Faker faker = new();

        [Test]
        public void Test_GetHash_IsConsistent()
        {
            var buf = faker.Random.Bytes(10);
            ReadOnlySpan<byte> span = new(buf);
            var h1 = HashUtils.GetHash(span);
            var h2 = HashUtils.GetHash(buf);

            Assert.That(h1, Is.EqualTo(h2));
        }
        [Test]
        public void Test_GetObjectHash_IsConsistent()
        {
            var obj = MockFsUtils.GenerateObject(faker);
            var set = new HashSet<ByteString>(new ByteStringComparer());

            // protobuf serialization is messy sometimes, doublecheck
            for (int i = 0; i < 128; i++)
            {
                set.Add(HashUtils.GetHash(obj.Object));
            }

            Assert.That(set, Has.Count.EqualTo(1));
        }

        [Test]
        public void Test_MultipleHashes_IsConsistent()
        {
            var buf = faker.Random.Bytes(10);
            ReadOnlySpan<byte> span = new(buf);
            var h1 = HashUtils.GetHash(span);
            var h2 = HashUtils.GetHash(buf);

            Assert.That(HashUtils.CombineHashes([h1, h2]),
                Is.EqualTo(HashUtils.GetHash(HashUtils.ConcatHashes([h1, h2]).ToByteArray())));
        }
    }
}
