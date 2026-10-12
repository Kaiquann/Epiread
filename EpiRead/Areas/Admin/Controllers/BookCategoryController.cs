using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.TypeConversion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead.Areas.Admin.Controllers;

/// <summary>
///     File: BookCategoryController.cs
///     Controller for managing book categories. Handles HTTP requests and delegates
///     database operations and business logic to the IBookCategoryService. This file
///     contains actions for listing, showing, creating, editing, importing, and
///     exporting book categories within the Admin area.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.RequireAdminOrHigher)]
[Area("Admin")]
public class BookCategoryController : Controller
{
    private const int ChunkSize = 5 * 1024 * 1024; // 5MB per chunk (compatible with most proxies)

    // Static dictionary to store chunk upload sessions for BookCategory (in production, consider using distributed cache)
    private static readonly Dictionary<string, ChunkUploadSession> _chunkSessions = new();
    private static readonly object _chunkSessionLock = new();
    private readonly IBookCategoryService _bookCategoryService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BookCategoryController" /> class.
    /// </summary>
    /// <param name="bookCategoryService">Service that handles book category operations.</param>
    public BookCategoryController(IBookCategoryService bookCategoryService)
    {
        _bookCategoryService = bookCategoryService ?? throw new ArgumentNullException(nameof(bookCategoryService));
    }

    // ============================================================================================
    // READ Operations
    // ============================================================================================

