using NFCI.Models;

namespace NFCI.Services
{
    public sealed class TagRepositoryFactory : ITagRepositoryFactory
    {
        public ITagRepository Create(DatabaseConnectionOptions options)
        {
            return options switch
            {
                PostgreSqlConnectionOptions postgreSqlOptions =>
                    new PostgreSqlTagRepository(postgreSqlOptions),

                _ => throw new NotSupportedException(
                    $"The database provider '{options.Provider}' is not supported.")
            };
        }
    }
}