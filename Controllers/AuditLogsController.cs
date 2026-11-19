
using AcxiomCRM.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    private const int PageSize = 15;

    public AuditLogsController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: /AuditLogs
    public async Task<IActionResult> Index(
        string? search,
        [FromQuery(Name = "action")] string? actionFilter,
        string? entity,
        string? userId,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1)
    {
        // Database diagnostics
        ViewBag.DatabaseName =
            _context.Database.GetDbConnection().Database;

        ViewBag.DatabaseServer =
            _context.Database.GetDbConnection().DataSource;

        ViewBag.AuditCount =
            await _context.AuditLogs.CountAsync();

        // Base query
        var query = _context.AuditLogs.AsNoTracking();

        // General search
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(a =>
                a.EntityName.Contains(search) ||
                a.RecordId.Contains(search) ||
                (a.IpAddress != null &&
                 a.IpAddress.Contains(search)));
        }

        // Action filter
        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a =>
                a.Action == actionFilter);
        }

        // Entity filter
        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(a =>
                a.EntityName == entity);
        }

        // User filter
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a =>
                a.UserId == userId);
        }

        // From date
        if (fromDate.HasValue)
        {
            var startDate = DateTime.SpecifyKind(
                fromDate.Value.Date,
                DateTimeKind.Utc);

            query = query.Where(a =>
                a.CreatedDate >= startDate);
        }

        // To date (inclusive)
        if (toDate.HasValue)
        {
            var endDate = DateTime.SpecifyKind(
                toDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);

            query = query.Where(a =>
                a.CreatedDate < endDate);
        }

        // Total filtered records
        var totalRecords = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)PageSize);

        page = Math.Max(
            1,
            Math.Min(page, Math.Max(totalPages, 1)));

        // Pagination
        var auditLogs = await query
            .OrderByDescending(a => a.CreatedDate)
            .ThenByDescending(a => a.AuditLogId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Resolve user emails
        var userIds = auditLogs
            .Where(a => !string.IsNullOrEmpty(a.UserId))
            .Select(a => a.UserId!)
            .Distinct()
            .ToList();

        ViewBag.UserNames = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.Email ?? u.UserName ?? u.Id);

        // Action dropdown
        ViewBag.Actions = await _context.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        // Entity dropdown
        ViewBag.Entities = await _context.AuditLogs
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        // User dropdown
        ViewBag.Users = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                Email = u.Email ?? u.UserName ?? u.Id
            })
            .ToListAsync();

        // Pagination information
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;

        // Preserve selected filters
        ViewData["Search"] = search;
        ViewData["Action"] = actionFilter;
        ViewData["Entity"] = entity;
        ViewData["UserId"] = userId;
        ViewData["FromDate"] =
            fromDate?.ToString("yyyy-MM-dd");
        ViewData["ToDate"] =
            toDate?.ToString("yyyy-MM-dd");

        return View(auditLogs);
    }

    // GET: /AuditLogs/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var auditLog = await _context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.AuditLogId == id);

        if (auditLog == null)
            return NotFound();

        // Resolve user email
        if (!string.IsNullOrEmpty(auditLog.UserId))
        {
            var user = await _userManager.FindByIdAsync(
                auditLog.UserId);

            ViewBag.UserEmail =
                user?.Email ?? "User no longer exists";
        }
        else
        {
            ViewBag.UserEmail = "System / Unknown";
        }

        return View(auditLog);
    }
}
