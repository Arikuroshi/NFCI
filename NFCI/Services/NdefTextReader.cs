using NFCI.Models;

namespace NFCI.Services
{
    public static class NdefTextReader
    {
        public static bool TryReadText(CardDump dump, out string text)
        {
            ArgumentNullException.ThrowIfNull(dump);

            text = string.Empty;

            return dump.Family switch
            {
                TagFamily.MifareType2 => TryReadType2Text(dump, out text),
                TagFamily.MifareClassic => TryReadClassicText(dump, out text),
                _ => false
            };
        }

        private static bool TryReadType2Text(CardDump dump, out string text)
        {
            var tlvData = dump.Memory
                .Where(page => page.Address >= 4)
                .OrderBy(page => page.Address)
                .SelectMany(page => page.Data)
                .ToArray();

            return NdefTextCodec.TryDecodeType2Text(tlvData, out text);
        }

        private static bool TryReadClassicText(CardDump dump, out string text)
        {
            text = string.Empty;

            var blocks = dump.Memory.ToDictionary(block => block.Address);

            if (!blocks.TryGetValue(1, out var madBlock1) ||
                !blocks.TryGetValue(2, out var madBlock2) ||
                madBlock1.Data.Length < 16 ||
                madBlock2.Data.Length < 16)
            {
                return false;
            }

            var ndefSectors = new List<int>();

            // MAD1 block 1 contains AIDs for sectors 1–7 after its two CRC bytes.
            for (var index = 0; index < 7; index++)
            {
                if (IsNdefAid(madBlock1.Data, 2 + index * 2))
                {
                    ndefSectors.Add(index + 1);
                }
            }

            // MAD1 block 2 contains AIDs for sectors 8–15.
            for (var index = 0; index < 8; index++)
            {
                if (IsNdefAid(madBlock2.Data, index * 2))
                {
                    ndefSectors.Add(index + 8);
                }
            }

            if (ndefSectors.Count == 0)
            {
                return false;
            }

            var ndefData = new List<byte>();

            foreach (var sector in ndefSectors)
            {
                var firstBlock = sector * 4;
                var sectorData = new List<byte>();
                var completeSector = true;

                // A small MIFARE Classic sector has three data blocks; the fourth is its trailer.
                for (var offset = 0; offset < 3; offset++)
                {
                    if (!blocks.TryGetValue(firstBlock + offset, out var block))
                    {
                        completeSector = false;
                        break;
                    }

                    sectorData.AddRange(block.Data);
                }

                ndefData.AddRange(sectorData);

                if (NdefTextCodec.TryDecodeType2Text(ndefData.ToArray(), out text))
                {
                    return true;
                }

                if (!completeSector)
                {
                    break;
                }
            }

            return false;
        }

        private static bool IsNdefAid(byte[] data, int offset)
        {
            // MAD stores the NDEF AID E103 as bytes 03 E1.
            return offset >= 0 &&
                   data.Length >= offset + 2 &&
                   data[offset] == 0x03 &&
                   data[offset + 1] == 0xE1;
        }
    }
}