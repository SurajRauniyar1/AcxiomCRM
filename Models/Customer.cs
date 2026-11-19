
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

public class Customer
{
    public int CustomerId { get; set; }

    [Required]
    [StringLength(20)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";

    [StringLength(1000)]
    public string? Notes { get; set; }

    // Assigned sales executive
    public string? OwnerId { get; set; }

    // Audit information
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
