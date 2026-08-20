using System.Windows;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class ShowroomFormView : Window
{
 public ShowroomFormView()
 {
 InitializeComponent();
 }

 private void OnSave(object sender, RoutedEventArgs e)
 {
 var vm = (ShowroomFormViewModel)DataContext;
 if (string.IsNullOrWhiteSpace(vm.Name))
 {
 MessageBox.Show("Enter the showroom name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
 return;
 }
 if (string.IsNullOrWhiteSpace(vm.Address))
 {
 MessageBox.Show("Enter the showroom address.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
 return;
 }
 DialogResult = true;
 }
}
