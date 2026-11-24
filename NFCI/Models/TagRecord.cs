namespace NFCI.Models
{
    public sealed record TagRecord(
        string Uid,
        string Name,
        DateTimeOffset CreatedAtUtc);
}