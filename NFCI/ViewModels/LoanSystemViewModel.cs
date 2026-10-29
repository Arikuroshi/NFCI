using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NFCI.ViewModels
{
    public sealed class LoanSystemViewModel : INotifyPropertyChanged
    {
        private string _connectionStatus = "Not configured.";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}