using System.Windows;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class EditAssignmentView : Window
{
    public EditAssignmentView()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (e.NewValue is EditAssignmentViewModel vm)
            {
                vm.CloseAction = result => DialogResult = result;
            }
        };
    }
}
