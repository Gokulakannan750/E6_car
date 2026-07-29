using E6CarSpa.Mobile.Pages;

namespace E6CarSpa.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// Pages reached by navigation rather than via a tab.
		Routing.RegisterRoute("invoice", typeof(InvoiceDetailPage));
		Routing.RegisterRoute("lowstock", typeof(LowStockPage));
		// Settings is reached from a gear toolbar item on each tab, not from the tab bar.
		Routing.RegisterRoute("settings", typeof(SettingsPage));
	}
}
