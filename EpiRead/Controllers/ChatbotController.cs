using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

/// <summary>
///     Controller for the application chatbot features.
///     This file exposes endpoints used by the front-end to view, create, update
///     and delete chat sessions and their messages. It orchestrates the
///     retrieval of session lists, pagination of sessions and messages, and
///     the delegation of user message handling to <see cref="IChatbotService" />.
///     Use this controller to start new conversations, fetch conversation
///     history and perform session maintenance (rename/delete) for the current
///     authenticated user.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize]
public class ChatbotController : Controller
{
    private readonly IChatbotService _chatbotService;

    private readonly DB _db;

    public ChatbotController(DB db, IChatbotService chatbotService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _chatbotService = chatbotService ?? throw new ArgumentNullException(nameof(chatbotService));
    }

    /// <summary>
    ///     Serves the chatbot landing page.
    ///     Loads the current user's chat sessions and places them in the ViewBag
    ///     so the UI can render the session list and a place to start a new
    ///     conversation.
    /// </summary>
    /// <returns>
    ///     The chatbot view showing sessions and a blank current session.
    /// </returns>
    public async Task<IActionResult> Index()
    {
        // Only log DB/network operations - not generic start/end here
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) return Unauthorized();

            LoggerUtilities.Info("[End] GetCurrentUser found=" + (currentUser != null));

            // Get user's chat sessions
            LoggerUtilities.Info("[Start] GetUserSessionsAsync");
            List<ChatSession> sessions =
                await _chatbotService.GetUserSessionsAsync(currentUser!.Id);
            LoggerUtilities.Info("[End] GetUserSessionsAsync count=" + (sessions?.Count ?? 0));

            // Pass data to view via ViewBag for starting a new chat
            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.Sessions = sessions;
            ViewBag.CurrentSession = null;
            ViewBag.CurrentSessionId = Guid.Empty;

