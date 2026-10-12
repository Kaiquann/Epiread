using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

public class BookController : Controller
{
    private readonly IBookService _bookService;
    private readonly DB _db;
    private readonly IGeminiHelper _geminiHelper;

    public BookController(DB db, IBookService bookService, IGeminiHelper geminiHelper)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
        _geminiHelper = geminiHelper ?? throw new ArgumentNullException(nameof(geminiHelper));
    }

    public async Task<IActionResult> Index(int? CategoryId)
    {
        LoggerUtilities.Info($"[Start] Loading book index CategoryId={CategoryId}");
        User? currentUser = await User.GetCurrentUser(_db);

        List<BookCategory> allCategories = await _db.BookCategories
            .Include(c => c.BookCategoryMaps.Where(map => map.Book.Status == nameof(BookStatusType.Available)))
            .ThenInclude(map => map.Book)
            .AsNoTracking()
            .ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {allCategories.Count} categories");

        // Compute system-wide preference counts per book (only PreferenceBooks determine top categories)
        var prefCounts = await _db.PreferenceBooks
            .GroupBy(pb => pb.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BookId, x => x.Count);

        // For each top-level (parent) category, include itself and all descendants when summing preferences
        var parentCategories = allCategories.Where(c => c.ParentId == null).ToList();
        var parentTotals = new List<(BookCategory Parent, int Total)>();

        foreach (var parent in parentCategories)
        {
            var catIds = new List<int> { parent.Id };
            AddAllChildren(allCategories, catIds, parent.Id); // Recursively add all child category ids

            int total = allCategories
                .Where(cat => catIds.Contains(cat.Id))
                .SelectMany(cat => cat.BookCategoryMaps ?? new List<BookCategoryMap>())
                .Sum(map => prefCounts.ContainsKey(map.BookId) ? prefCounts[map.BookId] : 0);

            if (total > 0) parentTotals.Add((parent, total));
        }

        var topCategories = parentTotals
            .OrderByDescending(x => x.Total)
            .Take(5)
            .Select(x => x.Parent)
            .ToList();

        Console.WriteLine("============================PARENT CATEGORIES (by PreferenceBooks)=====================");
        foreach (var p in parentTotals.OrderByDescending(x => x.Total).Take(5))
            Console.WriteLine($"Parent: {p.Parent.Name}, Score: {p.Total}");

        // How to use the List<BookCategory> topCategories in your view:
        Console.WriteLine(
            "============================FINAL LIST FOR VIEW (BookCategory Objects)=====================");
        foreach (var category in topCategories)
            Console.WriteLine($"Category Name: {category.Name}, ID: {category.Id}, ParentId: {category.ParentId}");
        // Your view can now bind to category.Name, category.Id, etc.
        // Always show all categories by default
        List<BookCategory> filteredCategories = allCategories;

        // If a category is selected → filter by it
        if (CategoryId != null)
            filteredCategories = allCategories
                .Where(c => c.Id == CategoryId || c.ParentId == CategoryId)
                .OrderBy(c => c.ParentId)
                .ToList();

        List<BookCategory>? topBookCategory = topCategories.Any() ? topCategories : null;

        BookVm bookVm = new BookVm
        {
            BookCategory = filteredCategories,
            TopBookCategory = topBookCategory,
            AuthorList = await _db.Users
                .Where(u => u.Role == nameof(UserRoleType.Author) && u.Status == nameof(UserStatusType.Active))
                .AsNoTracking()
                .ToListAsync(),
            SearchHistory = currentUser != null
                ? await _db.SearchHistories
                    .Where(sh => sh.UserId == currentUser.Id)
                    .AsNoTracking()
                    .ToListAsync()
                : new List<SearchHistory>()
        };

        LoggerUtilities.Info("[End] Book index loaded");
        if (Request.IsAjax())
            return PartialView("_Index", bookVm);

        return View(bookVm);
    }


    [HttpGet]
    public async Task<IActionResult> FilterBookList(List<string> authorNames, List<int> categoryIds,
        int? publicationYear)
    {
        bool isFilterEmpty = (authorNames == null || !authorNames.Any()) &&
                             (categoryIds == null || !categoryIds.Any()) &&
                             !publicationYear.HasValue;

        // If no filters are applied, return the default view logic (by category)
        if (isFilterEmpty)
        {
            List<BookCategory> allCategories = await _db.BookCategories
                .Include(c => c.BookCategoryMaps.Where(map => map.Book.Status == nameof(BookStatusType.Available)))
                .ThenInclude(map => map.Book)
                .AsNoTracking()
                .ToListAsync();

            var vm = new BookVm
            {
                BookCategory = allCategories,
                RelatedBooks = null
            };

            return PartialView("_Index", vm);
        }

        var books = _db.Books
            .Include(b => b.BookCategoryMaps)
            .AsQueryable();

        if (authorNames != null && authorNames.Any())
            books = books.Where(b =>
                authorNames.Contains(b.AuthorName) && b.Status == nameof(BookStatusType.Available));

        if (categoryIds != null && categoryIds.Any())
        {
            var allCategories = await _db.BookCategories.ToListAsync();
            var allCategoryIdsToFilter = new List<int>();

            foreach (var selectedId in categoryIds)
            {
                if (!allCategoryIdsToFilter.Contains(selectedId)) allCategoryIdsToFilter.Add(selectedId);
                AddAllChildren(allCategories, allCategoryIdsToFilter, selectedId);
            }

            books = books.Where(b =>
                b.BookCategoryMaps.Any(m => allCategoryIdsToFilter.Contains(m.BookCategoryId)) &&
                b.Status == nameof(BookStatusType.Available));
        }

        if (publicationYear.HasValue)
            books = books.Where(b =>
                b.PublicationYear.HasValue && b.PublicationYear.Value.Year == publicationYear.Value &&
                b.Status == nameof(BookStatusType.Available));

        var filteredBooks = await books.ToListAsync();

        var searchVm = new BookVm
        {
            RelatedBooks = filteredBooks,
            BookCategory = null // Intentionally null/empty to force "Search Results" view
        };

        return PartialView("_Index", searchVm);
    }

    private void AddAllChildren(List<BookCategory> allCategories, List<int> allCategoryIdsToFilter, int parentId)
    {
        var children = allCategories.Where(c => c.ParentId == parentId).ToList();

        foreach (var child in children)
        {
            if (!allCategoryIdsToFilter.Contains(child.Id)) allCategoryIdsToFilter.Add(child.Id);
            AddAllChildren(allCategories, allCategoryIdsToFilter, child.Id);
        }
    }


    public async Task<IActionResult> Search(string? search)
    {
        LoggerUtilities.Info($"[Start] Book search query={search}");
        User? currentUser = await User.GetCurrentUser(_db);

        List<BookCategory> bookCategories = new();
        List<Book>? books = new();

        if (!string.IsNullOrWhiteSpace(search))
        {
            books = await _db.Books
                .Where(b => (b.Title.ToLower().Contains(search.ToLower()) ||
                             b.AuthorName.ToLower().Contains(search.ToLower()) ||
                             b.Description.ToLower().Contains(search.ToLower())
                             || b.PublicationYear.ToString().Contains(search.ToLower())) &&
                            b.Status == nameof(BookStatusType.Available))
                .AsNoTracking()
                .ToListAsync();
            LoggerUtilities.Info($"[Process] Found {books.Count} books matching search");

            if (currentUser != null)
            {
                var existingSearch = await _db.SearchHistories
                    .FirstOrDefaultAsync(s => s.UserId == currentUser.Id && s.Context.ToLower() == search.ToLower());

                if (existingSearch == null)
                {
                    _db.SearchHistories.Add(new SearchHistory
                    {
                        UserId = currentUser.Id,
                        Context = search,
                        IsSaved = false,
                        CreatedAt = DateTime.Now.ToLocalTime(),
                        UpdatedAt = DateTime.Now.ToLocalTime()
                    });
                    await _db.SaveChangesAsync();
                    LoggerUtilities.Info("[Process] Search history saved");
                }
            }
        }

        BookVm bookVm = new BookVm
        {
            BookCategory = bookCategories,
            RelatedBooks = books,
            AuthorList = await _db.Users
                .Where(u => u.Role == nameof(UserRoleType.Author))
                .AsNoTracking()
                .ToListAsync(),
            SearchHistory = currentUser != null
                ? await _db.SearchHistories
                    .Where(sh => sh.UserId == currentUser.Id)
                    .AsNoTracking()
                    .ToListAsync()
                : new List<SearchHistory>()
        };
        LoggerUtilities.Info("[End] Search results prepared");
        if (Request.IsAjax())
            return PartialView("_Index", bookVm);

        return RedirectToAction("Index", bookVm);
    }

    [HttpGet]
    public async Task<IActionResult> GetBookSuggestions(string term)
    {
        term = term?.Trim();

        // 1️⃣ If user did not type anything → show trending books
        if (string.IsNullOrWhiteSpace(term))
        {
            // Get top book ids by preference count (translated to SQL)
            var topPrefIds = await _db.PreferenceBooks
                .GroupBy(bp => bp.BookId)
                .Select(g => new { BookId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Select(x => x.BookId)
                .ToListAsync();

            var trending = await _db.Books
                .Where(b => topPrefIds.Contains(b.Id) && b.Status == nameof(BookStatusType.Available))
                .Select(b => b.Title)
                .AsNoTracking()
                .ToListAsync();

            return Json(trending);
        }

        term = term.ToLower();

        // 2️⃣ Basic DB search
        var dbMatches = _db.Books
            .Where(b => b.Title.ToLower().Contains(term) ||
                        b.AuthorName.ToLower().Contains(term) ||
                        (b.Description.ToLower().Contains(term)
                         && b.Status == nameof(BookStatusType.Available)))
            .ToList();

        // 3️⃣ Use Google Gemini to rank candidate books (fallback to DB search results)
        // Prepare a limited candidate list to keep prompt size reasonable
        var candidates = dbMatches.Take(30).ToList();

        List<string> semanticMatches = new();

        if (candidates.Any())
        {
            // Build a concise prompt listing candidates
            var sb = new StringBuilder();
            sb.AppendLine($"User query: \"{term}\"\n");
            sb.AppendLine("Here are candidate books. Each entry has Title, Author and Description.\n");

            int idx = 1;
            foreach (var b in candidates)
            {
                string title = b.Title?.Replace('\n', ' ').Replace('\r', ' ') ?? string.Empty;
                string author = b.AuthorName?.Replace('\n', ' ').Replace('\r', ' ') ?? string.Empty;
                string desc = b.Description?.Replace('\n', ' ').Replace('\r', ' ') ?? string.Empty;

                sb.AppendLine($"{idx}) Title: {title}");
                sb.AppendLine($"   Author: {author}");
                sb.AppendLine($"   Description: {desc}\n");
                idx++;
            }

            sb.AppendLine(
                "Please return a JSON array (e.g. [\"Title1\", \"Title2\"]) of up to 10 book titles most relevant to the user query, ordered most relevant first. Only return the JSON array, nothing else.");

            string prompt = sb.ToString();

            try
            {
                string response = await _geminiHelper.GenerateTextAsync(prompt);

                // Pre-clean common assistant artifacts (triple-backticks and code fences),
                // then try to extract a JSON array between the first '[' and the last ']' if present.
                string CleanGeminiResponse(string txt)
                {
                    if (string.IsNullOrWhiteSpace(txt)) return txt ?? string.Empty;

                    // Remove only the code-fence markers (keep inner content). Examples to handle:
                    // ```json\n["Title1","Title2"]\n```  -> we want the array preserved
                    txt = Regex.Replace(txt, "```\\s*json\\s*", string.Empty, RegexOptions.IgnoreCase);
                    txt = txt.Replace("```", string.Empty);

                    // If assistant wraps JSON in a block but still includes other text, try to isolate the JSON array
                    int firstBracket = txt.IndexOf('[');
                    int lastBracket = txt.LastIndexOf(']');
                    if (firstBracket >= 0 && lastBracket > firstBracket)
                        txt = txt.Substring(firstBracket, lastBracket - firstBracket + 1);

                    return txt.Trim();
                }

                string cleanedResponse = CleanGeminiResponse(response);

                // Try to parse as JSON array of strings first
                try
                {
                    using var doc = JsonDocument.Parse(cleanedResponse);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var el in doc.RootElement.EnumerateArray())
                            if (el.ValueKind == JsonValueKind.String)
                                semanticMatches.Add(el.GetString() ?? string.Empty);
                }
                catch (JsonException)
                {
                    // Fallback: extract quoted strings or numbered list lines
                    var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .ToList();

                    // Try to find lines that look like '1) Title: ...' or '1. Title - Author'
                    foreach (var line in lines)
                    {
                        // match patterns like: 1) Title: The Title
                        var idxColon = line.IndexOf("Title:", StringComparison.OrdinalIgnoreCase);
                        if (idxColon >= 0)
                        {
                            var val = line.Substring(idxColon + "Title:".Length).Trim();
                            if (!string.IsNullOrEmpty(val)) semanticMatches.Add(val);
                            continue;
                        }

                        // match quoted strings
                        int firstQuote = line.IndexOf('"');
                        int lastQuote = line.LastIndexOf('"');
                        if (firstQuote >= 0 && lastQuote > firstQuote)
                        {
                            var val = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1).Trim();
                            if (!string.IsNullOrEmpty(val)) semanticMatches.Add(val);
                            continue;
                        }

                        // match '1. Title - Author' -> take part before ' - '
                        var parts = line.Split('-', 2);
                        if (parts.Length > 0)
                        {
                            var maybeTitle = parts[0];
                            // remove leading numbering
                            maybeTitle = Regex.Replace(maybeTitle, "^\\s*\\d+\\s*[).:-]?\\s*", "");
                            if (!string.IsNullOrEmpty(maybeTitle)) semanticMatches.Add(maybeTitle.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If Gemini fails, log and fall back to basic DB title list
                Console.WriteLine($"Gemini ranking error: {ex.Message}");
            }

            // If Gemini produced no result, fall back to simple DB search titles
            if (!semanticMatches.Any())
                semanticMatches = candidates.Select(b => b.Title).Where(t => !string.IsNullOrEmpty(t)).Take(10)
                    .ToList();
            else
                // Trim and remove very short or purely punctuational tokens introduced by imperfect LLM output
                semanticMatches = semanticMatches
                    .Select(s => s?.Trim() ?? string.Empty)
                    .Where(t => !string.IsNullOrEmpty(t) && t.Length > 1 && !Regex.IsMatch(t, @"^[\[\]`]+$"))
                    .Take(10)
                    .ToList();
        }

        // 6️⃣ Merge with trending books (that contain the term)
        // 6️⃣ Merge with trending books (that contain the term)
        var topPref = await _db.PreferenceBooks
            .GroupBy(bp => bp.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(50)
            .ToListAsync();

        var topIds = topPref.Select(x => x.BookId).ToList();

        var trendingBooksCandidates = await _db.Books
            .Where(b => topIds.Contains(b.Id) && b.Title.ToLower().Contains(term))
            .ToListAsync();

        // order by preference count using the in-memory list
        var prefDict = topPref.ToDictionary(x => x.BookId, x => x.Count);
        var trendingBooks = trendingBooksCandidates
            .OrderByDescending(b => prefDict.ContainsKey(b.Id) ? prefDict[b.Id] : 0)
            .Select(b => b.Title)
            .Take(3)
            .ToList();

        var finalList = trendingBooks
            .Union(semanticMatches)
            .Distinct()
            .ToList();

        return Json(finalList);
    }

    // Cosine similarity helper
    private double CosineSimilarity(IList<double> vectorA, IList<double> vectorB)
    {
        double dot = 0.0, magA = 0.0, magB = 0.0;
        for (int i = 0; i < vectorA.Count; i++)
        {
            dot += vectorA[i] * vectorB[i];
            magA += vectorA[i] * vectorA[i];
            magB += vectorB[i] * vectorB[i];
        }

        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }


    public async Task<IActionResult> BookDetails(int id)
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = "Invalid Id";
            return RedirectToAction("Index");
        }

        Book? book = await _db.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(rc => rc.Id == id && rc.Status == nameof(BookStatusType.Available));

        if (book == null)
        {
            TempData[SweetAlert.Danger] = "No Book found";
            return RedirectToAction("Index");
        }

        var bookCategoryId = await _db.BookCategoryMaps
            .AsNoTracking()
            .Where(bcm => bcm.BookId == id)
            .Select(bcm => bcm.BookCategoryId)
            .ToListAsync();

        bool isPreferred = false;
        bool hasReadBook = false;
        Rating? userExistingRating = null;

        if (currentUser != null)
        {
            isPreferred = await _db.PreferenceBooks
                .AnyAsync(p => p.UserId == currentUser.Id && p.BookId == id);

            hasReadBook = await _db.BookHistories
                .AnyAsync(h => h.BookId == id && h.UserId == currentUser.Id);

            userExistingRating = await _db.Ratings
                .FirstOrDefaultAsync(r => r.BookId == id && r.UserId == currentUser.Id);
        }

        var bookRatings = await _db.Ratings
            .Where(r => r.BookId == id)
            .ToListAsync();

        double averageRating = bookRatings.Any() ? bookRatings.Average(r => r.StartCount) : 0.0;

        BookVm bookVm = new BookVm
        {
            Id = id,
            AuthorName = book.AuthorName,
            Title = book.Title,
            Description = book.Description,
            PublicationYear = book.PublicationYear,
            LanguageCode = book.LanguageCode,
            Status = book.Status,
            CoverImageLink = book.CoverImageLink,
            IsPreferred = isPreferred,
            RelatedBooks = await _db.Books
                .Where(b => b.Id != id && b.Status == nameof(BookStatusType.Available))
                .Where(b => _db.BookCategoryMaps
                    .Any(bcm => bcm.BookId == b.Id && bookCategoryId.Contains(bcm.BookCategoryId)))
                .AsNoTracking()
                .ToListAsync(),
            DocumentList = await _db.BookDocuments
                .Where(bd => bd.BookId == id)
                .AsNoTracking()
                .ToListAsync(),
            CanRate = hasReadBook,
            AverageRating = averageRating,
            UserRatingValue = userExistingRating?.StartCount
        };

        return View(bookVm);
    }


    [HttpPost]
    public async Task<IActionResult> SubmitRating(int bookId, int ratingValue)
    {
        LoggerUtilities.Info($"[Start] Submit rating bookId={bookId} value={ratingValue}");
        var currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        // Check if user is allowed (must exist in BookHistory)
        bool hasReadBook = await _db.BookHistories
            .AnyAsync(h => h.BookId == bookId && h.UserId == currentUser.Id);

        if (!hasReadBook)
            return Forbid("You can only rate books you have read.");

        // Check if rating already exists
        var existingRating = await _db.Ratings
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == currentUser.Id);
        LoggerUtilities.Info($"[Process] Existing rating found={existingRating != null}");

        if (existingRating != null)
        {
            existingRating.StartCount = ratingValue;
            _db.Ratings.Update(existingRating);
        }
        else
        {
            _db.Ratings.Add(new Rating
            {
                BookId = bookId,
                UserId = currentUser.Id,
                StartCount = ratingValue,
                CreatedAt = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[End] Rating saved successfully");

        TempData[SweetAlert.Success] = "Rate successfull";
        return RedirectToAction("BookDetails", new { id = bookId });
    }

    public async Task<IActionResult> PreferenceBook()
    {
        LoggerUtilities.Info("[Start] Loading preference books");
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null) return Unauthorized();

        List<PreferenceBook> preferenceBook = await _db.PreferenceBooks
            .Include(p => p.Book)
            .Where(p => p.UserId == currentUser.Id)
            .AsNoTracking()
            .ToListAsync();
        LoggerUtilities.Info($"[End] Retrieved {preferenceBook.Count} preference books");

        return View(preferenceBook);
    }


    [HttpPost]
    public async Task<IActionResult> preferenceBookAction(int bookId, bool preferencePage)
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null) return Unauthorized();

        PreferenceBook? existing = await _db.PreferenceBooks
            .FirstOrDefaultAsync(p => p.UserId == currentUser.Id && p.BookId == bookId);

        // Perform add/remove

        if (existing != null)
        {
            _db.PreferenceBooks.Remove(existing);
            await _db.SaveChangesAsync();

            if (!Request.IsAjax()) TempData[SweetAlert.Success] = "Book removed from your preferences.";
        }
        else
        {
            PreferenceBook newPreference = new PreferenceBook
            {
                BookId = bookId,
                UserId = currentUser.Id,
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            };

            _db.PreferenceBooks.Add(newPreference);
            await _db.SaveChangesAsync();

            if (!Request.IsAjax()) TempData[SweetAlert.Success] = "Book added to your preferences.";
        }

        // Reload the list AFTER the change so the partial gets up-to-date data
        List<PreferenceBook> preferenceBook = await _db.PreferenceBooks
            .Include(p => p.Book)
            .Where(p => p.UserId == currentUser.Id)
            .ToListAsync();

        if (Request.IsAjax()) return PartialView("_PreferenceBookList", preferenceBook);

        return RedirectToAction("BookDetails", new { id = bookId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSearchHistory(int id)
    {
        var history = await _db.SearchHistories.FindAsync(id);
        if (history != null)
        {
            _db.SearchHistories.Remove(history);
            await _db.SaveChangesAsync();
        }

        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();
        BookVm bookVm = new BookVm
        {
            SearchHistory = await _db.SearchHistories
                .Where(s => s.UserId == currentUser.Id)
                .ToListAsync()
        };

        if (Request.IsAjax()) return PartialView("_SearchBar", bookVm);

        return RedirectToAction("Index");
    }


    [HttpPost]
    public async Task<IActionResult> ToggleSavedSearch(int id)
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();
        var search = await _db.SearchHistories.FirstOrDefaultAsync(s => s.SearchHistoryId == id);
        if (search != null)
        {
            search.IsSaved = !search.IsSaved;
            await _db.SaveChangesAsync();
        }

        BookVm bookVm = new BookVm
        {
            SearchHistory = await _db.SearchHistories
                .Where(s => s.UserId == currentUser.Id)
                .ToListAsync()
        };

        if (Request.IsAjax()) return PartialView("_SearchBar", bookVm);

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> GetSearchBar()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        var vm = new BookVm
        {
            SearchHistory = await _db.SearchHistories
                .Where(sh => sh.UserId == currentUser.Id)
                .ToListAsync()
        };
        return PartialView("_SearchBar", vm);
    }


    [HttpPost]
    public async Task<IActionResult> ReadBook(int documentId, string documentFormat, int? currentPage)
    {
        var currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        var bookDocument = await _db.BookDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (bookDocument == null)
        {
            TempData[SweetAlert.Danger] = "No book document found.";
            return RedirectToAction("Index");
        }

        // Ensure BookHistory exists or create new
        var existingHistory = await _db.BookHistories
            .FirstOrDefaultAsync(bh => bh.UserId == currentUser.Id
                                       && bh.BookId == bookDocument.BookId
                                       && bh.BookDocumentId == documentId);

        if (existingHistory == null)
        {
            _db.BookHistories.Add(new BookHistory
            {
                UserId = currentUser.Id,
                BookId = bookDocument.BookId,
                BookDocumentId = bookDocument.Id,
                BookProgress = 0,
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            });
            await _db.SaveChangesAsync();
        }

        // Normalize document format
        var format = documentFormat?.ToLowerInvariant();
        if (string.IsNullOrEmpty(format))
        {
            TempData[SweetAlert.Danger] = "Invalid document format.";
            return RedirectToAction("Index");
        }

        string? localFilePath = null;
        string? error = null;
        string? fileUrl = null;

        // --- File Retrieval Based on Format ---
        switch (format)
        {
            case "epub":
                (localFilePath, error) = await _bookService.GetOrDownloadEpubForPreviewAsync(bookDocument);
                break;

            case "pdf":
                if (Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant() != ".pdf")
                {
                    TempData[SweetAlert.Danger] = "PDF file not found.";
                    return RedirectToAction("Index");
                }

                (localFilePath, error) = await _bookService.GetOrDownloadPdfForPreviewAsync(bookDocument);
                break;

            default:
                TempData[SweetAlert.Danger] = "Unsupported document format.";
                return RedirectToAction("Index");
        }

        // --- Handle File Retrieval Errors ---
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(localFilePath))
        {
            TempData[SweetAlert.Danger] = error ?? "Failed to retrieve file.";
            return RedirectToAction("Index");
        }

        // Convert local file path to web URL
        string fileName = Path.GetFileName(localFilePath);
        fileUrl = Url.Content($"~/uploads/ebooks/{fileName}");

        // --- Common View Data ---
        ViewBag.ReaderType = format;
        ViewBag.documentId = bookDocument.Id;
        // expose user preferred language for translate toolbar defaults
        ViewBag.UserLanguage = currentUser.Language ?? nameof(LanguageCodeType.EN);
        ViewBag.bookId = bookDocument.BookId;
        ViewBag.currentPage = currentPage ?? 0;

        if (format == "epub")
            ViewBag.EpubFileUrl = fileUrl;
        else if (format == "pdf")
            ViewBag.PdfFileUrl = fileUrl;

        return View("ReadBook");
    }


    [HttpPost]
    public async Task<IActionResult> UpdateBookProgress(int documentId, int currentPage, int totalPages)
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (totalPages <= 0)
            return BadRequest("Invalid totalPages value.");

        var history = await _db.BookHistories
            .FirstOrDefaultAsync(h => h.UserId == currentUser.Id && h.BookDocumentId == documentId);

        if (history == null)
            return NotFound();

        int progress = (int)Math.Round((double)currentPage / totalPages * 100);
        progress = Math.Clamp(progress, 0, 100); // 👈 Prevents overflow or negative progress

        history.BookProgress = progress;
        history.CurrentPage = currentPage;
        history.TotalPages = totalPages;
        history.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();

        return Json(new { success = true, progress });
    }

    public IActionResult Unauthorized()
    {
        TempData[SweetAlert.Danger] = "You need to login first.";
        return RedirectToAction("Index", "login");
    }

    public async Task<IActionResult> BookHistory()
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null) return Unauthorized();

        List<BookHistory> bookHistories = await _db.BookHistories
            .AsNoTracking()
            .Include(bh => bh.Book)
            .Include(bh => bh.BookDocument)
            .ThenInclude(Bookmark => Bookmark.Bookmarks)
            .Where(bh => bh.UserId == currentUser.Id)
            .ToListAsync();

        BookHistoryVm vm = new BookHistoryVm
        {
            BookHistoryList = bookHistories,
            DocumentList = await _db.BookDocuments.AsNoTracking().ToListAsync()
        };
        // Include the current user's id for view-level checks
        vm.CurrentUserId = currentUser.Id;

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveBookHistory(int id)
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null) return Unauthorized();

        var history = await _db.BookHistories.FirstOrDefaultAsync(bh => bh.Id == id);
        if (history == null)
        {
            TempData[SweetAlert.Danger] = "History not found";
            return RedirectToAction("BookHistory");
        }

        // Authorization check: verify the current user owns this history record
        if (history.UserId != currentUser.Id)
        {
            TempData[SweetAlert.Danger] = "You do not have permission to delete this history";
            return RedirectToAction("BookHistory");
        }

        var bookmarks = await _db.Bookmarks
            .Where(bm => bm.UserId == history.UserId && bm.BookDocumentId == history.BookDocumentId)
            .ToListAsync();

        _db.BookHistories.Remove(history);
        _db.Bookmarks.RemoveRange(bookmarks);
        await _db.SaveChangesAsync();

        List<BookHistory> bookHistories = await _db.BookHistories
            .AsNoTracking()
            .Include(bh => bh.Book)
            .Include(bh => bh.BookDocument)
            .ThenInclude(Bookmark => Bookmark.Bookmarks)
            .Where(bh => bh.UserId == currentUser.Id)
            .ToListAsync();

        BookHistoryVm vm = new BookHistoryVm
        {
            BookHistoryList = bookHistories,
            DocumentList = await _db.BookDocuments.AsNoTracking().ToListAsync()
        };


        if (Request.IsAjax()) return PartialView("_BookHistoryList", vm);
        TempData[SweetAlert.Success] = "Remove Successful";

        return RedirectToAction("BookHistory");
    }


    [HttpPost]
    public async Task<IActionResult> ToggleBookmark(int documentId, int pageNumber)
    {
        try
        {
            var currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
                return Json(new { success = false, message = "Unauthorized" });

            var bookDocument = await _db.BookDocuments
                .FirstOrDefaultAsync(bd => bd.Id == documentId);

            if (bookDocument == null)
                return Json(new { success = false, message = "Invalid document" });

            // Check if bookmark exists
            var existingBookmark = await _db.Bookmarks.FirstOrDefaultAsync(bm =>
                bm.UserId == currentUser.Id &&
                bm.BookDocumentId == documentId &&
                bm.Page == pageNumber);

            if (existingBookmark != null)
            {
                _db.Bookmarks.Remove(existingBookmark);
                await _db.SaveChangesAsync();

                return Json(new { success = true, action = "removed" });
            }

            var newBookmark = new Bookmark
            {
                UserId = currentUser.Id,
                BookId = bookDocument.BookId,
                BookDocumentId = documentId,
                Page = pageNumber,
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            };

            _db.Bookmarks.Add(newBookmark);
            await _db.SaveChangesAsync();

            return Json(new { success = true, action = "added" });
        }
        catch (Exception ex)
        {
            // Log the real error message
            Console.WriteLine("ToggleBookmark Error: " + ex.Message);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ViewBookmark(int documentId)
    {
        var currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        // Fetch bookmarks with related book data
        List<Bookmark> bookmarks = await _db.Bookmarks
            .Include(b => b.BookDocument)
            .ThenInclude(bd => bd.Book)
            .Where(b => b.UserId == currentUser.Id && b.BookDocumentId == documentId)
            .AsNoTracking()
            .ToListAsync();

        return View(bookmarks);
    }

    [HttpGet]
    public async Task<IActionResult> GetBookmarksForDocument(int documentId)
    {
        var currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        var pages = await _db.Bookmarks
            .Where(b => b.BookDocumentId == documentId && b.UserId == currentUser.Id)
            .Select(b => b.Page)
            .ToListAsync();

        return Json(pages);
    }


    [HttpPost]
    public async Task<IActionResult> RemoveBookmark(int bookmarkId, int documentId)
    {
        var currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        // Find the bookmark by its ID and include related data if needed
        Bookmark? bookmark = await _db.Bookmarks
            .Where(b => b.Id == bookmarkId && b.UserId == currentUser.Id)
            .Include(b => b.BookDocument)
            .ThenInclude(bd => bd.Book)
            .FirstOrDefaultAsync();

        if (bookmark == null)
        {
            TempData[SweetAlert.Danger] = "Bookmark not found.";
            return RedirectToAction("ViewBookmark");
        }

        // Remove the bookmark
        _db.Bookmarks.Remove(bookmark);
        await _db.SaveChangesAsync();

        List<Bookmark> bookmarks = await _db.Bookmarks
            .Include(b => b.BookDocument)
            .ThenInclude(bd => bd.Book)
            .Where(b => b.UserId == currentUser.Id && b.BookDocumentId == documentId)
            .ToListAsync();


        if (Request.IsAjax()) return PartialView("_BookmarkPartial", bookmarks);

        return RedirectToAction("ViewBookmark");
    }
}