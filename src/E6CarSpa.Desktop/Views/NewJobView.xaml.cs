using System.Linq;
using System.Windows;
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

    // Phone: digits only (MaxLength="10" in XAML caps it at a 10-digit number).
    private void PhoneBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsDigit);

    private void PhoneBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(typeof(string)) is string s && !s.All(char.IsDigit))
            e.CancelCommand();
    }
}
