using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead.Services.Interfaces;

/// <summary>
///     Service interface for managing book categories including CRUD, validation,
///     import/export, and pagination/sorting helpers.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IBookCategoryService
{
    /// <summary>
    ///     CRUD Operations - basic create, update, delete for book categories.
    /// </summary>
    Task<(bool Success, string Message, BookCategory? Category)> CreateBookCategoryAsync(
        string name, string slug, string? description, int? parentId);

    /// <summary>
    ///     Update an existing book category with validation.
    /// </summary>
    Task<(bool Success, string Message)> UpdateBookCategoryAsync(
        int id, string name, string slug, string? description, int? parentId);

    /// <summary>
    ///     Delete book categories by IDs with transaction support.
    /// </summary>
    Task<(bool Success, string Message, int DeletedCount)> DeleteBookCategoriesAsync(string[] ids);

    /// <summary>
    ///     Reassign subcategories and books to General category before deletion.
    ///     (Helper used during Delete operations)
    /// </summary>
    Task ReassignSubcategoriesAndBooksToGeneralAsync(string[]? filteredIds, BookCategory? generalCategory);

    /// <summary>
    ///     Query Operations - read-only lookups for categories.
    /// </summary>
    Task<List<BookCategory>> GetAllCategories();

    /// <summary>
    ///     Get book category by ID with related data.
    /// </summary>
    Task<BookCategory?> GetBookCategoryById(int id);

    /// <summary>
    ///     Search book categories by name, slug, or description.
    /// </summary>
    Task<List<BookCategory>> SearchBookCategories(string searchTerm);

    /// <summary>
    ///     Get the General (default) category.
    /// </summary>
    Task<BookCategory?> GetGeneralCategory();

    /// <summary>
    ///     Get book categories by their IDs.
    /// </summary>
    Task<List<BookCategory>> GetBookCategoriesByIds(string[] ids);

    /// <summary>
    ///     Validation Operations - helper methods to validate input and detect conflicts.
    /// </summary>
    /// <summary>
    ///     Validate book category slug (format and uniqueness).
    ///     Slug must be lowercase, no spaces, and match the pattern: letters, numbers, hyphens.
    /// </summary>
    Task<string> ValidateBookCategorySlug(string slug, int? currentId = null);

    /// <summary>
    ///     Validate if a parent category ID is valid.
    /// </summary>
    Task<bool> IsValidParentCategory(int? parentId);

    /// <summary>
    ///     Get all descendant category IDs recursively.
    /// </summary>
    Task<HashSet<int>> GetAllDescendantCategoryIds(int categoryId);

    /// <summary>
    ///     Build parent category select list excluding current category and its descendants.
    ///     (Pagination & Sorting helper)
    /// </summary>
    Task<SelectList> GetParentCategorySelectList(int? currentCategoryId = null);

    /// <summary>
    ///     Import/Export Operations - CSV helpers.
    /// </summary>
    Task<BookCategoryImportConflictVm> ProcessCsvImportAsync(List<BookCategoryImportRow> importRows);

    /// <summary>
    ///     Confirm and complete CSV import operation.
    /// </summary>
    Task<(int Added, int Updated, int Skipped, int Unresolved)> ImportConfirmBookCategoriesAsync(
        List<BookCategoryImportConfirmItemVm> items);

    /// <summary>
    ///     Export book categories to CSV format.
    /// </summary>
    Task<byte[]> ExportZip(string[] ids);
}