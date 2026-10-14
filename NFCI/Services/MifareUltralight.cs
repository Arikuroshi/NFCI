using NFCI.Models;

namespace NFCI.Services
{
    public sealed class MifareUltralight
    {
        public CardDump? TryReadDump(
            string readerName,
            string uid,
            string? atr,
            string? ats,
            Func<byte[], byte[]> transmit)
        {
            ArgumentNullException.ThrowIfNull(transmit);

            var capabilityResponse = transmit(
                new byte[] { 0xFF, 0xB0, 0x00, 0x03, 0x04 });

            if (!TryGetPayload(capabilityResponse, out var capabilityContainer) ||
                capabilityContainer.Length < 4 ||
                capabilityContainer[0] != 0xE1 ||
                (capabilityContainer[1] & 0xF0) != 0x10 ||
                capabilityContainer[2] == 0 ||
                capabilityContainer[3] is not (0x00 or 0x0F))
            {
                return null;
            }

            var userMemoryPageCount = capabilityContainer[2] * 2;
            var lastPage = Math.Min(3 + userMemoryPageCount, byte.MaxValue);
            var tagProfile = GetTagProfile(capabilityContainer[2]);

            var dump = new CardDump
            {
                ReaderName = readerName,
                Uid = uid,
                Atr = atr ?? string.Empty,
                Ats = ats ?? string.Empty,
                TagType = tagProfile.TagType,
                ScannedAt = DateTime.UtcNow,
                Family = tagProfile.Family
            };

            for (var page = 0; page <= lastPage; page++)
            {
                byte[] response;

                if (page == 3)
                {
                    response = capabilityResponse;
                }
                else
                {
                    response = transmit(
                        new byte[] { 0xFF, 0xB0, 0x00, (byte)page, 0x04 });
                }

                if (!TryGetPayload(response, out var data) || data.Length < 4)
                {
                    dump.UnreadablePages.Add(page);
                    continue;
                }

                dump.Memory.Add(new MemoryPage
                {
                    Address = page,
                    Data = data[..4]
                });
            }

            return dump;
        }

        private static (string TagType, TagFamily Family) GetTagProfile(byte memorySize)
        {
            return memorySize switch
            {
                0x06 => ("MIFARE Ultralight (48-byte user memory)", TagFamily.MifareType2),
                0x10 => ("MIFARE Ultralight EV1 (128-byte user memory)", TagFamily.MifareType2),
                0x12 => ("NTAG213-compatible (144-byte user memory)", TagFamily.MifareType2),
                0x3E => ("NTAG215-compatible (504-byte user memory)", TagFamily.MifareType2),
                0x6D => ("NTAG216-compatible (872-byte CC memory)", TagFamily.MifareType2),
                _ => ($"MIFARE Type 2 tag ({memorySize * 8}-byte user memory)", TagFamily.MifareType2)
            };
        }

        private static bool TryGetPayload(byte[]? response, out byte[] payload)
        {
            payload = [];

            if (response is not { Length: >= 2 } ||
                response[^2] != 0x90 ||
                response[^1] != 0x00)
            {
                return false;
            }

            payload = response[..^2];
            return true;
        }
    }
}