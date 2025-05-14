using Google.Protobuf;

namespace node
{
    public interface IFilePathHandler : IDisposable
    {
        Task<string> GetPathAsync(ByteString hash);
        void RevealFile(string path);
        Task RevealHashAsync(ByteString hash);
        Task SetPathAsync(ByteString hash, string path);
    }
}
