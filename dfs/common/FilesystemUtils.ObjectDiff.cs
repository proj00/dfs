using Fs;
using Google.Protobuf;

namespace common
{
    public static partial class FilesystemUtils
    {
        public class ObjectDiff
        {
            public ByteString ToReplace { get; set; }
            public ObjectWithHash Target { get; set; }
            public ObjectDiff(ByteString toReplace, ObjectWithHash target)
            {
                ToReplace = toReplace;
                Target = target;
            }
        }
    }
}
