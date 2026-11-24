using System.Windows;
using NFCI.Views;
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

        private async void ConfigureDatabaseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DatabaseConnectionDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || dialog.Options is null)
            {
                return;
            }

            MainTabs.SelectedIndex = 2;

            var connected = await _viewModel.LoanSystem.ConfigureDatabaseAsync(dialog.Options);

            if (!connected)
            {
                MessageBox.Show(
                    this,
                    _viewModel.LoanSystem.LastError,
                    "Database connection failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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