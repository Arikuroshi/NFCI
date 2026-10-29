using System.Windows;
using NFCI.ViewModels;

namespace NFCI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;

            Loaded += (_, _) => _viewModel.Start();
            Closed += (_, _) => _viewModel.Dispose();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ReadTagsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedIndex = 0;
        }

        private void WriteTagsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedIndex = 1;
        }

        private void LoanSystemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedIndex = 2;
        }

        private void ConfigureDatabaseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedIndex = 2;
            _viewModel.LoanSystem.ConnectionStatus = "Configuration UI not implemented yet.";
            MessageBox.Show(
                "Database connection setup is not implemented yet.",
                "Database Connection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "NFCI\nNFC reader, writer, and loan workflow tool.",
                "About NFCI",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}