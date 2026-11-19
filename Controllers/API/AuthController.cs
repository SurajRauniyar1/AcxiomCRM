
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.Auth;
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    private const int AccessTokenMinutes = 30;
    private const int RefreshTokenDays = 7;

    public AuthController(
        UserManager<IdentityUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _configuration = configuration;
        _context = context;
    }

    // ------------------------------------------
    // REGISTER
    // POST: api/auth/register
    // ------------------------------------------

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(
            user,
            dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        const string defaultRole = "SalesExecutive";

        var roleResult = await _userManager.AddToRoleAsync(
            user,
            defaultRole);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                message = "Could not assign the default user role."
            });
        }

        var tokens = await IssueTokensAsync(user);

        return Ok(new
        {
            message = "Registration successful.",
            accessToken = tokens.AccessToken,
            refreshToken = tokens.RefreshToken,
            tokenType = "Bearer",
            expiresAt = tokens.ExpiresAt,
            refreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
            user = new
            {
                user.Id,
                user.Email,
                roles = tokens.Roles
            }
        });
    }

    // ------------------------------------------
    // LOGIN
    // POST: api/auth/login
    // ------------------------------------------

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null ||
            !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (!user.EmailConfirmed)
        {
            return Unauthorized(new
            {
                message = "Email confirmation is required."
            });
        }

        if (user.LockoutEnabled &&
            user.LockoutEnd.HasValue &&
            user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return Unauthorized(new
            {
                message = "Your account is temporarily locked."
            });
        }

        var tokens = await IssueTokensAsync(user);

        return Ok(new
        {
            message = "Login successful.",
            accessToken = tokens.AccessToken,
            refreshToken = tokens.RefreshToken,
            tokenType = "Bearer",
            expiresAt = tokens.ExpiresAt,
            refreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
            user = new
            {
                user.Id,
                user.Email,
                roles = tokens.Roles
            }
        });
    }

    // ------------------------------------------
    // REFRESH TOKEN
    // POST: api/auth/refresh
    // ------------------------------------------

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return BadRequest(new
            {
                message = "Refresh token is required."
            });
        }

        var oldTokenHash = HashToken(dto.RefreshToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == oldTokenHash);

        if (storedToken == null || !storedToken.IsActive)
        {
            return Unauthorized(new
            {
                message = "Invalid, expired, or revoked refresh token."
            });
        }

        var user = await _userManager.FindByIdAsync(
            storedToken.UserId);

        if (user == null ||
            (user.LockoutEnabled &&
             user.LockoutEnd.HasValue &&
             user.LockoutEnd.Value > DateTimeOffset.UtcNow))
        {
            return Unauthorized(new
            {
                message = "User account is unavailable."
            });
        }

        // Revoke the previous refresh token.
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = GetIpAddress();

        // Generate replacement tokens.
        var tokens = await IssueTokensAsync(user);

        storedToken.ReplacedByTokenHash =
            HashToken(tokens.RefreshToken);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Token refreshed successfully.",
            accessToken = tokens.AccessToken,
            refreshToken = tokens.RefreshToken,
            tokenType = "Bearer",
            expiresAt = tokens.ExpiresAt,
            refreshTokenExpiresAt = tokens.RefreshTokenExpiresAt
        });
    }

    // ------------------------------------------
    // REVOKE / LOGOUT
    // POST: api/auth/revoke
    // ------------------------------------------

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(
        [FromBody] RefreshTokenRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return BadRequest(new
            {
                message = "Refresh token is required."
            });
        }

        var tokenHash = HashToken(dto.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (storedToken == null || storedToken.IsRevoked)
        {
            return Ok(new
            {
                message = "Token is already invalid or revoked."
            });
        }

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = GetIpAddress();

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Refresh token revoked successfully."
        });
    }

    // ------------------------------------------
    // ISSUE ACCESS + REFRESH TOKENS
    // ------------------------------------------

    private async Task<TokenResult> IssueTokensAsync(
        IdentityUser user)
    {
        var accessToken = await GenerateJwtToken(user);

        // Generate a cryptographically secure refresh token.
        var rawRefreshToken = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(64));

        // Store only its hash.
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
            CreatedByIp = GetIpAddress()
        };

        _context.RefreshTokens.Add(refreshToken);

        await _context.SaveChangesAsync();

        return new TokenResult(
            accessToken.AccessToken,
            rawRefreshToken,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt,
            accessToken.Roles);
    }

    // ------------------------------------------
    // JWT GENERATION
    // ------------------------------------------

    private async Task<AccessTokenResult> GenerateJwtToken(
        IdentityUser user)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is missing.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is missing.");

        // Must match the UTF8 key encoding in Program.cs.
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(
                ClaimTypes.Name,
                user.UserName ?? user.Email ?? user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(
                new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(
            AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var encodedToken = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return new AccessTokenResult(
            encodedToken,
            expiresAt,
            roles.ToArray());
    }

    // ------------------------------------------
    // HASH REFRESH TOKEN
    // ------------------------------------------

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hash);
    }

    // ------------------------------------------
    // CLIENT IP ADDRESS
    // ------------------------------------------

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    // ------------------------------------------
    // TOKEN RESULT RECORDS
    // ------------------------------------------

    private sealed record AccessTokenResult(
        string AccessToken,
        DateTime ExpiresAt,
        string[] Roles);

    private sealed record TokenResult(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAt,
        DateTime RefreshTokenExpiresAt,
        string[] Roles);
}

// ------------------------------------------
// REFRESH TOKEN REQUEST DTO
// ------------------------------------------

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
