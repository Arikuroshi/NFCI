using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NFCI.Models;
using NFCI.Services;

namespace NFCI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Acr122Reader _reader = new();
        private string _status = "Reader monitoring is stopped.";
        private string _tagDetails = "No tag read.";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
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
        }

        public void Start()
        {
            _reader.Start();
        }

        public void Dispose()
        {
            _reader.StatusChanged -= OnStatusChanged;
            _reader.TagRead -= OnTagRead;
            _reader.Dispose();
        }

        private void OnStatusChanged(string status)
        {
            OnUiThread(() => Status = status);
        }

        private void OnTagRead(TagInfo tag)
        {
            OnUiThread(() =>
            {
                TagDetails =
                    $"Reader: {tag.ReaderName}{Environment.NewLine}" +
                    $"UID: {tag.Uid}{Environment.NewLine}" +
                    $"ATS: {tag.Ats ?? "<none>"}{Environment.NewLine}" +
                    $"ATR: {tag.Atr ?? "<none>"}";
            });
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
