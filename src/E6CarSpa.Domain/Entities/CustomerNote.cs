namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A timestamped text note attached to a customer. Used for preferences, follow-ups, visit history, etc.
/// </summary>
public class CustomerNote : BaseEntity
{
 public Guid CustomerId { get; set; }
 public Customer Customer { get; set; } = null!;

 /// <summary>The note text entered by staff.</summary>
 public string Text { get; set; } = string.Empty;

 /// <summary>ID of the staff/user who created the note.</summary>
 public Guid? CreatedByStaffId { get; set; }
}
