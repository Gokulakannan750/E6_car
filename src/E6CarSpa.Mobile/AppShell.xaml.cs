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
		Routing.RegisterRoute("advances", typeof(AdvancesPage));
	}
}
