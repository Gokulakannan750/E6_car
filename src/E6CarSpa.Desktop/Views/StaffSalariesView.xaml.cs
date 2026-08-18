using System.Windows.Controls;

namespace E6CarSpa.Desktop.Views;

/// <summary>
/// Salary payments to floor workers. Mirrors the Staff Advances view but tracks wages paid
/// (separate from informal cash handouts).
/// </summary>
public partial class StaffSalariesView : UserControl
{
    public StaffSalariesView() => InitializeComponent();
}