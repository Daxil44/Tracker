using System.Text.Json.Serialization;

namespace Tracker.Models;

public enum FeedbackKind
{
    Suggestion,
    Bug,
    Other,
}

public sealed record FeedbackMessage(FeedbackKind Kind, string Message, string? Name, string? ReplyTo);

public enum FeedbackResult
{
    Sent,
    NotConfigured,
    TooSoon,
    Failed,
}

/// <summary>
/// EmailJS identifiers from wwwroot/appsettings.json. All three are public by design (EmailJS is
/// called from the browser). The destination email address lives only in the EmailJS template.
/// </summary>
public sealed record EmailJsOptions(string? ServiceId, string? TemplateId, string? PublicKey)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceId) && !string.IsNullOrWhiteSpace(TemplateId) && !string.IsNullOrWhiteSpace(PublicKey);
}

/// <summary>Body for POST https://api.emailjs.com/api/v1.0/email/send.</summary>
public sealed class EmailJsRequest
{
    [JsonPropertyName("service_id")] public string ServiceId { get; set; } = "";
    [JsonPropertyName("template_id")] public string TemplateId { get; set; } = "";
    [JsonPropertyName("user_id")] public string PublicKey { get; set; } = "";
    [JsonPropertyName("template_params")] public Dictionary<string, string> TemplateParams { get; set; } = [];
}
