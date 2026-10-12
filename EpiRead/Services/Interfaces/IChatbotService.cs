namespace EpiRead.Services.Interfaces;

/// <summary>
///     Defines operations for managing chat sessions and messages, plus AI integration.
///     This interface exposes methods for creating sessions, listing sessions and messages,
///     adding messages, renaming and deleting sessions, and sending messages to the AI layer.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IChatbotService
{
    /// <summary>
    ///     Creates a chat session for the specified user and returns the created session.
    ///     Returns null on validation error or failure.
    /// </summary>
    /// <param name="userId">User ID who owns the session</param>
    /// <param name="title">Session title</param>
    Task<ChatSession?> CreateSessionAsync(int userId, string title);

    /// <summary>
    ///     Retrieves the chat session with the given id, including its messages.
    ///     Returns null if the session does not exist.
    /// </summary>
    /// <param name="sessionId">Chat session GUID</param>
    Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId);

    /// <summary>
    ///     Returns all chat sessions for the given user, ordered by newest first.
    /// </summary>
    /// <param name="userId">Owner user ID for sessions</param>
    Task<List<ChatSession>> GetUserSessionsAsync(int userId);

    /// <summary>
    ///     Returns a page of chat sessions for the given user.
    /// </summary>
    /// <param name="userId">Owner user ID for sessions</param>
    /// <param name="pageNumber">Page number (starting at 1)</param>
    /// <param name="pageSize">Page size</param>
    Task<List<ChatSession>> GetUserSessionsPagedAsync(int userId, int pageNumber, int pageSize);

    /// <summary>
    ///     Returns paginated messages for a specific session, ordered by newest first.
    /// </summary>
    /// <param name="sessionId">Target session ID</param>
    /// <param name="pageNumber">Page number (starting at 1)</param>
    /// <param name="pageSize">Page size</param>
    Task<List<ChatMessage>> GetSessionMessagesPagedAsync(Guid sessionId, int pageNumber, int pageSize);

    /// <summary>
    ///     Adds a message to a chat session. The <paramref name="sender" /> should be either
    ///     "user" or "bot". Returns the created ChatMessage or null on failure.
    /// </summary>
    /// <param name="sessionId">Target chat session ID</param>
    /// <param name="sender">Either "user" or "bot"</param>
    /// <param name="content">Message content</param>
    Task<ChatMessage?> AddMessageAsync(Guid sessionId, string sender, string content);

    /// <summary>
    ///     Renames an existing chat session. Returns true on success.
    /// </summary>
    /// <param name="sessionId">Target chat session ID</param>
    /// <param name="newTitle">New session title</param>
    Task<bool> RenameSessionAsync(Guid sessionId, string newTitle);

    /// <summary>
    ///     Deletes the chat session and all its messages. Returns true on success.
    /// </summary>
    /// <param name="sessionId">Target chat session ID</param>
    Task<bool> DeleteSessionAsync(Guid sessionId);

    /// <summary>
    ///     Sends a user message to the AI (Gemini) and stores both the user message and the AI response.
    ///     If <paramref name="sessionId" /> is empty, a new chat session is created.
    ///     Returns the session id and the AI response text, or an error message when applicable.
    /// </summary>
    /// <param name="sessionId">Chat session ID (use Guid.Empty to create a new session)</param>
    /// <param name="userId">Owner of the session</param>
    /// <param name="userMessage">The message to send to the AI</param>
    /// <param name="mode">The chatbot mode (e.g., "Find Book", "Book Summarize")</param>
    Task<(Guid sessionId, string response)> SendMessageAndStoreAsync(Guid sessionId, int userId, string userMessage,
        string mode);
}