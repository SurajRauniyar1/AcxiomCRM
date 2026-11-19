
using System.Security.Claims;
using AcxiomCRM.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UserManagementController : Controller
{
    private static readonly string[] AllowedRoles =
    {
        "Admin",
        "Manager",
        "SalesExecutive"
    };

    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // GET: /UserManagement
    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();

        var model = new UserManagementViewModel();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);

            var fullName = claims
                .FirstOrDefault(c => c.Type == "FullName")?.Value;

            model.Users.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = fullName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "No Role",
                IsActive = !await _userManager.IsLockedOutAsync(user)
            });
        }

        return View(model);
    }

    // GET: /UserManagement/Create
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    // POST: /UserManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role), "Please select a valid role.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role), "Selected role does not exist.");

            return View(model);
        }

        var email = model.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            ModelState.AddModelError(
                nameof(model.Email), "This email is already registered.");

            return View(model);
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(
            user, model.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        var claimResult = await _userManager.AddClaimAsync(
            user,
            new Claim("FullName", model.FullName.Trim()));

        if (!claimResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            foreach (var error in claimResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user, model.Role);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        TempData["Success"] = "User created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /UserManagement/EditRole/{id}
    public async Task<IActionResult> EditRole(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return View(new EditUserRoleViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty
        });
    }

    // POST: /UserManagement/EditRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(EditUserRoleViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role), "Please select a valid role.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role), "Selected role does not exist.");

            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);

        if (user == null)
            return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault();

        var currentUserId = _userManager.GetUserId(User);

        if (user.Id == currentUserId && currentRole == "Admin"
            && model.Role != "Admin")
        {
            TempData["Error"] =
                "You cannot remove your own Admin role.";

            return RedirectToAction(nameof(Index));
        }

        if (currentRole == "Admin" && model.Role != "Admin")
        {
            var adminCount = await GetAdminCountAsync();

            if (adminCount <= 1)
            {
                TempData["Error"] =
                    "You cannot remove the last Admin account.";

                return RedirectToAction(nameof(Index));
            }
        }

        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user, currentRoles);

            if (!removeResult.Succeeded)
            {
                TempData["Error"] =
                    string.Join("; ", removeResult.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Index));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(
            user, model.Role);

        if (!addResult.Succeeded)
        {
            TempData["Error"] =
                string.Join("; ", addResult.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "User role updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /UserManagement/ToggleStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        var currentUserId = _userManager.GetUserId(User);

        if (user.Id == currentUserId)
        {
            TempData["Error"] =
                "You cannot deactivate your own account.";

            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("Admin"))
        {
            var adminCount = await GetAdminCountAsync();

            if (adminCount <= 1)
            {
                TempData["Error"] =
                    "You cannot deactivate the last Admin account.";

                return RedirectToAction(nameof(Index));
            }
        }

        await _userManager.SetLockoutEnabledAsync(user, true);

        var isLockedOut = await _userManager.IsLockedOutAsync(user);

        IdentityResult result;

        if (isLockedOut)
        {
            result = await _userManager.SetLockoutEndDateAsync(
                user, null);
        }
        else
        {
            result = await _userManager.SetLockoutEndDateAsync(
                user, DateTimeOffset.MaxValue);
        }

        if (!result.Succeeded)
        {
            TempData["Error"] =
                string.Join("; ", result.Errors.Select(e => e.Description));
        }
        else
        {
            TempData["Success"] = isLockedOut
                ? "User account activated successfully."
                : "User account deactivated successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<int> GetAdminCountAsync()
    {
        var users = _userManager.Users.ToList();
        var count = 0;

        foreach (var user in users)
        {
            if (await _userManager.IsInRoleAsync(user, "Admin"))
                count++;
        }

        return count;
    }
}
