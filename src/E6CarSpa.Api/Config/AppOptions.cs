namespace E6CarSpa.Api.Config;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "E6CarSpa";
    public string Audience { get; set; } = "E6CarSpa.Desktop";
    /// <summary>Symmetric signing key. MUST be overridden in configuration / environment for production.</summary>
    public string Key { get; set; } = "CHANGE_ME_super_secret_key_at_least_32_chars_long!";
    public int ExpiryHours { get; set; } = 8;
}

/// <summary>
/// WhatsApp Business Cloud API settings. Works with Meta Cloud API directly or any
/// provider that exposes a Graph-style /messages endpoint (AiSensy, Interakt, Gupshup).
/// </summary>
public class WhatsAppOptions
{
    public const string Section = "WhatsApp";

    /// <summary>When false, messages are logged but not actually sent (safe for dev / before onboarding).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Full URL to POST messages to, e.g. https://graph.facebook.com/v21.0/&lt;PHONE_NUMBER_ID&gt;/messages</summary>
    public string ApiUrl { get; set; } = "";

    /// <summary>Bearer token / API key for the provider.</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Approved template name to use for the post-payment thank-you (fully paid).</summary>
    public string PaymentTemplateName { get; set; } = "payment_thank_you";

    /// <summary>
    /// Approved template sent when an invoice is generated. Must be created in Meta with a
    /// DOCUMENT header so the invoice PDF can be attached, plus 3 body parameters
    /// (customer name, invoice number, amount).
    /// </summary>
    public string InvoiceTemplateName { get; set; } = "invoice_generated";

    /// <summary>
    /// Approved template sent after a PART payment: 4 body parameters
    /// (customer name, amount paid, balance remaining, car number).
    /// </summary>
    public string PartialPaymentTemplateName { get; set; } = "payment_partial";

    /// <summary>Template language code, e.g. "en" or "en_US".</summary>
    public string TemplateLanguage { get; set; } = "en";

    /// <summary>Country calling code prefixed to 10-digit numbers, e.g. "91" for India.</summary>
    public string DefaultCountryCode { get; set; } = "91";
}
