
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models;

public class Lead
{
    public int LeadId { get; set; }

    [Required]
    [StringLength(20)]
    public string LeadCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required]
    [StringLength(100)]
    public string Source { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "New";

    [Column(TypeName = "numeric(18,2)")]
    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Expected Value")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    // Assigned sales executive's Identity user ID
    public string? AssignedTo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    // Lead conversion tracking
    public bool IsConverted { get; set; } = false;

    public int? ConvertedCustomerId { get; set; }
}
