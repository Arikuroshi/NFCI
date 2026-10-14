using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NFCI.Models;

namespace NFCI.Services
{
    public sealed class MifareClassic
    {
        private const byte KeyASlotType = 0x60;
        private const byte KeyBSlotType = 0x61;
        private const byte KeySlot = 0x00;
        private static readonly byte[] DefaultKey = Convert.FromHexString("FFFFFFFFFFFF");

        private readonly List<byte[]> _keys = [DefaultKey];

        public MifareClassic(IEnumerable<string>? additionalKeys = null)
        {
            if (additionalKeys is null)
            {
                return;
            }

            foreach (var keyHex in additionalKeys)
            {
                var normalizedKey = keyHex
                    .Replace(" ", string.Empty, StringComparison.Ordinal)
                    .Replace("-", string.Empty, StringComparison.Ordinal);

                if (normalizedKey.Length != 12)
                {
                    throw new ArgumentException(
                        $"MIFARE Classic keys must contain exactly 12 hexadecimal characters: '{keyHex}'.",
                        nameof(additionalKeys));
                }

                try
                {
                    _keys.Add(Convert.FromHexString(normalizedKey));
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException(
                        $"MIFARE Classic key is not valid hexadecimal: '{keyHex}'.",
                        nameof(additionalKeys),
                        ex);
                }
            }
        }

        public CardDump ReadDump(
            string readerName,
            string uid,
            string? atr,
            string? ats,
            Func<byte[], byte[]> transmit)
        {
            ArgumentNullException.ThrowIfNull(transmit);

            var dump = new CardDump
            {
                ReaderName = readerName,
                Uid = uid,
                Atr = atr ?? string.Empty,
                Ats = ats ?? string.Empty,
                ScannedAt = DateTime.UtcNow
            };

            var upperSectorWasReadable = false;

            for (var sector = 0; sector < 40; sector++)
            {
                var firstBlock = sector < 32
                    ? sector * 4
                    : 128 + ((sector - 32) * 16);

                var blocksInSector = sector < 32 ? 4 : 16;

                if (!TryAuthenticateSector(firstBlock, transmit))
                {
                    dump.SectorsWithoutAcceptedKey.Add(sector);
                    continue;
                }

                for (var offset = 0; offset < blocksInSector; offset++)
                {
                    var block = firstBlock + offset;
                    var response = transmit(
                        new byte[] { 0xFF, 0xB0, 0x00, (byte)block, 0x10 });

                    if (!TryGetPayload(response, out var data) || data.Length != 16)
                    {
                        dump.UnreadableBlocks.Add(block);
                        continue;
                    }

                    dump.Memory.Add(new MemoryPage
                    {
                        Address = block,
                        Data = data
                    });

                    if (block >= 64)
                    {
                        upperSectorWasReadable = true;
                    }
                }
            }

            dump.TagType = upperSectorWasReadable
                ? "MIFARE Classic 4K (upper sector readable)"
                : "MIFARE Classic (1K/4K size unconfirmed)";

            dump.Family = TagFamily.MifareClassic;

            return dump;
        }

        private bool TryAuthenticateSector(
            int firstBlock,
            Func<byte[], byte[]> transmit)
        {
            foreach (var key in _keys)
            {
                var loadKeyCommand = new byte[11]
                {
                    0xFF, 0x82, 0x00, KeySlot, 0x06,
                    key[0], key[1], key[2], key[3], key[4], key[5]
                };

                if (!IsSuccessful(transmit(loadKeyCommand)))
                {
                    continue;
                }

                if (TryAuthenticate(firstBlock, KeyASlotType, transmit) ||
                    TryAuthenticate(firstBlock, KeyBSlotType, transmit))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryAuthenticate(
            int block,
            byte keyType,
            Func<byte[], byte[]> transmit)
        {
            var response = transmit(
            [
                0xFF, 0x86, 0x00, 0x00, 0x05,
                0x01, 0x00, (byte)block, keyType, KeySlot
            ]);

            return IsSuccessful(response);
        }

        private static bool IsSuccessful(byte[]? response)
        {
            return response is { Length: >= 2 } &&
                   response[^2] == 0x90 &&
                   response[^1] == 0x00;
        }

        private static bool TryGetPayload(byte[]? response, out byte[] payload)
        {
            payload = [];

            if (!IsSuccessful(response))
            {
                return false;
            }

            payload = response![..^2];
            return true;
        }
    }
}
