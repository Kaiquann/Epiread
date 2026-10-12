using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommonUtilities.Utilities.Database;
using CsvHelper;
using EpiRead.Models.StoredProcedure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EpiRead.Services;

/// <summary>
///     Service implementation for managing book categories. This class implements
///     <see cref="IBookCategoryService" /> and provides database backed CRUD,
///     validation, import/export, and helper routines to support the web UI.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class BookCategoryService : IBookCategoryService
{
    private readonly IConfiguration _configuration;
    private readonly DB _db;

    public BookCategoryService(DB db, IConfiguration configuration)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<List<BookCategory>> GetAllCategories()
    {
        LoggerUtilities.Info("[Start] GetAllCategories");
        List<BookCategory> result = await _db.BookCategories
            .Include(bc => bc.Parent)
            .Include(bc => bc.SubBookCategories)
            .Include(bc => bc.Books)
            .Include(bc => bc.BookCategoryMaps!)
            .ThenInclude(bcm => bcm.Book)
            .ToListAsync();
        LoggerUtilities.Info($"[End] GetAllCategories returnedCount={result.Count}");
        return result;
    }

    /// <inheritdoc />
    public async Task<BookCategory?> GetBookCategoryById(int id)
    {
        LoggerUtilities.Info($"[Start] GetBookCategoryById id={id}");
        BookCategory? category = await _db.BookCategories
            .Where(bc => bc.Id == id)
            .Include(bc => bc.Parent)
            .Include(bc => bc.SubBookCategories)
            .Include(bc => bc.Books)
            .Include(bc => bc.BookCategoryMaps!)
            .ThenInclude(bcm => bcm.Book)
            .FirstOrDefaultAsync();
        LoggerUtilities.Info($"[End] GetBookCategoryById id={id} found={category != null}");
        return category;
    }

    /// <inheritdoc />
    public async Task<List<BookCategory>> SearchBookCategories(string searchTerm)
    {
        LoggerUtilities.Info($"[Start] SearchBookCategories searchTerm='{searchTerm}'");
        searchTerm = searchTerm.Trim();

        IQueryable<BookCategory> query = _db.BookCategories
            .Include(bc => bc.Parent)
            .Include(bc => bc.SubBookCategories)
            .Include(bc => bc.Books)
            .Include(bc => bc.BookCategoryMaps!)
            .ThenInclude(bcm => bcm.Book);

        if (!string.IsNullOrEmpty(searchTerm))
            query = query.Where(bc => bc.Name.Contains(searchTerm) ||
                                      bc.Slug.Contains(searchTerm) ||
                                      (bc.Description != null && bc.Description.Contains(searchTerm)));

        List<BookCategory> results = await query.ToListAsync();
        LoggerUtilities.Info($"[End] SearchBookCategories returnedCount={results.Count}");
        return results;
    }

    /// <inheritdoc />
    public async Task<BookCategory?> GetGeneralCategory()
    {
        LoggerUtilities.Info("[Start] GetGeneralCategory");
        BookCategory? general = await _db.BookCategories
            .FirstOrDefaultAsync(bc => bc.Name == "General" || bc.Slug == "general");
        LoggerUtilities.Info($"[End] GetGeneralCategory found={general != null}");
        return general;
    }

    /// <inheritdoc />
    public async Task<List<BookCategory>> GetBookCategoriesByIds(string[]? ids)
    {
        LoggerUtilities.Info($"[Start] GetBookCategoriesByIds requestedCount={ids?.Length ?? 0}");
        List<BookCategory> list = await _db.BookCategories
            .Where(bc => ids != null && ids.Contains(bc.Id.ToString()))
            .Include(bc => bc.Parent)
            .ToListAsync();
        LoggerUtilities.Info($"[End] GetBookCategoriesByIds returnedCount={list.Count}");
        return list;
    }

    /// <inheritdoc />
    public async Task<string> ValidateBookCategorySlug(string slug, int? currentId = null)
    {
        LoggerUtilities.Info($"[Start] ValidateBookCategorySlug slug='{slug}' currentId={currentId}");
        if (string.IsNullOrWhiteSpace(slug))
            return "Slug is required.";
        string slugPattern = @"^[a-z0-9-]+$";
        if (!Regex.IsMatch(slug, slugPattern))
            return "Slug must be lowercase and contain only letters, numbers, and hyphens.";
        bool isUnique = !await _db.BookCategories
            .AnyAsync(bc => bc.Slug == slug && bc.Id != (currentId ?? 0));
        if (!isUnique)
        {
            LoggerUtilities.Warning("[Process] ValidateBookCategorySlug slug exists: " + slug);
            return "Slug already exists.";
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public async Task<bool> IsValidParentCategory(int? parentId)
    {
        LoggerUtilities.Info($"[Start] IsValidParentCategory parentId={parentId}");
        if (parentId == null) return true;
        return await _db.BookCategories
            .FirstOrDefaultAsync(bc => bc.Id == parentId) != null;
    }

    /// <inheritdoc />
    public async Task<HashSet<int>> GetAllDescendantCategoryIds(int categoryId)
    {
        LoggerUtilities.Info($"[Start] GetAllDescendantCategoryIds categoryId={categoryId}");
        HashSet<int> descendants = new();
        List<BookCategory> childCategories =
            await _db.BookCategories.Where(bc => bc.ParentId == categoryId).ToListAsync();
        foreach (BookCategory category in childCategories)
        {
            descendants.Add(category.Id);
            HashSet<int> childDescendants = await GetAllDescendantCategoryIds(category.Id);
            foreach (int id in childDescendants)
                descendants.Add(id);
        }

        LoggerUtilities.Info($"[End] GetAllDescendantCategoryIds descendantCount={descendants.Count}");
        return descendants;
    }

    /// <inheritdoc />
    public async Task<SelectList> GetParentCategorySelectList(int? currentCategoryId = null)
    {
        LoggerUtilities.Info("[Start] GetParentCategorySelectList");
        List<BookCategory> allCategories = await _db.BookCategories.ToListAsync();
        HashSet<int> excludeIds = new();
        if (currentCategoryId != null)
        {
            excludeIds.Add(currentCategoryId.Value);
            HashSet<int> descendants = await GetAllDescendantCategoryIds(currentCategoryId.Value);
            foreach (int id in descendants)
                excludeIds.Add(id);
        }

        List<BookCategory> availableCategories = allCategories
            .Where(bc => !excludeIds.Contains(bc.Id))
            .ToList();
        availableCategories.Insert(0, new BookCategory { Id = -1, Name = "None" });
        LoggerUtilities.Info($"[End] GetParentCategorySelectList availableCount={availableCategories.Count}");
        return new SelectList(availableCategories, "Id", "Name");
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, BookCategory? Category)> CreateBookCategoryAsync(
        string name, string slug, string? description, int? parentId)
    {
        LoggerUtilities.Info($"[Start] CreateBookCategoryAsync name='{name}' slug='{slug}' parentId={parentId}");
        try
        {
            string slugValidationError = await ValidateBookCategorySlug(slug);
            if (!string.IsNullOrEmpty(slugValidationError))
            {
                LoggerUtilities.Warning("[Process] CreateBookCategoryAsync invalid slug: " + slug + " - " +
                                        slugValidationError);
                return (false, slugValidationError, null);
            }

            if (!await IsValidParentCategory(parentId))
            {
                LoggerUtilities.Warning("[Process] CreateBookCategoryAsync invalid parentId: " + parentId);
                return (false, "Invalid parent category selected.", null);
            }

            BookCategory newCategory = new()
            {
                Name = name,
                Slug = slug,
                Description = description,
                ParentId = parentId
            };
            await _db.BookCategories.AddAsync(newCategory);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] CreateBookCategoryAsync newCategoryId={newCategory.Id}");
            return (true, "Book category created successfully.", newCategory);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                "[Danger] CreateBookCategoryAsync error creating category. name=" + name + ", slug=" + slug);
            return (false, $"Error creating book category: {ex.Message}", null);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> UpdateBookCategoryAsync(
        int id, string name, string slug, string? description, int? parentId)
    {
        LoggerUtilities.Info(
            $"[Start] UpdateBookCategoryAsync id={id} name='{name}' slug='{slug}' parentId={parentId}");
        try
        {
            BookCategory category = await GetBookCategoryById(id) ??
                                    throw new Exception("Book category not found!");
            if (IsGeneral(category))
            {
                LoggerUtilities.Warning("[Process] UpdateBookCategoryAsync attempt update General id=" + id);
                return (false, "The default General category cannot be updated.");
            }

            if (parentId == id)
            {
                LoggerUtilities.Warning("[Process] UpdateBookCategoryAsync parentId equals id for id=" + id);
                return (false, "A category cannot be set as its own parent.");
            }

            if (!await IsValidParentCategory(parentId))
            {
                LoggerUtilities.Warning("[Process] UpdateBookCategoryAsync invalid parentId: " + parentId);
                return (false, "Invalid parent category selected.");
            }

            if (parentId != null)
            {
                HashSet<int> descendants = await GetAllDescendantCategoryIds(id);
                if (descendants.Contains(parentId.Value))
                {
                    LoggerUtilities.Warning("[Process] UpdateBookCategoryAsync circular parent detected. id=" + id +
                                            ", parentId=" + parentId);
                    return (false, "Cannot set the parent category to one of its sub-categories.");
                }
            }

            category.Name = name;
            category.Slug = slug;
            category.Description = description;
            category.ParentId = parentId;
            category.UpdatedAt = DateTime.Now.ToLocalTime();
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] UpdateBookCategoryAsync id={id}");
            return (true, "Book category updated successfully.");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Danger] UpdateBookCategoryAsync error updating id=" + id);
            return (false, $"Error updating book category: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message, int DeletedCount)> DeleteBookCategoriesAsync(string[]? ids)
    {
        LoggerUtilities.Info($"[Start] DeleteBookCategoriesAsync requestedCount={ids?.Length ?? 0}");

        try
        {
            if (ids == null || ids.Length == 0)
            {
                LoggerUtilities.Warning("[Process] DeleteBookCategoriesAsync no ids provided");
                return (false, "No categories selected for deletion.", 0);
            }

            // Get connection string from appsettings.json
            string? connectionString = _configuration.GetConnectionString("DBConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggerUtilities.Error("[Danger] Database connection string 'DBConnection' not found");
                return (false, "Database configuration error.", 0);
            }

            // Convert array to comma-separated string for stored procedure
            string categoryIdsParam = string.Join(",", ids);

            // Prepare stored procedure parameters
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@CategoryIds", categoryIdsParam)
            };

            // Execute stored procedure using DatabaseUtilities
            LoggerUtilities.Info($"[Process] Executing sp_DeleteBookCategories for categoryIds={categoryIdsParam}");
            List<DeleteBookCategoryResult> results = await DatabaseUtilities.ExecuteAsync<DeleteBookCategoryResult>(
                connectionString,
                "sp_DeleteBookCategories",
                parameters
            );

            // Check if stored procedure execution was successful
            if (results == null || results.Count == 0)
            {
                LoggerUtilities.Error("[Danger] Stored procedure returned no results");
                return (false, "Failed to delete categories - no response from database.", 0);
            }

            DeleteBookCategoryResult result = results[0];
            if (result.Success == 0)
            {
                LoggerUtilities.Error($"[Danger] Stored procedure failed: {result.ErrorMessage}");
                return (false, result.ErrorMessage ?? "Failed to delete categories.", 0);
            }

            int deletedCount = result.DeletedCount ?? 0;
            LoggerUtilities.Info($"[End] DeleteBookCategoriesAsync deletedCount={deletedCount}");

            string message =
                $"{deletedCount} book categor{(deletedCount == 1 ? "y was" : "ies were")} permanently deleted.";
            return (true, message, deletedCount);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[Danger] DeleteBookCategoriesAsync error deleting categories.");
            return (false, $"Error deleting categories: {ex.Message}", 0);
        }
    }

    /// <inheritdoc />
    public async Task ReassignSubcategoriesAndBooksToGeneralAsync(string[]? filteredIds, BookCategory? generalCategory)
    {
        LoggerUtilities.Info("[Start] ReassignSubcategoriesAndBooksToGeneralAsync");
        // Convert to integer ids to avoid repeated ToString() calls and improve DB query performance
        int[] parentFilterIds = filteredIds?.Select(s =>
            {
                bool ok = int.TryParse(s, out int x);
                return (ok, x);
            })
            .Where(t => t.ok)
            .Select(t => t.x)
            .ToArray() ?? Array.Empty<int>();

        List<BookCategory> subcategoriesToReassign = await _db.BookCategories
            .Where(b => b.ParentId != null && parentFilterIds.Contains(b.ParentId.Value))
            .ToListAsync();

        if (subcategoriesToReassign.Count > 0)
        {
            LoggerUtilities.Info(
                $"[Process] Reassigning {subcategoriesToReassign.Count} subcategories to General id={generalCategory?.Id}");
            foreach (BookCategory b in subcategoriesToReassign)
            {
                b.ParentId = generalCategory?.Id;
                b.UpdatedAt = DateTime.Now.ToLocalTime();
            }
        }

        // Reassign book category mappings.
        // BookCategoryMap uses a composite primary key (BookId, BookCategoryId) so we cannot update
        // BookCategoryId directly; instead we must delete dependents and recreate them.
        // Remove BookCategoryMap rows that reference deleted categories. Use ints for filtering.
        List<BookCategoryMap> mapsToRemove = await _db.BookCategoryMaps
            .Where(map => parentFilterIds.Contains(map.BookCategoryId))
            .ToListAsync();

        if (mapsToRemove.Count > 0)
        {
            // Keep track of affected book ids so we can create new mappings for them
            // after the delete. Also avoid creating duplicate maps for books that already
            // have the general category mapping.
            LoggerUtilities.Info($"[Process] Found {mapsToRemove.Count} BookCategoryMap mappings to remove.");
            HashSet<int> affectedBookIds = mapsToRemove.Select(m => m.BookId).ToHashSet();

            // Remove the offending mappings (deferred persistence until caller SaveChangesAsync)
            _db.BookCategoryMaps.RemoveRange(mapsToRemove);

            LoggerUtilities.Info($"[Process] Removed {mapsToRemove.Count} BookCategoryMap mappings.");

            // For each affected book, add mapping to general category if it doesn't exist
            int targetCategoryId = generalCategory?.Id ?? 1;
            List<Book> affectedBooks = await _db.Books
                .Where(b => affectedBookIds.Contains(b.Id))
                .Include(b => b.BookCategoryMaps)
                .ToListAsync();

            int newMappingsAdded = 0;
            foreach (Book b in affectedBooks)
            {
                if (b.BookCategoryMaps == null || b.BookCategoryMaps.All(m => m.BookCategoryId != targetCategoryId))
                {
                    BookCategoryMap newMap = new()
                    {
                        BookId = b.Id,
                        BookCategoryId = targetCategoryId
                    };
                    // defer adding to be saved later in caller SaveChangesAsync
                    _db.BookCategoryMaps.Add(newMap);
                    newMappingsAdded++;
                }

                b.UpdatedAt = DateTime.Now.ToLocalTime();
                _db.Books.Update(b);
            }

            // Persistence will be handled by caller (DeleteBookCategoriesAsync) - keep calls batched
            LoggerUtilities.Info(
                $"[Process] Recreated general mappings. booksChecked={affectedBooks.Count}, newMappingsAdded={newMappingsAdded}");
        }

        LoggerUtilities.Info("[End] ReassignSubcategoriesAndBooksToGeneralAsync completed");
    }

    /// <inheritdoc />
    public async Task<BookCategoryImportConflictVm> ProcessCsvImportAsync(List<BookCategoryImportRow>? importRows)
    {
        BookCategoryImportConflictVm conflictVm = new();
        try
        {
            LoggerUtilities.Info($"[Start] ProcessCsvImportAsync rows={importRows?.Count ?? 0}");

            // With integrity verification in place, CSV duplicates are impossible
            // Only check database conflicts (slug must be unique, name can be duplicate)
            List<BookCategory> existingCategories = await _db.BookCategories.ToListAsync();
            HashSet<string> existingSlugs = existingCategories
                .Select(c => c.Slug.Trim().ToLower())
                .ToHashSet();

            if (importRows != null)
                foreach (BookCategoryImportRow row in importRows)
                {
                    BookCategoryImportConflictItem item = new() { Row = row };
                    string slugKey = row.Slug.ToLower();

                    // Check if slug exists in database (names can be duplicate)
                    if (existingSlugs.Contains(slugKey))
                    {
                        item.ConflictType = "Slug Exists";
                        item.SuggestedAction = "Skip";
                        item.ExistingCategoryName = row.Name;
                    }
                    else
                    {
                        item.ConflictType = "New";
                        item.SuggestedAction = "Add";
                        item.ExistingCategoryName = null;
                    }

                    conflictVm.Items.Add(item);
                }
        }
        catch (Exception ex)
        {
            conflictVm.Items.Clear();
            conflictVm.Items.Add(new BookCategoryImportConflictItem
            {
                ConflictType = "Error",
                ExistingCategoryName = $"Error processing import: {ex.Message}"
            });
            LoggerUtilities.Error(ex, "[Danger] ProcessCsvImportAsync error processing import.");
        }

        return conflictVm;
    }

    /// <inheritdoc />
    public async Task<(int Added, int Updated, int Skipped, int Unresolved)> ImportConfirmBookCategoriesAsync(
        List<BookCategoryImportConfirmItemVm>? items)
    {
        int added = 0, updated = 0, skipped = 0, unresolved = 0;
        try
        {
            LoggerUtilities.Info($"[Start] ImportConfirmBookCategoriesAsync items={items?.Count ?? 0}");

            // Load all existing categories once and build quick lookups by slug only
            List<BookCategory> existingCategories = await _db.BookCategories.ToListAsync();
            Dictionary<string, BookCategory> existingBySlug = existingCategories
                .Where(c => !string.IsNullOrWhiteSpace(c.Slug))
                .ToDictionary(c => c.Slug.ToLowerInvariant(), c => c);

            // Track slugs added within this import loop to prevent duplicate slugs
            Dictionary<string, BookCategory> addedBySlug = new(StringComparer.OrdinalIgnoreCase);
            // Track all categories (by name) for parent resolution
            Dictionary<string, BookCategory> allByName = existingCategories
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c, StringComparer.OrdinalIgnoreCase);

            // Process items in-memory and only use EF once via SaveChangesAsync at the end
            await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
            if (items != null)
                foreach (BookCategoryImportConfirmItemVm item in items)
                    try
                    {
                        if (IsGeneral(item.Name, item.Slug))
                        {
                            skipped++;
                            LoggerUtilities.Warning("[Process] skipped General category: " + item.Name);
                            continue;
                        }

                        string slugKey = item.Slug.ToLowerInvariant();

                        // Check for slug conflicts only (existing or newly added in this batch)
                        existingBySlug.TryGetValue(slugKey, out BookCategory? existingSlugCategory);
                        addedBySlug.TryGetValue(slugKey, out BookCategory? addedSlugCategory);
                        BookCategory? resolvedExisting = existingSlugCategory ?? addedSlugCategory;

                        // Resolve parent category by name lookup
                        BookCategory? parentCategory = null;
                        if (!string.IsNullOrWhiteSpace(item.ParentCategory) && item.ParentCategory != "-")
                        {
                            string parentKey = item.ParentCategory.Trim().ToLowerInvariant();
                            allByName.TryGetValue(parentKey, out parentCategory);
                        }

                        // Determine action
                        switch (item.Action)
                        {
                            case "Add" when resolvedExisting == null:
                            {
                                // Add new category (no slug conflict exists)
                                BookCategory newCategory = new()
                                {
                                    Name = item.Name,
                                    Slug = item.Slug,
                                    Description = item.Description,
                                    Parent = parentCategory,
                                    CreatedAt = item.CreatedAt,
                                    UpdatedAt = item.UpdatedAt
                                };
                                _db.BookCategories.Add(newCategory);
                                added++;
                                // Track by slug and name for later references
                                if (!string.IsNullOrWhiteSpace(slugKey)) addedBySlug[slugKey] = newCategory;
                                string newNameKey = item.Name.Trim().ToLowerInvariant();
                                if (!string.IsNullOrWhiteSpace(newNameKey)) allByName[newNameKey] = newCategory;
                                break;
                            }
                            case "Add" when resolvedExisting != null:
                            {
                                // Slug conflict - skip
                                skipped++;
                                LoggerUtilities.Warning("[Process] Add skipped due to slug conflict: " + item.Slug);
                                break;
                            }
                            case "Overwrite" when resolvedExisting != null:
                                // Remove old slug from lookup
                                string? oldSlugKey = resolvedExisting.Slug.ToLowerInvariant();
                                if (!string.IsNullOrWhiteSpace(oldSlugKey))
                                    existingBySlug.Remove(oldSlugKey);

                                // Remove old name from name lookup
                                string? oldNameKey = resolvedExisting.Name.Trim().ToLowerInvariant();
                                if (!string.IsNullOrWhiteSpace(oldNameKey))
                                    allByName.Remove(oldNameKey);

                                // Overwrite ALL fields with CSV data (CSV has priority)
                                resolvedExisting.Name = item.Name;
                                resolvedExisting.Slug = item.Slug;
                                resolvedExisting.Description = item.Description;
                                resolvedExisting.Parent = parentCategory;
                                resolvedExisting.CreatedAt = item.CreatedAt;
                                resolvedExisting.UpdatedAt = item.UpdatedAt;

                                // Update lookups with new slug and name
                                existingBySlug[slugKey] = resolvedExisting;
                                string updatedNameKey = item.Name.Trim().ToLowerInvariant();
                                if (!string.IsNullOrWhiteSpace(updatedNameKey))
                                    allByName[updatedNameKey] = resolvedExisting;

                                updated++;
                                break;
                            case "Skip":
                                skipped++;
                                break;
                            case "Merge":
                                if (resolvedExisting != null && item.UpdatedAt > resolvedExisting.UpdatedAt)
                                {
                                    // Remove old slug and name from lookups
                                    string? oldSlugKeyMerge = resolvedExisting.Slug.ToLowerInvariant();
                                    if (!string.IsNullOrWhiteSpace(oldSlugKeyMerge))
                                        existingBySlug.Remove(oldSlugKeyMerge);

                                    string? oldNameKeyMerge = resolvedExisting.Name.Trim().ToLowerInvariant();
                                    if (!string.IsNullOrWhiteSpace(oldNameKeyMerge))
                                        allByName.Remove(oldNameKeyMerge);

                                    // Merge: update all fields since CSV is newer
                                    resolvedExisting.Name = item.Name;
                                    resolvedExisting.Slug = item.Slug;
                                    resolvedExisting.Description = item.Description;
                                    resolvedExisting.Parent = parentCategory;
                                    if (item.CreatedAt < DateTime.Now.ToLocalTime() &&
                                        item.CreatedAt < resolvedExisting.CreatedAt)
                                        resolvedExisting.CreatedAt = item.CreatedAt;
                                    resolvedExisting.UpdatedAt = item.UpdatedAt;

                                    // Update lookups with new slug and name
                                    existingBySlug[slugKey] = resolvedExisting;
                                    string newNameKeyMerge = item.Name.Trim().ToLowerInvariant();
                                    if (!string.IsNullOrWhiteSpace(newNameKeyMerge))
                                        allByName[newNameKeyMerge] = resolvedExisting;

                                    updated++;
                                }
                                else if (resolvedExisting != null)
                                {
                                    skipped++;
                                }
                                else
                                {
                                    // No existing category found, add as new
                                    BookCategory newCategory = new()
                                    {
                                        Name = item.Name,
                                        Slug = item.Slug,
                                        Description = item.Description,
                                        Parent = parentCategory,
                                        CreatedAt = item.CreatedAt,
                                        UpdatedAt = item.UpdatedAt
                                    };
                                    _db.BookCategories.Add(newCategory);
                                    added++;
                                    LoggerUtilities.Info("[Process] Merge added new category: " + item.Name);
                                    string newMergeNameKey = item.Name.Trim().ToLowerInvariant();
                                    if (!string.IsNullOrWhiteSpace(newMergeNameKey))
                                        allByName[newMergeNameKey] = newCategory;
                                    if (!string.IsNullOrWhiteSpace(slugKey)) addedBySlug[slugKey] = newCategory;
                                }

                                break;
                            default:
                                // Unknown action, skip it
                                skipped++;
                                LoggerUtilities.Warning("[Process] Unknown import action, skipped: " + item.Action);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        unresolved++;
                        LoggerUtilities.Error(ex, "[Danger] unresolved error for item: " + item.Name);
                    }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            LoggerUtilities.Info(
                $"[End] ImportConfirmBookCategoriesAsync added={added}, updated={updated}, skipped={skipped}, unresolved={unresolved}");
        }
        catch
        {
            if (items != null)
            {
                unresolved = items.Count;
                LoggerUtilities.Error("[Danger] ImportConfirmBookCategoriesAsync batch save failed. totalItems=" +
                                      items.Count);
            }
        }

        return (added, updated, skipped, unresolved);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportZip(string[]? ids)
    {
        LoggerUtilities.Info($"[Start] ExportZip requestedCount={ids?.Length ?? 0}");
        List<BookCategory> categoriesToExport;
        if (ids is { Length: > 0 })
            categoriesToExport = await GetBookCategoriesByIds(ids);
        else
            categoriesToExport = await GetAllCategories();

        // Create ZIP with CSV and integrity manifest
        using MemoryStream zipStream = new();
        using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Create, true))
        {
            // Create BookCategories.csv entry
            ZipArchiveEntry csvEntry = zipArchive.CreateEntry("BookCategories.csv");
            await using (Stream csvStream = csvEntry.Open())
            await using (StreamWriter streamWriter = new(csvStream, Encoding.UTF8))
            await using (CsvWriter csvWriter = new(streamWriter, CultureInfo.InvariantCulture))
            {
                csvWriter.WriteField("Name");
                csvWriter.WriteField("Slug");
                csvWriter.WriteField("Description");
                csvWriter.WriteField("ParentCategory");
                csvWriter.WriteField("CreatedAt");
                csvWriter.WriteField("UpdatedAt");
                await csvWriter.NextRecordAsync();

                foreach (BookCategory category in categoriesToExport)
                {
                    csvWriter.WriteField(category.Name);
                    csvWriter.WriteField(category.Slug);
                    csvWriter.WriteField(category.Description ?? string.Empty);
                    csvWriter.WriteField(category.Parent != null ? category.Parent.Name : "-");
                    csvWriter.WriteField(category.CreatedAt.ToUniversalTime().ToString("o"));
                    csvWriter.WriteField(category.UpdatedAt.ToUniversalTime().ToString("o"));
                    await csvWriter.NextRecordAsync();
                }

                await csvWriter.FlushAsync();
                await streamWriter.FlushAsync();
            }
        }

        // Generate integrity manifest for export verification
        zipStream.Position = 0;
        using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Update, true))
        {
            LoggerUtilities.Info("[Process] Generating export integrity manifest");

            // Compute SHA256 hash for BookCategories.csv
            ZipArchiveEntry? csvEntry = zipArchive.GetEntry("BookCategories.csv");
            string csvHash;
            if (csvEntry != null)
            {
                await using Stream entryStream = csvEntry.Open();
                using var sha256 = SHA256.Create();
                byte[] hashBytes = await sha256.ComputeHashAsync(entryStream);
                csvHash = Convert.ToBase64String(hashBytes);
                LoggerUtilities.Info($"[Process] Computed hash for BookCategories.csv: {csvHash.Substring(0, 16)}...");
            }
            else
            {
                throw new InvalidOperationException("Failed to create BookCategories.csv in ZIP archive");
            }

            // Generate export timestamp
            DateTime exportTimestamp = DateTime.UtcNow;

            // Compute integrity key: SHA256(hash + timestamp)
            string combinedData = $"{csvHash}|{exportTimestamp:o}";

            using var sha256Final = SHA256.Create();
            byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
            string integrityKey = Convert.ToBase64String(integrityBytes);

            // Create manifest object
            var manifest = new
            {
                Version = "1.0",
                ExportTimestamp = exportTimestamp.ToString("o"),
                CategoryCount = categoriesToExport.Count,
                FileHashes = new Dictionary<string, string>
                {
                    { "BookCategories.csv", csvHash }
                },
                IntegrityKey = integrityKey
            };

            // Write manifest to ZIP
            ZipArchiveEntry manifestEntry = zipArchive.CreateEntry(".epiread_manifest.json");
            await using (Stream manifestStream = manifestEntry.Open())
            await using (StreamWriter manifestWriter = new(manifestStream, Encoding.UTF8))
            {
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await manifestWriter.WriteAsync(manifestJson);
                await manifestWriter.FlushAsync();
            }

            LoggerUtilities.Info(
                $"[Process] Export manifest created with integrity key: {integrityKey.Substring(0, 16)}...");
        }

        LoggerUtilities.Info($"[End] ExportZip exportedCount={categoriesToExport.Count}");
        return zipStream.ToArray();
    }
}