using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpiRead.Services;

/// <summary>
/// Sends transactional email through Brevo's HTTPS API. Render free services
/// block outbound SMTP ports, while HTTPS traffic on port 443 remains available.
/// </summary>
public sealed class BrevoMailerHelper : IMailerHelper
{
    private readonly BrevoConfig _brevo;
    private readonly HttpClient _httpClient;
    private readonly SmtpConfig _sender;

    public BrevoMailerHelper(HttpClient httpClient, BrevoConfig brevo,
        SmtpConfig sender)
    {
        _httpClient = httpClient;
        _brevo = brevo;
        _sender = sender;
    }

    public async Task<bool> SendEmail(MailerConfig mail)
    {
        if (string.IsNullOrWhiteSpace(_brevo.ApiKey))
            throw new InvalidOperationException("Brevo API key is not configured.");

        if (string.IsNullOrWhiteSpace(_sender.From))
            throw new InvalidOperationException("The sender email address is not configured.");

        using HttpRequestMessage request = new(HttpMethod.Post, "smtp/email");
        request.Headers.Add("api-key", _brevo.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { name = _sender.Name, email = _sender.From },
            to = mail.To.Select(email => new { email }).ToArray(),
            cc = mail.Cc?.Select(email => new { email }).ToArray(),
            bcc = mail.Bcc?.Select(email => new { email }).ToArray(),
            subject = mail.Subject,
            htmlContent = mail.Body
        }, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode) return true;

        string details = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Brevo rejected the email request ({(int)response.StatusCode}): {details}");
    }
}
