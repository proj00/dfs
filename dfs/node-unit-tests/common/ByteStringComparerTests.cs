using common;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.common
{
    class ByteStringComparerTests
    {
        private static readonly Bogus.Faker faker = new();
        [Test]
        public void TestCompare_EqualReturnsZero()
        {
            var comparer = new ByteStringComparer();
            var str = ByteString.CopyFrom(faker.Random.Bytes(1023));
            Assert.That(comparer.Compare(str, str), Is.EqualTo(0));
        }

        [Test]
        public void TestCompare_NotEqualReturnsNotZero()
        {
            var comparer = new ByteStringComparer();
            var str = ByteString.CopyFrom(faker.Random.Bytes(1023));
            var str1 = ByteString.CopyFrom(faker.Random.Bytes(1023));
            Assert.That(comparer.Compare(str, str1), Is.Not.EqualTo(0));
        }
    }
}
