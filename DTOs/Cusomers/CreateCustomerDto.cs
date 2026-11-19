
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.DTOs.Customers;

public class CreateCustomerDto
{
    [Required]
    [StringLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    public string? Notes { get; set; }
}
