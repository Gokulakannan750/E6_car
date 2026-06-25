using System.Windows.Controls;
using System.Windows.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class NewJobView : UserControl
{
    public NewJobView() => InitializeComponent();

    private void Catalogue_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NewJobViewModel vm && CatalogueList.SelectedItem is ServiceDto s)
            vm.AddServiceLine(s);
    }
}
