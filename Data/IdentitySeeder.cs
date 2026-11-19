
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Web.Data;

public static class IdentitySeeder
{
    // Create the default application roles.
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager)
    {
        string[] roles =
        {
            "Admin",
            "Manager",
            "SalesExecutive"
        };

        foreach (var role in roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Unable to create role '{role}': {errors}");
            }
        }
    }

    // Assign the initial administrator using configuration.
    public static async Task AssignInitialAdminAsync(
        UserManager<IdentityUser> userManager,
        string? adminEmail)
    {
        // No configured email means bootstrap assignment is disabled.
        if (string.IsNullOrWhiteSpace(adminEmail))
            return;

        var adminUser = await userManager.FindByEmailAsync(
            adminEmail.Trim());

        if (adminUser == null)
        {
            throw new InvalidOperationException(
                $"Initial Admin account '{adminEmail}' was not found. Register the account first.");
        }

        if (await userManager.IsInRoleAsync(adminUser, "Admin"))
            return;

        var result = await userManager.AddToRoleAsync(
            adminUser,
            "Admin");

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Unable to assign Admin role: {errors}");
        }
    }
}
