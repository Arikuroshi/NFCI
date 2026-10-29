using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NFCI.Models;
using NFCI.Services;
using System.Text;

namespace NFCI.ViewModels
{
    public sealed class WriteTagsViewModel : INotifyPropertyChanged
    {
        private readonly IEnumerable<string> _additionalClassicKeys;
        private CardDump? _currentDump;
        private string _payloadHex = string.Empty;
        private string _writeStatus = "Place a supported tag on the reader.";
        private string _tagSummary = "No tag scanned.";
        private bool _payloadIsText;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<WritableUnitItem> Units { get; } = [];

        public string PayloadHex
        {
            get => _payloadHex;
            set
            {
                if (SetProperty(ref _payloadHex, value))
                {
                    OnPropertyChanged(nameof(CanWrite));
                }
            }
        }

        public string WriteStatus
        {
            get => _writeStatus;
            private set => SetProperty(ref _writeStatus, value);
        }

        public string TagSummary
        {
            get => _tagSummary;
            private set => SetProperty(ref _tagSummary, value);
        }

        public bool PayloadIsText
        {
            get => _payloadIsText;
            set
            {
                if (SetProperty(ref _payloadIsText, value))
                {
                    OnPropertyChanged(nameof(CanWrite));
                }
            }
        }

        public bool CanWrite =>
            _currentDump is not null &&
            Units.Any(unit => unit.IsWritable && unit.IsSelected) &&
            !string.IsNullOrEmpty(PayloadHex);

        public WriteTagsViewModel(IEnumerable<string> additionalClassicKeys)
        {
            _additionalClassicKeys = additionalClassicKeys;
        }

        public void LoadDump(CardDump dump)
        {
            _currentDump = dump;
            Units.Clear();

            foreach (var unit in TagWriteService.BuildPlan(dump))
            {
                Units.Add(new WritableUnitItem(unit, OnSelectionChanged));
            }

            TagSummary = $"{dump.TagType} | UID: {dump.Uid} | Select writable pages/blocks.";
            WriteStatus = "Enter payload bytes as hex, select destinations, then write.";
            OnPropertyChanged(nameof(CanWrite));
        }

        public void WriteSelected()
        {
            if (_currentDump is null)
            {
                WriteStatus = "Scan a supported tag first.";
                return;
            }

            byte[] payload;

            if (PayloadIsText)
            {
                if (PayloadHex.Length == 0)
                {
                    WriteStatus = "Enter text to write.";
                    return;
                }

                payload = Encoding.UTF8.GetBytes(PayloadHex);
            }
            else if (!TryParseHex(PayloadHex, out payload))
            {
                WriteStatus = "Enter valid hexadecimal bytes, for example: DE AD BE EF.";
                return;
            }

            var selected = Units
                .Where(unit => unit.IsWritable && unit.IsSelected)
                .Select(unit => unit.MemoryUnit)
                .ToList();

            try
            {
                WriteStatus = TagWriteService.WriteSelected(
                    _currentDump,
                    selected,
                    payload,
                    _additionalClassicKeys);
            }
            catch (Exception ex)
            {
                WriteStatus = ex.Message;
            }
        }

        private void OnSelectionChanged()
        {
            OnPropertyChanged(nameof(CanWrite));
        }

        private static bool TryParseHex(string text, out byte[] bytes)
        {
            bytes = [];

            var normalized = text
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (normalized.Length == 0 || normalized.Length % 2 != 0)
            {
                return false;
            }

            try
            {
                bytes = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public sealed class WritableUnitItem : INotifyPropertyChanged
        {
            private readonly Action _selectionChanged;
            private bool _isSelected;

            public event PropertyChangedEventHandler? PropertyChanged;

            public WritableMemoryUnit MemoryUnit { get; }
            public int Address => MemoryUnit.Address;
            public string Label => MemoryUnit.Label;
            public string CurrentDataHex => Convert.ToHexString(MemoryUnit.CurrentData);
            public bool IsWritable => MemoryUnit.IsWritable;
            public string ProtectionReason => MemoryUnit.ProtectionReason;

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value)
                    {
                        return;
                    }

                    _isSelected = value;
                    PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(nameof(IsSelected)));
                    _selectionChanged();
                }
            }

            public WritableUnitItem(
                WritableMemoryUnit memoryUnit,
                Action selectionChanged)
            {
                MemoryUnit = memoryUnit;
                _selectionChanged = selectionChanged;
            }
        }
    }
}