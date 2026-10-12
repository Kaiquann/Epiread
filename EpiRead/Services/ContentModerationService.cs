using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EpiRead.Services;

/// <summary>
///     Service responsible for analyzing user-generated text for offensive,
///     harmful, or otherwise inappropriate material using an AI model (Gemini).
///     The implementation strips HTML, builds a moderation prompt, invokes the
///     Gemini helper, and parses the AI response into a <see cref="ContentModerationResult" />.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class ContentModerationService : IContentModerationService
{
    private readonly IGeminiHelper _geminiHelper;

    public ContentModerationService(IGeminiHelper geminiHelper)
    {
        _geminiHelper = geminiHelper ?? throw new ArgumentNullException(nameof(geminiHelper));
    }

    /// <inheritdoc />
    public async Task<ContentModerationResult> AnalyzeContentAsync(string content)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(content))
                return new ContentModerationResult
                {
                    OffensiveScore = 0,
                    IsOffensive = false,
                    Reason = "Content is empty"
                };

            // Strip HTML tags to analyze plain text
            string plainText = StripHtmlTags(content);

            if (string.IsNullOrWhiteSpace(plainText))
                return new ContentModerationResult
                {
                    OffensiveScore = 0,
                    IsOffensive = false,
                    Reason = "No text content to analyze"
                };

            // Sanitize quotes to prevent breaking the prompt
            string sanitizedText = SanitizeQuotes(plainText);

            // Construct the moderation prompt
            string prompt = BuildModerationPrompt(sanitizedText);

            // Network operation: call Gemini AI
            LoggerUtilities.Info("[Start] Calling Gemini for content analysis");
            string aiResponse = await _geminiHelper.GenerateTextAsync(prompt);
            LoggerUtilities.Info("[End] Received response from Gemini");

            // Parse the AI response
            ContentModerationResult result = ParseAiResponse(aiResponse);

            return result;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                "[End] CRITICAL: Error analyzing content for offensive material. Content will be BLOCKED for safety.");

            // CHANGED: Fail-safe now BLOCKS content when AI analysis fails (more conservative approach)
            // This prevents offensive content from passing through when the AI service is down
            return new ContentModerationResult
            {
                OffensiveScore = 100,
                IsOffensive = true,
                Reason =
                    "Content moderation service is temporarily unavailable. Please try again later or contact support if this persists."
            };
        }
    }

    /// <summary>
    ///     Removes HTML tags and decodes HTML entities from the input, returning
    ///     normalized plain text suitable for moderation analysis.
    /// </summary>
    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Remove HTML tags
        string plainText = Regex.Replace(html, "<.*?>", " ");

        // Decode HTML entities
        plainText = WebUtility.HtmlDecode(plainText);

        // Remove extra whitespace
        plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

        return plainText;
    }

    /// <summary>
    ///     Sanitizes text by replacing problematic quote characters that could break
    ///     string interpolation or rendering. Replaces double quotes with single quotes
    ///     and normalizes other quote types.
    /// </summary>
    private string SanitizeQuotes(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Replace double quotes with single quotes
        text = text.Replace("\"", "'");

        // Replace other quote types with standard single quote
        text = text.Replace("\u2018", "'"); // Left single quotation mark
        text = text.Replace("\u2019", "'"); // Right single quotation mark
        text = text.Replace("\u201C", "'"); // Left double quotation mark
        text = text.Replace("\u201D", "'"); // Right double quotation mark
        text = text.Replace("\u0060", "'"); // Grave accent (backtick)

        return text;
    }

    /// <summary>
    ///     Constructs the moderation prompt sent to the AI. The prompt instructs
    ///     the model to evaluate the text for hate speech, harassment, sexual
    ///     content, threats, spam, profanity, and other relevant categories and
    ///     to return a JSON object with score, reason and categories.
    /// </summary>
    private string BuildModerationPrompt(string content)
    {
        return
            $$"""
              You are a highly accurate AI content moderator for a book discussion forum.
              Analyze the following user-generated text and determine whether it contains offensive, harmful, or inappropriate content.

              Content to analyze:
              "{{content}}"

              Evaluate carefully for:
              - Hate speech (racism, sexism, homophobia, religious intolerance, etc.)
              - Harassment or personal attacks
              - Violence or threats
              - Adult or sexual content
              - Spam or malicious links
              - Profanity, vulgarity, or disguised profanity
              - Bullying, insults, or demeaning language

              Important contextual instructions:
              - Consider intentional misspellings, substitutions, or censored forms of profanity (e.g., "fuk", "fuker", "btch", "sh1t", "phuck", "azzhole", "bi7ch") as equivalent to the original word.
              - Consider context carefully:
                * "This book is f***ing amazing!" → low severity (enthusiastic context)
                * "You’re a f***ing idiot" → high severity (personal attack)
                * "The villain was a complete asshole" → low severity (fictional context)
                * "You are an asshole" → high severity (direct insult)
              - Do not penalize literary discussions or fictional analysis that mention such words descriptively.

              Output strictly as a valid JSON object (no markdown, no extra text):
              {
                "score": <number from 0 to 100>,
                "reason": "<brief explanation>",
                "categories": ["<category1>", "<category2>"]
              }

              Scoring scale:
              - 0–20: Safe or neutral content.
              - 21–59: Mild language or low-context profanity.
              - 60–79: Offensive or inappropriate language, but not hate speech.
              - 80–100: Highly offensive, threatening, or hateful content.

              Example outputs:
              {"score": 15, "reason": "Contains mild profanity used positively in a review", "categories": ["profanity"]}
              {"score": 65, "reason": "Contains disguised strong profanity", "categories": ["profanity"]}
              {"score": 95, "reason": "Severe personal attack with vulgar language", "categories": ["personal attack", "profanity"]}
              """;
    }


    /// <summary>
    ///     Parses the AI's JSON output (or natural-language fallback) and returns
    ///     a <see cref="ContentModerationResult" /> populated with the score,
    ///     a reason, and detected categories. Falls back to naive numeric
    ///     extraction when the JSON parsing fails.
    /// </summary>
    private ContentModerationResult ParseAiResponse(string aiResponse)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(aiResponse)) throw new Exception("Empty AI response");

            // Clean the response - remove markdown code blocks if present
            string cleanedResponse = aiResponse.Trim();

            // Remove markdown code block markers
            if (cleanedResponse.StartsWith("```json"))
                cleanedResponse = cleanedResponse.Substring(7);
            else if (cleanedResponse.StartsWith("```"))
                cleanedResponse = cleanedResponse.Substring(3);

            if (cleanedResponse.EndsWith("```"))
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);

            cleanedResponse = cleanedResponse.Trim();

            // Parse JSON response
            JsonDocument jsonDoc = JsonDocument.Parse(cleanedResponse);
            JsonElement root = jsonDoc.RootElement;

            int score = root.GetProperty("score").GetInt32();
            string reason = root.GetProperty("reason").GetString() ?? "No reason provided";

            // Sanitize reason to prevent breaking rendering with quotes
            reason = SanitizeQuotes(reason);

            List<string> categories = [];
            if (root.TryGetProperty("categories", out JsonElement categoriesElement))
                foreach (JsonElement category in categoriesElement.EnumerateArray())
                {
                    string? categoryValue = category.GetString();
                    if (!string.IsNullOrEmpty(categoryValue))
                        categories.Add(categoryValue);
                }

            ContentModerationResult result = new()
            {
                OffensiveScore = Math.Clamp(score, 0, 100),
                IsOffensive = score > 60,
                Reason = reason,
                Categories = categories
            };


            return result;
        }
        catch (JsonException)
        {
            // Fallback: Try to extract score from natural language response
            Match scoreMatch = Regex.Match(aiResponse, @"\b(\d{1,3})\b");
            if (scoreMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out int extractedScore))
            {
                extractedScore = Math.Clamp(extractedScore, 0, 100);
                string fallbackReason = aiResponse.Length > 200 ? aiResponse.Substring(0, 200) + "..." : aiResponse;
                fallbackReason = SanitizeQuotes(fallbackReason);

                return new ContentModerationResult
                {
                    OffensiveScore = extractedScore,
                    IsOffensive = extractedScore > 60,
                    Reason = fallbackReason
                };
            }

            // If all parsing fails, throw to trigger fail-safe blocking
            throw new Exception("Unable to parse AI response");
        }
    }
}