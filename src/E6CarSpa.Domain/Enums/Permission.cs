namespace E6CarSpa.Domain.Enums;

/// <summary>
/// What a user is allowed to reach, stored per user as a bit field. Roles are only presets that
/// fill this in — the permission set is what actually gets enforced, so one worker can be given
/// Reports without opening it up for every worker.
/// </summary>
/// <remarks>
/// Values are explicit powers of two and must never be renumbered: they are persisted as an int
/// and embedded in issued tokens. Add new permissions with the next free bit.
/// </remarks>
[Flags]
public enum Permission
{
    None = 0,

    /// <summary>New Job, quotations, invoices, payments — the counter workflow.</summary>
    Billing = 1 << 0,

    /// <summary>Customer directory and vehicle lookup.</summary>
    Customers = 1 << 1,

    /// <summary>Service catalogue and pricing.</summary>
    Catalogue = 1 << 2,

    /// <summary>Cash advances given to workers (wage data).</summary>
    StaffAdvances = 1 << 3,

    /// <summary>Sales reports and takings.</summary>
    Reports = 1 << 4,

    /// <summary>Stock and low-stock management.</summary>
    Inventory = 1 << 5,

    /// <summary>Company profile, GST details, invoice numbering.</summary>
    Settings = 1 << 6,

    /// <summary>Showroom management — locations, visits, and per-showroom revenue.</summary>
    Showroom = 1 << 8,

    /// <summary>Create staff logins and set their permissions.</summary>
    ManageUsers = 1 << 7,

    All = Billing | Customers | Catalogue | StaffAdvances | Reports | Inventory | Settings | Showroom | ManageUsers
}

public static class PermissionPresets
{
    /// <summary>The permissions a role starts with. The admin can then tick/untick per person.</summary>
    public static Permission For(UserRole role) => role switch
    {
        UserRole.Admin => Permission.All,
        UserRole.Manager => Permission.Billing | Permission.Customers | Permission.Catalogue |
                            Permission.StaffAdvances | Permission.Reports | Permission.Inventory |
                            Permission.Showroom,
        _ => Permission.Billing | Permission.Customers
    };
}
