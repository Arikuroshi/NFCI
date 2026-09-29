using System.Buffers;
using NFCI.Logging;
using NFCI.Models;
using PCSC;
using PCSC.Monitoring;
using Serilog;

namespace NFCI.Services
{
    public sealed class TagIdentifier
    {
        private static IDisposable? _context;
        private static IDisposable? _monitor;

        public static event Action<string>? StatusChanged;
        public static event Action<TagInfo>? TagRead;

        public  void Start()
        {
            if (_monitor is not null)
            {
                return;
            }

            try
            {
                var context = ContextFactory.Instance.Establish(SCardScope.System);
                _context = context;

                var readers = context.GetReaders();
                if (readers is null || readers.Length == 0)
                {
                    SetStatus("No card readers found.");
                    StopResources();
                    return;
                }

                var readerName = readers[0];
                var monitor = MonitorFactory.Instance.Create(SCardScope.System);
                _monitor = monitor;

                monitor.CardInserted += (_, e) =>
                {
                    SetStatus($"Card inserted into {e.ReaderName}.");

                    try
                    {
                        using var card = context.ConnectReader(
                            e.ReaderName,
                            SCardShareMode.Shared,
                            SCardProtocol.Any);

                        var sendPci = SCardPCI.GetPci(card.Protocol);
                        byte[][] commands =
                        {
                            new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 },
                            new byte[] { 0xFF, 0xCA, 0x01, 0x00, 0x00 }
                        };

                        var atr = Array.Empty<byte>();
                        try
                        {
                            atr = card.GetAttrib(SCardAttribute.AtrString);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not retrieve ATR for {Reader}", e.ReaderName);
                        }

                        string? uid = null;
                        string? ats = null;
                        var buffer = ArrayPool<byte>.Shared.Rent(256);

                        try
                        {
                            for (var i = 0; i < commands.Length; i++)
                            {
                                var command = commands[i];

                                try
                                {
                                    var length = card.Transmit(sendPci, command, buffer);
                                    var responseHex = Convert.ToHexString(buffer.AsSpan(0, length));

                                    ApduLogger.LogRead(
                                        e.ReaderName,
                                        Convert.ToHexString(command),
                                        responseHex,
                                        length,
                                        atr.Length > 0 ? Convert.ToHexString(atr) : "<none>");

                                    if (i == 0)
                                    {
                                        uid = responseHex;
                                    }
                                    else
                                    {
                                        ats = responseHex;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(
                                        ex,
                                        "APDU command failed for {Reader}",
                                        e.ReaderName);
                                }
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }

                        if (uid is not null)
                        {
                            TagRead?.Invoke(new TagInfo(
                                e.ReaderName,
                                uid,
                                ats,
                                atr.Length > 0 ? Convert.ToHexString(atr) : null));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Card read failed for {Reader}", e.ReaderName);
                        SetStatus($"Card read failed: {ex.Message}");
                    }
                };

                monitor.CardRemoved += (_, e) =>
                    SetStatus($"Card removed from {e.ReaderName}.");

                monitor.Start(readerName);
                SetStatus($"Watching {readerName}. Place a tag on the reader.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not start NFC reader monitoring.");
                SetStatus($"Could not start reader monitoring: {ex.Message}");
                StopResources();
            }
        }

        public void Stop()
        {
            StopResources();
            SetStatus("Reader monitoring stopped.");
        }

        private void StopResources()
        {
            _monitor?.Dispose();
            _monitor = null;

            _context?.Dispose();
            _context = null;
        }

        private void SetStatus(string status)
        {
            StatusChanged?.Invoke(status);
        }
    }

    public static class TagIdentifierHelper
    {
        public static TagInfo Create(
            string readerName,
            byte[] uidResponse,
            byte[]? atsResponse,
            byte[] atr)
        {
            var uid = GetSuccessfulPayload(uidResponse)
                ?? throw new InvalidOperationException("The reader returned an invalid UID response.");

            var ats = GetSuccessfulPayload(atsResponse);

            return new TagInfo(
                readerName,
                uid,
                ats,
                atr.Length > 0 ? Convert.ToHexString(atr) : null);
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
