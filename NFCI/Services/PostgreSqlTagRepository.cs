using NFCI.Models;
using Npgsql;
using NpgsqlTypes;

namespace NFCI.Services
{
    public sealed class PostgreSqlTagRepository : ITagRepository
    {
        private readonly NpgsqlDataSource _dataSource;

        public PostgreSqlTagRepository(PostgreSqlConnectionOptions options)
        {
            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = options.Host,
                Port = options.Port,
                Database = options.Database,
                Username = options.Username,
                Password = options.Password,
                SslMode = SslMode.Require,
                Timeout = 15,
                CommandTimeout = 15
            }.ConnectionString;

            _dataSource = NpgsqlDataSource.Create(connectionString);
        }

        public async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                CREATE TABLE IF NOT EXISTS nfci_tags (
                    uid text PRIMARY KEY,
                    name text NOT NULL,
                    created_at timestamptz NOT NULL DEFAULT now()
                );
                """;

            await using var command = _dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<TagRecord?> FindByUidAsync(
            string uid,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT uid, name, created_at
                FROM nfci_tags
                WHERE uid = @uid;
                """;

            await using var command = _dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("uid", NpgsqlDbType.Text, uid);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new TagRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2));
        }

        public async Task SaveAsync(
            TagRecord tag,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO nfci_tags (uid, name)
                VALUES (@uid, @name)
                ON CONFLICT (uid)
                DO UPDATE SET name = EXCLUDED.name;
                """;

            await using var command = _dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("uid", NpgsqlDbType.Text, tag.Uid);
            command.Parameters.AddWithValue("name", NpgsqlDbType.Text, tag.Name);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public void Dispose()
        {
            _dataSource.Dispose();
        }
    }
}