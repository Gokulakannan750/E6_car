using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Pages;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("invoice", typeof(InvoiceDetailPage));
		Routing.RegisterRoute("lowstock", typeof(LowStockPage));
		Routing.RegisterRoute("settings", typeof(SettingsPage));
		Routing.RegisterRoute("users", typeof(UsersPage));

		ApplyPermissions();
	}

	private void ApplyPermissions()
	{
		var user = AppServices.Api.CurrentUser;
		if (user is null) return;

		void Remove(ShellContent tab)
		{
			if (tab.Parent is TabBar bar && bar.Items.Contains(tab)) bar.Items.Remove(tab);
		}

		if (!user.Can(Permission.Billing)) { Remove(NewJobTab); Remove(JobsTab); }
		if (!user.Can(Permission.Reports)) Remove(ReportsTab);
		if (!user.Can(Permission.StaffAdvances)) Remove(AdvancesTab);
		if (!user.Can(Permission.Showroom)) Remove(ShowroomTab);
	}
}
