using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using NFCI.Models;
using NFCI.Services;

namespace NFCI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Acr122Reader _reader = new();
        private string _status = "Reader monitoring is stopped.";
        private string _tagReadStatus = "WAITING";
        private string _tagType = "No tag";
        private string _tagId = "—";
        private string _tagDetails = "Place a tag on the reader.";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<CardDump>? DumpAvailable;

        public IList<string> AdditionalMifareClassicKeys =>
            _reader.AdditionalMifareClassicKeys;

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public string TagReadStatus
        {
            get => _tagReadStatus;
            private set => SetProperty(ref _tagReadStatus, value);
        }

        public string TagType
        {
            get => _tagType;
            private set => SetProperty(ref _tagType, value);
        }

        public string TagId
        {
            get => _tagId;
            private set => SetProperty(ref _tagId, value);
        }

        public string TagDetails
        {
            get => _tagDetails;
            private set => SetProperty(ref _tagDetails, value);
        }

        public MainViewModel()
        {
            _reader.StatusChanged += OnStatusChanged;
            _reader.TagRead += OnTagRead;
            _reader.DumpRead += OnDumpRead;
        }

        public void Start()
        {
            _reader.Start();
        }

        public void Dispose()
        {
            _reader.StatusChanged -= OnStatusChanged;
            _reader.TagRead -= OnTagRead;
            _reader.DumpRead -= OnDumpRead;
            _reader.Dispose();
        }

        private void OnStatusChanged(string status)
        {
            OnUiThread(() =>
            {
                Status = status;

                if (status.StartsWith(
                    "No MIFARE Classic blocks",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TagReadStatus = "NO DATA";
                    TagType = "Unknown — no Classic sector authenticated";
                    TagDetails = "The tag UID was detected, but no MIFARE Classic blocks were readable with the configured keys.";
                }
                else if (status.StartsWith(
                    "Tag detected, but the Classic dump attempt failed",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TagReadStatus = "ERROR";
                    TagDetails = status;
                }
                else if (status.StartsWith(
                    "Card read failed",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TagReadStatus = "ERROR";
                    TagDetails = status;
                }
                else if (status.StartsWith(
                    "Reader disconnected",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TagReadStatus = "WAITING";
                }
            });
        }

        private void OnTagRead(TagInfo tag)
        {
            OnUiThread(() =>
            {
                TagReadStatus = "DETECTED";
                TagType = "Identifying…";
                TagId = FormatHex(tag.Uid);
                TagDetails =
                    $"Reader: {tag.ReaderName}{Environment.NewLine}" +
                    $"ATS: {FormatHex(tag.Ats)}{Environment.NewLine}" +
                    $"ATR: {FormatHex(tag.Atr)}";
            });
        }

        private void OnDumpRead(CardDump dump)
        {
            OnUiThread(() =>
            {
                var isType2 = dump.Family == TagFamily.MifareType2;
                var memoryLabel = isType2 ? "Pages" : "Blocks";
                var unreadableCount = isType2
                    ? dump.UnreadablePages.Count
                    : dump.UnreadableBlocks.Count;

                TagReadStatus = unreadableCount == 0 ? "OK" : "PARTIAL";
                TagType = dump.TagType;
                TagId = FormatHex(dump.Uid);

                var familyDetails = isType2
                    ? $"Unreadable pages: {dump.UnreadablePages.Count}"
                    : $"Sectors without an accepted key or outside detected capacity: {dump.SectorsWithoutAcceptedKey.Count}{Environment.NewLine}" +
                      $"Unreadable blocks: {dump.UnreadableBlocks.Count}";

                TagDetails =
                    $"Reader: {dump.ReaderName}{Environment.NewLine}" +
                    $"{memoryLabel} read: {dump.Memory.Count}{Environment.NewLine}" +
                    $"{familyDetails}{Environment.NewLine}" +
                    $"ATS: {FormatHex(dump.Ats)}{Environment.NewLine}" +
                    $"ATR: {FormatHex(dump.Atr)}{Environment.NewLine}" +
                    $"ATQA: {FormatHex(dump.Atqa)}{Environment.NewLine}" +
                    $"SAK: {dump.Sak.ToString("X2") ?? "Not reported"}{Environment.NewLine}";
                DumpAvailable?.Invoke(dump);
            });
        }

        private static string FormatHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Not reported";
            }

            var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);

            try
            {
                return string.Join(
                    " ",
                    Convert.FromHexString(normalized)
                        .Select(value => value.ToString("X2")));
            }
            catch (FormatException)
            {
                return value;
            }
        }

        private static void OnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(action);
                return;
            }

            action();
        }

        private void SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
