using NFCI.Models;
using PCSC;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace NFCI.Services
{
    public static class TagWriteService
    {
        private const byte KeyASlotType = 0x60;
        private const byte KeyBSlotType = 0x61;
        private const byte KeySlot = 0x00;

        public static IReadOnlyList<WritableMemoryUnit> BuildPlan(CardDump dump)
        {
            ArgumentNullException.ThrowIfNull(dump);

            return dump.Family switch
            {
                TagFamily.MifareClassic => BuildClassicPlan(dump),
                TagFamily.MifareType2 => BuildType2Plan(dump),
                _ => []
            };
        }

        public static string WriteSelected(
            CardDump dump,
            IReadOnlyList<WritableMemoryUnit> selectedUnits,
            byte[] payload,
            IEnumerable<string> additionalClassicKeys)
        {
            ArgumentNullException.ThrowIfNull(dump);
            ArgumentNullException.ThrowIfNull(selectedUnits);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(additionalClassicKeys);

            if (selectedUnits.Count == 0)
            {
                throw new InvalidOperationException("Select at least one writable page or block.");
            }

            if (payload.Length == 0)
            {
                throw new InvalidOperationException("Enter a non-empty hexadecimal payload.");
            }

            var plan = BuildPlan(dump)
                .Where(unit => unit.IsWritable)
                .ToDictionary(unit => unit.Address);

            foreach (var selected in selectedUnits)
            {
                if (!selected.IsWritable ||
                    !plan.TryGetValue(selected.Address, out var plannedUnit) ||
                    !plannedUnit.CurrentData.AsSpan().SequenceEqual(selected.CurrentData))
                {
                    throw new InvalidOperationException(
                        $"Address {selected.Address} is not in the current writable plan.");
                }
            }

            var orderedUnits = selectedUnits
                .OrderBy(unit => unit.Address)
                .ToList();

            var capacity = orderedUnits.Sum(unit => unit.CurrentData.Length);

            if (payload.Length > capacity)
            {
                throw new InvalidOperationException(
                    $"Payload is {payload.Length} bytes, but selected units hold only {capacity} bytes.");
            }

            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            using var card = context.ConnectReader(
                dump.ReaderName,
                SCardShareMode.Shared,
                SCardProtocol.Any);

            var sendPci = SCardPCI.GetPci(card.Protocol);

            byte[] Transmit(byte[] command)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(256);

                try
                {
                    var length = card.Transmit(sendPci, command, buffer);
                    return buffer.AsSpan(0, length).ToArray();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            var currentUid = ReadUid(Transmit);

            if (!string.Equals(
                currentUid,
                NormalizeHex(dump.Uid),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The tag in the reader does not match the tag that was scanned. Scan it again before writing.");
            }

            if (dump.Family == TagFamily.MifareType2)
            {
                EnsureType2WriteState(dump, Transmit);
            }

            var keys = BuildClassicKeys(additionalClassicKeys);
            var payloadOffset = 0;
            var writtenUnits = 0;

            foreach (var unit in orderedUnits)
            {
                if (payloadOffset >= payload.Length)
                {
                    break;
                }

                var currentData = ReadUnit(dump.Family, unit.Address, Transmit, keys);

                if (!currentData.AsSpan().SequenceEqual(unit.CurrentData))
                {
                    throw new InvalidOperationException(
                        $"Address {unit.Address} changed since it was scanned. Scan the tag again before writing.");
                }

                var dataToWrite = currentData.ToArray();
                var byteCount = Math.Min(dataToWrite.Length, payload.Length - payloadOffset);

                Array.Copy(payload, payloadOffset, dataToWrite, 0, byteCount);

                WriteUnit(dump.Family, unit.Address, dataToWrite, Transmit, keys);

                var verifiedData = ReadUnit(dump.Family, unit.Address, Transmit, keys);

                if (!verifiedData.AsSpan().SequenceEqual(dataToWrite))
                {
                    throw new InvalidOperationException(
                        $"Read-back verification failed at address {unit.Address}. Earlier selected units may already have been written.");
                }

                payloadOffset += byteCount;
                writtenUnits++;
            }

            return $"Write and read-back verification succeeded. Units written: {writtenUnits}; payload bytes: {payload.Length}.";
        }

        private static IReadOnlyList<WritableMemoryUnit> BuildType2Plan(CardDump dump)
        {
            var page2 = dump.Memory.FirstOrDefault(page => page.Address == 2)?.Data;
            var page3 = dump.Memory.FirstOrDefault(page => page.Address == 3)?.Data;

            var protectionReason = GetType2ProtectionReason(dump, page2, page3);

            return dump.Memory
                .Where(page => page.Address >= 4)
                .OrderBy(page => page.Address)
                .Select(page => new WritableMemoryUnit(
                    page.Address,
                    "Page",
                    page.Data.ToArray(),
                    protectionReason.Length == 0,
                    protectionReason))
                .ToList();
        }

        private static string GetType2ProtectionReason(
            CardDump dump,
            byte[]? page2,
            byte[]? page3)
        {
            if (page2 is not { Length: >= 4 } || page3 is not { Length: >= 4 })
            {
                return "Lock or Capability Container data was not read; writing is disabled.";
            }

            if (page3[3] != 0x00)
            {
                return "Capability Container is not marked read/write; writing is disabled.";
            }

            if (page2[2] != 0 || page2[3] != 0)
            {
                return "Static lock bits are set; writing is disabled for safety.";
            }

            return GetDynamicLockPage(dump.TagType) == -1
                ? "Unknown Type 2 profile; lock layout cannot be verified."
                : string.Empty;
        }

        private static int GetDynamicLockPage(string tagType)
        {
            if (tagType.Contains("NTAG213", StringComparison.OrdinalIgnoreCase))
            {
                return 40;
            }

            if (tagType.Contains("NTAG215", StringComparison.OrdinalIgnoreCase))
            {
                return 130;
            }

            if (tagType.Contains("NTAG216", StringComparison.OrdinalIgnoreCase))
            {
                return 226;
            }

            if (tagType.Contains("MIFARE Ultralight", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return -1;
        }

        private static void EnsureType2WriteState(
            CardDump dump,
            Func<byte[], byte[]> transmit)
        {
            var page2Response = transmit(new byte[] { 0xFF, 0xB0, 0x00, 0x02, 0x04 });
            var page3Response = transmit(new byte[] { 0xFF, 0xB0, 0x00, 0x03, 0x04 });

            if (!TryGetPayload(page2Response, out var page2) ||
                page2.Length < 4 ||
                !TryGetPayload(page3Response, out var page3) ||
                page3.Length < 4)
            {
                throw new InvalidOperationException(
                    "Could not recheck Type 2 lock and Capability Container state.");
            }

            if (page3[3] != 0x00)
            {
                throw new InvalidOperationException(
                    "The tag Capability Container does not indicate writable access.");
            }

            if (page2[2] != 0 || page2[3] != 0)
            {
                throw new InvalidOperationException(
                    "The tag has static lock bits set. Writing is disabled.");
            }

            var dynamicLockPage = GetDynamicLockPage(dump.TagType);

            if (dynamicLockPage < 0)
            {
                throw new InvalidOperationException(
                    "This Type 2 tag profile is not recognized. Its lock layout cannot be checked.");
            }

            if (dynamicLockPage == 0)
            {
                return;
            }

            var lockResponse = transmit(
                new byte[] { 0xFF, 0xB0, 0x00, (byte)dynamicLockPage, 0x04 });

            if (!TryGetPayload(lockResponse, out var dynamicLockData) ||
                dynamicLockData.Length < 4)
            {
                throw new InvalidOperationException(
                    "Could not read the NTAG dynamic lock page; writing is disabled.");
            }

            if (dynamicLockData[0] != 0 ||
                dynamicLockData[1] != 0 ||
                dynamicLockData[2] != 0 ||
                dynamicLockData[3] != 0)
            {
                throw new InvalidOperationException(
                    "NTAG dynamic lock bits are set; writing is disabled for safety.");
            }
        }

        private static IReadOnlyList<WritableMemoryUnit> BuildClassicPlan(CardDump dump)
        {
            var unreadableBlocks = dump.UnreadableBlocks.ToHashSet();

            return dump.Memory
                .OrderBy(block => block.Address)
                .Select(block =>
                {
                    var reason = block.Address == 0
                        ? "Manufacturer block is never writable."
                        : IsClassicSectorTrailer(block.Address)
                            ? "Sector trailer contains keys and access bits; excluded."
                            : unreadableBlocks.Contains(block.Address)
                                ? "Block was not readable."
                                : string.Empty;

                    return new WritableMemoryUnit(
                        block.Address,
                        "Block",
                        block.Data.ToArray(),
                        reason.Length == 0,
                        reason);
                })
                .ToList();
        }

        private static byte[] ReadUnit(
            TagFamily family,
            int address,
            Func<byte[], byte[]> transmit,
            IReadOnlyList<byte[]> keys)
        {
            if (family == TagFamily.MifareClassic &&
                !TryAuthenticateClassicBlock(address, transmit, keys))
            {
                throw new InvalidOperationException(
                    $"Could not authenticate MIFARE Classic block {address} with the configured keys.");
            }

            var expectedLength = family == TagFamily.MifareType2 ? 4 : 16;
            var response = transmit(
                new byte[] { 0xFF, 0xB0, 0x00, (byte)address, (byte)expectedLength });

            if (!TryGetPayload(response, out var data) || data.Length < expectedLength)
            {
                throw new InvalidOperationException($"Could not read address {address}.");
            }

            return data[..expectedLength];
        }

        private static void WriteUnit(
            TagFamily family,
            int address,
            byte[] data,
            Func<byte[], byte[]> transmit,
            IReadOnlyList<byte[]> keys)
        {
            if (family == TagFamily.MifareType2)
            {
                if (data.Length != 4)
                {
                    throw new InvalidOperationException("Type 2 pages must be exactly 4 bytes.");
                }

                var command = new byte[]
                {
                    0xFF, 0xD6, 0x00, (byte)address, 0x04,
                    data[0], data[1], data[2], data[3]
                };

                if (!IsSuccessful(transmit(command)))
                {
                    throw new InvalidOperationException($"The tag rejected page {address}.");
                }

                return;
            }

            if (data.Length != 16)
            {
                throw new InvalidOperationException("MIFARE Classic blocks must be exactly 16 bytes.");
            }

            if (!TryAuthenticateClassicBlock(address, transmit, keys))
            {
                throw new InvalidOperationException(
                    $"Could not authenticate MIFARE Classic block {address} before writing.");
            }

            var classicCommand = new byte[21];
            classicCommand[0] = 0xFF;
            classicCommand[1] = 0xD6;
            classicCommand[2] = 0x00;
            classicCommand[3] = (byte)address;
            classicCommand[4] = 0x10;
            Array.Copy(data, 0, classicCommand, 5, data.Length);

            if (!IsSuccessful(transmit(classicCommand)))
            {
                throw new InvalidOperationException($"The tag rejected block {address}.");
            }
        }

        private static bool TryAuthenticateClassicBlock(
            int block,
            Func<byte[], byte[]> transmit,
            IReadOnlyList<byte[]> keys)
        {
            foreach (var key in keys)
            {
                var loadKeyCommand = new byte[]
                {
                    0xFF, 0x82, 0x00, KeySlot, 0x06,
                    key[0], key[1], key[2], key[3], key[4], key[5]
                };

                if (!IsSuccessful(transmit(loadKeyCommand)))
                {
                    continue;
                }

                if (TryAuthenticate(block, KeyASlotType, transmit) ||
                    TryAuthenticate(block, KeyBSlotType, transmit))
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
                new byte[]
                {
                    0xFF, 0x86, 0x00, 0x00, 0x05,
                    0x01, 0x00, (byte)block, keyType, KeySlot
                });

            return IsSuccessful(response);
        }

        private static IReadOnlyList<byte[]> BuildClassicKeys(
            IEnumerable<string> additionalKeys)
        {
            var keys = new List<byte[]>
            {
                Convert.FromHexString("FFFFFFFFFFFF")
            };

            foreach (var keyHex in additionalKeys)
            {
                var normalized = NormalizeHex(keyHex);

                if (normalized.Length != 12)
                {
                    continue;
                }

                try
                {
                    keys.Add(Convert.FromHexString(normalized));
                }
                catch (FormatException)
                {
                    // Ignore invalid configured keys; the default key remains available.
                }
            }

            return keys;
        }

        private static bool IsClassicSectorTrailer(int block)
        {
            return block < 128
                ? block % 4 == 3
                : (block - 128) % 16 == 15;
        }

        private static string ReadUid(Func<byte[], byte[]> transmit)
        {
            var response = transmit(new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 });

            if (!TryGetPayload(response, out var payload) || payload.Length == 0)
            {
                throw new InvalidOperationException("Could not read the tag UID.");
            }

            return Convert.ToHexString(payload);
        }

        private static string NormalizeHex(string value)
        {
            return value
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToUpperInvariant();
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