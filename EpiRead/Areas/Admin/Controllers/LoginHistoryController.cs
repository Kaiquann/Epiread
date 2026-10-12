using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Areas.Admin.Controllers;

/// <summary>
///     File: LoginHistoryController.cs
///     The controller in the Admin area that provides browsing, searching,
///     sorting and pagination for the application's login history records.
///     It is used by administrators to inspect login events including
///     IP, location and timestamp details.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.RequireAdminOrHigher)]
[Area("Admin")]
public class LoginHistoryController : Controller
{
    private readonly DB _db;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoginHistoryController" /> class.
    /// </summary>
    /// <param name="db">The application's database context used to access login history entries.</param>
    /// <remarks>
    ///     Guard clause ensures <paramref name="db" /> is not <c>null</c>.
    /// </remarks>
    public LoginHistoryController(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    ///     Returns a view or partial view containing a paginated list of
    ///     login history entries that match the provided search and sort
    ///     criteria.
    /// </summary>
    /// <param name="search">
    ///     Optional search string used to filter results by email, IP, location, organization and other
    ///     login-related fields.
    /// </param>
    /// <param name="sort">
    ///     Optional column name to sort by. Supported values are Email, Ip Address, City, Region, Country, Loc,
    ///     Org, Postal, Timezone and LoginTime. Defaults to LoginTime.
    /// </param>
    /// <param name="dir">Sort direction. Use 'des' for descending; any other value sorts ascending.</param>
    /// <param name="page">One-based page number for pagination. Values below 1 will redirect to page 1.</param>
    /// <returns>An <see cref="IActionResult" /> containing the (partial) view with the requested page of results.</returns>
    public async Task<IActionResult> Index(string? search, string? sort, string? dir, int page = 1)
    {
        ViewBag.Search = search = search?.Trim() ?? "";

        LoggerUtilities.Info("[Start] fetching login histories. search='" + search + "'");
        // DB operation: fetch matching login history entries
        List<LoginHistory> searched = await _db.LoginHistories
            .Where(ll => ll.Email.Contains(search) ||
                         ll.Ip!.Contains(search) ||
                         ll.City!.Contains(search) ||
                         ll.Region!.Contains(search) ||
                         ll.Country!.Contains(search) ||
                         ll.Loc!.Contains(search) ||
                         ll.Org!.Contains(search) ||
                         ll.Postal!.Contains(search) ||
                         ll.Timezone!.Contains(search)
            )
            .ToListAsync();

        LoggerUtilities.Info("[Process] fetched " + (searched?.Count ?? 0) + " login history entries.");

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, page = 1 });

        // Default to newest-first on first arrival if no sort parameters supplied
        if (string.IsNullOrWhiteSpace(sort)) sort = "Login Time";
        if (string.IsNullOrWhiteSpace(dir)) dir = "des";

        IPagedList<LoginHistory> model = SortAndPaginationLoginHistory(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, page = model.PageCount });

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for page " + page);
            return PartialView("LoginHistory/_Index", model);
        }

        LoggerUtilities.Info("[End] returning view for page " + page);
        return View(model);
    }

    /// <summary>
    ///     Sorts an in-memory collection of login history records and returns
    ///     a paginated <see cref="IPagedList{LoginHistory}" /> according to the
    ///     given sort column and direction.
    /// </summary>
    /// <param name="searched">
    ///     The list of login history items to be sorted and paged. May be <c>null</c>, in which case an
    ///     empty list is used.
    /// </param>
    /// <param name="sort">The name of the property to sort by. Falls back to LoginTime by default.</param>
    /// <param name="dir">Sort direction. "des" will sort descending; otherwise ascending.</param>
    /// <param name="page">The page number (1-based) to return in the paged list.</param>
    /// <returns>An <see cref="IPagedList{LoginHistory}" /> representing the requested page from the sorted results.</returns>
    private IPagedList<LoginHistory> SortAndPaginationLoginHistory(List<LoginHistory>? searched, string? sort,
        string? dir,
        int page = 1)
    {
        // Guard against null input (method is in-memory pagination/sorting)
        searched = searched ?? new List<LoginHistory>();

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<LoginHistory, object> fn = sort switch
        {
            "Email" => s => s.Email,
            "Ip Address" => s => s.Ip!,
            "City" => s => s.City!,
            "Region" => s => s.Region!,
            "Country" => s => s.Country!,
            "Loc" => s => s.Loc!,
            "Org" => s => s.Org!,
            "Postal" => s => s.Postal!,
            "Timezone" => s => s.Timezone!,
            "LoginTime" => s => s.LoginTime,
            _ => s => s.LoginTime
        };

        IOrderedEnumerable<LoginHistory> sorted = dir == "des"
            ? searched.AsEnumerable().OrderByDescending(fn)
            : searched.AsEnumerable().OrderBy(fn);

        // (3) Paging ---------------------------
        IPagedList<LoginHistory> pagedList = sorted.ToPagedList(page, 10);

        return pagedList;
    }
}