    /// <summary>
    ///     Display the list of book categories with search, sort, filter, and pagination.
    /// </summary>
    public async Task<IActionResult> Index(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";
        // Method start
        LoggerUtilities.Info("[Start] Starting Index with search='" + search + "', page=" + page);
        // Get search results from service (DB op)
        LoggerUtilities.Info("[Process] Searching categories for '" + search + "'");
        List<BookCategory> searchedCategories = await _bookCategoryService.SearchBookCategories(search);
        LoggerUtilities.Info("[Process] Search returned " + (searchedCategories?.Count ?? 0) + " categories");

        // Validate page number
        if (page < 1) return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        // Apply sorting and pagination
        IPagedList<BookCategoryTreeItemVm> model =
            SortAndPaginateBookCategoriesTree(searchedCategories ?? new List<BookCategory>(), sort, dir, page);

        // Validate page exists
        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] Returning partial Index for page=" + page);
            return PartialView("_Index", model);
        }

        LoggerUtilities.Info("[End] Completed Index for page=" + page);
        return View(model);
    }

    /// <summary>
    ///     Display a single book category details.
    /// </summary>
    public async Task<IActionResult> Show(int id, string? search, string? sort, string? dir, string? filter,
        int page = 1)
    {
        LoggerUtilities.Info("[Start] Starting Show for id=" + id + ", page=" + page);
        // Load category by id (DB op)
        LoggerUtilities.Info("[Process] Fetching BookCategory by id=" + id);
        BookCategory? bookCategory = await _bookCategoryService.GetBookCategoryById(id);
        if (bookCategory == null)
        {
            LoggerUtilities.Warning("[End] BookCategory not found id=" + id);
            TempData[SweetAlert.Danger] = "Book category not found.";
            return RedirectToAction("Index");
        }

        // Store search parameters in ViewBag for the view to use
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        // Get books from the category
        List<BookCategoryMap> bookCategoryMaps = bookCategory.BookCategoryMaps?.ToList() ?? new List<BookCategoryMap>();
        List<Book> booksList = bookCategoryMaps
            .Where(map => map.Book != null && map.Book.Status == nameof(BookStatusType.Available))
            .Select(map => map.Book!)
            .ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
            booksList = booksList.Where(b =>
                b.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                b.AuthorName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (b.Description != null && b.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        // Validate page number
        if (page < 1)
        {
            LoggerUtilities.Warning("BookCategory.Show corrected invalid page " + page + " to 1");
            return RedirectToAction("Show", new { id, search, sort, dir, filter, page = 1 });
        }

        // Use the existing helper method to sort and paginate
        IPagedList<Book> pagedBooks = SortAndPaginateBooks(booksList, sort, dir, page);

        // Validate page exists
        if (page > pagedBooks.PageCount && pagedBooks.PageCount > 0)
        {
            LoggerUtilities.Warning(
                "BookCategory.Show requested page " + page + " greater than pageCount " + pagedBooks.PageCount +
                ", redirecting");
            return RedirectToAction("Show", new { id, search, sort, dir, filter, page = pagedBooks.PageCount });
        }

        // Create view model with both BookCategory and paginated books
        BookCategoryShowVm viewModel = new BookCategoryShowVm
        {
            BookCategory = bookCategory,
            PagedBooks = pagedBooks
        };

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] Returning partial for id=" + id + ", page=" + page);
            return PartialView("_ShowBooks", viewModel);
        }

        LoggerUtilities.Info("[End] Completed Show for id=" + id + ", page=" + page);
        return View(viewModel);
    }

    /// <summary>
    ///     Display the export page with filtering and selection options.
    /// </summary>
    public async Task<IActionResult> Export(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        LoggerUtilities.Info("[Start] Starting Export with search=" + search + ", page=" + page);
        // Get search results from service
        LoggerUtilities.Info("[Process] Searching categories for " + search);
        List<BookCategory> searchedCategories = await _bookCategoryService.SearchBookCategories(search);

        // Validate page number
        if (page < 1)
            return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        // Apply sorting and pagination
        IPagedList<BookCategory> model = SortAndPaginateBookCategories(searchedCategories, sort, dir, page);

        // Validate page exists
        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] Returning partial Export for page=" + page);
            return PartialView("_Export", model);
        }

        LoggerUtilities.Info("[End] Completed Export for page=" + page);
        return View(model);
    }

    // ============================================================================================
    // CREATE Operations
    // ============================================================================================

    /// <summary>
    ///     Display the form to create a new book category.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        // Get parent category list (DB op)
        LoggerUtilities.Info("[Start] Starting Create");
        LoggerUtilities.Info("[Process] Fetching parent category select list");
        SelectList parentCategorySelectList = await _bookCategoryService.GetParentCategorySelectList();
        LoggerUtilities.Info("[Process] Fetched parent categories count=" + (parentCategorySelectList?.Count() ?? 0));

        CreateBookCategoryVm createBookCategoryVm = new()
        {
            ParentBookCategories = parentCategorySelectList
        };

        LoggerUtilities.Info("[End] Returning Create view");
        return View(createBookCategoryVm);
    }

    /// <summary>
    ///     Create a new book category.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store(CreateBookCategoryVm createBookCategoryVm)
    {
        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer();
        }

        // Convert -1 (None) to null (guard nullability)
        int? parentId = createBookCategoryVm.ParentId == -1 ? null : createBookCategoryVm.ParentId;

        LoggerUtilities.Info("[Start] Starting Store to create category Name=" + createBookCategoryVm?.Name);
        // Use service to create category with full validation
        LoggerUtilities.Info("[Process] Calling CreateBookCategoryAsync for " + createBookCategoryVm?.Name);
        (bool success, string message, BookCategory? category) = await _bookCategoryService.CreateBookCategoryAsync(
            createBookCategoryVm?.Name ?? string.Empty,
            createBookCategoryVm?.Slug ?? string.Empty,
            createBookCategoryVm?.Description,
            parentId);

        if (!success)
        {
            LoggerUtilities.Info("[End] Failed to create category: " + message);
            TempData[SweetAlert.Danger] = message;
            return Request.RedirectToReferer();
        }

        LoggerUtilities.Info("[End] Created category id=" + category?.Id);
        TempData[SweetAlert.Success] = message;
        return RedirectToAction("Show", new { id = category?.Id });
    }

    // ============================================================================================
    // UPDATE Operations
    // ============================================================================================

    /// <summary>
    ///     Display the form to edit an existing book category.
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer();
        }

        LoggerUtilities.Info("[Start] Starting Edit for id=" + id);
        LoggerUtilities.Info("[Process] Fetching BookCategory by id=" + id);
        BookCategory? bookCategory = await _bookCategoryService.GetBookCategoryById(id);

        if (bookCategory == null)
        {
            LoggerUtilities.Warning("[End] Book category not found id=" + id);
            TempData[SweetAlert.Danger] = "Book category not found.";
            return RedirectToAction("Index");
        }

        // Prevent editing the General category
        if (IsGeneral(bookCategory))
        {
            LoggerUtilities.Warning("[Process] Attempt to edit General category id=" + bookCategory.Id);
            TempData[SweetAlert.Danger] = "The default General category cannot be edited.";
            return RedirectToAction("Show", new { id = bookCategory.Id });
        }

        LoggerUtilities.Info("[Process] Fetching parent category select list excluding id=" + bookCategory.Id);
        SelectList parentCategorySelectList = await _bookCategoryService.GetParentCategorySelectList(bookCategory.Id);

        EditBookCategoryVm editBookCategoryVm = new()
        {
            Id = bookCategory.Id,
            Name = bookCategory.Name,
            Slug = bookCategory.Slug!,
            Description = bookCategory.Description!,
            ParentId = bookCategory.ParentId,
            ParentBookCategories = parentCategorySelectList
        };

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] Returning partial Edit for id=" + editBookCategoryVm.Id);
            return PartialView("_Edit", editBookCategoryVm);
        }

        LoggerUtilities.Info("[End] Returning Edit view for id=" + editBookCategoryVm.Id);
        return View(editBookCategoryVm);
    }

    /// <summary>
    ///     Update an existing book category.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Update(EditBookCategoryVm editBookCategoryVm)
    {
        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer();
        }

        // Convert -1 (None) to null
        EditBookCategoryVm vm = editBookCategoryVm;
        int? parentId = vm.ParentId == -1 ? null : vm.ParentId;

        LoggerUtilities.Info("[Start] Starting Update for id=" + vm.Id);
        // Use service to update category with full validation (DB op)
        LoggerUtilities.Info("[Process] Updating category id=" + vm.Id);
        (bool success, string message) =
            await _bookCategoryService.UpdateBookCategoryAsync(vm.Id, vm.Name, vm.Slug, vm.Description, parentId);

        LoggerUtilities.Info("[End] Completed Update for id=" + vm.Id + " success=" + success);
        TempData[success ? SweetAlert.Success : SweetAlert.Danger] = message;
        return Request.RedirectToReferer();
    }

    // ============================================================================================
    // DELETE Operations
    // ============================================================================================

    /// <summary>
    ///     Delete one or more book categories.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string[] ids)
    {
        LoggerUtilities.Info("[Start] Starting Delete. idsCount=" + (ids?.Length ?? 0));
        if (!ModelState.IsValid || ids == null || ids.Length == 0)
        {
            LoggerUtilities.Info("[End] Invalid request or empty ids");
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer(this, "Index", "Admin");
        }

        // Use service to delete categories with transaction support
        LoggerUtilities.Info("[Process] Calling DeleteBookCategoriesAsync for idsCount=" + ids.Length);
        (bool success, string message, int deletedCount) = await _bookCategoryService.DeleteBookCategoriesAsync(ids);

        LoggerUtilities.Info("[End] Completed Delete. deletedCount=" + deletedCount + " success=" + success);
        TempData[success ? SweetAlert.Success : SweetAlert.Danger] = message;
        return Request.RedirectToReferer(this, "Index", "Admin");
    }

    // ============================================================================================
    // IMPORT/EXPORT Operations
    // ============================================================================================

    /// <summary>
    ///     Display the import upload form.
    /// </summary>
    public IActionResult Import()
    {
        if (Request.IsAjax())
            return PartialView("_ImportUpload");

        return View();
    }

    /// <summary>
    ///     Upload and process CSV file for import.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UploadZipFile([FromForm] BookCategoryUploadCsvVm model)
    {
        LoggerUtilities.Info("[Start] Starting UploadZipFile present=" + (model.CsvFile != null));
        if (!ModelState.IsValid || model.CsvFile == null)
        {
            TempData[SweetAlert.Danger] = "Please select a valid ZIP file.";
            LoggerUtilities.Info("[End] Invalid ZIP upload request");
            return PartialView("_ImportUpload", model);
        }

        // Validate file extension
        string[] allowedExtensions = new[] { ".zip" };
        string fileExt = Path.GetExtension(model.CsvFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExt))
        {
            ModelState.AddModelError("CsvFile", "Only .zip files are allowed.");
            LoggerUtilities.Info("[End] Invalid file extension: " + fileExt);
            return PartialView("_ImportUpload", model);
        }

        try
        {
            // Extract and verify ZIP file
            await using Stream zipStream = model.CsvFile.OpenReadStream();
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);

            // Verify export integrity using manifest
            LoggerUtilities.Info("[Process] Verifying export integrity...");
            ZipArchiveEntry? manifestEntry = archive.GetEntry(".epiread_manifest.json");
            if (manifestEntry == null)
            {
                ModelState.AddModelError("CsvFile",
                    "This export file is missing integrity verification data. Please re-export the categories using the latest version.");
                TempData[SweetAlert.Danger] = "Export file integrity verification failed: Missing manifest file.";
                return PartialView("_ImportUpload", model);
            }

            // Read and parse manifest
            dynamic? manifest = null;
            try
            {
                await using Stream manifestStream = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<JsonElement>(manifestStream);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("CsvFile", "Failed to read integrity manifest.");
                TempData[SweetAlert.Danger] = $"Export file integrity verification failed: {ex.Message}";
                LoggerUtilities.Error($"[Process] Manifest read error: {ex.Message}");
                return PartialView("_ImportUpload", model);
            }

            // Recompute hash for BookCategories.csv
            ZipArchiveEntry? csvEntry = archive.GetEntry("BookCategories.csv");
            if (csvEntry == null)
            {
                ModelState.AddModelError("CsvFile", "BookCategories.csv not found in the ZIP file.");
                TempData[SweetAlert.Danger] = "BookCategories.csv not found in the ZIP file.";
                return PartialView("_ImportUpload", model);
            }

            string computedHash;
            await using (Stream csvStream = csvEntry.Open())
            {
                using var sha256 = SHA256.Create();
                byte[] hashBytes = await sha256.ComputeHashAsync(csvStream);
                computedHash = Convert.ToBase64String(hashBytes);
            }

            // Verify file hash matches manifest
            JsonElement fileHashesElement = ((JsonElement)manifest).GetProperty("FileHashes");
            string expectedHash = fileHashesElement.GetProperty("BookCategories.csv").GetString() ?? "";

            if (!string.Equals(expectedHash, computedHash, StringComparison.Ordinal))
            {
                ModelState.AddModelError("CsvFile",
                    "File integrity check failed for BookCategories.csv. This export file may have been tampered with.");
                TempData[SweetAlert.Danger] = "Export file integrity verification failed: File hash mismatch detected.";
                LoggerUtilities.Warning(
                    $"[Process] Hash mismatch for BookCategories.csv: Expected={expectedHash.Substring(0, 16)}..., Computed={computedHash.Substring(0, 16)}...");
                return PartialView("_ImportUpload", model);
            }

            LoggerUtilities.Info("[Process] Hash verified for BookCategories.csv");

            // Verify integrity key
            string exportTimestamp = ((JsonElement)manifest).GetProperty("ExportTimestamp").GetString() ?? "";
            string combinedData = $"{computedHash}|{exportTimestamp}";

            using var sha256Final = SHA256.Create();
            byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
            string computedIntegrityKey = Convert.ToBase64String(integrityBytes);
            string expectedIntegrityKey = ((JsonElement)manifest).GetProperty("IntegrityKey").GetString() ?? "";

            if (!string.Equals(expectedIntegrityKey, computedIntegrityKey, StringComparison.Ordinal))
            {
                ModelState.AddModelError("CsvFile",
                    "Integrity key verification failed. This export file may have been tampered with.");
                TempData[SweetAlert.Danger] = "Export file integrity verification failed: Integrity key mismatch.";
                LoggerUtilities.Warning(
                    $"[Process] Integrity key mismatch: Expected={expectedIntegrityKey.Substring(0, 16)}..., Computed={computedIntegrityKey.Substring(0, 16)}...");
                return PartialView("_ImportUpload", model);
            }

            LoggerUtilities.Info("[Success] Export integrity verified successfully");

            // Parse CSV file after integrity verification
            List<BookCategoryImportRow> importRows;
            csvEntry = archive.GetEntry("BookCategories.csv");
            await using (Stream csvStream = csvEntry!.Open())
            using (StreamReader streamReader = new(csvStream))
            using (CsvReader csvReader = new(streamReader, CultureInfo.InvariantCulture))
            {
                importRows = csvReader.GetRecords<BookCategoryImportRow>().ToList();
            }

            // Process import and detect conflicts via service
            LoggerUtilities.Info("[Process] Processing " + importRows.Count + " CSV rows");
            BookCategoryImportConflictVm conflictVm = await _bookCategoryService.ProcessCsvImportAsync(importRows);
            LoggerUtilities.Info("[End] Parsed and processed " + importRows.Count + " rows");
            return PartialView("_ImportConflict", conflictVm);
        }
        catch (TypeConverterException ex)
        {
            LoggerUtilities.Error("[End] CSV format error: " + ex.Message);
            ModelState.AddModelError("CsvFile", $"CSV format error: {ex.Message}");
            TempData[SweetAlert.Danger] = $"CSV format error: {ex.Message}";
            return PartialView("_ImportUpload", model);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] Unexpected error: " + ex.Message);
            ModelState.AddModelError("CsvFile", $"Error parsing CSV: {ex.Message}");
            TempData[SweetAlert.Danger] = $"Error parsing CSV: {ex.Message}";
            return PartialView("_ImportUpload", model);
        }
    }

    /// <summary>
    ///     Initialize a chunked upload session for a large ZIP file.
    /// </summary>
    [HttpPost]
    public IActionResult InitChunkUpload([FromBody] ChunkUploadInitVm model)
    {
        LoggerUtilities.Info($"[Start] Initializing chunk upload for file: {model?.FileName}, size: {model?.FileSize}");

        if (model == null || !ModelState.IsValid)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request. Please provide valid file information."
            });

        // Validate file extension
        string fileExt = Path.GetExtension(model.FileName).ToLowerInvariant();
        if (fileExt != ".zip")
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Only .zip files are allowed."
            });

        // Generate unique upload ID
        string uploadId = Guid.NewGuid().ToString();

        // Create temp directory for chunks
        string tempFolder = Path.Combine(Path.GetTempPath(), $"epiread_categ_chunk_{uploadId}");
        Directory.CreateDirectory(tempFolder);

        // Store session info
        lock (_chunkSessionLock)
        {
            _chunkSessions[uploadId] = new ChunkUploadSession
            {
                UploadId = uploadId,
                FileName = model.FileName,
                FileSize = model.FileSize,
                TotalChunks = model.TotalChunks,
                TempFolder = tempFolder,
                ReceivedChunks = new HashSet<int>(),
                CreatedAt = DateTime.UtcNow
            };
        }

        LoggerUtilities.Info($"[End] Chunk upload session initialized: {uploadId}");

        return Json(new ChunkUploadResponseVm
        {
            Success = true,
            UploadId = uploadId,
            Message = "Upload session initialized successfully."
        });
    }

    /// <summary>
    ///     Upload a single chunk of the file.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(8 * 1024 * 1024)] // 8MB limit per chunk request (5MB chunk + overhead)
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    public async Task<IActionResult> UploadChunk([FromForm] ChunkUploadVm model)
    {
        if (model == null || !ModelState.IsValid || model.ChunkData == null)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid chunk data."
            });

        ChunkUploadSession? session;
        lock (_chunkSessionLock)
        {
            if (!_chunkSessions.TryGetValue(model.UploadId, out session))
                return Json(new ChunkUploadResponseVm
                {
                    Success = false,
                    Message = "Upload session not found or expired."
                });
        }

        try
        {
            // Save chunk to temp file
            string chunkPath = Path.Combine(session.TempFolder, $"chunk_{model.ChunkIndex}");
            await using (FileStream fs = new(chunkPath, FileMode.Create, FileAccess.Write))
            {
                await model.ChunkData.CopyToAsync(fs);
            }

            // Mark chunk as received
            lock (_chunkSessionLock)
            {
                session.ReceivedChunks.Add(model.ChunkIndex);
            }

            LoggerUtilities.Info(
                $"[Process] Received chunk {model.ChunkIndex + 1}/{session.TotalChunks} for upload {model.UploadId}");

            return Json(new ChunkUploadResponseVm
            {
                Success = true,
                UploadId = model.UploadId,
                ChunksReceived = session.ReceivedChunks.Count,
                TotalChunks = session.TotalChunks,
                Message = $"Chunk {model.ChunkIndex + 1} uploaded successfully."
            });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Error] Failed to save chunk {model.ChunkIndex} for upload {model.UploadId}");
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = $"Failed to save chunk: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Complete the chunked upload and process the assembled ZIP file.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CompleteChunkUpload([FromBody] ChunkUploadCompleteVm model)
    {
        LoggerUtilities.Info($"[Start] Completing chunk upload: {model?.UploadId}");

        if (model == null || !ModelState.IsValid)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request."
            });

        ChunkUploadSession? session;
        lock (_chunkSessionLock)
        {
            if (!_chunkSessions.TryGetValue(model.UploadId, out session))
                return Json(new ChunkUploadResponseVm
                {
                    Success = false,
                    Message = "Upload session not found or expired."
                });
        }

        // Verify all chunks received
        if (session.ReceivedChunks.Count != session.TotalChunks)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                ChunksReceived = session.ReceivedChunks.Count,
                TotalChunks = session.TotalChunks,
                Message = $"Missing chunks. Received {session.ReceivedChunks.Count} of {session.TotalChunks}."
            });

        try
        {
            // Assemble chunks into final file
            string finalFilePath = Path.Combine(session.TempFolder, session.FileName);
            await using (FileStream finalStream = new(finalFilePath, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < session.TotalChunks; i++)
                {
                    string chunkPath = Path.Combine(session.TempFolder, $"chunk_{i}");
                    if (!System.IO.File.Exists(chunkPath)) throw new FileNotFoundException($"Chunk {i} not found.");

                    await using FileStream chunkStream = new(chunkPath, FileMode.Open, FileAccess.Read);
                    await chunkStream.CopyToAsync(finalStream);
                }
            }

            LoggerUtilities.Info($"[Process] Assembled {session.TotalChunks} chunks into {finalFilePath}");

            // Process the assembled ZIP file using the existing logic
            BookCategoryImportConflictVm conflictVm = await ProcessZipFileForImport(finalFilePath);

            // Clean up session
            CleanupChunkSession(model.UploadId);

            LoggerUtilities.Info($"[End] Chunk upload completed successfully: {model.UploadId}");

            // Return the conflict view
            return PartialView("_ImportConflict", conflictVm);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Error] Failed to complete chunk upload: {model.UploadId}");

            // Clean up on error
            CleanupChunkSession(model.UploadId);

            TempData[SweetAlert.Danger] = $"Error processing ZIP file: {ex.Message}";
            return PartialView("_ImportUpload", new BookCategoryUploadCsvVm());
        }
    }

    /// <summary>
    ///     Cancel a chunk upload session and clean up resources.
    /// </summary>
    [HttpPost]
    public IActionResult CancelChunkUpload([FromBody] ChunkUploadCompleteVm model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.UploadId))
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request."
            });

        CleanupChunkSession(model.UploadId);

        return Json(new ChunkUploadResponseVm
        {
            Success = true,
            Message = "Upload cancelled."
        });
    }

    /// <summary>
    ///     Process the ZIP file for import (extracted from UploadZipFile for reuse).
    /// </summary>
    private async Task<BookCategoryImportConflictVm> ProcessZipFileForImport(string zipFilePath)
    {
        await using FileStream zipFileStream = new(zipFilePath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(zipFileStream, ZipArchiveMode.Read);

        // Verify export integrity using manifest
        LoggerUtilities.Info("[Process] Verifying export integrity...");
        ZipArchiveEntry? manifestEntry = archive.GetEntry(".epiread_manifest.json");
        if (manifestEntry == null)
            throw new InvalidOperationException(
                "This export file is missing integrity verification data. Please re-export the categories using the latest version.");

        // Read and parse manifest
        dynamic? manifest = null;
        try
        {
            await using Stream manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<JsonElement>(manifestStream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read integrity manifest: {ex.Message}");
        }

        // Recompute hash for BookCategories.csv
        ZipArchiveEntry? csvEntry = archive.GetEntry("BookCategories.csv");
        if (csvEntry == null) throw new InvalidOperationException("BookCategories.csv not found in the ZIP file.");

        string computedHash;
        await using (Stream csvStream = csvEntry.Open())
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(csvStream);
            computedHash = Convert.ToBase64String(hashBytes);
        }

        // Verify file hash matches manifest
        JsonElement fileHashesElement = ((JsonElement)manifest).GetProperty("FileHashes");
        string expectedHash = fileHashesElement.GetProperty("BookCategories.csv").GetString() ?? "";

        if (!string.Equals(expectedHash, computedHash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "File integrity check failed for BookCategories.csv. This export file may have been tampered with.");

        LoggerUtilities.Info("[Process] Hash verified for BookCategories.csv");

        // Verify integrity key
        string exportTimestamp = ((JsonElement)manifest).GetProperty("ExportTimestamp").GetString() ?? "";
        string combinedData = $"{computedHash}|{exportTimestamp}";

        using var sha256Final = SHA256.Create();
        byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
        string computedIntegrityKey = Convert.ToBase64String(integrityBytes);
        string expectedIntegrityKey = ((JsonElement)manifest).GetProperty("IntegrityKey").GetString() ?? "";

        if (!string.Equals(expectedIntegrityKey, computedIntegrityKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Integrity key verification failed. This export file may have been tampered with.");

        LoggerUtilities.Info("[Success] Export integrity verified successfully");

        // Parse CSV file after integrity verification
        List<BookCategoryImportRow> importRows;
        csvEntry = archive.GetEntry("BookCategories.csv");
        await using (Stream csvStream = csvEntry!.Open())
        using (StreamReader streamReader = new(csvStream))
        using (CsvReader csvReader = new(streamReader, CultureInfo.InvariantCulture))
        {
            importRows = csvReader.GetRecords<BookCategoryImportRow>().ToList();
        }

        // Process import and detect conflicts via service
        LoggerUtilities.Info("[Process] Processing " + importRows.Count + " CSV rows");
        BookCategoryImportConflictVm conflictVm = await _bookCategoryService.ProcessCsvImportAsync(importRows);
        LoggerUtilities.Info("[End] Parsed and processed " + importRows.Count + " rows");

        return conflictVm;
    }

    /// <summary>
    ///     Clean up chunk upload session and temporary files.
    /// </summary>
    private void CleanupChunkSession(string uploadId)
    {
        lock (_chunkSessionLock)
        {
            if (_chunkSessions.TryGetValue(uploadId, out ChunkUploadSession? session))
            {
                try
                {
                    if (Directory.Exists(session.TempFolder)) Directory.Delete(session.TempFolder, true);
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning($"[Process] Failed to clean up temp folder for {uploadId}: {ex.Message}");
                }

                _chunkSessions.Remove(uploadId);
            }
        }
    }

    /// <summary>
    ///     Confirm and complete the CSV import operation.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportConfirm([FromBody] BookCategoryImportConfirmVm model)
    {
        LoggerUtilities.Info("[Start] Starting ImportConfirm itemsCount=" + (model?.Items?.Count ?? 0));
        if (model?.Items == null || !model.Items.Any())
        {
            LoggerUtilities.Info("[End] No items received");
            TempData[SweetAlert.Danger] = "No import data received.";
            return PartialView("_ImportSummary",
                new BookCategoryImportSummaryVm { Added = 0, Updated = 0, Skipped = 0, Unresolved = 0 });
        }

        // Use service to complete import operation
        LoggerUtilities.Info("[Process] Calling ImportConfirmBookCategoriesAsync for " + model.Items.Count + " items");
        (int added, int updated, int skipped, int unresolved) =
            await _bookCategoryService.ImportConfirmBookCategoriesAsync(model.Items);

        BookCategoryImportSummaryVm summary = new()
        {
            Added = added,
            Updated = updated,
            Skipped = skipped,
            Unresolved = unresolved
        };

        LoggerUtilities.Info("[End] Completed. added=" + added + " updated=" + updated + " skipped=" + skipped +
                             " unresolved=" + unresolved);
        return PartialView("_ImportSummary", summary);
    }

    /// <summary>
    ///     Export selected or all book categories as CSV.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExportZip(string[] ids)
    {
        LoggerUtilities.Info("[Start] Starting ExportZip idsCount=" + (ids?.Length ?? 0));
        try
        {
            // Use service to generate ZIP with CSV and integrity manifest
            LoggerUtilities.Info("[Process] Calling ExportZip service for idsCount=" + (ids?.Length ?? 0));
            byte[] zipContent = await _bookCategoryService.ExportZip(ids!);
            string fileName = $"BookCategories_{DateTime.Now.ToLocalTime():yyyyMMdd_HHmmss}.zip";
            LoggerUtilities.Info("[End] Generated file " + fileName);
            return File(zipContent, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] Error exporting categories: " + ex.Message);
            TempData[SweetAlert.Danger] = $"Error exporting categories: {ex.Message}";
            return Request.RedirectToReferer();
        }
    }

    // ============================================================================================
    // VALIDATION Operations
    // ============================================================================================

    /// <summary>
    ///     Remote validation for book category slug (format and uniqueness).
    ///     Slug must be lowercase, no spaces, and match the pattern: letters, numbers, hyphens.
    ///     Used for client-side validation via AJAX.
    /// </summary>
    [HttpPost]
    public async Task<JsonResult> IsBookCategorySlugValid(string slug, int? id)
    {
        LoggerUtilities.Info("[Start] Validating slug='" + slug + "', id=" + id);
        LoggerUtilities.Info("[Process] Calling ValidateBookCategorySlug");
        string validationError = await _bookCategoryService.ValidateBookCategorySlug(slug, id);

        LoggerUtilities.Info("[End] Validation completed. hasError=" + !string.IsNullOrEmpty(validationError));
        // Return true if valid (empty error message), otherwise return error message
        return string.IsNullOrEmpty(validationError)
            ? Json(true)
            : Json(validationError);
    }

    // ============================================================================================
    // Private Helper Methods
    // ============================================================================================

    /// <summary>
    ///     Sort and paginate book categories (flat list for exports).
    /// </summary>
    private IPagedList<BookCategory> SortAndPaginateBookCategories(
        List<BookCategory> categories, string? sort, string? dir, int page = 1)
    {
        // Store sort parameters in ViewBag for UI
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        // Define sorting function based on sort parameter
        Func<BookCategory, object> sortFunc = sort switch
        {
            "Name" => c => c.Name,
            "Slug" => c => c.Slug ?? string.Empty,
            "Description" => c => c.Description ?? string.Empty,
            _ => c => c.Id
        };

        // Apply sort order
        IOrderedEnumerable<BookCategory> sorted = dir == "des"
            ? categories.AsEnumerable().OrderByDescending(sortFunc)
            : categories.AsEnumerable().OrderBy(sortFunc);

        // Apply pagination
        return sorted.ToPagedList(page, 10);
    }

    /// <summary>
    ///     Sort and paginate book categories in hierarchical tree structure.
    /// </summary>
    private IPagedList<BookCategoryTreeItemVm> SortAndPaginateBookCategoriesTree(
        List<BookCategory> categories, string? sort, string? dir, int page = 1)
    {
        // Store sort parameters in ViewBag for UI
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        // Build hierarchical tree structure
        List<BookCategoryTreeItemVm> treeItems = BuildCategoryTree(categories, sort, dir);

        // Apply pagination
        return treeItems.ToPagedList(page, 10);
    }

    /// <summary>
    ///     Build a hierarchical tree structure from flat list of categories.
    /// </summary>
    private List<BookCategoryTreeItemVm> BuildCategoryTree(
        List<BookCategory> categories, string? sort, string? dir)
    {
        List<BookCategoryTreeItemVm> result = new();

        // Define sorting function based on sort parameter
        Func<BookCategory, object> sortFunc = sort switch
        {
            "Name" => c => c.Name,
            "Slug" => c => c.Slug ?? string.Empty,
            "Description" => c => c.Description ?? string.Empty,
            _ => c => c.Id
        };

        // Get category IDs in the result set for quick lookup
        HashSet<int> categoryIds = categories.Select(c => c.Id).ToHashSet();

        // Get root categories (no parent) OR categories whose parent is not in the result set
        // This ensures sub-categories appear in search results even when their parent doesn't match
        List<BookCategory> rootCategories = categories
            .Where(c => c.ParentId == null || (c.ParentId.HasValue && !categoryIds.Contains(c.ParentId.Value)))
            .ToList();

        // Sort root categories
        IOrderedEnumerable<BookCategory> sortedRoots = dir == "des"
            ? rootCategories.OrderByDescending(sortFunc)
            : rootCategories.OrderBy(sortFunc);

        // Build tree recursively
        foreach (BookCategory root in sortedRoots) AddCategoryWithChildren(root, categories, sortFunc, dir, result, 0);

        return result;
    }

    /// <summary>
    ///     Recursively add category and its children to the tree.
    /// </summary>
    private void AddCategoryWithChildren(
        BookCategory category,
        List<BookCategory> allCategories,
        Func<BookCategory, object> sortFunc,
        string? dir,
        List<BookCategoryTreeItemVm> result,
        int level)
    {
        // Add current category
        result.Add(new BookCategoryTreeItemVm
        {
            Category = category,
            Level = level,
            ParentName = category.Parent?.Name
        });

        // Get and sort children
        List<BookCategory> children = allCategories.Where(c => c.ParentId == category.Id).ToList();
        IOrderedEnumerable<BookCategory> sortedChildren = dir == "des"
            ? children.OrderByDescending(sortFunc)
            : children.OrderBy(sortFunc);

        // Recursively add children
        foreach (BookCategory child in sortedChildren)
            AddCategoryWithChildren(child, allCategories, sortFunc, dir, result, level + 1);
    }

    /// <summary>
    ///     Sort and paginate books within a category.
    /// </summary>
    private IPagedList<Book> SortAndPaginateBooks(List<Book> searched, string? sort, string? dir,
        int page = 1)
    {
        // Handle null or empty list
        if (!searched.Any()) return new List<Book>().ToPagedList(page, 10);

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Book, object> fn = sort switch
        {
            "Title" => s => s.Title,
            "Author Name" => s => s.AuthorName,
            "Year" => s => s.PublicationYear.ToString() ?? "N/A",
            "Language" => s => s.LanguageCode,
            _ => s => s.Id
        };

        IOrderedEnumerable<Book> sorted = dir == "des"
            ? searched.AsEnumerable().OrderByDescending(fn)
            : searched.AsEnumerable().OrderBy(fn);

        // (3) Paging ---------------------------
        IPagedList<Book> pagedList = sorted.ToPagedList(page, 10);

        return pagedList;
    }
}