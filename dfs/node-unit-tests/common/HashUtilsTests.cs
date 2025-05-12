using common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

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
