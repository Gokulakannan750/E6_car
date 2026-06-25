using System.Net.Http.Json;
using System.Text.Json;
using E6CarSpa.Api.Config;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace E6CarSpa.Api.Services;

/// <summary>
/// Sends the automatic "thank you" WhatsApp message after a customer pays, using the
/// official WhatsApp Business Cloud API (template message). Every attempt is recorded
/// in <see cref="NotificationLog"/>. When disabled, it logs the intent without sending —
/// useful before the business completes WhatsApp onboarding / template approval.
/// </summary>
public class WhatsAppService(
    IHttpClientFactory httpFactory,
    IOptions<WhatsAppOptions> options,
    AppDbContext db,
    ILogger<WhatsAppService> logger)
{
    private readonly WhatsAppOptions _opt = options.Value;

    public async Task SendPaymentThankYouAsync(Invoice invoice, CancellationToken ct = default)
    {
        var phone = NormalisePhone(invoice.Customer?.Phone ?? "");
        var customerName = invoice.Customer?.Name ?? "Customer";
        var amount = invoice.GrandTotal.ToString("0.##");
        var car = invoice.Vehicle?.CarNumber ?? "";

        var log = new NotificationLog
        {
            InvoiceId = invoice.Id,
            ToPhone = phone,
            TemplateName = _opt.PaymentTemplateName,
            Body = $"Thank you {customerName}! Payment of Rs.{amount} for {car} received. - E6 Car Spa",
            Status = NotificationStatus.Pending
        };

        try
        {
            if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.ApiUrl) || string.IsNullOrWhiteSpace(phone))
            {
                log.Status = NotificationStatus.Pending;
                log.ProviderReference = "WhatsApp disabled or not configured — message not sent.";
                logger.LogInformation("WhatsApp skipped for invoice {Invoice}: disabled/unconfigured.", invoice.InvoiceNumber);
            }
            else
            {
                var payload = BuildTemplatePayload(phone, customerName, amount, car);
                var client = httpFactory.CreateClient("whatsapp");

                // Auth set per-request (don't mutate the pooled client's default headers).
                using var request = new HttpRequestMessage(HttpMethod.Post, _opt.ApiUrl)
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opt.AccessToken);

                var resp = await client.SendAsync(request, ct);
                var respBody = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    log.Status = NotificationStatus.Sent;
                    log.SentAt = DateTime.UtcNow;
                    log.ProviderReference = ExtractMessageId(respBody);
                }
                else
                {
                    log.Status = NotificationStatus.Failed;
                    log.ProviderReference = $"HTTP {(int)resp.StatusCode}: {respBody}";
                    logger.LogWarning("WhatsApp send failed for invoice {Invoice}: {Body}", invoice.InvoiceNumber, respBody);
                }
            }
        }
        catch (Exception ex)
        {
            log.Status = NotificationStatus.Failed;
            log.ProviderReference = ex.Message;
            logger.LogError(ex, "WhatsApp send threw for invoice {Invoice}", invoice.InvoiceNumber);
        }

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Meta Cloud API template-message body. Body template expects 3 positional params.</summary>
    private object BuildTemplatePayload(string phone, string name, string amount, string car) => new
    {
        messaging_product = "whatsapp",
        to = phone,
        type = "template",
        template = new
        {
            name = _opt.PaymentTemplateName,
            language = new { code = _opt.TemplateLanguage },
            components = new[]
            {
                new
                {
                    type = "body",
                    parameters = new[]
                    {
                        new { type = "text", text = name },
                        new { type = "text", text = amount },
                        new { type = "text", text = car },
                    }
                }
            }
        }
    };

    private static string? ExtractMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0
                ? msgs[0].GetProperty("id").GetString()
                : body;
        }
        catch { return body; }
    }

    /// <summary>Strip spaces/+, prepend country code for bare 10-digit Indian numbers.</summary>
    private string NormalisePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = _opt.DefaultCountryCode + digits;
        return digits;
    }
}
