using common;
using Fs;

namespace unit_tests.node
{
    public static class MockFsUtils
    {
        public static ObjectWithHash GenerateObject(Bogus.Faker faker, bool big = true)
        {
            int fileSize = big ? 1048576 : 10 * 1024;
            var obj = new Fs.FileSystemObject()
            {
                Name = faker.System.FileName(),
                File = new()
                {
                    Size = fileSize,
                    Hashes = new()
                    {
                        ChunkSize = 1024,
                        Hash =
                        {
                            Enumerable.Range(0, fileSize / 1024 + fileSize % 1024)
                            .Select(a => HashUtils.GetHash(faker.System.Random.Bytes((a + 1) * 10)))
                        }
                    }
                }

            };

            return new() { Hash = HashUtils.GetHash(obj), Object = obj };
        }
    }
}
