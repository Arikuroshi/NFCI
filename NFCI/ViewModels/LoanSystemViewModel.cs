using System.ComponentModel;
using System.Runtime.CompilerServices;
using NFCI.Models;
using NFCI.Services;

namespace NFCI.ViewModels
{
    public sealed class LoanSystemViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ITagRepositoryFactory _repositoryFactory;
        private ITagRepository? _tagRepository;
        private LoanService? _loanService;
        private string _providerDisplayName = "N/A";
        private string _connectionStatus = "Not configured.";
        private string _currentUid = string.Empty;
        private string _tagName = string.Empty;
        private string _operationStatus = "Scan a tag to begin.";
        private string _lastError = string.Empty;
        private bool _isConnected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoanSystemViewModel(ITagRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public string ProviderDisplayName
        {
            get => _providerDisplayName;
            set => SetProperty(ref _providerDisplayName, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string CurrentUid
        {
            get => _currentUid;
            private set => SetProperty(ref _currentUid, value);
        }

        public string TagName
        {
            get => _tagName;
            set => SetProperty(ref _tagName, value);
        }

        public string OperationStatus
        {
            get => _operationStatus;
            private set => SetProperty(ref _operationStatus, value);
        }

        public string LastError
        {
            get => _lastError;
            private set => SetProperty(ref _lastError, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        public async Task<bool> ConfigureDatabaseAsync(
            DatabaseConnectionOptions options)
        {
            LastError = string.Empty;
            ProviderDisplayName = GetProviderDisplayName(options.Provider);
            ConnectionStatus = "Connecting...";
            IsConnected = false;

            var previousRepository = _tagRepository;
            _tagRepository = null;
            _loanService = null;
            previousRepository?.Dispose();

            ITagRepository? repository = null;

            try
            {
                repository = _repositoryFactory.Create(options);
                await repository.InitializeAsync();

                _tagRepository = repository;
                _loanService = new LoanService(repository);
                IsConnected = true;
                ConnectionStatus = "Connected.";
                OperationStatus = "Scan a tag to begin.";
                return true;
            }
            catch (Exception exception)
            {
                repository?.Dispose();
                ConnectionStatus = "Connection failed.";
                LastError = exception.Message;
                return false;
            }
        }

        public void OnTagDetected(TagInfo tag)
        {
            CurrentUid = tag.Uid;
            TagName = string.Empty;
            OperationStatus = "Tag scanned. Enter a name and save it.";

            if (_loanService is not null)
            {
                _ = LookUpScannedTagAsync(tag.Uid);
            }
        }

        public async Task RegisterCurrentTagAsync()
        {
            if (_loanService is null)
            {
                OperationStatus = "Configure a database connection first.";
                return;
            }

            try
            {
                await _loanService.RegisterTagAsync(CurrentUid, TagName);
                OperationStatus = $"Saved '{TagName.Trim()}' for UID {CurrentUid}.";
            }
            catch (Exception exception)
            {
                OperationStatus = "Could not save the tag.";
                LastError = exception.Message;
            }
        }

        public void Dispose()
        {
            _tagRepository?.Dispose();
            _tagRepository = null;
            _loanService = null;
        }

        private async Task LookUpScannedTagAsync(string uid)
        {
            try
            {
                var tag = await _loanService!.FindTagByUidAsync(uid);

                if (tag is null)
                {
                    OperationStatus = "Tag not registered. Enter a name and save it.";
                    return;
                }

                TagName = tag.Name;
                OperationStatus = $"Tag is registered as '{tag.Name}'. Saving updates its name.";
            }
            catch (Exception exception)
            {
                OperationStatus = "Could not look up the scanned tag.";
                LastError = exception.Message;
            }
        }

        private static string GetProviderDisplayName(DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.PostgreSql => "PostgreSQL",
                _ => "N/A"
            };
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}