namespace NFCI.Models
{
    public sealed record WritableMemoryUnit(
        int Address,
        string Label,
        byte[] CurrentData,
        bool IsWritable,
        string ProtectionReason = "");
}