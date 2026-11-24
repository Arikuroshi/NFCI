using NFCI.Services;

namespace NFCI.ViewModels
{
    public sealed class MainWindowViewModel : IDisposable
    {
        private readonly MainViewModel _readTags = new();

        public MainViewModel ReadTags => _readTags;
        public WriteTagsViewModel WriteTags { get; }
        public LoanSystemViewModel LoanSystem { get; }

        public MainWindowViewModel()
        {
            LoanSystem = new LoanSystemViewModel(new TagRepositoryFactory());
            WriteTags = new WriteTagsViewModel(_readTags.AdditionalMifareClassicKeys);

            _readTags.DumpAvailable += WriteTags.LoadDump;
            _readTags.TagDetected += LoanSystem.OnTagDetected;
        }

        public void Start()
        {
            _readTags.Start();
        }

        public void Dispose()
        {
            _readTags.DumpAvailable -= WriteTags.LoadDump;
            _readTags.TagDetected -= LoanSystem.OnTagDetected;
            LoanSystem.Dispose();
            _readTags.Dispose();
        }
    }
}