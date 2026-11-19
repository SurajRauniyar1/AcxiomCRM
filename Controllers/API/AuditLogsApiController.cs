
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/auditlogs")]
[Authorize(Roles = "Admin")]
public class AuditLogsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuditLogsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/auditlogs
    // Optional filters: action, entityName, userId, fromDate, toDate
    // Pagination: page, pageSize
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery(Name = "action")] string? actionFilter,
        [FromQuery] string? entityName,
        [FromQuery] string? userId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1)
            return BadRequest(new { message = "Page must be at least 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });

        if (fromDate.HasValue && toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "From date cannot be later than to date."
            });
        }

        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            var action = actionFilter.Trim();

            query = query.Where(
                x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var entity = entityName.Trim();

            query = query.Where(
                x => x.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = userId.Trim();

            query = query.Where(
                x => x.UserId == user);
        }

        if (fromDate.HasValue)
        {
            var startDate = DateTime.SpecifyKind(
                fromDate.Value.Date,
                DateTimeKind.Utc);

            query = query.Where(
                x => x.CreatedDate >= startDate);
        }

        if (toDate.HasValue)
        {
            var endDate = DateTime.SpecifyKind(
                toDate.Value.Date.AddDays(1),
                DateTimeKind.Utc);

            query = query.Where(
                x => x.CreatedDate < endDate);
        }

        var totalRecords = await query.CountAsync();

        var auditLogs = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogResponseDto
            {
                AuditLogId = x.AuditLogId,
                UserId = x.UserId,
                Action = x.Action,
                EntityName = x.EntityName,
                RecordId = x.RecordId,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                CreatedDate = x.CreatedDate,
                IpAddress = x.IpAddress
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalRecords,
            totalPages = (int)Math.Ceiling(
                totalRecords / (double)pageSize),
            data = auditLogs
        });
    }

    // GET: api/auditlogs/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var auditLog = await _context.AuditLogs
            .AsNoTracking()
            .Where(x => x.AuditLogId == id)
            .Select(x => new AuditLogResponseDto
            {
                AuditLogId = x.AuditLogId,
                UserId = x.UserId,
                Action = x.Action,
                EntityName = x.EntityName,
                RecordId = x.RecordId,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                CreatedDate = x.CreatedDate,
                IpAddress = x.IpAddress
            })
            .FirstOrDefaultAsync();

        if (auditLog == null)
            return NotFound(new { message = "Audit log not found." });

        return Ok(auditLog);
    }
}
