
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.Customers;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    public CustomersApiController(
        ApplicationDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool IsAdmin => User.IsInRole("Admin");

    // =====================================
    // ACCESSIBLE CUSTOMER QUERY
    // =====================================

    private IQueryable<Customer> AccessibleCustomers()
    {
        var query = _context.Customers.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(c => c.OwnerId == userId);
        }

        return query;
    }

    // =====================================
    // GET: api/customers
    // =====================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await AccessibleCustomers()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerResponseDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();

        return Ok(customers);
    }

    // =====================================
    // GET: api/customers/{id}
    // =====================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await AccessibleCustomers()
            .AsNoTracking()
            .Where(c => c.CustomerId == id)
            .Select(c => new CustomerResponseDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound(new
            {
                message = "Customer not found."
            });
        }

        return Ok(customer);
    }

    // =====================================
    // POST: api/customers
    // =====================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerName))
        {
            return BadRequest(new
            {
                message = "Customer name is required."
            });
        }

        var email = dto.Email?.Trim() ?? string.Empty;
        var phone = dto.Phone?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(email) &&
            await _context.Customers.AnyAsync(c => c.Email == email))
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(phone) &&
            await _context.Customers.AnyAsync(c => c.Phone == phone))
        {
            return Conflict(new
            {
                message = "Phone already exists."
            });
        }

        var customer = new Customer
        {
            CustomerCode = $"CUS-TMP-{Guid.NewGuid():N}",

            CustomerName = dto.CustomerName.Trim(),
            Email = email,
            Phone = phone,
            CompanyName = dto.CompanyName?.Trim() ?? string.Empty,
            Address = dto.Address?.Trim() ?? string.Empty,
            City = dto.City?.Trim(),
            State = dto.State?.Trim(),

            Status = string.IsNullOrWhiteSpace(dto.Status)
                ? "Active"
                : dto.Status.Trim(),

            Notes = dto.Notes,

            OwnerId = CurrentUserId,
            CreatedBy = CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            customer.CustomerCode =
                $"CUS-{customer.CustomerId:D5}";

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                newValue: new
                {
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.Email,
                    customer.Phone,
                    customer.OwnerId
                });

            await transaction.CommitAsync();

            var response = new CustomerResponseDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate
            };

            return CreatedAtAction(
                nameof(GetById),
                new { id = customer.CustomerId },
                response);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================
    // PUT: api/customers/{id}
    // =====================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateCustomerDto dto)
    {
        var customer = await AccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "Customer not found."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.CustomerName))
        {
            return BadRequest(new
            {
                message = "Customer name is required."
            });
        }

        var email = dto.Email?.Trim() ?? string.Empty;
        var phone = dto.Phone?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(email) &&
            await _context.Customers.AnyAsync(c =>
                c.CustomerId != id && c.Email == email))
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(phone) &&
            await _context.Customers.AnyAsync(c =>
                c.CustomerId != id && c.Phone == phone))
        {
            return Conflict(new
            {
                message = "Phone already exists."
            });
        }

        var oldValue = new
        {
            customer.CustomerName,
            customer.Email,
            customer.Phone,
            customer.CompanyName,
            customer.Address,
            customer.City,
            customer.State,
            customer.Status,
            customer.Notes
        };

        var newValue = new
        {
            CustomerName = dto.CustomerName.Trim(),
            Email = email,
            Phone = phone,
            CompanyName = dto.CompanyName?.Trim() ?? string.Empty,
            Address = dto.Address?.Trim() ?? string.Empty,
            City = dto.City?.Trim(),
            State = dto.State?.Trim(),
            Status = dto.Status?.Trim() ?? customer.Status,
            Notes = dto.Notes
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            customer.CustomerName = newValue.CustomerName;
            customer.Email = newValue.Email;
            customer.Phone = newValue.Phone;
            customer.CompanyName = newValue.CompanyName;
            customer.Address = newValue.Address;
            customer.City = newValue.City;
            customer.State = newValue.State;
            customer.Status = newValue.Status;
            customer.Notes = newValue.Notes;
            customer.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Update",
                "Customer",
                customer.CustomerId.ToString(),
                oldValue,
                newValue);

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Customer updated successfully."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================
    // DELETE: api/customers/{id}
    // ADMIN ONLY
    // =====================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "Customer not found."
            });
        }

        var oldValue = new
        {
            customer.CustomerCode,
            customer.CustomerName,
            customer.Email,
            customer.Phone,
            customer.CompanyName,
            customer.OwnerId
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Delete",
                "Customer",
                customer.CustomerId.ToString(),
                oldValue);

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Customer deleted successfully."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
