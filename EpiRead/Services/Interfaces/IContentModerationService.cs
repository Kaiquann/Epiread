namespace EpiRead.Services.Interfaces;

/// <summary>
///     Provides methods for analyzing user-generated text content and identifying
///     whether it contains offensive or inappropriate material.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IContentModerationService
{
    /// <summary>
    ///     Analyze the provided <paramref name="content" /> and return a
    ///     <see cref="ContentModerationResult" /> describing the AI-generated
    ///     assessment (score, reason and categories).
    /// </summary>
    /// <param name="content">
    ///     The user-generated text to analyze.
    ///     This may include HTML; implementations should strip HTML before analysis.
    /// </param>
    /// <returns>
    ///     A task that returns a <see cref="ContentModerationResult" />
    ///     with the offensive score, whether it is considered offensive, reason and categories.
    /// </returns>
    Task<ContentModerationResult> AnalyzeContentAsync(string content);
}

/// <summary>
///     Represents the result of a content moderation analysis. This contains a
///     numerical score indicating severity and additional metadata such as a
///     descriptive reason and list of detected categories.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class ContentModerationResult
{
    /// <summary>
    ///     Offensive score on a scale from 0 to 100. Higher values indicate more
    ///     severe or offensive content.
    /// </summary>
    public int OffensiveScore { get; set; }

    /// <summary>
    ///     True when the content should be considered offensive based on the
    ///     offensive score and policy thresholds.
    /// </summary>
    public bool IsOffensive { get; set; }

    /// <summary>
    ///     A concise reason or explanation for the score, for example "Contains
    ///     severe personal attack" or "Mild profanity used positively in review".
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    ///     List of categories identified in the content (e.g., "profanity",
    ///     "hate speech", "personal attack"). Each category is a short label.
    /// </summary>
    public List<string> Categories { get; set; } = new();
}