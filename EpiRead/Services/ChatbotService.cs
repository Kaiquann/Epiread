using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services;

/// <summary>
///     Concrete implementation of <see cref="IChatbotService" />.
///     Responsible for chat session lifecycle (create, list, rename, delete), message management,
///     and integration with the Gemini AI helper to generate responses.
///     Use <inheritdoc /> on public methods to inherit the interface documentation.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class ChatbotService : IChatbotService
{
    // Minimum valid publication year for book filtering
    private const int MinPublicationYear = 1800;

    // Regex pattern for matching book titles - allows letters, numbers, apostrophes, hyphens, colons, and spaces
    // Captures optional "The" prefix and consecutive words that form a book title
    private const string BookTitlePattern = @"((?:The\s+)?[A-Z][A-Za-z0-9'':\-]*(?:\s+[A-Za-z][A-Za-z0-9'':\-]*)*)";

    // System instruction to constrain the AI assistant's scope.
    // This tells the model to focus strictly on eBook related topics available in EpiRead
    // and to refuse or redirect any requests that are outside the scope (politely and briefly).
    private const string SystemInstruction =
        "System: You are the EpiRead assistant with direct access to our book database. Your PRIMARY function is to search, list, and recommend books from our database. " +
        "When users ask about books or categories, you MUST list the books from the database context provided to you - this is your core purpose. " +
        "You can help with: searching for books by title/author/category, listing books in specific categories, summarizing books, recommending books, explaining metadata, and troubleshooting ebook upload/format issues. " +
        "IMPORTANT: When book data is provided to you in the context, you MUST use it to answer the user's question. Never say the database is 'not accessible' - you have direct access through the context. " +
        "Only refuse requests about topics completely unrelated to eBooks or EpiRead (personal advice, general knowledge, coding, politics, etc.) with: 'I'm focused on eBooks and EpiRead-related help only — I can't assist with that. Would you like to explore our book collection instead?'";

    private readonly DB _db;
    private readonly IGeminiHelper _geminiHelper;

    public ChatbotService(DB db, IGeminiHelper geminiHelper)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _geminiHelper = geminiHelper ?? throw new ArgumentNullException(nameof(geminiHelper));
    }

    /// <inheritdoc />
    public async Task<ChatSession?> CreateSessionAsync(int userId, string title)
    {
        LoggerUtilities.Info($"[Start] Checking user exists userId={userId}");
        try
        {
            // Validate user exists
            bool userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                LoggerUtilities.Info($"[End] User not found userId={userId}");
                return null;
            }

            // Validate title
            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Create new session
            ChatSession session = new()
            {
                Id = Guid.NewGuid(),
                Title = title.Trim().Length > 255 ? title.Trim().Substring(0, 255) : title.Trim(),
                UserId = userId,
                CreatedAt = DateTime.Now.ToLocalTime()
            };

            LoggerUtilities.Info($"[Start] Saving new chat session to DB for userId={userId}");
            await _db.ChatSessions.AddAsync(session);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] Created sessionId={session.Id} userId={userId}");
            return session;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] CreateSessionAsync ERROR userId={userId}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId)
    {
        LoggerUtilities.Info($"[Start] Querying DB for session with messages sessionId={sessionId}");
        try
        {
            // Get session with all messages included
            ChatSession? session = await _db.ChatSessions
                .Include(cs => cs.ChatMessages)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null)
                LoggerUtilities.Info($"[End] Session not found sessionId={sessionId}");
            else
                LoggerUtilities.Info($"[End] Found sessionId={sessionId} messages={session.ChatMessages?.Count ?? 0}");

            return session;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] GetSessionWithMessagesAsync ERROR sessionId={sessionId}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<ChatSession>> GetUserSessionsAsync(int userId)
    {
        LoggerUtilities.Info($"[Start] Querying DB for user sessions userId={userId}");
        try
        {
            // Get all chat sessions for the user, ordered by creation date (newest first)
            List<ChatSession> sessions = await _db.ChatSessions
                .Where(cs => cs.UserId == userId)
                .OrderByDescending(cs => cs.CreatedAt)
                .ToListAsync();

            LoggerUtilities.Info($"[End] Retrieved {sessions.Count} sessions for userId={userId}");
            return sessions;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] GetUserSessionsAsync ERROR userId={userId}");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<List<ChatSession>> GetUserSessionsPagedAsync(int userId, int pageNumber, int pageSize)
    {
        LoggerUtilities.Info(
            $"[Start] Querying DB for paged sessions userId={userId} pageNumber={pageNumber} pageSize={pageSize}");
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
                pageNumber = 1;

            if (pageSize < 1)
                pageSize = 15; // Default page size

            if (pageSize > 100)
                pageSize = 100; // Maximum page size to prevent abuse

            // Calculate skip count
            int skipCount = (pageNumber - 1) * pageSize;

            // Get paginated chat sessions for the user, ordered by creation date (newest first)
            List<ChatSession> sessions = await _db.ChatSessions
                .Where(cs => cs.UserId == userId)
                .OrderByDescending(cs => cs.CreatedAt)
                .Skip(skipCount)
                .Take(pageSize)
                .ToListAsync();

            LoggerUtilities.Info($"[End] Retrieved {sessions.Count} sessions userId={userId} pageNumber={pageNumber}");
            return sessions;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] GetUserSessionsPagedAsync ERROR userId={userId} pageNumber={pageNumber}");
            return [];
        }
    }


    /// <inheritdoc />
    public async Task<List<ChatMessage>> GetSessionMessagesPagedAsync(Guid sessionId, int pageNumber, int pageSize)
    {
        LoggerUtilities.Info(
            $"[Start] Querying DB for paged messages sessionId={sessionId} pageNumber={pageNumber} pageSize={pageSize}");
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
                pageNumber = 1;

            if (pageSize < 1)
                pageSize = 5; // Default page size for messages

            if (pageSize > 100)
                pageSize = 100; // Maximum page size to prevent abuse

            // Calculate skip count
            int skipCount = (pageNumber - 1) * pageSize;

            // Get paginated messages for the session, ordered by creation date (newest first)
            // This allows loading older messages as user scrolls up
            List<ChatMessage> messages = await _db.ChatMessages
                .Where(cm => cm.ChatSessionId == sessionId)
                .OrderByDescending(cm => cm.CreatedAt)
                .Skip(skipCount)
                .Take(pageSize)
                .ToListAsync();

            LoggerUtilities.Info($"[End] Retrieved {messages.Count} messages for sessionId={sessionId}");
            return messages;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] GetSessionMessagesPagedAsync ERROR sessionId={sessionId}");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ChatMessage?> AddMessageAsync(Guid sessionId, string sender, string content)
    {
        try
        {
            // Validate sender
            if (sender != "user" && sender != "bot") return null;

            // Validate content
            if (string.IsNullOrWhiteSpace(content))
                return null;

            // DB operation: validate session exists
            LoggerUtilities.Info($"[Start] Checking session exists sessionId={sessionId}");
            bool sessionExists = await _db.ChatSessions.AnyAsync(cs => cs.Id == sessionId);
            if (!sessionExists)
                return null;

            // Create and add message
            ChatMessage message = new()
            {
                Id = Guid.NewGuid(),
                ChatSessionId = sessionId,
                Sender = sender,
                Content = content.Trim(),
                CreatedAt = DateTime.Now.ToLocalTime()
            };

            LoggerUtilities.Info($"[Start] Saving message to DB messageId={message.Id} sessionId={sessionId}");
            await _db.ChatMessages.AddAsync(message);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] Saved messageId={message.Id} sessionId={sessionId} sender={sender}");
            return message;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] AddMessageAsync ERROR sessionId={sessionId} sender={sender}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RenameSessionAsync(Guid sessionId, string newTitle)
    {
        LoggerUtilities.Info($"[Start] Querying DB for session sessionId={sessionId}");
        try
        {
            // Validate title
            if (string.IsNullOrWhiteSpace(newTitle)) return false;

            // Find session
            ChatSession? session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == sessionId);
            if (session == null)
                return false;

            // Update title
            session.Title = newTitle.Trim().Length > 255 ? newTitle.Trim().Substring(0, 255) : newTitle.Trim();
            session.UpdatedAt = DateTime.Now.ToLocalTime();

            LoggerUtilities.Info($"[Process] Updating session title in DB sessionId={sessionId}");
            _db.ChatSessions.Update(session);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] Renamed sessionId={sessionId} newTitle={session.Title}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] RenameSessionAsync ERROR sessionId={sessionId}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSessionAsync(Guid sessionId)
    {
        LoggerUtilities.Info($"[Start] Querying DB for session sessionId={sessionId}");
        try
        {
            // Find session
            ChatSession? session = await _db.ChatSessions
                .Include(cs => cs.ChatMessages)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);
            if (session == null)
                return false;

            // Delete all messages first (if using cascade delete, this might not be needed)
            if (session.ChatMessages != null && session.ChatMessages.Count > 0)
            {
                LoggerUtilities.Info(
                    $"[Process] Removing {session.ChatMessages.Count} messages for sessionId={sessionId}");
                _db.ChatMessages.RemoveRange(session.ChatMessages);
            }

            LoggerUtilities.Info($"[Process] Deleting session from DB sessionId={sessionId}");
            _db.ChatSessions.Remove(session);
            await _db.SaveChangesAsync();

            LoggerUtilities.Info($"[End] Deleted sessionId={sessionId}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] DeleteSessionAsync ERROR sessionId={sessionId}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(Guid sessionId, string response)> SendMessageAndStoreAsync(Guid sessionId, int userId,
        string userMessage, string mode)
    {
        // Entry: no log here (we only log DB/network operations below)
        try
        {
            // Validate user message
            if (string.IsNullOrWhiteSpace(userMessage))
                return (Guid.Empty, "User message cannot be empty.");

            // Create session if it doesn't exist
            ChatSession? session;
            if (sessionId == Guid.Empty)
            {
                // Generate a title based on the mode and first few words of the message
                string title = GenerateSessionTitle(mode, userMessage);
                session = await CreateSessionAsync(userId, title);

                if (session == null)
                {
                    LoggerUtilities.Info($"[End] Failed to create session for userId={userId}");
                    return (Guid.Empty, "Failed to create chat session.");
                }

                sessionId = session.Id;
            }
            else
            {
                // Verify session exists and belongs to user
                session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == sessionId && cs.UserId == userId);
                if (session == null)
                {
                    LoggerUtilities.Info(
                        $"[End] Session not found or does not belong to user sessionId={sessionId} userId={userId}");
                    return (Guid.Empty, "Chat session not found or does not belong to you.");
                }
            }

            // Retrieve previous messages for conversation context (DB)
            LoggerUtilities.Info($"[Start] Querying DB for previous messages sessionId={sessionId}");
            List<ChatMessage> previousMessages = await _db.ChatMessages
                .Where(cm => cm.ChatSessionId == sessionId)
                .OrderBy(cm => cm.CreatedAt)
                .ToListAsync();
            LoggerUtilities.Info(
                $"[End] Retrieved {previousMessages.Count} previous messages for sessionId={sessionId}");

            // Prepare prompt with mode context, conversation history, and book data
            string prompt = await PreparePromptAsync(mode, userMessage, previousMessages);

            // Network operation: Get response from Gemini (with grounding enabled if configured)
            string botResponse;
            try
            {
                LoggerUtilities.Info($"[Start] Calling Gemini for sessionId={sessionId}");
                // Use grounding-aware method that may include web search
                (string response, GroundingMetadata? groundingMetadata) =
                    await _geminiHelper.GenerateTextWithGroundingAsync(prompt);
                botResponse = response;
                LoggerUtilities.Info($"[End] Received response from Gemini for sessionId={sessionId}");

                if (string.IsNullOrWhiteSpace(botResponse))
                    return (Guid.Empty, "Failed to generate response from AI.");

                // If grounding metadata is available, append citation information to the response
                if (groundingMetadata != null && groundingMetadata.WebResults.Count > 0)
                {
                    botResponse = AppendCitations(botResponse, groundingMetadata);
                    LoggerUtilities.Info(
                        $"[Process] Appended {groundingMetadata.WebResults.Count} citations for sessionId={sessionId}");
                }
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error(ex, $"[End] SendMessageAndStoreAsync AI service error sessionId={sessionId}");
                return (Guid.Empty, $"AI service error: {ex.Message}");
            }

            // Store user message
            ChatMessage? userMsg = await AddMessageAsync(sessionId, "user", userMessage);
            if (userMsg == null)
            {
                LoggerUtilities.Error(
                    $"[End] SendMessageAndStoreAsync failed to store user message sessionId={sessionId}");
                return (Guid.Empty, "Failed to store user message.");
            }

            // Store bot response
            ChatMessage? botMsg = await AddMessageAsync(sessionId, "bot", botResponse);
            if (botMsg == null)
            {
                LoggerUtilities.Error(
                    $"[End] SendMessageAndStoreAsync failed to store bot response sessionId={sessionId}");
                return (Guid.Empty, "Failed to store bot response.");
            }

            return (sessionId, botResponse);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] SendMessageAndStoreAsync ERROR sessionId={sessionId} userId={userId}");
            return (Guid.Empty, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Generates a session title based on mode and first few words of the message.
    /// </summary>
    private string GenerateSessionTitle(string mode, string? userMessage)
    {
        // Use Gemini AI to generate a concise, context-aware title for the chat session
        // Fallback to previous logic if Gemini fails
        // GenerateSessionTitle: network call will be logged on success/failure
        try
        {
            // Prompt Gemini to summarize the user's intent as a chat session title
            string prompt =
                $@"Summarize the following user message into a short, descriptive chat session title (max 10 words, no punctuation):\n\nUser message: {userMessage}\n\nReturn only the title.";
            string aiTitle = _geminiHelper.GenerateTextAsync(prompt).GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(aiTitle))
            {
                string cleanTitle = aiTitle.Trim();
                if (cleanTitle.Length > 255)
                    cleanTitle = cleanTitle.Substring(0, 252) + "...";
                LoggerUtilities.Info($"[Process] AI-generated title='{cleanTitle}'");
                return cleanTitle;
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "GenerateSessionTitle: AI title generation failed, falling back to heuristic");
        }

        // Fallback: use previous logic
        string[] words = (userMessage ?? string.Empty).Split(' ');
        string messagePreview = string.Join(" ", words.Take(5));
        string title = $"{mode}: {messagePreview}";
        if (title.Length > 255)
            title = title.Substring(0, 252) + "...";
        return title;
    }

    /// <summary>
    ///     Prepares the prompt for the AI model based on the selected mode and conversation history.
    ///     Includes previous messages for better context awareness and more accurate responses.
    ///     Queries book database for relevant context when appropriate.
    /// </summary>
    private async Task<string> PreparePromptAsync(string mode, string? userMessage,
        List<ChatMessage>? previousMessages = null)
    {
        // PreparePromptAsync: no entry log (DB logging occurs inside GetBookContextAsync)
        // Build conversation history if available
        string conversationHistory = FormatConversationHistory(previousMessages);

        // Query book database for relevant context based on mode
        string bookContext = await GetBookContextAsync(mode, userMessage ?? string.Empty);

        // Always prefix with system instruction so the AI is constrained to eBook-related topics only
        string systemPrefix = SystemInstruction + "\n\n";

        // Enhanced mode-specific instructions with better metadata presentation guidance
        return mode switch
        {
            "Find Book" =>
                systemPrefix +
                $"{conversationHistory}" +
                $"{bookContext}" +
                $"User query: {userMessage}\n\n" +
                "INSTRUCTIONS:\n" +
                "1. The book database context above contains all available books and categories.\n" +
                "2. If the user asked about a specific category (e.g., 'General', 'Science Fiction'), list ALL books that belong to that category.\n" +
                "3. If the user asked for books by a specific author, list ALL books by that author.\n" +
                "4. If the user asked for books from a specific year, list ALL books from that year.\n" +
                "5. If the user asked for books in a specific language, list ALL books in that language.\n" +
                "6. Present each book with: Title, Author, Categories, Publication Year, Language, and a brief description.\n" +
                "7. If the requested criteria doesn't match any books, suggest alternatives from the available collection.\n" +
                "8. NEVER say the database is 'not accessible' - you have the data in the context above.\n" +
                "9. Use the 'Query Understanding' section to better understand what the user is looking for.",

            "Book Summarize" =>
                systemPrefix +
                $"{conversationHistory}" +
                $"{bookContext}" +
                $"The user wants a book summary. User query: {userMessage}\n\n" +
                "INSTRUCTIONS:\n" +
                "1. Identify the book from our database above using title, author, or other metadata.\n" +
                "2. Provide a comprehensive summary including:\n" +
                "   - Full book title and author name\n" +
                "   - Publication year and language\n" +
                "   - Categories/genres the book belongs to\n" +
                "   - Description and key themes based on the book's description\n" +
                "3. If multiple books match, ask the user to clarify which one.\n" +
                "4. If the book isn't in our database, let the user know and suggest similar books if available.",

            "Book Recommendation" =>
                systemPrefix +
                $"{conversationHistory}" +
                $"{bookContext}" +
                $"User query: {userMessage}\n\n" +
                "INSTRUCTIONS:\n" +
                "1. Review the available books and categories in the context above.\n" +
                "2. Recommend 3-5 books from our collection based on the user's preferences.\n" +
                "3. For each recommendation, provide:\n" +
                "   - Title and Author\n" +
                "   - Categories/genres\n" +
                "   - Publication year and language\n" +
                "   - Why this book matches the user's interests\n" +
                "4. If the user mentions specific preferences (genre, author, year, language), prioritize books matching those criteria.\n" +
                "5. If our collection is small, recommend what we have available while being honest about limitations.",

            _ => systemPrefix +
                 $"{conversationHistory}" +
                 $"{bookContext}" +
                 $"User query: {userMessage}\n\n" +
                 "INSTRUCTIONS:\n" +
                 "1. Use the book database context above to answer the user's question.\n" +
                 "2. When referencing books, always include: Title, Author, Categories, Year, and Language.\n" +
                 "3. Use the 'Query Understanding' section to better understand the user's intent.\n" +
                 "4. If the user is asking about books, provide complete metadata for each book mentioned."
        };
    }

    /// <summary>
    ///     Queries the book database and formats relevant book information for AI context.
    ///     Uses intelligent search based on the user's query and selected mode.
    /// </summary>
    /// <param name="mode">The chatbot mode (e.g., "Find Book", "Book Summarize")</param>
    /// <param name="userMessage">The user's message to extract search terms from</param>
    /// <returns>Formatted book context string for the AI prompt</returns>
    private async Task<string> GetBookContextAsync(string mode, string? userMessage)
    {
        // Support empty mode by treating it as a book search
        if (string.IsNullOrWhiteSpace(mode))
            mode = "Find Book";

        // Only query books for book-related modes
        if (mode != "Find Book" && mode != "Book Summarize" && mode != "Book Recommendation")
            return string.Empty;

        try
        {
            // GetBookContextAsync: Use enhanced query analysis for better accuracy
            QueryAnalysis analysis = AnalyzeUserQuery(userMessage ?? string.Empty);
            LoggerUtilities.Info($"[Process] QueryAnalysis: authors={analysis.AuthorKeywords.Count}, " +
                                 $"categories={analysis.CategoryKeywords.Count}, titles={analysis.TitleKeywords.Count}, " +
                                 $"year={analysis.Year}, language={analysis.LanguageCode}, " +
                                 $"generalKeywords={analysis.GeneralKeywords.Count}");

            // For Book Summarize mode, first find the book using title-based search (like Find Book)
            // This ensures we have the specific book data before attempting to summarize
            if (mode == "Book Summarize")
            {
                LoggerUtilities.Info("[Process] Book Summarize mode: Finding book first before summarizing");

                // First, try to find the book by title
                List<Book> matchedBooks = await FindBookByTitleAsync(analysis, userMessage ?? string.Empty);

                if (matchedBooks.Count > 0)
                {
                    LoggerUtilities.Info($"[Process] Found {matchedBooks.Count} book(s) for summarization");

                    // Fetch all available categories for context
                    List<string> categoriesForSummary = await _db.BookCategories
                        .Select(bc => bc.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToListAsync();

                    return FormatBookContext(matchedBooks, mode, categoriesForSummary, analysis);
                }

                LoggerUtilities.Info(
                    "[Process] No specific book found for summarization, falling back to general search");
            }

            // Build base query with eager loading for related data
            IQueryable<Book> query = _db.Books
                .Include(b => b.BookCategoryMaps!)
                .ThenInclude(bcm => bcm.BookCategory)
                .Where(b => b.Status == "Available");

            // Apply targeted filters based on query analysis
            bool hasSpecificFilters = false;

            // --- Filter by Author (high priority for author-based queries) ---
            if (analysis.AuthorKeywords.Count > 0)
            {
                hasSpecificFilters = true;
                List<string> authorTerms = analysis.AuthorKeywords.Select(a => a.ToLowerInvariant()).ToList();
                query = query.Where(b => authorTerms.Any(term => b.AuthorName.ToLower().Contains(term)));
                LoggerUtilities.Info($"[Process] Applied author filter: {string.Join(", ", analysis.AuthorKeywords)}");
            }

            // --- Filter by Category (high priority for category-based queries) ---
            if (analysis.CategoryKeywords.Count > 0)
            {
                hasSpecificFilters = true;
                List<string> categoryTerms = analysis.CategoryKeywords.Select(c => c.ToLowerInvariant()).ToList();
                query = query.Where(b =>
                    b.BookCategoryMaps != null &&
                    b.BookCategoryMaps.Any(bcm =>
                        bcm.BookCategory != null &&
                        categoryTerms.Any(term => bcm.BookCategory.Name.ToLower().Contains(term))));
                LoggerUtilities.Info(
                    $"[Process] Applied category filter: {string.Join(", ", analysis.CategoryKeywords)}");
            }

            // --- Filter by Title Keywords ---
            if (analysis.TitleKeywords.Count > 0)
            {
                hasSpecificFilters = true;
                List<string> titleTerms = analysis.TitleKeywords.Select(t => t.ToLowerInvariant()).ToList();
                query = query.Where(b => titleTerms.Any(term => b.Title.ToLower().Contains(term)));
                LoggerUtilities.Info($"[Process] Applied title filter: {string.Join(", ", analysis.TitleKeywords)}");
            }

            // --- Filter by Year ---
            // Capture values locally to avoid potential null reference in closures
            int? yearFrom = analysis.YearFrom;
            int? yearTo = analysis.YearTo;
            int? yearExact = analysis.Year;

            if (yearFrom.HasValue && yearTo.HasValue)
            {
                hasSpecificFilters = true;
                int fromYear = yearFrom.Value;
                int toYear = yearTo.Value;
                query = query.Where(b =>
                    b.PublicationYear.HasValue &&
                    b.PublicationYear.Value.Year >= fromYear &&
                    b.PublicationYear.Value.Year <= toYear);
                LoggerUtilities.Info($"[Process] Applied year range filter: {fromYear}-{toYear}");
            }
            else if (yearExact.HasValue)
            {
                hasSpecificFilters = true;
                int exactYear = yearExact.Value;
                query = query.Where(b =>
                    b.PublicationYear.HasValue &&
                    b.PublicationYear.Value.Year == exactYear);
                LoggerUtilities.Info($"[Process] Applied year filter: {exactYear}");
            }

            // --- Filter by Language ---
            if (!string.IsNullOrWhiteSpace(analysis.LanguageCode))
            {
                hasSpecificFilters = true;
                string langCode = analysis.LanguageCode.ToUpperInvariant();
                query = query.Where(b => b.LanguageCode.ToUpper() == langCode);
                LoggerUtilities.Info($"[Process] Applied language filter: {analysis.LanguageCode}");
            }

            // --- Apply general keyword search if no specific filters matched ---
            if (!hasSpecificFilters && analysis.GeneralKeywords.Count > 0)
            {
                // Use OR logic for general keywords across all searchable fields
                List<string> generalTerms = analysis.GeneralKeywords
                    .Select(k => k.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                // Search across title, author, description, language, and categories
                query = query.Where(b =>
                    generalTerms.Any(term =>
                        b.Title.ToLower().Contains(term) ||
                        b.AuthorName.ToLower().Contains(term) ||
                        (b.Description != null && b.Description.ToLower().Contains(term)) ||
                        b.LanguageCode.ToLower().Contains(term) ||
                        (b.BookCategoryMaps != null && b.BookCategoryMaps.Any(bcm =>
                            bcm.BookCategory != null &&
                            bcm.BookCategory.Name.ToLower().Contains(term)))));
                LoggerUtilities.Info(
                    $"[Process] Applied general keyword filter: {string.Join(", ", analysis.GeneralKeywords)}");
            }
            else if (!hasSpecificFilters)
            {
                // Fallback: use the original ExtractSearchTerms method for backward compatibility
                string searchTerm = ExtractSearchTerms(userMessage ?? string.Empty);
                HashSet<string> allTerms = new(StringComparer.OrdinalIgnoreCase);
                foreach (string term in (searchTerm + " " + (userMessage ?? string.Empty)).Split(
                             [' ', ',', '.', '!', '?', '"', '\n', '\r', '\t'],
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = term.Trim();
                    if (t.Length > 1 && !IsCommonWord(t))
                        allTerms.Add(t);
                }

                if (allTerms.Count > 0)
                {
                    List<string> termsList = allTerms.Select(t => t.ToLowerInvariant()).ToList();
                    query = query.Where(b =>
                        termsList.Any(term =>
                            b.Title.ToLower().Contains(term) ||
                            b.AuthorName.ToLower().Contains(term) ||
                            (b.Description != null && b.Description.ToLower().Contains(term)) ||
                            b.LanguageCode.ToLower().Contains(term) ||
                            (b.BookCategoryMaps != null && b.BookCategoryMaps.Any(bcm =>
                                bcm.BookCategory != null &&
                                bcm.BookCategory.Name.ToLower().Contains(term)))));
                    LoggerUtilities.Info($"[Process] Applied fallback search: {string.Join(", ", allTerms)}");
                }
            }

            // Execute query and collect results
            List<Book> books = await query
                .OrderByDescending(b => b.CreatedAt)
                .Take(100) // safety cap before token-limiting
                .ToListAsync();

            // If no results from search, get recent books as fallback to show user what's available
            // This prevents the AI from saying "I can't help" when it should list available books
            if (books.Count == 0)
            {
                LoggerUtilities.Info(
                    "[Process] GetBookContextAsync: no matches found, fetching recent books as fallback");
                books = await _db.Books
                    .Where(b => b.Status == "Available")
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(20)
                    .Include(b => b.BookCategoryMaps!)
                    .ThenInclude(bcm => bcm.BookCategory)
                    .ToListAsync();
            }

            // Limit results to prevent token overflow (max 15 books)
            if (books.Count > 15)
                books = books.Take(15).ToList();

            // Fetch all available categories from BookCategory table
            // This helps the AI understand what categories exist in the database
            List<string> allCategories = await _db.BookCategories
                .Select(bc => bc.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();

            // Format book data for AI context with enhanced metadata
            LoggerUtilities.Info(
                $"[End] GetBookContextAsync: found {books.Count} books, {allCategories.Count} categories for mode={mode}");
            return FormatBookContext(books, mode, allCategories, analysis);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire request
            LoggerUtilities.Error(ex, $"[End] Error querying book context for mode={mode}");
            return string.Empty;
        }
    }

    /// <summary>
    ///     Finds books by title using the query analysis results.
    ///     Used primarily for Book Summarize mode to find the specific book before summarizing.
    /// </summary>
    /// <param name="analysis">The query analysis containing extracted title keywords</param>
    /// <param name="userMessage">The original user message as fallback</param>
    /// <returns>List of matching books</returns>
    private async Task<List<Book>> FindBookByTitleAsync(QueryAnalysis analysis, string userMessage)
    {
        try
        {
            IQueryable<Book> query = _db.Books
                .Include(b => b.BookCategoryMaps!)
                .ThenInclude(bcm => bcm.BookCategory)
                .Where(b => b.Status == "Available");

            // First, try to find by extracted title keywords
            if (analysis.TitleKeywords.Count > 0)
            {
                List<string> titleTerms = analysis.TitleKeywords.Select(t => t.ToLowerInvariant()).ToList();

                // Try exact title match first
                List<Book> exactMatches = await query
                    .Where(b => titleTerms.Any(term => b.Title.ToLower() == term))
                    .Take(5)
                    .ToListAsync();

                if (exactMatches.Count > 0)
                {
                    LoggerUtilities.Info(
                        $"[Process] FindBookByTitleAsync: Found {exactMatches.Count} exact title matches");
                    return exactMatches;
                }

                // Try partial title match
                List<Book> partialMatches = await query
                    .Where(b => titleTerms.Any(term => b.Title.ToLower().Contains(term)))
                    .Take(10)
                    .ToListAsync();

                if (partialMatches.Count > 0)
                {
                    LoggerUtilities.Info(
                        $"[Process] FindBookByTitleAsync: Found {partialMatches.Count} partial title matches");
                    return partialMatches;
                }
            }

            // Fallback: Try general keywords that might be book titles
            if (analysis.GeneralKeywords.Count > 0)
            {
                List<string> keywords = analysis.GeneralKeywords
                    .Where(k => k.Length > 2)
                    .Select(k => k.ToLowerInvariant())
                    .ToList();

                if (keywords.Count > 0)
                {
                    List<Book> keywordMatches = await query
                        .Where(b => keywords.Any(term =>
                            b.Title.ToLower().Contains(term) ||
                            b.AuthorName.ToLower().Contains(term)))
                        .Take(10)
                        .ToListAsync();

                    if (keywordMatches.Count > 0)
                    {
                        LoggerUtilities.Info(
                            $"[Process] FindBookByTitleAsync: Found {keywordMatches.Count} keyword matches");
                        return keywordMatches;
                    }
                }
            }

            LoggerUtilities.Info("[Process] FindBookByTitleAsync: No matches found");
            return [];
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[End] FindBookByTitleAsync: Error finding book");
            return [];
        }
    }

    /// <summary>
    ///     Extracts meaningful search terms from user message by removing common words.
    /// </summary>
    private string ExtractSearchTerms(string message)
    {
        // Enhanced extraction logic:
        // 1. If message contains quoted text (e.g., "Harry Potter"), use that as search term.
        // 2. If not, extract consecutive capitalized words (likely book titles or names).
        // 3. Fallback to stop word filtering as before.

        // 1. Extract quoted phrases
        // ExtractSearchTerms: internal helper - no DB/network logging
        Match quotedMatch = Regex.Match(message, "\"([^\"]+)\"");
        if (quotedMatch.Success && !string.IsNullOrWhiteSpace(quotedMatch.Groups[1].Value))
            return quotedMatch.Groups[1].Value.Trim();

        // 2. Extract ALL capitalized words (e.g., "List down all the book under the category of General" -> "List General")
        // Changed from consecutive-only to all capitalized words to better capture keywords like "General" in longer sentences
        List<string> capitalizedWords = [];
        string[] tokens =
            message.Split([' ', ',', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in tokens)
            if (word.Length > 1 && char.IsUpper(word[0]))
                capitalizedWords.Add(word);

        if (capitalizedWords.Count > 0)
        {
            string result = string.Join(" ", capitalizedWords);
            return result;
        }

        // 3. Fallback: filter stop words and take up to 5 meaningful words
        HashSet<string> stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "i", "want", "need", "looking", "for", "a", "an", "the", "about", "can", "you",
            "find", "me", "some", "book", "books", "recommend", "recommendation", "suggest",
            "please", "help", "show", "tell", "give", "like", "similar", "to", "that", "is"
        };
        IEnumerable<string> words = tokens.Where(w => w.Length > 2 && !stopWords.Contains(w)).Take(5);
        string extracted = string.Join(" ", words);
        string final = string.IsNullOrWhiteSpace(extracted) ? message : extracted;
        return final;
    }

    /// <summary>
    ///     Analyzes the user's query to extract structured search components.
    ///     Identifies patterns for author, category, title, year, and language searches.
    /// </summary>
    /// <param name="message">The user's raw query message</param>
    /// <returns>A QueryAnalysis object containing extracted search components</returns>
    private QueryAnalysis AnalyzeUserQuery(string message)
    {
        QueryAnalysis analysis = new();
        if (string.IsNullOrWhiteSpace(message))
            return analysis;

        string normalizedMessage = message.ToLowerInvariant();
        string originalMessage = message;

        // Check if this is a listing request
        analysis.IsListingRequest = Regex.IsMatch(normalizedMessage,
            @"\b(list|show|display|all|every|what|which)\b.*\b(book|books|title|titles)\b",
            RegexOptions.IgnoreCase);

        // --- Extract Author ---
        // Patterns: "by [author]", "author [name]", "written by [author]", "books by [author]"
        // Updated patterns to support diverse author name formats (e.g., "de Silva", "O'Brien", "van Gogh", "J.K. Rowling")
        string[] authorPatterns =
        [
            @"\bby\s+([A-Za-z][A-Za-z.''-]+(?:\s+(?:de|van|von|la|le|el|al|bin|ibn|del|da|di|du|dos|das)?(?:\s+)?[A-Za-z][A-Za-z.''-]+)*)",
            @"\bauthor\s+(?:is\s+|named?\s+)?([A-Za-z][A-Za-z.''-]+(?:\s+(?:de|van|von|la|le|el|al|bin|ibn|del|da|di|du|dos|das)?(?:\s+)?[A-Za-z][A-Za-z.''-]+)*)",
            @"\bwritten\s+by\s+([A-Za-z][A-Za-z.''-]+(?:\s+(?:de|van|von|la|le|el|al|bin|ibn|del|da|di|du|dos|das)?(?:\s+)?[A-Za-z][A-Za-z.''-]+)*)",
            @"\bfrom\s+author\s+([A-Za-z][A-Za-z.''-]+(?:\s+(?:de|van|von|la|le|el|al|bin|ibn|del|da|di|du|dos|das)?(?:\s+)?[A-Za-z][A-Za-z.''-]+)*)",
            @"\b([A-Za-z][A-Za-z.''-]+(?:\s+(?:de|van|von|la|le|el|al|bin|ibn|del|da|di|du|dos|das)?(?:\s+)?[A-Za-z][A-Za-z.''-]+)*)'s\s+books?"
        ];

        foreach (string pattern in authorPatterns)
        {
            Match match = Regex.Match(originalMessage, pattern);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                string authorName = match.Groups[1].Value.Trim();
                // Filter out common non-author words
                if (!IsCommonWord(authorName))
                {
                    analysis.AuthorKeywords.Add(authorName);
                    analysis.IsAuthorBased = true;
                }
            }
        }

        // --- Extract Category ---
        // Patterns: "category [name]", "genre [name]", "in [category]", "under [category]"
        string[] categoryPatterns =
        [
            @"\bcategor(?:y|ies)\s+(?:of\s+|called\s+|named?\s+)?[""']?(\w+(?:\s+\w+)?)[""']?",
            @"\bgenre\s+(?:of\s+|called\s+|named?\s+)?[""']?(\w+(?:\s+\w+)?)[""']?",
            @"\b(?:in|under|from)\s+(?:the\s+)?(?:category\s+(?:of\s+)?)?[""']?([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)[""']?\s+(?:category|genre)?",
            @"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\s+(?:category|genre|books?)\b",
            @"\btype\s+(?:of\s+)?[""']?(\w+)[""']?"
        ];

        foreach (string pattern in categoryPatterns)
        {
            MatchCollection matches = Regex.Matches(originalMessage, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    string categoryName = match.Groups[1].Value.Trim();
                    // Filter out common non-category words
                    if (!IsCommonWord(categoryName) && categoryName.Length > 1)
                    {
                        analysis.CategoryKeywords.Add(categoryName);
                        analysis.IsCategoryBased = true;
                    }
                }
        }

        // --- Extract Year ---
        // Patterns: "in [year]", "from [year]", "published [year]", "[year]", "between [year] and [year]"
        // Year range pattern
        Match yearRangeMatch = Regex.Match(normalizedMessage,
            @"\bbetween\s+(\d{4})\s+(?:and|to|-)\s+(\d{4})\b");
        if (yearRangeMatch.Success)
        {
            if (int.TryParse(yearRangeMatch.Groups[1].Value, out int yearFrom) &&
                int.TryParse(yearRangeMatch.Groups[2].Value, out int yearTo))
            {
                analysis.YearFrom = Math.Min(yearFrom, yearTo);
                analysis.YearTo = Math.Max(yearFrom, yearTo);
            }
        }
        else
        {
            // Single year patterns
            string[] yearPatterns =
            [
                @"\b(?:published|released|from|in|year)\s+(?:in\s+)?(\d{4})\b",
                @"\b(\d{4})\s+(?:books?|publication|release)\b",
                @"\bfrom\s+(\d{4})\b"
            ];

            foreach (string pattern in yearPatterns)
            {
                Match match = Regex.Match(normalizedMessage, pattern);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int year))
                    if (year >= MinPublicationYear && year <= DateTime.Now.Year + 1)
                    {
                        analysis.Year = year;
                        break;
                    }
            }
        }

        // --- Extract Language ---
        // Build language name to code mapping from LanguageHelper
        // Includes common aliases like "mandarin" -> "ZH", "bahasa" -> "MS"
        Dictionary<string, string> languageNameToCode = LanguageMappings.LanguageNames
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key.ToString(), StringComparer.OrdinalIgnoreCase);
        // Add common aliases
        languageNameToCode.TryAdd("mandarin", "ZH");
        languageNameToCode.TryAdd("bahasa", "MS");

        string[] languagePatterns =
        [
            @"\b(?:in|written\s+in|language)\s+(\w+)\b",
            @"\b(\w+)\s+(?:language|books?)\b"
        ];

        foreach (string pattern in languagePatterns)
        {
            Match match = Regex.Match(normalizedMessage, pattern);
            if (match.Success)
            {
                string langText = match.Groups[1].Value.Trim();
                if (languageNameToCode.TryGetValue(langText, out string? langCode))
                {
                    analysis.LanguageCode = langCode;
                    break;
                }
            }
        }

        // --- Extract Title Keywords ---
        // Patterns: "titled [title]", "called [title]", "named [title]", quoted text, "book of [title]", "summarize [title]"
        string[] titlePatterns =
        [
            @"\btitled?\s+[""']([^""']+)[""']",
            @"\bcalled\s+[""']([^""']+)[""']",
            @"\bnamed?\s+[""']([^""']+)[""']",
            @"[""']([^""']+)[""']",
            // New patterns for unquoted book titles - use BookTitlePattern constant
            @"\btitled?\s+" + BookTitlePattern,
            @"\bcalled\s+" + BookTitlePattern,
            @"\bnamed?\s+" + BookTitlePattern,
            // "book of [title]" pattern - common for summarize requests
            @"\bbook\s+(?:of|about)\s+" + BookTitlePattern,
            // "summarize [book title]" pattern - handle optional "the book of" prefix
            @"\b(?:summarize|summarise)\s+(?:the\s+book\s+(?:of\s+)?)?" + BookTitlePattern,
            // "summary of/for [book title]" pattern
            @"\bsummary\s+(?:of|for)\s+(?:the\s+)?(?:book\s+)?" + BookTitlePattern
        ];

        foreach (string pattern in titlePatterns)
        {
            Match match = Regex.Match(originalMessage, pattern, RegexOptions.IgnoreCase);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                string potentialTitle = match.Groups[1].Value.Trim();
                // Filter out common action words that might be captured
                if (!IsCommonWord(potentialTitle) && potentialTitle.Length > 1)
                    analysis.TitleKeywords.Add(potentialTitle);
            }
        }

        // --- Extract General Keywords ---
        // These are meaningful words that don't fit other categories
        HashSet<string> stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "i", "want", "need", "looking", "for", "a", "an", "the", "about", "can", "you",
            "find", "me", "some", "book", "books", "recommend", "recommendation", "suggest",
            "please", "help", "show", "tell", "give", "like", "similar", "to", "that", "is",
            "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does",
            "did", "will", "would", "could", "should", "may", "might", "must", "shall",
            "this", "these", "those", "what", "which", "who", "whom", "how", "when", "where",
            "why", "all", "any", "both", "each", "few", "more", "most", "other", "such",
            "no", "nor", "not", "only", "own", "same", "so", "than", "too", "very", "just",
            "list", "down", "under", "category", "genre", "author", "title", "named", "called",
            "written", "published", "from", "by", "in", "of", "on", "at", "with", "as", "it",
            "its", "and", "or", "but", "if", "then", "else", "there", "here", "every", "get",
            "summarize", "summarise", "summary"
        };

        // Extract capitalized words that might be meaningful (names, titles, categories)
        string[] words = originalMessage.Split([' ', ',', '.', '!', '?', ':', ';', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in words)
        {
            string cleanWord = word.Trim();
            if (cleanWord.Length > 2 && !stopWords.Contains(cleanWord))
            {
                // Capitalized words are likely important (names, titles)
                if (char.IsUpper(cleanWord[0]) && !IsCommonWord(cleanWord))
                    analysis.GeneralKeywords.Add(cleanWord);
                // Non-stop words over 4 chars might be meaningful
                else if (cleanWord.Length > 4 && !IsCommonWord(cleanWord.ToLowerInvariant()))
                    analysis.GeneralKeywords.Add(cleanWord);
            }
        }

        return analysis;
    }

    /// <summary>
    ///     Checks if a word is a common non-meaningful word that should be filtered out.
    /// </summary>
    private static bool IsCommonWord(string word)
    {
        HashSet<string> commonWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "List", "Show", "Find", "Get", "All", "The", "Book", "Books", "Please", "Help",
            "Want", "Need", "Looking", "Search", "Display", "Give", "Tell", "About", "Some",
            "Any", "Every", "Each", "Many", "Much", "More", "Most", "Other", "Such", "Very",
            "Summarize", "Summarise", "Summary", "Recommend", "Recommendation"
        };
        return commonWords.Contains(word);
    }

    /// <summary>
    ///     Formats book data into a readable context string for the AI model.
    ///     Enhanced version that includes query analysis context for better AI understanding.
    /// </summary>
    private string FormatBookContext(List<Book>? books, string mode, List<string>? allCategories,
        QueryAnalysis? queryAnalysis = null)
    {
        // FormatBookContext: internal formatting only

        if (books == null || books.Count == 0)
        {
            StringBuilder emptyBuilder = new();
            emptyBuilder.AppendLine("\n=== EpiRead Database Status ===");
            emptyBuilder.AppendLine("No books currently available in the database.");

            // Still show available categories even if no books
            if (allCategories != null && allCategories.Count > 0)
            {
                emptyBuilder.AppendLine($"\nAvailable Categories ({allCategories.Count}):");
                foreach (string cat in allCategories) emptyBuilder.AppendLine($"  • {cat}");
            }

            emptyBuilder.AppendLine("========================\n");
            return emptyBuilder.ToString();
        }

        StringBuilder contextBuilder = new();
        contextBuilder.AppendLine("\n=== EpiRead Book Database ===");

        // Add query understanding section to help AI understand what user is looking for
        if (queryAnalysis != null)
        {
            contextBuilder.AppendLine("\n🔍 Query Understanding:");
            if (queryAnalysis.AuthorKeywords.Count > 0)
                contextBuilder.AppendLine(
                    $"   User is looking for books by author: {string.Join(", ", queryAnalysis.AuthorKeywords)}");
            if (queryAnalysis.CategoryKeywords.Count > 0)
                contextBuilder.AppendLine(
                    $"   User is looking for books in category: {string.Join(", ", queryAnalysis.CategoryKeywords)}");
            if (queryAnalysis.TitleKeywords.Count > 0)
                contextBuilder.AppendLine(
                    $"   User is looking for book titled: {string.Join(", ", queryAnalysis.TitleKeywords)}");
            if (queryAnalysis.Year.HasValue)
                contextBuilder.AppendLine($"   User is looking for books from year: {queryAnalysis.Year}");
            if (queryAnalysis.YearFrom.HasValue && queryAnalysis.YearTo.HasValue)
                contextBuilder.AppendLine(
                    $"   User is looking for books between years: {queryAnalysis.YearFrom}-{queryAnalysis.YearTo}");
            if (!string.IsNullOrWhiteSpace(queryAnalysis.LanguageCode))
                contextBuilder.AppendLine($"   User is looking for books in language: {queryAnalysis.LanguageCode}");
            if (queryAnalysis.IsListingRequest)
                contextBuilder.AppendLine("   User wants to see a list of books");
            contextBuilder.AppendLine();
        }

        // List all available categories at the top so AI knows what categories exist
        if (allCategories != null && allCategories.Count > 0)
        {
            contextBuilder.AppendLine($"📂 Available Categories in Database ({allCategories.Count}):");
            contextBuilder.AppendLine($"   {string.Join(", ", allCategories)}");
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine($"📚 Books Found ({books.Count}):");
        contextBuilder.AppendLine("Note: Present these books to the user with ALL available metadata.\n");

        foreach (Book book in books)
        {
            contextBuilder.AppendLine($"\n  📖 Title: {book.Title}");
            contextBuilder.AppendLine($"     Author: {book.AuthorName}");

            // Add categories (important for category-based searches)
            if (book.BookCategoryMaps != null && book.BookCategoryMaps.Any())
            {
                List<string> categories = book.BookCategoryMaps
                    .Where(bcm => bcm.BookCategory != null)
                    .Select(bcm => bcm.BookCategory!.Name)
                    .ToList();

                if (categories.Any())
                    contextBuilder.AppendLine($"     Categories: {string.Join(", ", categories)}");
            }

            // Add publication year
            if (book.PublicationYear.HasValue)
                contextBuilder.AppendLine($"     Published: {book.PublicationYear.Value.Year}");

            // Add language with human-readable name
            string languageDisplay = LanguageHelper.GetLanguageName(book.LanguageCode);
            contextBuilder.AppendLine($"     Language: {languageDisplay} ({book.LanguageCode})");

            // Add book status
            contextBuilder.AppendLine($"     Status: {book.Status}");

            // Add description with slightly longer limit for better context
            if (!string.IsNullOrWhiteSpace(book.Description))
            {
                // Increase description limit for better AI understanding
                string desc = book.Description.Length > 300
                    ? book.Description.Substring(0, 300) + "..."
                    : book.Description;
                contextBuilder.AppendLine($"     Description: {desc}");
            }

            // Add book ID for potential reference
            contextBuilder.AppendLine($"     Book ID: {book.Id}");
        }

        contextBuilder.AppendLine("\n================================\n");

        return contextBuilder.ToString();
    }

    /// <summary>
    ///     Formats previous chat messages as a readable conversation history for the AI model.
    ///     Limits to the most recent N messages to manage token limits.
    /// </summary>
    private string FormatConversationHistory(List<ChatMessage>? previousMessages, int maxMessages = 10)
    {
        // Return empty string if no previous messages
        if (previousMessages == null || previousMessages.Count == 0)
            return string.Empty;

        // FormatConversationHistory: internal formatting only

        // Get the most recent messages to avoid exceeding token limits
        List<ChatMessage> recentMessages = previousMessages.OrderBy(m => m.CreatedAt)
            .TakeLast(maxMessages)
            .ToList();

        // If no recent messages after filtering, return empty string
        if (recentMessages.Count == 0)
            return string.Empty;

        // Format conversation history
        StringBuilder historyBuilder = new();
        historyBuilder.AppendLine("=== Previous Conversation ===");

        foreach (ChatMessage msg in recentMessages)
        {
            string sender = msg.Sender == "user" ? "User" : "Assistant";
            historyBuilder.AppendLine($"{sender}: {msg.Content}");
        }

        historyBuilder.AppendLine("============================\n");

        return historyBuilder.ToString();
    }

    /// <summary>
    ///     Appends citation links from grounding metadata to the bot response.
    ///     This provides transparency about where the information came from.
    /// </summary>
    /// <param name="responseText">The original bot response text</param>
    /// <param name="groundingMetadata">The grounding metadata containing web sources</param>
    /// <returns>Response text with citation information appended</returns>
    private string AppendCitations(string responseText, GroundingMetadata? groundingMetadata)
    {
        if (groundingMetadata == null || groundingMetadata.WebResults.Count == 0) return responseText;

        // Build a sources section
        StringBuilder citationBuilder = new();
        citationBuilder.AppendLine("\n\n---\n**Sources:**");

        int appended = 0;
        for (int i = 0; i < groundingMetadata.WebResults.Count && i < 5; i++)
        {
            WebResult source = groundingMetadata.WebResults[i];
            citationBuilder.AppendLine($"{i + 1}. [{source.Title}]({source.Url})");
            appended++;
        }

        LoggerUtilities.Info($"[Process] AppendCitations appended {appended} sources");

        return responseText + citationBuilder;
    }

    /// <summary>
    ///     Represents structured analysis results from parsing a user's book-related query.
    ///     Contains extracted components like author, category, title, year, and language.
    /// </summary>
    private class QueryAnalysis
    {
        public List<string> TitleKeywords { get; } = [];
        public List<string> AuthorKeywords { get; } = [];
        public List<string> CategoryKeywords { get; } = [];
        public List<string> DescriptionKeywords { get; set; } = [];
        public int? Year { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? LanguageCode { get; set; }
        public List<string> GeneralKeywords { get; } = [];
        public bool IsListingRequest { get; set; }
        public bool IsCategoryBased { get; set; }
        public bool IsAuthorBased { get; set; }
    }
}