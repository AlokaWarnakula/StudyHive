using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;

namespace StudyHive.Api.Services;

/// <summary>Brevo transactional email: POST /v3/smtp/email. The api-key header is set once on the
/// typed HttpClient (see <see cref="EmailSenderRegistration"/>); this class never sees or logs it.
/// Brevo answers 201 with <c>{"messageId": "&lt;...&gt;"}</c>, and a 4xx/5xx with
/// <c>{"code": ..., "message": ...}</c>, which is what last_error records.</summary>
public sealed class BrevoEmailProvider(HttpClient http, IOptions<BrevoOptions> options) : IEmailProvider
{
    private const int MaxErrorLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var brevo = options.Value;
        var payload = new
        {
            sender = new { name = brevo.SenderName, email = brevo.SenderEmail },
            to = new[] { new { email = message.ToEmail, name = message.ToName } },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.TextBody,
        };

        try
        {
            using var response = await http.PostAsJsonAsync("/v3/smtp/email", payload, JsonOptions, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return EmailSendResult.Failed(Truncate($"Brevo returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}"));
            }

            return EmailSendResult.Sent(ReadMessageId(body));
        }
        catch (HttpRequestException ex)
        {
            return EmailSendResult.Failed(Truncate($"Brevo request failed: {ex.Message}"));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return EmailSendResult.Failed("Brevo request timed out.");
        }
    }

    private static string? ReadMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("messageId", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string text) => text.Length <= MaxErrorLength ? text : text[..MaxErrorLength];
}
