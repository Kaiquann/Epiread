namespace EpiRead.ViewModels;
using EpiRead.Models.Database;

public class HomeVm
{
    public List<Book> TrendingBooks { get; set; } = new();
    public List<Book> PersonalizedBooks { get; set; } = new();

    public List<Book> SimilarBooks { get; set; } = new();

    // Additional homepage sections
    public List<Book> NewReleases { get; set; } = new();
    public List<Book> TopRated { get; set; } = new();

    public List<Book> PromoBooks { get; set; } = new();

    // Books most liked by users (based on PreferenceBooks count)
    public List<Book> MostLiked { get; set; } = new();
    // Optional: top preferred book for the current user (removed)
    // Aggregate summary for the personalized recommendations removed; Home shows recommendations only
    // (kept minimal) Home view model contains three lists: Trending, Personalized, Similar
}

// Helper types used by the recommendation pipeline. Kept small and serializable-friendly
public class UserSignals
{
    public List<BookHistory> Histories { get; set; } = new();
    public List<int> ViewedIds { get; set; } = new();
    public List<Rating> Ratings { get; set; } = new();
    public List<int> RatedIds { get; set; } = new();
    public Dictionary<int, int> RatingMap { get; set; } = new();
    public List<int> PreferenceIds { get; set; } = new();
    public List<string> SearchTerms { get; set; } = new();
    public List<int> DiscussionLikedBookIds { get; set; } = new();
    public bool HasSignals { get; set; } = false;

    public List<string> GetTokens()
    {
        var tokens = new List<string>();
        if (SearchTerms != null)
        {
            foreach (var s in SearchTerms)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                var parts = s.Split(new[] { ' ', ',', '.', '-', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var t = p.Trim();
                    if (t.Length >= 3) tokens.Add(t);
                }
            }
        }
        return tokens.Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
    }
}

public class ScoredBook
{
    public Book Book { get; set; } = null!;
    public int Score { get; set; }
}