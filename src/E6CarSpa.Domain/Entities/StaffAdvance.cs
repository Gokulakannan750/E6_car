namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A cash advance handed to a worker. Workers are free-typed names — they are deliberately NOT
/// app users, since the floor staff don't log in. This is a simple record of money given out
/// (no repayment tracking, no payroll).
/// </summary>
public class StaffAdvance : BaseEntity
{
    public string WorkerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>The day the advance was given (not necessarily when it was keyed in).</summary>
    public DateTime AdvanceDate { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public Guid? RecordedByUserId { get; set; }

    // ----- Soft delete -----
    // These are money records, so a mistaken entry is marked obsolete rather than erased: the row
    // stays for the audit trail carrying who removed it and when. Obsolete rows are excluded from
    // listings and from the per-worker totals unless explicitly asked for.

    /// <summary>When the advance was marked obsolete. Null for a live record.</summary>
    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// Username captured at the moment of deletion. Stored next to the id so the trail still reads
    /// correctly if that account is later renamed or deactivated.
    /// </summary>
    public string? DeletedByUsername { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
