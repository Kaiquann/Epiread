using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

/**
 * @author: Chong Jun Xiang
 */
[Authorize(Policy = AuthorizationPolicies.AuthenticatedUsersOnly)]
public class LoginHistoryController : Controller
{
    private readonly DB _db;

    public LoginHistoryController(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IActionResult> Index(string? search, string? sort, string? dir, int page = 1)
    {
        LoggerUtilities.Info($"[Start] Fetching login history search={search} page={page}");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        ViewBag.Search = search = search?.Trim() ?? "";

        List<LoginHistory> searched = await _db.LoginHistories
            .Where(ll => (ll.Email.Contains(search) ||
                          ll.Ip!.Contains(search) ||
                          ll.City!.Contains(search) ||
                          ll.Region!.Contains(search) ||
                          ll.Country!.Contains(search) ||
                          ll.Loc!.Contains(search) ||
                          ll.Org!.Contains(search) ||
                          ll.Postal!.Contains(search) ||
                          ll.Timezone!.Contains(search)) &&
                         ll.UserId == currentUser.Id)
            .ToListAsync();

        LoggerUtilities.Info($"[Process] Retrieved {searched.Count} login history records");

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, page = 1 });

        // Ensure default ordering is newest-first when user first arrives without query params.
        if (string.IsNullOrWhiteSpace(sort)) sort = "Login Time";
        if (string.IsNullOrWhiteSpace(dir)) dir = "des";

        IPagedList<LoginHistory> model = SortAndPagination(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, page = model.PageCount });

        LoggerUtilities.Info($"[End] Returning login history pageCount={model.PageCount} currentPage={page}");
        if (Request.IsAjax()) return PartialView("LoginHistory/_Index", model);

        return View(model);
    }

    /**
     * @description: Sort and Pagination
     */
    private IPagedList<LoginHistory> SortAndPagination(List<LoginHistory> searched, string? sort, string? dir,
        int page = 1)
    {
        // (2) Sorting --------------------------
        // Default to showing most recent logins first when no explicit sort/direction provided.
        if (string.IsNullOrWhiteSpace(sort))
        {
            // Use the header label as the default so the header link is highlighted correctly
            sort = "Login Time";
            if (string.IsNullOrWhiteSpace(dir)) dir = "des"; // default descending
        }

        // If sorting by Login Time or LoginTime but no dir supplied, prefer descending
        if ((sort == "Login Time" || sort == "LoginTime") && string.IsNullOrWhiteSpace(dir)) dir = "des";

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
            "Login Time" => s => s.LoginTime,
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