
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.Opportunities;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] AllowedStages =
    {
        "Qualification",
        "Proposal",
        "Negotiation",
        "Won",
        "Lost"
    };

    private static readonly string[] AllowedStatuses =
    {
        "Open",
        "Won",
        "Lost",
        "Closed"
    };

    public OpportunitiesApiController(
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

    private IQueryable<Opportunity> AccessibleOpportunities()
    {
        var query = _context.Opportunities.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(x => x.AssignedTo == userId);
        }

        return query;
    }

    private static OpportunityResponseDto ToResponse(
        Opportunity opportunity)
    {
        return new OpportunityResponseDto
        {
            OpportunityId = opportunity.OpportunityId,
            OpportunityName = opportunity.OpportunityName,
            CustomerId = opportunity.CustomerId,
            LeadId = opportunity.LeadId,
            Amount = opportunity.Amount,
            Stage = opportunity.Stage,
            Probability = opportunity.Probability,
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            Status = opportunity.Status,
            WeightedValue = opportunity.WeightedValue,
            CreatedDate = opportunity.CreatedDate
        };
    }

    private static bool IsValidStage(string stage)
    {
        return AllowedStages.Any(
            x => x.Equals(stage, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidStatus(string status)
    {
        return AllowedStatuses.Any(
            x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> HasAccessibleRelatedRecord(
        int? customerId,
        int? leadId)
    {
        // Exactly one relationship is required.
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

    // GET: api/opportunities
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var opportunities = await AccessibleOpportunities()
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return Ok(opportunities.Select(ToResponse));
    }

    // GET: api/opportunities/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var opportunity = await AccessibleOpportunities()
            .FirstOrDefaultAsync(x => x.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        return Ok(ToResponse(opportunity));
    }

    // POST: api/opportunities
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOpportunityDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        if (!IsValidStage(dto.Stage))
        {
            return BadRequest(new
            {
                message = "Invalid opportunity stage.",
                allowedStages = AllowedStages
            });
        }

        if (!IsValidStatus(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid opportunity status.",
                allowedStatuses = AllowedStatuses
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

        if (dto.ExpectedCloseDate.HasValue &&
            dto.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new
            {
                message = "Expected close date cannot be in the past."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var opportunity = new Opportunity
            {
                OpportunityName = dto.OpportunityName.Trim(),
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage.Trim(),
                Probability = dto.Probability,

                // Default to 30 days from today when no date is supplied.
                ExpectedCloseDate = dto.ExpectedCloseDate
                    ?? DateTime.UtcNow.AddDays(30),

                Status = dto.Status.Trim(),
                AssignedTo = CurrentUserId,
                CreatedBy = User.Identity?.Name,
                CreatedDate = DateTime.UtcNow
            };

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                null,
                new
                {
                    opportunity.OpportunityName,
                    opportunity.CustomerId,
                    opportunity.LeadId,
                    opportunity.Amount,
                    opportunity.Stage,
                    opportunity.Probability,
                    opportunity.Status
                });

            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = opportunity.OpportunityId },
                ToResponse(opportunity));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // PUT: api/opportunities/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateOpportunityDto dto)
    {
        var opportunity = await AccessibleOpportunities()
            .FirstOrDefaultAsync(x => x.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        if (!IsValidStage(dto.Stage))
        {
            return BadRequest(new
            {
                message = "Invalid opportunity stage.",
                allowedStages = AllowedStages
            });
        }

        if (!IsValidStatus(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid opportunity status.",
                allowedStatuses = AllowedStatuses
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

        if (dto.ExpectedCloseDate.HasValue &&
            dto.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new
            {
                message = "Expected close date cannot be in the past."
            });
        }

        var oldValue = new
        {
            opportunity.OpportunityName,
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.Amount,
            opportunity.Stage,
            opportunity.Probability,
            opportunity.ExpectedCloseDate,
            opportunity.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            opportunity.OpportunityName = dto.OpportunityName.Trim();
            opportunity.CustomerId = dto.CustomerId;
            opportunity.LeadId = dto.LeadId;
            opportunity.Amount = dto.Amount;
            opportunity.Stage = dto.Stage.Trim();
            opportunity.Probability = dto.Probability;

            // Preserve the existing date when no new date is supplied.
            opportunity.ExpectedCloseDate =
                dto.ExpectedCloseDate ?? opportunity.ExpectedCloseDate;

            opportunity.Status = dto.Status.Trim();
            opportunity.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newValue = new
            {
                opportunity.OpportunityName,
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.Amount,
                opportunity.Stage,
                opportunity.Probability,
                opportunity.ExpectedCloseDate,
                opportunity.Status
            };

            await _auditLogService.LogAsync(
                "Update",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                oldValue,
                newValue);

            await transaction.CommitAsync();

            return Ok(ToResponse(opportunity));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // DELETE: api/opportunities/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(x => x.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        var oldValue = new
        {
            opportunity.OpportunityName,
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.Amount,
            opportunity.Stage,
            opportunity.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Opportunities.Remove(opportunity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Delete",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                oldValue,
                null);

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Opportunity deleted successfully."
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message = "This opportunity cannot be deleted because related records exist."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
