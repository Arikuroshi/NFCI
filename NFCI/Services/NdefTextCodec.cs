using System.Buffers.Binary;
using System.Text;

namespace NFCI.Services
{
    public static class NdefTextCodec
    {
        private const byte NdefTlvType = 0x03;
        private const byte TerminatorTlvType = 0xFE;
        private const byte NullTlvType = 0x00;
        private const byte WellKnownTextRecordType = 0x01;

        private static readonly UTF8Encoding StrictUtf8 = new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public static byte[] EncodeType2Text(string text, string languageCode = "en")
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(languageCode);

            var language = Encoding.ASCII.GetBytes(languageCode);

            if (language.Length > 0x3F ||
                language.Any(value => value > 0x7F))
            {
                throw new ArgumentException(
                    "The language code must contain at most 63 ASCII characters.",
                    nameof(languageCode));
            }

            var textBytes = Encoding.UTF8.GetBytes(text);
            var recordPayload = new byte[1 + language.Length + textBytes.Length];

            recordPayload[0] = (byte)language.Length;
            language.CopyTo(recordPayload, 1);
            textBytes.CopyTo(recordPayload, 1 + language.Length);

            var ndefMessage = EncodeTextRecord(recordPayload);
            return WrapInType2Tlv(ndefMessage);
        }

        public static bool TryDecodeType2Text(
            ReadOnlySpan<byte> type2Memory,
            out string text)
        {
            text = string.Empty;
            var offset = 0;

            while (offset < type2Memory.Length)
            {
                var tlvType = type2Memory[offset++];

                if (tlvType == NullTlvType)
                {
                    continue;
                }

                if (tlvType == TerminatorTlvType || offset >= type2Memory.Length)
                {
                    return false;
                }

                var firstLength = type2Memory[offset++];
                int length;

                if (firstLength == 0xFF)
                {
                    if (type2Memory.Length - offset < 2)
                    {
                        return false;
                    }

                    length = BinaryPrimitives.ReadUInt16BigEndian(
                        type2Memory.Slice(offset, 2));
                    offset += 2;
                }
                else
                {
                    length = firstLength;
                }

                if (length > type2Memory.Length - offset)
                {
                    return false;
                }

                if (tlvType == NdefTlvType &&
                    TryDecodeTextRecord(type2Memory.Slice(offset, length), out text))
                {
                    return true;
                }

                offset += length;
            }

            return false;
        }

        private static byte[] EncodeTextRecord(byte[] payload)
        {
            var isShortRecord = payload.Length <= byte.MaxValue;
            var header = isShortRecord ? (byte)0xD1 : (byte)0xC1;
            var lengthFieldSize = isShortRecord ? 1 : 4;
            var message = new byte[3 + 1 + lengthFieldSize + payload.Length];

            message[0] = header;
            message[1] = 1;

            if (isShortRecord)
            {
                message[2] = (byte)payload.Length;
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(
                    message.AsSpan(2, 4),
                    payload.Length);
            }

            var typeOffset = 2 + lengthFieldSize;
            message[typeOffset] = (byte)'T';
            payload.CopyTo(message, typeOffset + 1);

            return message;
        }

        private static byte[] WrapInType2Tlv(byte[] ndefMessage)
        {
            if (ndefMessage.Length > ushort.MaxValue)
            {
                throw new ArgumentException(
                    "The NDEF message exceeds the Type 2 TLV size limit.",
                    nameof(ndefMessage));
            }

            var extendedLength = ndefMessage.Length >= 0xFF;
            var lengthFieldSize = extendedLength ? 3 : 1;
            var result = new byte[1 + lengthFieldSize + ndefMessage.Length + 1];

            result[0] = NdefTlvType;

            if (extendedLength)
            {
                result[1] = 0xFF;
                BinaryPrimitives.WriteUInt16BigEndian(
                    result.AsSpan(2, 2),
                    (ushort)ndefMessage.Length);
            }
            else
            {
                result[1] = (byte)ndefMessage.Length;
            }

            ndefMessage.CopyTo(result, 1 + lengthFieldSize);
            result[^1] = TerminatorTlvType;

            return result;
        }

        private static bool TryDecodeTextRecord(
            ReadOnlySpan<byte> message,
            out string text)
        {
            text = string.Empty;
            var offset = 0;
            var isFirstRecord = true;

            while (offset < message.Length)
            {
                var header = message[offset++];

                if ((header & 0x20) != 0)
                {
                    return false;
                }

                var isMessageBegin = (header & 0x80) != 0;
                var isMessageEnd = (header & 0x40) != 0;
                var isShortRecord = (header & 0x10) != 0;
                var hasId = (header & 0x08) != 0;
                var typeNameFormat = (byte)(header & 0x07);

                if (isMessageBegin != isFirstRecord || offset >= message.Length)
                {
                    return false;
                }

                var typeLength = message[offset++];
                int payloadLength;

                if (isShortRecord)
                {
                    if (offset >= message.Length)
                    {
                        return false;
                    }

                    payloadLength = message[offset++];
                }
                else
                {
                    if (message.Length - offset < 4)
                    {
                        return false;
                    }

                    payloadLength = BinaryPrimitives.ReadInt32BigEndian(
                        message.Slice(offset, 4));
                    offset += 4;

                    if (payloadLength < 0)
                    {
                        return false;
                    }
                }

                var idLength = 0;

                if (hasId)
                {
                    if (offset >= message.Length)
                    {
                        return false;
                    }

                    idLength = message[offset++];
                }

                if (typeLength + idLength + payloadLength > message.Length - offset)
                {
                    return false;
                }

                var type = message.Slice(offset, typeLength);
                offset += typeLength + idLength;
                var payload = message.Slice(offset, payloadLength);
                offset += payloadLength;

                if (typeNameFormat == WellKnownTextRecordType &&
                    type.SequenceEqual([(byte)'T']) &&
                    TryDecodeUtf8TextPayload(payload, out text))
                {
                    return true;
                }

                if (isMessageEnd)
                {
                    return false;
                }

                isFirstRecord = false;
            }

            return false;
        }

        private static bool TryDecodeUtf8TextPayload(
            ReadOnlySpan<byte> payload,
            out string text)
        {
            text = string.Empty;

            if (payload.IsEmpty)
            {
                return false;
            }

            var status = payload[0];

            // This codec supports UTF-8 Text records, not UTF-16 records.
            if ((status & 0x80) != 0)
            {
                return false;
            }

            var languageLength = status & 0x3F;

            if (payload.Length < 1 + languageLength)
            {
                return false;
            }

            try
            {
                text = StrictUtf8.GetString(payload[(1 + languageLength)..]);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}