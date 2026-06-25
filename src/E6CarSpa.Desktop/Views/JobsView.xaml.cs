using System.Windows.Controls;
using System.Windows.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class JobsView : UserControl
{
    public JobsView() => InitializeComponent();

    private void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is JobsViewModel vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    private void Jobs_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is JobsViewModel vm && sender is DataGrid { SelectedItem: InvoiceListItemDto item }
            && vm.OpenCommand.CanExecute(item))
            vm.OpenCommand.Execute(item);
    }
}
