using System.Windows;
using System.Windows.Controls;
using NFCI.ViewModels;

namespace NFCI.Views
{
    public partial class WriteTagsView : UserControl
    {
        public WriteTagsView()
        {
            InitializeComponent();
        }

        private void WriteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not WriteTagsViewModel viewModel)
            {
                return;
            }

            var selectedCount = viewModel.Units.Count(unit => unit.IsWritable && unit.IsSelected);

            var confirmation = MessageBox.Show(
                $"Write the entered bytes to {selectedCount} selected unit(s) on UID {viewModel.TagSummary}?",
                "Confirm tag write",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation == MessageBoxResult.Yes)
            {
                viewModel.WriteSelected();
            }
        }
    }
}