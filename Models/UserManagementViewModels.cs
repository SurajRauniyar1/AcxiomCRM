using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.ViewModels;

public class UserManagementViewModel
{
    public List<UserListItemViewModel> Users { get; set; } = new();
}

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = "No Role";

    public bool IsActive { get; set; }
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "SalesExecutive";
}

public class EditUserRoleViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}