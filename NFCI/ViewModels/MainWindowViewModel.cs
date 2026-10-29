namespace NFCI.ViewModels
{
    public sealed class MainWindowViewModel : IDisposable
    {
        private readonly MainViewModel _readTags = new();

        public MainViewModel ReadTags => _readTags;
        public WriteTagsViewModel WriteTags { get; }
        public LoanSystemViewModel LoanSystem { get; } = new();

        public MainWindowViewModel()
        {
            WriteTags = new WriteTagsViewModel(_readTags.AdditionalMifareClassicKeys);
            _readTags.DumpAvailable += WriteTags.LoadDump;
        }

        public void Start()
        {
            _readTags.Start();
        }

        public void Dispose()
        {
            _readTags.DumpAvailable -= WriteTags.LoadDump;
            _readTags.Dispose();
        }
    }
}