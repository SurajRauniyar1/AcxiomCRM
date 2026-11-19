
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.Activities;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/activities")]
[Authorize]
public class ActivitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] AllowedTypes =
    {
        "Call",
        "Meeting",
        "Email",
        "Task"
    };

    public ActivitiesApiController(
        ApplicationDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool IsAdmin =>
        User.IsInRole("Admin");

    private IQueryable<Activity> AccessibleActivities()
    {
        var query = _context.Activities.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(x => x.AssignedTo == userId);
        }

        return query;
    }

    private static ActivityResponseDto ToResponse(Activity activity)
    {
        return new ActivityResponseDto
        {
            ActivityId = activity.ActivityId,
            ActivityType = activity.ActivityType,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            LeadId = activity.LeadId,
            AssignedTo = activity.AssignedTo,
            Status = activity.Status,
            CreatedDate = activity.CreatedDate
        };
    }

    private static bool IsValidType(string type)
    {
        return AllowedTypes.Any(
            x => x.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime AsUtc(DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    private async Task<bool> HasAccessibleRelatedRecord(
        int? customerId,
        int? leadId)
    {
        // An activity must belong to exactly one Customer or Lead.
        if ((customerId.HasValue && leadId.HasValue) ||
            (!customerId.HasValue && !leadId.HasValue))
        {
            return false;
        }

        if (customerId.HasValue)
        {
            var customerQuery = _context.Customers
                .Where(x => x.CustomerId == customerId.Value);

            if (!IsAdmin)
            {
                customerQuery = customerQuery.Where(
                    x => x.OwnerId == CurrentUserId);
            }

            return await customerQuery.AnyAsync();
        }

        var leadQuery = _context.Leads
            .Where(x => x.LeadId == leadId!.Value);

        if (!IsAdmin)
        {
            leadQuery = leadQuery.Where(
                x => x.AssignedTo == CurrentUserId);
        }

        return await leadQuery.AnyAsync();
    }

    // GET: api/activities
    // Optional filters: type, status, fromDate, toDate
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var query = AccessibleActivities();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(
                x => x.ActivityType == type.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(
                x => x.Status == status.Trim());
        }

        if (fromDate.HasValue)
        {
            var start = AsUtc(fromDate.Value);
            query = query.Where(x => x.ActivityDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = AsUtc(toDate.Value);
            query = query.Where(x => x.ActivityDate <= end);
        }

        var activities = await query
            .OrderByDescending(x => x.ActivityDate)
            .ToListAsync();

        return Ok(activities.Select(ToResponse));
    }

    // GET: api/activities/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var activity = await AccessibleActivities()
            .FirstOrDefaultAsync(x => x.ActivityId == id);

        if (activity == null)
            return NotFound(new { message = "Activity not found." });

        return Ok(ToResponse(activity));
    }

    // POST: api/activities
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateActivityDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        if (!IsValidType(dto.ActivityType))
        {
            return BadRequest(new
            {
                message = "Invalid activity type.",
                allowedTypes = AllowedTypes
            });
        }

        if (!await HasAccessibleRelatedRecord(
            dto.CustomerId,
            dto.LeadId))
        {
            return BadRequest(new
            {
                message = "Select exactly one accessible Customer or Lead."
            });
        }

        var activity = new Activity
        {
            ActivityType = dto.ActivityType.Trim(),
            Subject = dto.Subject.Trim(),
            Description = dto.Description?.Trim(),
            ActivityDate = AsUtc(dto.ActivityDate),
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            AssignedTo = CurrentUserId,
            Status = dto.Status.Trim(),
            CreatedBy = User.Identity?.Name,
            CreatedDate = DateTime.UtcNow
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                null,
                new
                {
                    activity.ActivityType,
                    activity.Subject,
                    activity.CustomerId,
                    activity.LeadId,
                    activity.ActivityDate,
                    activity.Status
                });

            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = activity.ActivityId },
                ToResponse(activity));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // PUT: api/activities/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateActivityDto dto)
    {
        var activity = await AccessibleActivities()
            .FirstOrDefaultAsync(x => x.ActivityId == id);

        if (activity == null)
            return NotFound(new { message = "Activity not found." });

        if (!IsValidType(dto.ActivityType))
        {
            return BadRequest(new
            {
                message = "Invalid activity type.",
                allowedTypes = AllowedTypes
            });
        }

        if (!await HasAccessibleRelatedRecord(
            dto.CustomerId,
            dto.LeadId))
        {
            return BadRequest(new
            {
                message = "Select exactly one accessible Customer or Lead."
            });
        }

        var oldValue = new
        {
            activity.ActivityType,
            activity.Subject,
            activity.Description,
            activity.ActivityDate,
            activity.CustomerId,
            activity.LeadId,
            activity.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            activity.ActivityType = dto.ActivityType.Trim();
            activity.Subject = dto.Subject.Trim();
            activity.Description = dto.Description?.Trim();
            activity.ActivityDate = AsUtc(dto.ActivityDate);
            activity.CustomerId = dto.CustomerId;
            activity.LeadId = dto.LeadId;
            activity.Status = dto.Status.Trim();
            activity.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newValue = new
            {
                activity.ActivityType,
                activity.Subject,
                activity.Description,
                activity.ActivityDate,
                activity.CustomerId,
                activity.LeadId,
                activity.Status
            };

            await _auditLogService.LogAsync(
                "Update",
                "Activity",
                activity.ActivityId.ToString(),
                oldValue,
                newValue);

            await transaction.CommitAsync();

            return Ok(ToResponse(activity));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // DELETE: api/activities/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(x => x.ActivityId == id);

        if (activity == null)
            return NotFound(new { message = "Activity not found." });

        var oldValue = new
        {
            activity.ActivityType,
            activity.Subject,
            activity.Description,
            activity.CustomerId,
            activity.LeadId,
            activity.ActivityDate,
            activity.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Delete",
                "Activity",
                activity.ActivityId.ToString(),
                oldValue,
                null);

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Activity deleted successfully."
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message = "This activity cannot be deleted because related records exist."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
