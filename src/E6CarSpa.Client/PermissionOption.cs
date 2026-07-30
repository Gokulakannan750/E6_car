using System.ComponentModel;
using System.Runtime.CompilerServices;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Client;

/// <summary>
/// One tickable permission in a user editor, with a label a shop owner would recognise. Lives in
/// the shared client so the desktop and phone offer exactly the same list in the same order.
/// </summary>
public class PermissionOption : INotifyPropertyChanged
{
    public Permission Value { get; init; }
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";

    private bool _isGranted;
    public bool IsGranted
    {
        get => _isGranted;
        set
        {
            if (_isGranted == value) return;
            _isGranted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGranted)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Every permission, in display order, ticked to match <paramref name="granted"/>.</summary>
    public static List<PermissionOption> BuildList(Permission granted) =>
    [
        Make(Permission.Billing,       "Billing / New Job", "Create jobs, quotations, invoices and take payments", granted),
        Make(Permission.Customers,     "Customers",         "Customer directory and vehicle lookup",               granted),
        Make(Permission.Catalogue,     "Catalogue",         "Services and prices",                                 granted),
        Make(Permission.StaffAdvances, "Staff Advances",    "Cash advances given to workers",                      granted),
        Make(Permission.Reports,       "Reports",           "Sales figures and takings",                           granted),
        Make(Permission.Inventory,     "Inventory",         "Stock levels and low-stock alerts",                   granted),
        Make(Permission.Settings,      "Settings",          "Company profile, GST details, invoice numbering",      granted),
        Make(Permission.ManageUsers,   "Manage users",      "Create staff logins and set their permissions",        granted),
    ];

    private static PermissionOption Make(Permission value, string label, string description, Permission granted) =>
        new() { Value = value, Label = label, Description = description, IsGranted = granted.HasFlag(value) };

    /// <summary>Fold a tick-list back into the single bit field the API stores.</summary>
    public static Permission Combine(IEnumerable<PermissionOption> options) =>
        options.Where(o => o.IsGranted).Aggregate(Permission.None, (acc, o) => acc | o.Value);
}
