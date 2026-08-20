using System.Windows.Controls;

namespace E6CarSpa.Desktop.Views;

/// <summary>
/// Daily showroom staff assignment tab.
/// </summary>
public partial class ShowroomDailyView : UserControl
{
    public ShowroomDailyView()
    {
        InitializeComponent();
    }

    private void BulkStaffList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.ShowroomDailyViewModel vm && sender is ListBox lb)
        {
            vm.BulkSelectedStaff = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<Contracts.StaffDto>(lb.SelectedItems));
        }
    }
}
