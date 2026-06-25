using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Domain.Entities;

/// <summary>Audit of WhatsApp messages sent to customers (e.g. the post-payment thank-you).</summary>
public class NotificationLog : BaseEntity
{
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string ToPhone { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Rendered message body for the record.</summary>
    public string? Body { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>Provider message id when sent, or error detail when failed.</summary>
    public string? ProviderReference { get; set; }
    public DateTime? SentAt { get; set; }
}
