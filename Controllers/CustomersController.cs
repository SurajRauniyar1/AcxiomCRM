
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ApplicationDbContext context,
        AuditLogService auditLogService,
        ILogger<CustomersController> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User ID not found.");

    // Admin can access all customers.
    // Manager and SalesExecutive can access their own customers.
    private IQueryable<Customer> GetAccessibleCustomers()
    {
        var query = _context.Customers.AsQueryable();

        if (User.IsInRole("Admin"))
            return query;

        return query.Where(c => c.OwnerId == CurrentUserId);
    }

    // GET: /Customers
    public async Task<IActionResult> Index(string? search)
    {
        var customers = GetAccessibleCustomers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            customers = customers.Where(c =>
                c.CustomerName.Contains(search) ||
                c.Email.Contains(search) ||
                c.Phone.Contains(search) ||
                c.CustomerCode.Contains(search) ||
                (c.CompanyName != null &&
                 c.CompanyName.Contains(search)));
        }

        ViewData["Search"] = search;

        var result = await customers
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        return View(result);
    }

    // GET: /Customers/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var customer = await GetAccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        return View(customer);
    }

    // GET: /Customers/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Customers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        ModelState.Remove(nameof(Customer.CustomerCode));
        ModelState.Remove(nameof(Customer.OwnerId));
        ModelState.Remove(nameof(Customer.CreatedDate));
        ModelState.Remove(nameof(Customer.CreatedBy));
        ModelState.Remove(nameof(Customer.ModifiedDate));

        customer.Email = customer.Email?.Trim() ?? string.Empty;
        customer.Phone = customer.Phone?.Trim() ?? string.Empty;

        if (await _context.Customers.AnyAsync(c =>
            c.Email == customer.Email))
        {
            ModelState.AddModelError(
                nameof(customer.Email),
                "This email address is already registered.");
        }

        if (await _context.Customers.AnyAsync(c =>
            c.Phone == customer.Phone))
        {
            ModelState.AddModelError(
                nameof(customer.Phone),
                "This phone number is already registered.");
        }

        if (!ModelState.IsValid)
            return View(customer);

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            customer.CustomerCode =
                $"TMP-{Guid.NewGuid():N}"[..20];

            customer.OwnerId = CurrentUserId;
            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = User.Identity?.Name;
            customer.ModifiedDate = null;

            _context.Customers.Add(customer);

            await _context.SaveChangesAsync();

            customer.CustomerCode =
                $"CUST-{customer.CustomerId:D4}";

            await _context.SaveChangesAsync();

            // Record customer creation.
            await _auditLogService.LogAsync(
                action: "Create",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                newValue: new
                {
                    customer.CustomerId,
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.Email,
                    customer.Phone,
                    customer.CompanyName,
                    customer.Address,
                    customer.City,
                    customer.State,
                    customer.Status,
                    customer.OwnerId
                });

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Customer created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, "Error creating customer.");

            _context.Entry(customer).State =
                EntityState.Detached;

            ModelState.AddModelError(
                string.Empty,
                "Unable to create the customer. Please try again.");

            return View(customer);
        }
    }

    // GET: /Customers/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var customer = await GetAccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        return View(customer);
    }

    // POST: /Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        Customer customer)
    {
        if (id != customer.CustomerId)
            return NotFound();

        ModelState.Remove(nameof(Customer.CustomerCode));
        ModelState.Remove(nameof(Customer.OwnerId));
        ModelState.Remove(nameof(Customer.CreatedDate));
        ModelState.Remove(nameof(Customer.CreatedBy));
        ModelState.Remove(nameof(Customer.ModifiedDate));

        customer.Email = customer.Email?.Trim() ?? string.Empty;
        customer.Phone = customer.Phone?.Trim() ?? string.Empty;

        if (await _context.Customers.AnyAsync(c =>
            c.Email == customer.Email &&
            c.CustomerId != id))
        {
            ModelState.AddModelError(
                nameof(customer.Email),
                "This email address is already used by another customer.");
        }

        if (await _context.Customers.AnyAsync(c =>
            c.Phone == customer.Phone &&
            c.CustomerId != id))
        {
            ModelState.AddModelError(
                nameof(customer.Phone),
                "This phone number is already used by another customer.");
        }

        var existingCustomer = await GetAccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (existingCustomer == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            customer.CustomerCode = existingCustomer.CustomerCode;
            customer.OwnerId = existingCustomer.OwnerId;
            customer.CreatedDate = existingCustomer.CreatedDate;
            customer.CreatedBy = existingCustomer.CreatedBy;

            return View(customer);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Capture previous values before modifying the entity.
            var oldValue = new
            {
                existingCustomer.CustomerName,
                existingCustomer.Email,
                existingCustomer.Phone,
                existingCustomer.CompanyName,
                existingCustomer.Address,
                existingCustomer.City,
                existingCustomer.State,
                existingCustomer.Status,
                existingCustomer.Notes
            };

            existingCustomer.CustomerName = customer.CustomerName;
            existingCustomer.Email = customer.Email;
            existingCustomer.Phone = customer.Phone;
            existingCustomer.CompanyName = customer.CompanyName;
            existingCustomer.Address = customer.Address;
            existingCustomer.City = customer.City;
            existingCustomer.State = customer.State;
            existingCustomer.Status = customer.Status;
            existingCustomer.Notes = customer.Notes;

            existingCustomer.ModifiedDate = DateTime.UtcNow;

            var newValue = new
            {
                existingCustomer.CustomerName,
                existingCustomer.Email,
                existingCustomer.Phone,
                existingCustomer.CompanyName,
                existingCustomer.Address,
                existingCustomer.City,
                existingCustomer.State,
                existingCustomer.Status,
                existingCustomer.Notes
            };

            await _context.SaveChangesAsync();

            // Record previous and updated values.
            await _auditLogService.LogAsync(
                action: "Update",
                entityName: "Customer",
                recordId: existingCustomer.CustomerId.ToString(),
                oldValue: oldValue,
                newValue: newValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Customer updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(
                ex,
                "Error updating customer {CustomerId}.",
                id);

            ModelState.AddModelError(
                string.Empty,
                "Unable to update the customer. Please try again.");

            return View(customer);
        }
    }

    // GET: /Customers/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var customer = await GetAccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        return View(customer);
    }

    // POST: /Customers/Delete/5
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await GetAccessibleCustomers()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Preserve customer information before deletion.
            var oldValue = new
            {
                customer.CustomerId,
                customer.CustomerCode,
                customer.CustomerName,
                customer.Email,
                customer.Phone,
                customer.CompanyName,
                customer.Address,
                customer.City,
                customer.State,
                customer.Status,
                customer.OwnerId
            };

            // Record deletion before removing the customer.
            await _auditLogService.LogAsync(
                action: "Delete",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                oldValue: oldValue);

            _context.Customers.Remove(customer);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Customer deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(
                ex,
                "Error deleting customer {CustomerId}.",
                id);

            TempData["ErrorMessage"] =
                "Unable to delete the customer. Please try again.";

            return RedirectToAction(nameof(Index));
        }
    }
}
