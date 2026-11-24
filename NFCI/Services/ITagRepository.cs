using NFCI.Models;

namespace NFCI.Services
{
    public interface ITagRepository : IDisposable
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);

        Task<TagRecord?> FindByUidAsync(
            string uid,
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            TagRecord tag,
            CancellationToken cancellationToken = default);
    }
}