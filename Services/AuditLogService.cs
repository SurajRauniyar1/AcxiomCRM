
using System.Text.Json;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Web.Services;

public class AuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string recordId,
        object? oldValue = null,
        object? newValue = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var userId = user?.Identity?.IsAuthenticated == true
            ? _userManager.GetUserId(user)
            : null;

        var ipAddress = httpContext?.Connection
            .RemoteIpAddress?.ToString();

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue == null
                ? null
                : JsonSerializer.Serialize(oldValue),
            NewValue = newValue == null
                ? null
                : JsonSerializer.Serialize(newValue),
            CreatedDate = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }
}
