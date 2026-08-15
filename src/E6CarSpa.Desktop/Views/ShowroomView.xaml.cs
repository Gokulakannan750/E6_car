using System.Windows;
using System.Windows.Controls;

namespace E6CarSpa.Desktop.Views;

/// <summary>
/// Code-behind for ShowroomView. The view has no logic of its own — everything lives
/// in ShowroomViewModel so it can be unit-tested without a window.
/// </summary>
public partial class ShowroomView : UserControl
{
    public ShowroomView()
    {
        InitializeComponent();
    }
}
