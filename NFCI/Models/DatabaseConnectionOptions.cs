namespace NFCI.Models
{
    public abstract record DatabaseConnectionOptions(DatabaseProvider Provider);

    public sealed record PostgreSqlConnectionOptions(
        string Host,
        int Port,
        string Database,
        string Username,
        string Password)
        : DatabaseConnectionOptions(DatabaseProvider.PostgreSql);
}