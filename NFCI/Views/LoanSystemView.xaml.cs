using System.Windows;
using System.Windows.Controls;
using NFCI.ViewModels;

namespace NFCI.Views
{
    public partial class LoanSystemView : UserControl
    {
        public LoanSystemView()
        {
            InitializeComponent();
        }

        private async void RegisterTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoanSystemViewModel viewModel)
            {
                await viewModel.RegisterCurrentTagAsync();
            }
        }
    }
}