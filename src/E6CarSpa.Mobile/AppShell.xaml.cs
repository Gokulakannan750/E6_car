using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Pages;
using E6CarSpa.Mobile.Services;

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
		Routing.RegisterRoute("users", typeof(UsersPage));

		ApplyPermissions();
	}

	/// <summary>
	/// Drop tabs the signed-in user has no permission for. Dashboard always stays so there is
	/// somewhere to land. The API enforces the same permissions, so this only avoids offering
	/// screens that would fail.
	/// </summary>
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
	}
}
