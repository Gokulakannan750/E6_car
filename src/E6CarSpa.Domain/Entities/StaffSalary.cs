using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace E6CarSpa.Domain.Entities;

public class StaffSalary
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StaffId { get; set; }

    [Required]
    [Column(TypeName = "numeric(12,2)")]
    public decimal Amount { get; set; }

    /// <summary>Which month this salary entry belongs to (first day of month).</summary>
    public DateTime SalaryDate { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // ----- Navigation -----
    public Staff Staff { get; set; } = null!;

    // ----- Audit -----
    public Guid? DeletedByUserId { get; set; }
    public string? DeletedByUsername { get; set; }

    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByUsername { get; set; }
}
