using Google.Protobuf;
using System.Diagnostics.CodeAnalysis;

namespace common
{
    public class ByteStringComparer : IEqualityComparer<ByteString>, IComparer<ByteString>
    {
        public int Compare(ByteString? x, ByteString? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);
            if (x.Length != y.Length)
            {
                throw new ArgumentException("Compare failed; invalid arguments (mismatched ByteString lengths)");
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
            ArgumentNullException.ThrowIfNull(obj);

            return obj.GetHashCode();
        }
    }
}
