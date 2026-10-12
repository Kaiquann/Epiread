using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

/// <summary>
///     Controller responsible for providing translation services by proxying
///     requests to an external translation provider (Google Translate API endpoint).
///     This controller receives text and language parameters from a POST form and
///     forwards the request to the translation endpoint. It returns the provider's
///     JSON response as an <see cref="Microsoft.AspNetCore.Mvc.ContentResult" />.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class TranslateController : Controller
{
    /// <summary>
    ///     Translate the supplied <paramref name="text" /> by proxying the request to
    ///     the external translation service endpoint. Uses HTTP GET for short text
    ///     (appends text as query) and HTTP POST for large text bodies.
    /// </summary>
    /// <param name="text">The text to be translated (required).</param>
    /// <param name="sl">Source language code (ISO). If empty, defaults to "auto" for auto-detection.</param>
    /// <param name="tl">Target language code (ISO). If empty, defaults to "EN".</param>
    /// <returns>
    ///     An <see cref="IActionResult" /> that is either the translated JSON content
    ///     returned by the provider or an error response with a status code and
    ///     error message.
    /// </returns>
    /// <remarks>
    ///     This action returns a ContentResult containing the raw JSON from the
    ///     translation provider. It will return HTTP 400 if the provided text is
    ///     invalid, HTTP 500 for proxy-level errors, or the provider status code
    ///     if the translation provider returns a non-successful status.
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> TranslateText([FromForm] string text, [FromForm] string sl, [FromForm] string tl)
    {
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Invalid text" });
        // Only log DB/network operations: translation proxy is a network operation
        LoggerUtilities.Info($"[Start] TranslateText textLength={text.Length} sl={sl} tl={tl}");
        sl = string.IsNullOrWhiteSpace(sl) ? "auto" : sl;
        tl = string.IsNullOrWhiteSpace(tl) ? "EN" : tl;
        try
        {
            HttpClient httpClient = new();
            string baseUri = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t";
            HttpResponseMessage message;
            if (text.Length > 2000)
            {
                FormUrlEncodedContent formContent = new([
                    new KeyValuePair<string, string>("q", text)
                ]);
                message = await httpClient.PostAsync(baseUri, formContent);
            }
            else
            {
                string s = Uri.EscapeDataString(text);
                string uri = baseUri + $"&q={s}";
                message = await httpClient.GetAsync(uri);
            }

            if (!message.IsSuccessStatusCode)
            {
                // Log provider non-success status
                LoggerUtilities.Warning($"[Process] Translation provider returned status={(int)message.StatusCode}");
                return StatusCode((int)message.StatusCode, new { error = "Translation provider error" });
            }

            string content = await message.Content.ReadAsStringAsync();
            // Log success and response size
            LoggerUtilities.Info(
                $"[End] TranslateText success status={(int)message.StatusCode} responseLength={content?.Length ?? 0}");
            return Content(content ?? string.Empty, "application/json");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] TranslateText Unhandled exception");
            return StatusCode(500, new { error = "Proxy error" });
        }
    }
}