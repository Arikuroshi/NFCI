using NFCI.Models;

namespace NFCI.Services
{
    public interface ITagRepositoryFactory
    {
        ITagRepository Create(DatabaseConnectionOptions options);
    }
}