            // End
            return View();
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] Index Unhandled exception");
            return StatusCode(500, new { error = "An error occurred." });
        }
    }

    /// <summary>
    ///     Displays the chat UI for an existing session.
    ///     The requested session will be loaded along with its messages and
    ///     validated to belong to the current authenticated user.
    /// </summary>
    /// <param name="id">The chat session identifier</param>
    /// <returns>
    ///     The chatbot view populated with session details (or a redirect on error).
    /// </returns>
    public async Task<IActionResult> Chat(Guid id)
    {
        // Only log DB/network operations
        try
        {
            // Validate sessionId
            if (!ModelState.IsValid)
                // model validation is not a DB/network operation - no LoggerUtilities call
                return BadRequest(new { error = "Invalid session ID." });

            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) return Unauthorized();

            LoggerUtilities.Info("[Start] GetUserSessionsAsync");
            List<ChatSession> sessions =
                await _chatbotService.GetUserSessionsAsync(currentUser.Id);
            LoggerUtilities.Info("[End] GetUserSessionsAsync count=" + (sessions?.Count ?? 0));

            // Load the specific session with messages
            LoggerUtilities.Info("[Start] GetSessionWithMessagesAsync");
            ChatSession? currentSession = await _chatbotService.GetSessionWithMessagesAsync(id);
            LoggerUtilities.Info("[End] GetSessionWithMessagesAsync found=" + (currentSession != null));

            // Verify session belongs to current user
            if (currentSession == null || currentSession.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] Chat session not found or does not belong to user. sessionId={id}, userId={currentUser.Id}");
                return RedirectToAction("Index");
            }

            // Pass data to view via ViewBag (use lightweight DTOs to avoid serialization loops)
            ViewBag.CurrentUserId = currentUser.Id;
            ViewBag.Sessions = sessions;
            ViewBag.CurrentSession = new
            {
                id = currentSession.Id,
                title = currentSession.Title
            };
            ViewBag.CurrentSessionId = id;

            // End
            return View("Index");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] Chat Unhandled exception for sessionId=" + id);
            return StatusCode(500, new { error = "An error occurred." });
        }
    }

    /// <summary>
    ///     Returns the current user's chat sessions as JSON.
    ///     This is used by the client for AJAX-based session list refreshes.
    /// </summary>
    /// <returns>
    ///     A JSON object with a 'success' flag and the 'sessions' array.
    /// </returns>
    public async Task<IActionResult> GetSessions()
    {
        // Only log DB/network operations
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            LoggerUtilities.Info("[Start] GetUserSessionsAsync");
            List<ChatSession> sessions =
                await _chatbotService.GetUserSessionsAsync(currentUser.Id);
            LoggerUtilities.Info("[End] GetUserSessionsAsync count=" + (sessions?.Count ?? 0));

            // Map to DTO for response
            var sessionsDto = (sessions ?? new List<ChatSession>()).Select(s => new
            {
                id = s.Id,
                title = s.Title,
                createdAt = s.CreatedAt
            }).ToList();

            // End
            return Ok(new { success = true, sessions = sessionsDto });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetSessions Failed to get sessions");
            return StatusCode(500, new { success = false, error = "Failed to get sessions." });
        }
    }

    /// <summary>
    ///     Returns the current user's chat sessions rendered into a partial
    ///     Razor view fragment. The UI will inject this partial for list
    ///     updates without a full page refresh.
    /// </summary>
    /// <returns>
    ///     A partial view containing the sessions list.
    /// </returns>
    public async Task<IActionResult> GetSessionsPartial()
    {
        // Only log DB/network operations
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            LoggerUtilities.Info("[Start] GetUserSessionsAsync");
            List<ChatSession> sessions =
                await _chatbotService.GetUserSessionsAsync(currentUser.Id);
            LoggerUtilities.Info("[End] GetUserSessionsAsync count=" + (sessions?.Count ?? 0));

            // Return partial view with sessions
            return PartialView("Partials/_SessionsList", sessions);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetSessionsPartial Failed to load sessions");
            return StatusCode(500, "Failed to load sessions.");
        }
    }

    /// <summary>
    ///     Returns a page of the current user's chat sessions in a partial
    ///     view. Intended for infinite scroll and incremental loading.
    /// </summary>
    /// <param name="pageNumber">1-based page number</param>
    /// <param name="pageSize">Number of sessions per page (default 15)</param>
    /// <returns>A partial view with the sessions for the specified page.</returns>
    public async Task<IActionResult> GetSessionsPagedPartial(int pageNumber = 1, int pageSize = 15)
    {
        // Only log DB or network operations - skip generic start log
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Get paginated user's chat sessions
            List<ChatSession> sessions =
                await _chatbotService.GetUserSessionsPagedAsync(currentUser.Id, pageNumber, pageSize);

            LoggerUtilities.Info("[End] GetUserSessionsPagedAsync count=" + (sessions?.Count ?? 0));

            // Return partial view with sessions
            return PartialView("Partials/_SessionsList", sessions);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetSessionsPagedPartial Failed to load sessions");
            return StatusCode(500, "Failed to load sessions.");
        }
    }

    /// <summary>
    ///     Accepts a user message and obtains an AI assistant response.
    ///     If no session ID is provided a new session is created. Both the
    ///     user message and the AI response are stored using the chatbot
    ///     service.
    /// </summary>
    /// <param name="request">The request body containing session ID (optional), message and mode.</param>
    /// <returns>
    ///     A JSON object containing success, the sessionId and the AI response;
    ///     returns error details when message is empty or processing fails.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        // Only log DB/network operations
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Message))
                // not a DB operation
                return BadRequest(new { success = false, error = "Message cannot be empty." });

            if (string.IsNullOrWhiteSpace(request.Mode))
                request.Mode = "General";

            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            LoggerUtilities.Info("[Start] SendMessageAndStoreAsync");

            // Send message and get response
            (Guid sessionId, string response) = await _chatbotService.SendMessageAndStoreAsync(
                request.SessionId ?? Guid.Empty,
                currentUser.Id,
                request.Message,
                request.Mode);

            // Check if there was an error from AI service / storage
            if (sessionId == Guid.Empty)
            {
                LoggerUtilities.Warning("[Process] SendMessageAndStoreAsync error: " + response);
                return BadRequest(new { success = false, error = response });
            }

            LoggerUtilities.Info("[End] SendMessageAndStoreAsync sessionId=" + sessionId);

            // Success: return session ID and response
            return Ok(new
            {
                success = true,
                sessionId,
                response
            });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] SendMessage Unhandled exception");
            return StatusCode(500, new { success = false, error = "An error occurred while sending the message." });
        }
    }

    /// <summary>
    ///     Gets all messages from a session and returns them as JSON.
    ///     Messages are ordered by creation time before being sent to the
    ///     caller. Access is restricted to the owner of the session.
    /// </summary>
    /// <param name="id">The chat session id.</param>
    /// <returns>A JSON object containing the session messages.</returns>
    public async Task<IActionResult> GetSessionMessages(Guid id)
    {
        // Only log DB/network operations
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Get session with messages
            ChatSession? session = await _chatbotService.GetSessionWithMessagesAsync(id);

            // Verify session belongs to current user
            if (session == null || session.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] GetSessionMessages session not found or not owned by user. sessionId={id} userId={currentUser.Id}");
                return Unauthorized();
            }

            // Map messages to DTO
            List<object> messagesDto = new();
            if (session.ChatMessages != null && session.ChatMessages.Count > 0)
                messagesDto = session.ChatMessages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => (object)new
                    {
                        id = m.Id,
                        sender = m.Sender,
                        content = m.Content,
                        createdAt = m.CreatedAt
                    })
                    .ToList();

            // End
            return Ok(new { success = true, messages = messagesDto });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetSessionMessages Failed for sessionId=" + id);
            return StatusCode(500, new { success = false, error = "Failed to get messages." });
        }
    }

    /// <summary>
    ///     Returns all messages for a given session as an HTML partial.
    ///     The partial can be used by the UI to render the full conversation.
    /// </summary>
    /// <param name="id">The chat session id.</param>
    /// <returns>A partial view rendering the messages list.</returns>
    public async Task<IActionResult> GetMessages(Guid id)
    {
        // Only log DB/network operations
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Get session with messages
            ChatSession? session = await _chatbotService.GetSessionWithMessagesAsync(id);

            // Verify session belongs to current user
            if (session == null || session.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] GetMessages session not found or not owned by user. sessionId={id} userId={currentUser.Id}");
                return Unauthorized();
            }

            // Return partial view with messages
            List<ChatMessage> messages = session.ChatMessages?.ToList() ?? new List<ChatMessage>();
            // End
            return PartialView("Partials/_MessagesList", messages);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetMessages Failed for sessionId=" + id);
            return StatusCode(500, "Failed to load messages.");
        }
    }

    /// <summary>
    ///     Returns a single page of messages for a session as a partial view.
    ///     Messages are paginated with the newest messages first to support
    ///     infinite scroll / lazy loading of earlier messages.
    /// </summary>
    /// <param name="id">The chat session id.</param>
    /// <param name="pageNumber">Page number to retrieve (default 1).</param>
    /// <param name="pageSize">Number of messages per page (default 5).</param>
    /// <returns>A partial view with the requested page of messages.</returns>
    public async Task<IActionResult> GetMessagesPagedPartial(Guid id, int pageNumber = 1, int pageSize = 5)
    {
        // Only log DB/network operations
        try
        {
            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Verify session belongs to current user
            LoggerUtilities.Info("[Start] ChatSession.Find sessionId=" + id);
            ChatSession? session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == id);
            LoggerUtilities.Info("[End] ChatSession.Find found=" + (session != null));
            if (session == null || session.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] GetMessagesPagedPartial session not found or not owned by user. sessionId={id} userId={currentUser.Id}");
                return Unauthorized();
            }

            // Get paginated messages
            LoggerUtilities.Info("[Start] GetSessionMessagesPagedAsync");
            List<ChatMessage> messages = await _chatbotService.GetSessionMessagesPagedAsync(id, pageNumber, pageSize);
            LoggerUtilities.Info("[End] GetSessionMessagesPagedAsync count=" + (messages?.Count ?? 0));

            // End

            // Return partial view with messages
            return PartialView("Partials/_MessagesList", messages);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] GetMessagesPagedPartial Failed for sessionId=" + id);
            return StatusCode(500, "Failed to load messages.");
        }
    }

    /// <summary>
    ///     Lightweight health-check endpoint for front-end polling.
    ///     Does not require authentication and returns a short text message.
    /// </summary>
    /// <returns>A JSON object with a message text.</returns>
    [AllowAnonymous]
    public IActionResult Ping()
    {
        // Ping: no DB/network ops - skip logging
        return Ok(new { message = "Want to use the chatbot provided by EpiRead?" });
    }

    /// <summary>
    ///     Renames a chat session owned by the current user.
    ///     Validates the request and ensures the session belongs to the caller
    ///     before updating the title in the storage via the chatbot service.
    /// </summary>
    /// <param name="request">Rename request containing session id and the new title.</param>
    /// <returns>JSON object with success state and the new title when successful.</returns>
    [HttpPost]
    public async Task<IActionResult> RenameSession([FromBody] RenameRequest request)
    {
        // Only log DB/network operations
        try
        {
            // Validate request
            if (!ModelState.IsValid)
                // Not a DB operation - skip logging
                return BadRequest(new { success = false, error = "Invalid request" });

            string newTitle = (request.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newTitle))
                // Not a DB operation - skip logging
                return BadRequest(new { success = false, error = "Title cannot be empty" });

            if (newTitle.Length > 255)
                newTitle = newTitle.Substring(0, 255);

            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Verify session belongs to current user
            LoggerUtilities.Info("[Start] ChatSession.Find sessionId=" + request.Id);
            ChatSession? session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == request.Id);
            LoggerUtilities.Info("[End] ChatSession.Find found=" + (session != null));
            if (session == null || session.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] RenameSession session not found or not owned by user. id={request.Id} userId={currentUser.Id}");
                return Unauthorized();
            }

            // Rename session
            LoggerUtilities.Info("[Start] RenameSessionAsync id=" + request.Id);
            bool success = await _chatbotService.RenameSessionAsync(request.Id, newTitle);
            LoggerUtilities.Info("[End] RenameSessionAsync success=" + success);

            if (!success)
            {
                LoggerUtilities.Warning("[Process] RenameSessionAsync failed id=" + request.Id);
                return BadRequest(new { success = false, error = "Failed to rename session" });
            }

            // End
            return Ok(new { success = true, id = request.Id, title = newTitle });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] RenameSession Unhandled exception id=" + request?.Id);
            return StatusCode(500, new { success = false, error = "An error occurred." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSession([FromBody] DeleteRequest request)
    {
        // Only log DB/network operations
        try
        {
            // Validate request
            if (!ModelState.IsValid)
                // Not a DB op - skip logging
                return BadRequest(new { success = false, error = "Invalid request" });

            // Get current user
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null)
            {
                LoggerUtilities.Warning("[Process] GetCurrentUser returned null (unauthenticated)");
                return Unauthorized();
            }

            // Verify session belongs to current user
            LoggerUtilities.Info("[Start] ChatSession.Find sessionId=" + request.Id);
            ChatSession? session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == request.Id);
            LoggerUtilities.Info("[End] ChatSession.Find found=" + (session != null));
            if (session == null || session.UserId != currentUser.Id)
            {
                LoggerUtilities.Warning(
                    $"[Process] DeleteSession session not found or not owned by user. id={request.Id} userId={currentUser.Id}");
                return Unauthorized();
            }

            // Delete session
            LoggerUtilities.Info("[Start] DeleteSessionAsync id=" + request.Id);
            bool success = await _chatbotService.DeleteSessionAsync(request.Id);
            LoggerUtilities.Info("[End] DeleteSessionAsync success=" + success);

            if (!success)
            {
                LoggerUtilities.Warning("[Process] DeleteSessionAsync failed id=" + request.Id);
                return BadRequest(new { success = false, error = "Failed to delete session" });
            }

            // End
            return Ok(new { success = true, id = request.Id });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Process] DeleteSession Unhandled exception id=" + request?.Id);
            return StatusCode(500, new { success = false, error = "An error occurred." });
        }
    }

    // DTOs for binding
    /// <summary>
    ///     Request payload for the SendMessage endpoint.
    /// </summary>
    public class SendMessageRequest
    {
        public Guid? SessionId { get; set; }
        public string? Message { get; set; }
        public string? Mode { get; set; }
    }

    /// <summary>
    ///     Request payload for the RenameSession endpoint.
    /// </summary>
    public class RenameRequest
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>
    ///     Request payload for the DeleteSession endpoint.
    /// </summary>
    public class DeleteRequest
    {
        public Guid Id { get; set; }
    }
}