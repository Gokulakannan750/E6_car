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
}
