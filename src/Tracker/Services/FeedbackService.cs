using System.Text;
using System.Text.Json;
using Tracker.Models;
using Tracker.Serialization;

namespace Tracker.Services;

/// <summary>
/// Sends feedback through EmailJS's REST API straight from the browser (no backend, no SDK).
/// Only what the user typed is sent, plus the time and the app's address.
/// </summary>
public sealed class FeedbackService(HttpClient http, EmailJsOptions options, TimeProvider clock, ILogger<FeedbackService> logger)
{
    public const int MinMessageLength = 5;
    public const int MaxMessageLength = 2_000;
    private const string Endpoint = "https://api.emailjs.com/api/v1.0/email/send";
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    private DateTimeOffset? _lastSentAt;

    public bool IsConfigured => options.IsConfigured;

    /// <summary>Seconds until another message may be sent (simple per-tab guard against double sends).</summary>
    public int SecondsUntilNextSend =>
        _lastSentAt is { } last ? Math.Max(0, (int)Math.Ceiling((last + Cooldown - clock.GetUtcNow()).TotalSeconds)) : 0;

    public async Task<FeedbackResult> SendAsync(FeedbackMessage feedback, string appUrl)
    {
        if (!options.IsConfigured)
        {
            return FeedbackResult.NotConfigured;
        }

        if (SecondsUntilNextSend > 0)
        {
            return FeedbackResult.TooSoon;
        }

        var request = new EmailJsRequest
        {
            ServiceId = options.ServiceId!,
            TemplateId = options.TemplateId!,
            PublicKey = options.PublicKey!,
            TemplateParams = new()
            {
                ["feedback_type"] = feedback.Kind.ToString(),
                ["message"] = feedback.Message,
                ["from_name"] = string.IsNullOrWhiteSpace(feedback.Name) ? "Anonymous" : feedback.Name,
                ["reply_to"] = feedback.ReplyTo ?? "",
                ["sent_at"] = clock.GetLocalNow().ToString("ddd, MMM d, yyyy h:mm tt zzz", System.Globalization.CultureInfo.InvariantCulture),
                ["app_url"] = appUrl,
            },
        };

        try
        {
            var json = JsonSerializer.Serialize(request, AppJsonContext.Default.EmailJsRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(Endpoint, content);
            if (response.IsSuccessStatusCode)
            {
                _lastSentAt = clock.GetUtcNow();
                return FeedbackResult.Sent;
            }

            logger.LogWarning("EmailJS returned {Status}: {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            return FeedbackResult.Failed;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not reach EmailJS.");
            return FeedbackResult.Failed;
        }
    }
}
