using System.Buffers;
using NFCI.Logging;
using NFCI.Models;
using PCSC;
using PCSC.Monitoring;
using Serilog;

namespace NFCI.Services
{
    public static class TagIdentifierHelper
    {
        public static TagInfo Create(
            string readerName,
            byte[] uidResponse,
            byte[]? atsResponse,
            byte[] atr,
            string? atqa = null,
            byte? sak = null)
        {
            var uid = GetSuccessfulPayload(uidResponse)
                ?? throw new InvalidOperationException("The reader returned an invalid UID response.");

            var ats = GetSuccessfulPayload(atsResponse);

            return new TagInfo(
                readerName,
                uid,
                ats,
                atr.Length > 0 ? Convert.ToHexString(atr) : null,
                atqa,
                sak);
        }

        private static string? GetSuccessfulPayload(byte[]? response)
        {
            if (response is null || response.Length < 3)
            {
                return null;
            }

            var payloadLength = response.Length - 2;
            var statusWord1 = response[payloadLength];
            var statusWord2 = response[payloadLength + 1];

            if (statusWord1 != 0x90 || statusWord2 != 0x00)
            {
                return null;
            }

            return Convert.ToHexString(response.AsSpan(0, payloadLength));
        }
    }
}
