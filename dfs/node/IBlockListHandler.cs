using Ui;

namespace node
{
    public interface IBlockListHandler : IDisposable
    {
        Task FixBlockListAsync(BlockListRequest request);
        Task<BlockListResponse> GetBlockListAsync();
        Task<bool> IsInBlockListAsync(Uri url);
    }
}
