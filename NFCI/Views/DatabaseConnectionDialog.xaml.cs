using System.Windows;
using NFCI.Models;

namespace NFCI.Views
{
    public partial class DatabaseConnectionDialog : Window
    {
        public DatabaseConnectionOptions? Options { get; private set; }

        public DatabaseConnectionDialog()
        {
            InitializeComponent();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HostTextBox.Text) ||
                string.IsNullOrWhiteSpace(DatabaseTextBox.Text) ||
                string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show(
                    this,
                    "Enter the host, database, username, and password.",
                    "Missing connection details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text, out var port) ||
                port is < 1 or > 65535)
            {
                MessageBox.Show(
                    this,
                    "Enter a valid port number.",
                    "Invalid port",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Options = new PostgreSqlConnectionOptions(
                HostTextBox.Text.Trim(),
                port,
                DatabaseTextBox.Text.Trim(),
                UsernameTextBox.Text.Trim(),
                PasswordBox.Password);

            DialogResult = true;
        }
    }
}