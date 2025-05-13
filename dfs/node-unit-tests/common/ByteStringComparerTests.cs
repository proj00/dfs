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

        [Test]
        public void TestCompare_NotEqualSizes_Throws()
        {
            var comparer = new ByteStringComparer();
            var str = ByteString.CopyFrom(faker.Random.Bytes(1023));
            var str1 = ByteString.CopyFrom(faker.Random.Bytes(1024));
            var ex = Assert.Throws<ArgumentException>(() => comparer.Compare(str, str1));
            Assert.That(ex.Message, Does.Contain("Compare failed; invalid arguments (mismatched ByteString lengths)"));
        }

        [Test]
        public void TestCompare_NullArgs_Throws([Values] bool useLhs, [Values] bool useRhs)
        {
            var comparer = new ByteStringComparer();
            var lhs = useLhs ? ByteString.CopyFrom(faker.Random.Bytes(1023)) : null;
            var rhs = useRhs ? ByteString.CopyFrom(faker.Random.Bytes(1024)) : null;

            if (!useLhs || !useRhs)
                Assert.Throws<ArgumentNullException>(() => comparer.Compare(lhs, rhs));
        }
    }
}
