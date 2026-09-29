using System.Buffers;
using NFCI.Logging;
using NFCI.Models;
using PCSC;
using PCSC.Monitoring;
using Serilog;

namespace NFCI.Services
{
    public sealed class Acr122Reader : IDisposable
    {
        private IDisposable? _context;
        private IDisposable? _monitor;

        public event Action<string>? StatusChanged;
        public event Action<TagInfo>? TagRead;

        public void Start()
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
                        var atr = Array.Empty<byte>();

                        try
                        {
                            atr = card.GetAttrib(SCardAttribute.AtrString);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not retrieve ATR for {Reader}", e.ReaderName);
                        }

                        byte[] Transmit(byte[] command)
                        {
                            var buffer = ArrayPool<byte>.Shared.Rent(256);

                            try
                            {
                                var length = card.Transmit(sendPci, command, buffer);
                                var response = buffer.AsSpan(0, length).ToArray();

                                ApduLogger.LogRead(
                                    e.ReaderName,
                                    Convert.ToHexString(command),
                                    Convert.ToHexString(response),
                                    response.Length,
                                    atr.Length > 0 ? Convert.ToHexString(atr) : "<none>");

                                return response;
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }

                        var uidResponse = Transmit(
                            new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 });

                        byte[]? atsResponse = null;

                        try
                        {
                            atsResponse = Transmit(
                                new byte[] { 0xFF, 0xCA, 0x01, 0x00, 0x00 });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not retrieve ATS for {Reader}", e.ReaderName);
                        }

                        TagRead?.Invoke(new TagInfo(
                            ReaderName: e.ReaderName,
                            Uid: Convert.ToHexString(uidResponse),
                            Ats: atsResponse != null ? Convert.ToHexString(atsResponse) : null,
                            Atr: atr.Length > 0 ? Convert.ToHexString(atr) : null
                        ));
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

        public void Dispose()
        {
            StopResources();
        }

        private void StopResources()
        {
            try
            {
                _monitor?.Dispose();
            }
            finally
            {
                _monitor = null;
                _context?.Dispose();
                _context = null;
            }
        }

        private void SetStatus(string status)
        {
            StatusChanged?.Invoke(status);
        }
    }
}
