using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

public class HomeController : Controller
{
    private readonly DB _db;
    private readonly IGeminiHelper _geminiHelper;
    private readonly ILogger<HomeController> _logger;

    public HomeController(DB db, IGeminiHelper geminiHelper, ILogger<HomeController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _geminiHelper = geminiHelper ?? throw new ArgumentNullException(nameof(geminiHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Access Denied page
    /// </summary>
    /// <returns></returns>
    public IActionResult AccessDenied()
    {
        return View();
    }

    // AI-driven trending endpoint: returns list of books that are "trending now" using the
    // candidate set from recent saves/reads and an AI ranking model.

    public async Task<IActionResult> Index()
    {
        LoggerUtilities.Info("[Start] Loading home page");
        // small helper type for deserializing Gemini output (moved to controller scope)

        User? user = await User.GetCurrentUser(_db);
        if (user != null)
            TempData["Username"] = user.UserName;

        HomeVm vm = new HomeVm();

        // Trending / Personalized / Similar sections are loaded on-demand by the client.
        // Leave vm.TrendingBooks / vm.PersonalizedBooks / vm.SimilarBooks empty so the page can load fast

        // New Releases: latest created books
        vm.NewReleases = await _db.Books
            .Where(b => b.Status == nameof(BookStatusType.Available))
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {vm.NewReleases.Count} new releases");


        // Top Rated: compute primarily by the number of ratings (most rated books)
        var ratingCounts = await _db.Books
            .Where(b => b.Status == nameof(BookStatusType.Available))
            .Select(b => new
            {
                b.Id,
                Count = _db.Ratings.Where(r => r.BookId == b.Id).Count(),
                b.UpdatedAt
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(8)
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {ratingCounts.Count} top rated books");

        if (ratingCounts.Any(x => x.Count > 0))
        {
            var topRatedIds = ratingCounts.Select(x => x.Id).ToList();
            var topRatedBooks = await _db.Books
                .Where(b => topRatedIds.Contains(b.Id) && b.Status == nameof(BookStatusType.Available)).ToListAsync();
            vm.TopRated = topRatedIds.Select(id => topRatedBooks.FirstOrDefault(b => b.Id == id)).Where(b => b != null)
                .Select(b => b!).ToList()!;
        }
        else
        {
            vm.TopRated = new List<Book>();
        }

        // Promo / Featured section: prefer highly-rated recent books (max rating >=4), fallback to recent updates
        var promoIds = await _db.Books
            .Where(b => b.Status == nameof(BookStatusType.Available))
            .Select(b => new
            {
                b.Id, Max = _db.Ratings.Where(r => r.BookId == b.Id).Select(r => (int?)r.StartCount).Max(), b.UpdatedAt
            })
            .Where(x => (x.Max ?? 0) >= 4)
            .OrderByDescending(x => x.Max ?? 0)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(3)
            .Select(x => x.Id)
            .ToListAsync();

        if (promoIds.Any())
        {
            var promoBooks = await _db.Books.Where(b => promoIds.Contains(b.Id)).ToListAsync();
            vm.PromoBooks = promoIds.Select(id => promoBooks.FirstOrDefault(b => b.Id == id)).Where(b => b != null)
                .Select(b => b!).ToList()!;
        }
        else
        {
            vm.PromoBooks = await _db.Books
                .Where(b => b.Status == nameof(BookStatusType.Available))
                .OrderByDescending(b => b.UpdatedAt)
                .Take(3)
                .ToListAsync();
        }

        // Most liked: using discussion likes aggregated per book (prefer books with most likes in discussion posts)
        var likedByDiscussion = await _db.DiscussionPosts
            .Where(dp => dp.Discussion != null)
            .Select(dp => new { dp.Discussion.BookId, Likes = dp.DiscussionLikeCounts.Count() })
            .GroupBy(x => x.BookId)
            .Select(g => new { BookId = g.Key, TotalLikes = g.Sum(x => x.Likes) })
            .OrderByDescending(x => x.TotalLikes)
            .Take(8)
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {likedByDiscussion.Count} most liked books");

        if (likedByDiscussion.Any(x => x.TotalLikes > 0))
        {
            var likedIds = likedByDiscussion.Select(x => x.BookId).ToList();
            var likedBooks = await _db.Books
                .Where(b => likedIds.Contains(b.Id) && b.Status == nameof(BookStatusType.Available)).ToListAsync();
            vm.MostLiked = likedIds.Select(id => likedBooks.FirstOrDefault(b => b.Id == id)).Where(b => b != null)
                .Select(b => b!).ToList()!;
        }
        else
        {
            vm.MostLiked = new List<Book>();
        }

        LoggerUtilities.Info("[End] Home page data loaded");
        return View(vm);
    }

    public async Task<IActionResult> GetRecommend()
    {
        LoggerUtilities.Info("[Start] Loading personalized recommendations");
        var vm = new HomeVm();
        var user = await User.GetCurrentUser(_db);

        if (user != null)
            TempData["Username"] = user.UserName;

        if (user == null)
        {
            vm.PersonalizedBooks = new List<Book>();
            return PartialView("_Recommend", vm);
        }

        // STEP 1: Load signals
        var signals = await LoadUserSignals(user.Id);
        LoggerUtilities.Info($"[Process] User signals loaded hasSignals={signals.HasSignals}");

        // STEP 2: If no signal → fallback
        if (!signals.HasSignals)
        {
            vm.PersonalizedBooks = new List<Book>();
            return PartialView("_Recommend", vm);
        }

        // STEP 3: Build candidates from signals
        var candidates = await BuildPersonalizedCandidates(signals);
        LoggerUtilities.Info($"[Process] Built {candidates.Count} candidate books");

        if (!candidates.Any())
        {
            vm.PersonalizedBooks = await GetFallbackRecommendations(user.Id, vm);
            return PartialView("_Recommend", vm);
        }

        // STEP 4: Apply scoring
        var scored = await ApplyScores(candidates, signals);

        // STEP 5: Sort + choose top 20
        var top = scored.OrderByDescending(x => x.Score).Take(20).Select(x => x.Book).ToList();

        // STEP 6: AI re-rank (optional)
        top = await TryAIRerank(top, signals);
        LoggerUtilities.Info("[Process] AI re-ranking completed");

        // STEP 7: High progress override
        top = await ApplyHighProgressLogic(top, user.Id, signals);

        vm.PersonalizedBooks = top.Take(5).ToList();

        LoggerUtilities.Info($"[End] Returning {vm.PersonalizedBooks.Count} personalized books");
        return PartialView("_Recommend", vm);
    }

    private async Task<UserSignals> LoadUserSignals(int userId)
    {
        LoggerUtilities.Info($"[Start] Loading user signals userId={userId}");
        var signals = new UserSignals();

        signals.Histories = await _db.BookHistories.Where(h => h.UserId == userId).ToListAsync();
        signals.ViewedIds = signals.Histories.Select(h => h.BookId).Distinct().ToList();
        signals.Ratings = await _db.Ratings.Where(r => r.UserId == userId).ToListAsync();
        signals.RatedIds = signals.Ratings.Select(r => r.BookId).ToList();
        signals.RatingMap = signals.Ratings.GroupBy(r => r.BookId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.StartCount));
        signals.PreferenceIds =
            await _db.PreferenceBooks.Where(p => p.UserId == userId).Select(p => p.BookId).ToListAsync();
        signals.SearchTerms = await _db.SearchHistories.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt).Select(s => s.Context).Take(10).ToListAsync();
        signals.DiscussionLikedBookIds = await _db.DiscussionLikeCounts
            .Where(l => l.UserId == userId)
            .Join(_db.DiscussionPosts, l => l.DiscussionPostId, p => p.Id, (l, p) => p)
            .Join(_db.Discussions, p => p.DiscussionId, d => d.Id, (p, d) => d.BookId)
            .Distinct().ToListAsync();
        LoggerUtilities.Info(
            $"[Process] Retrieved histories={signals.Histories.Count} ratings={signals.Ratings.Count}");

        signals.HasSignals =
            signals.RatedIds.Any() ||
            signals.PreferenceIds.Any() ||
            signals.ViewedIds.Any() ||
            signals.SearchTerms.Any(s => !string.IsNullOrWhiteSpace(s)) ||
            signals.Histories.Any(h => h.BookProgress > 0) ||
            signals.DiscussionLikedBookIds.Any();

        return signals;
    }

    private async Task<List<Book>> GetFallbackRecommendations(int userId, HomeVm vm)
    {
        LoggerUtilities.Info($"[Start] Getting fallback recommendations userId={userId}");
        var otherPrefIds = await _db.PreferenceBooks
            .Where(p => p.UserId != userId)
            .GroupBy(p => p.BookId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(10)
            .ToListAsync();

        var otherRatedIds = await _db.Ratings
            .Where(r => r.UserId != userId)
            .GroupBy(r => r.BookId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(10)
            .ToListAsync();
        LoggerUtilities.Info(
            $"[Process] Retrieved fallback candidates prefIds={otherPrefIds.Count} ratedIds={otherRatedIds.Count}");

        var exclusion = vm.TrendingBooks?.Select(b => b.Id).ToList() ?? new List<int>();

        var finalIds = otherPrefIds.Union(otherRatedIds)
            .Where(id => !exclusion.Contains(id))
            .Take(5)
            .ToList();

        return await _db.Books.Where(b => finalIds.Contains(b.Id)).ToListAsync();
    }

    private async Task<List<Book>> BuildPersonalizedCandidates(UserSignals s)
    {
        LoggerUtilities.Info("[Start] Building personalized candidates");
        var seedBookIds = s.PreferenceIds
            .Concat(s.ViewedIds)
            .Concat(s.RatedIds)
            .Concat(s.DiscussionLikedBookIds)
            .ToHashSet();

        // 1. Related categories
        var relatedCategoryIds = await _db.BookCategoryMaps
            .Where(m => s.RatedIds.Contains(m.BookId) || s.PreferenceIds.Contains(m.BookId))
            .Select(m => m.BookCategoryId)
            .Distinct()
            .ToListAsync();

        // 2. Authors
        var seedAuthors = await _db.Books
            .Where(b => s.RatedIds.Contains(b.Id) || s.PreferenceIds.Contains(b.Id))
            .Select(b => b.AuthorName)
            .Distinct()
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Found categories={relatedCategoryIds.Count} authors={seedAuthors.Count}");

        // 3. Token matches
        var tokens = s.GetTokens();

        // Build query
        IQueryable<Book> q = _db.Books.Where(b =>
            b.Status == nameof(BookStatusType.Available)
            && !seedBookIds.Contains(b.Id));

        var candidates =
            q.Where(b =>
                relatedCategoryIds.Any(cat =>
                    _db.BookCategoryMaps.Any(m => m.BookId == b.Id && m.BookCategoryId == cat)) ||
                seedAuthors.Contains(b.AuthorName) ||
                tokens.Any(t => (b.Title ?? "").Contains(t) || (b.Description ?? "").Contains(t))
            ).Distinct();

        return await candidates.Take(200).ToListAsync();
    }

    // AI re-ranker: ask the Gemini helper to re-score candidate books.
    // This method is defensive: it enforces a short timeout, cleans the response,
    // only accepts IDs already present in the candidate list, and falls back to
    // the original ordering when anything goes wrong.
    private async Task<List<Book>> TryAIRerank(List<Book> candidates, UserSignals signals)
    {
        if (candidates == null || !candidates.Any()) return candidates ?? new List<Book>();

        try
        {
            // Build a concise prompt including candidate ids and small metadata
            var sb = new StringBuilder();
            sb.AppendLine("You are a ranking model for book recommendations.");
            sb.AppendLine("You must follow these RULES exactly and ONLY return valid JSON, nothing else:");
            sb.AppendLine(
                "1) Output must be a JSON array of objects, where each object has an integer 'Id' and a numeric 'Score' between 0 and 1.");
            sb.AppendLine("   Example: [{ \"Id\": 123, \"Score\": 0.87 }, { \"Id\": 456, \"Score\": 0.83 }]");
            sb.AppendLine(
                "2) Return at most 20 items and ONLY return Ids that are present in the provided Candidate list.");
            sb.AppendLine(
                "3) Do not include any book IDs not in the Candidate list. Do not include text, explanation, or markup — only JSON.");
            sb.AppendLine("4) If you cannot rank the candidates, return an empty JSON array: []");
            sb.AppendLine(
                "5) Duplicates are not allowed — each Id should appear at most once. Order items by descending Score.");
            sb.AppendLine();

            sb.AppendLine("UserSignals:");
            sb.AppendLine(
                $"PreferredIds: {(signals.PreferenceIds != null ? string.Join(',', signals.PreferenceIds.Take(5)) : string.Empty)}");
            sb.AppendLine(
                $"RatedIds: {(signals.RatedIds != null ? string.Join(',', signals.RatedIds.Take(5)) : string.Empty)}");
            sb.AppendLine(
                $"Authors: {(signals.Histories != null ? string.Join(',', signals.Histories.Select(h => h.Book?.AuthorName).Where(a => !string.IsNullOrWhiteSpace(a)).Take(5)) : string.Empty)}");
            sb.AppendLine(
                $"SearchTerms: {(signals.SearchTerms != null ? string.Join(',', signals.SearchTerms.Take(5)) : string.Empty)}");
            sb.AppendLine();

            sb.AppendLine("Candidates (Id | Title | Desc truncated to 240 chars):");
            foreach (var b in candidates.Take(20))
            {
                var title = (b.Title ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
                var desc = (b.Description ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
                if (desc.Length > 240) desc = desc.Substring(0, 240);
                sb.AppendLine($"Id:{b.Id} | Title:{title} | Desc:{desc}");
            }

            var prompt = sb.ToString();

            // Defensive timeout: we wait for a short period for the model response
            LoggerUtilities.Info("[Start] AI re-ranking API call");
            var aiTask = _geminiHelper.GenerateTextAsync(prompt);
            var completed = await Task.WhenAny(aiTask, Task.Delay(3500)); // 3.5s timeout
            if (completed != aiTask)
            {
                LoggerUtilities.Warning("[Process] AI re-ranking timed out");
                return candidates;
            }

            var aiResp = aiTask.Result ?? string.Empty;
            LoggerUtilities.Info("[End] AI re-ranking API response received");
            if (string.IsNullOrWhiteSpace(aiResp))
                return candidates;

            string cleaned = CleanGeminiResponse(aiResp);
            if (string.IsNullOrWhiteSpace(cleaned)) return candidates;

            try
            {
                using var doc = JsonDocument.Parse(cleaned);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return candidates;

                var candidateIds = new HashSet<int>(candidates.Select(c => c.Id));
                var parsed = new List<(int Id, double Score)>();

                foreach (var el in doc.RootElement.EnumerateArray())
                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        if (el.TryGetProperty("Id", out var idProp) && idProp.TryGetInt32(out var id))
                        {
                            double score = 0.0;
                            if (el.TryGetProperty("Score", out var sProp) && sProp.TryGetDouble(out var sc)) score = sc;
                            parsed.Add((id, score));
                        }
                    }
                    else if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var idNum))
                    {
                        // if AI returned array of ids, treat equal score ordering
                        parsed.Add((idNum, 1.0));
                    }

                if (!parsed.Any()) return candidates;

                // Respect the RULES: remove any ids not in the candidate set, de-duplicate while preserving order,
                // sort by score desc and limit to 20 items max.
                var uniqueOrdered = new List<int>();
                foreach (var p in parsed.OrderByDescending(p => p.Score))
                {
                    if (uniqueOrdered.Count >= 20) break;
                    if (!candidateIds.Contains(p.Id)) continue;
                    if (uniqueOrdered.Contains(p.Id)) continue;
                    uniqueOrdered.Add(p.Id);
                }

                var orderedIds = uniqueOrdered;

                var reordered = orderedIds.Select(id => candidates.FirstOrDefault(c => c.Id == id))
                    .Where(b => b != null).Select(b => b!).ToList();
                var missing = candidates.Where(c => !reordered.Any(r => r!.Id == c.Id)).ToList();
                reordered.AddRange(missing);

                return reordered.ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "TryAIRerank: failed to parse AI response; falling back to heuristic");
                return candidates;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryAIRerank: AI re-rank failed unexpectedly");
            return candidates;
        }
    }

    // Small utility to clean common Gemini assistant artifacts and extract the JSON payload
    private static string CleanGeminiResponse(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return txt ?? string.Empty;

        // Remove any code fences (``` or ```json)
        txt = Regex.Replace(txt, "```.*?```", string.Empty, RegexOptions.Singleline);

        // If assistant wraps JSON in a block but still includes other text, try to isolate the JSON array
        int firstBracket = txt.IndexOf('[');
        int lastBracket = txt.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            txt = txt.Substring(firstBracket, lastBracket - firstBracket + 1);

        return txt.Trim();
    }

    // If the user has items with very high progress, prefer books from the same categories
    private async Task<List<Book>> ApplyHighProgressLogic(List<Book> top, int userId, UserSignals s)
    {
        var highProgressIds = s.Histories.Where(h => h.BookProgress > 80).Select(h => h.BookId).Distinct().ToList();
        if (!highProgressIds.Any()) return top;

        var relatedCatIds = await _db.BookCategoryMaps
            .Where(m => highProgressIds.Contains(m.BookId))
            .Select(m => m.BookCategoryId)
            .Distinct()
            .ToListAsync();

        if (!relatedCatIds.Any()) return top;

        var excludeIds = new HashSet<int>(top.Select(b => b.Id).Concat(s.PreferenceIds).Concat(s.RatedIds)
            .Concat(s.ViewedIds).Concat(s.DiscussionLikedBookIds).Concat(highProgressIds));

        var sameCategoryCandidates = await _db.Books
            .Where(b => b.Status == nameof(BookStatusType.Available)
                        && !excludeIds.Contains(b.Id)
                        && _db.BookCategoryMaps.Any(m => m.BookId == b.Id && relatedCatIds.Contains(m.BookCategoryId)))
            .OrderByDescending(b =>
                _db.BookHistories.Count(h => h.BookId == b.Id) + _db.Ratings.Count(r => r.BookId == b.Id))
            .Take(10)
            .ToListAsync();

        var final = top.ToList();
        foreach (var cand in sameCategoryCandidates)
        {
            if (final.Count >= top.Count) break;
            if (!final.Any(b => b.Id == cand.Id)) final.Insert(0, cand);
        }

        return final.Take(top.Count).ToList();
    }

    private async Task<List<ScoredBook>> ApplyScores(List<Book> candidates, UserSignals s)
    {
        var bookToCategories = await _db.BookCategoryMaps
            .Where(m => candidates.Select(c => c.Id).Contains(m.BookId))
            .GroupBy(m => m.BookId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.BookCategoryId).ToList());

        // Build seed info from user signals (categories & authors) so we can give category/author boosts
        var seedBookIds = s.PreferenceIds.Concat(s.RatedIds).Concat(s.ViewedIds).Concat(s.DiscussionLikedBookIds)
            .Distinct().ToList();
        var seedCategoryIds = await _db.BookCategoryMaps
            .Where(m => seedBookIds.Contains(m.BookId))
            .Select(m => m.BookCategoryId)
            .Distinct()
            .ToListAsync();

        var seedAuthors = await _db.Books
            .Where(b => seedBookIds.Contains(b.Id))
            .Select(b => b.AuthorName)
            .Distinct()
            .ToListAsync();

        // Highly-rated categories: categories where average rating across books is >= 4.0
        var highlyRatedCategoryIds = await _db.BookCategoryMaps
            .Join(_db.Ratings, bcm => bcm.BookId, r => r.BookId, (bcm, r) => new { bcm.BookCategoryId, r.StartCount })
            .GroupBy(x => x.BookCategoryId)
            .Select(g => new { CategoryId = g.Key, Avg = g.Average(x => x.StartCount) })
            .Where(x => x.Avg >= 4.0)
            .Select(x => x.CategoryId)
            .ToListAsync();

        var scores = new List<ScoredBook>();

        foreach (var b in candidates)
        {
            int score = 0;

            // Preference → strong explicit signal (keep existing +5 boost)
            if (s.PreferenceIds.Contains(b.Id)) score += 5;

            // ratingMap: add the user's saved rating value (e.g., 4 or 5) if present
            if (s.RatingMap.TryGetValue(b.Id, out var rate)) score += rate;

            // Related category match (seed categories from user): +5
            if (bookToCategories.TryGetValue(b.Id, out var cats) &&
                cats.Any(c => seedCategoryIds.Contains(c))) score += 5;

            // Highly-rated category match: +6
            if (bookToCategories.TryGetValue(b.Id, out var cats2) &&
                cats2.Any(c => highlyRatedCategoryIds.Contains(c))) score += 6;

            // Same author: +4
            if (!string.IsNullOrWhiteSpace(b.AuthorName) && seedAuthors.Any(a =>
                    !string.IsNullOrWhiteSpace(a) && a.Equals(b.AuthorName, StringComparison.OrdinalIgnoreCase)))
                score += 4;

            // Discussion liked: +3
            if (s.DiscussionLikedBookIds.Contains(b.Id)) score += 3;

            // Search term match (title or description): +2
            if (s.SearchTerms.Any(t =>
                    !string.IsNullOrWhiteSpace(t) &&
                    ((b.Title ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (b.Description ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))) score += 2;

            // History: baseline +2; if >50% progress add +3 extra
            var hist = s.Histories.FirstOrDefault(h => h.BookId == b.Id);
            if (hist != null)
            {
                score += 2;
                if (hist.BookProgress > 50) score += 3; // additional
            }

            scores.Add(new ScoredBook { Book = b, Score = score });
        }

        return scores;
    }

    public async Task<IActionResult> GetSimiliar()
    {
        LoggerUtilities.Info("[Start] Loading similar books");
        HomeVm vm = new HomeVm();
        // 3) Similar to user's preference/history (ONLY use history + preference as seed)
        vm.SimilarBooks = new List<Book>();

        User? user = await User.GetCurrentUser(_db);
        if (user != null)
            TempData["Username"] = user.UserName;

        if (user != null)
            try
            {
                var seedFromHistory = await _db.BookHistories.Where(h => h.UserId == user.Id).Select(h => h.BookId)
                    .ToListAsync();
                var seedFromPref = await _db.PreferenceBooks.Where(p => p.UserId == user.Id).Select(p => p.BookId)
                    .ToListAsync();
                var seedIds = seedFromHistory.Union(seedFromPref).Distinct().ToList();
                LoggerUtilities.Info($"[Process] Found {seedIds.Count} seed books from history and preferences");

                List<Book> similar = new List<Book>();

                if (seedIds.Any())
                {
                    var relatedCategoryIds = await _db.BookCategoryMaps
                        .Where(m => seedIds.Contains(m.BookId))
                        .Select(m => m.BookCategoryId)
                        .Distinct()
                        .ToListAsync();

                    similar = new List<Book>();
                    if (relatedCategoryIds.Any())
                        similar = await _db.Books
                            .Where(b => b.Status == nameof(BookStatusType.Available)
                                        && !seedIds.Contains(b.Id)
                                        && _db.BookCategoryMaps.Any(m =>
                                            m.BookId == b.Id && relatedCategoryIds.Contains(m.BookCategoryId)))
                            // Popularity tie-breaker: recent reads + rating count
                            .OrderByDescending(b =>
                                _db.BookHistories.Count(h => h.BookId == b.Id) +
                                _db.Ratings.Count(r => r.BookId == b.Id))
                            .Take(8)
                            .ToListAsync();

                    // fallback: match by same author as any seed book
                    if (!similar.Any())
                    {
                        var seedAuthors = await _db.Books.Where(b => seedIds.Contains(b.Id)).Select(b => b.AuthorName)
                            .Distinct().ToListAsync();
                        if (seedAuthors.Any())
                            // Author fallback: exclude seed books and prefer popular ones
                            similar = await _db.Books
                                .Where(b => b.Status == nameof(BookStatusType.Available) && !seedIds.Contains(b.Id) &&
                                            seedAuthors.Contains(b.AuthorName))
                                .OrderByDescending(b =>
                                    _db.BookHistories.Count(h => h.BookId == b.Id) +
                                    _db.Ratings.Count(r => r.BookId == b.Id))
                                .Take(8)
                                .ToListAsync();
                    }
                }

                // assign the similar list (possibly empty) back to the view model
                vm.SimilarBooks = similar;
                LoggerUtilities.Info($"[Process] Retrieved {vm.SimilarBooks.Count} similar books");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building similar-from-history recommendations");
            }

        LoggerUtilities.Info("[End] Similar books loaded");
        return PartialView("_Similar", vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetTrending()
    {
        LoggerUtilities.Info("[Start] Loading trending books");
        var list = await BuildTrendingBooksAsync();
        var vm = new HomeVm { TrendingBooks = list };
        LoggerUtilities.Info($"[End] Returning {vm.TrendingBooks.Count} trending books");
        return PartialView("_Trending", vm);
    }

    private async Task<List<Book>> BuildTrendingBooksAsync(int take = 12)
    {
        var topPref = await _db.PreferenceBooks
            .GroupBy(p => p.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync();

        var topIds = topPref.Select(x => x.BookId).ToList();
        var trending = await _db.Books
            .Where(b => topIds.Contains(b.Id) && b.Status == nameof(BookStatusType.Available))
            .Take(take)
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {trending.Count} trending books");

        if (trending.Count < take)
        {
            var byPopularity = await _db.Books
                .Where(b => b.Status == nameof(BookStatusType.Available))
                .OrderByDescending(b =>
                    _db.BookHistories.Count(h => h.BookId == b.Id) + _db.Ratings.Count(r => r.BookId == b.Id))
                .Take(take)
                .ToListAsync();

            trending = trending.Union(byPopularity).Take(take).ToList();
        }

        return trending;
    }

    public IActionResult Aboutus()
    {
        return View();
    }
}