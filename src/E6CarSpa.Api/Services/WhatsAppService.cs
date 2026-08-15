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

    /// <summary>Fully paid — thank the customer. Body params: name, amount, car.</summary>
    public Task SendPaymentThankYouAsync(Invoice invoice, CancellationToken ct = default)
    {
        var name = invoice.Customer?.Name ?? "Customer";
        var amount = invoice.GrandTotal.ToString("0.##");
        var car = invoice.Vehicle?.CarNumber ?? "";

        return SendTemplateAsync(invoice, _opt.PaymentTemplateName,
            new[] { name, amount, car },
            $"Thank you {name}! Payment of Rs.{amount} for {car} received. - E6 Car Spa",
            documentMediaId: null, ct);
    }

    /// <summary>
    /// Part payment taken — tell the customer what is still outstanding.
    /// Body params: name, amount paid now, balance remaining, car.
    /// </summary>
    public Task SendPartialPaymentAsync(Invoice invoice, decimal paidNow, CancellationToken ct = default)
    {
        var name = invoice.Customer?.Name ?? "Customer";
        var paid = paidNow.ToString("0.##");
        var car = invoice.Vehicle?.CarNumber ?? "";

        return SendTemplateAsync(invoice, _opt.PartialPaymentTemplateName,
            new[] { name, paid, car },
            $"Hi {name}, we received Rs.{paid} for {car}. - E6 Car Spa",
            documentMediaId: null, ct);
    }

    /// <summary>
    /// Invoice generated — send it to the customer with the PDF attached. The template must have a
    /// DOCUMENT header; if the upload fails we still send the text-only template rather than nothing.
    /// Body params: name, invoice number, amount.
    /// </summary>
    public async Task SendInvoiceAsync(Invoice invoice, byte[] pdf, CancellationToken ct = default)
    {
        var name = invoice.Customer?.Name ?? "Customer";
        var number = invoice.InvoiceNumber ?? "";
        var amount = invoice.GrandTotal.ToString("0.##");
        var fileName = $"{(string.IsNullOrEmpty(number) ? "invoice" : number.Replace("/", "-"))}.pdf";

        string? mediaId = null;
        if (_opt.Enabled && pdf.Length > 0)
            mediaId = await UploadPdfAsync(pdf, fileName, ct);

        await SendTemplateAsync(invoice, _opt.InvoiceTemplateName,
            new[] { name, number, amount },
            $"Hi {name}, invoice {number} for Rs.{amount} is attached. - E6 Car Spa",
            mediaId, ct, fileName);
    }

    // ---------- transport ----------

    /// <summary>
    /// Single place that actually talks to the provider: builds the template payload, posts it, and
    /// records the outcome in NotificationLog. Never throws — a messaging failure must not break
    /// billing, so problems are logged and surfaced through the notification log instead.
    /// </summary>
    private async Task SendTemplateAsync(
        Invoice invoice, string templateName, string[] bodyParams, string readableBody,
        string? documentMediaId, CancellationToken ct, string? fileName = null)
    {
        var phone = NormalisePhone(invoice.Customer?.Phone ?? "");

        var log = new NotificationLog
        {
            InvoiceId = invoice.Id,
            ToPhone = phone,
            TemplateName = templateName,
            Body = readableBody,
            Status = NotificationStatus.Pending
        };

        try
        {
            if (!_opt.Enabled || string.IsNullOrWhiteSpace(_opt.ApiUrl) || string.IsNullOrWhiteSpace(phone))
            {
                log.ProviderReference = "WhatsApp disabled or not configured — message not sent.";
                logger.LogInformation("WhatsApp skipped for invoice {Invoice} ({Template}): disabled/unconfigured.",
                    invoice.InvoiceNumber, templateName);
            }
            else
            {
                var payload = BuildTemplatePayload(phone, templateName, bodyParams, documentMediaId, fileName);
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
                    logger.LogWarning("WhatsApp send failed for invoice {Invoice} ({Template}): {Body}",
                        invoice.InvoiceNumber, templateName, respBody);
                }
            }
        }
        catch (Exception ex)
        {
            log.Status = NotificationStatus.Failed;
            log.ProviderReference = ex.Message;
            logger.LogError(ex, "WhatsApp send threw for invoice {Invoice} ({Template})", invoice.InvoiceNumber, templateName);
        }

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upload the invoice PDF to the provider's /media endpoint and return its media id, or null if
    /// the upload fails (the caller then sends the message without the attachment).
    /// </summary>
    private async Task<string?> UploadPdfAsync(byte[] pdf, string fileName, CancellationToken ct)
    {
        try
        {
            // The messages endpoint is .../<PHONE_NUMBER_ID>/messages; media lives alongside it.
            var mediaUrl = _opt.ApiUrl.Replace("/messages", "/media", StringComparison.OrdinalIgnoreCase);

            using var content = new MultipartFormDataContent
            {
                { new StringContent("whatsapp"), "messaging_product" },
                { new StringContent("application/pdf"), "type" }
            };
            var file = new ByteArrayContent(pdf);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(file, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, mediaUrl) { Content = content };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opt.AccessToken);

            var client = httpFactory.CreateClient("whatsapp");
            var resp = await client.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("WhatsApp media upload failed: {Body}", body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WhatsApp media upload threw");
            return null;
        }
    }

    /// <summary>Meta Cloud API template-message body, optionally with a PDF document header.</summary>
    private object BuildTemplatePayload(
        string phone, string templateName, string[] bodyParams, string? documentMediaId, string? fileName)
    {
        var components = new List<object>();

        if (!string.IsNullOrEmpty(documentMediaId))
        {
            components.Add(new
            {
                type = "header",
                parameters = new object[]
                {
                    new { type = "document", document = new { id = documentMediaId, filename = fileName ?? "invoice.pdf" } }
                }
            });
        }

        components.Add(new
        {
            type = "body",
            parameters = bodyParams.Select(p => new { type = "text", text = p }).ToArray()
        });

        return new
        {
            messaging_product = "whatsapp",
            to = phone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = _opt.TemplateLanguage },
                components = components.ToArray()
            }
        };
    }

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
