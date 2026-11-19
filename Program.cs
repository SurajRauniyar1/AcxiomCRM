
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------
// DATABASE
// --------------------------------------

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// --------------------------------------
// IDENTITY
// --------------------------------------

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --------------------------------------
// JWT CONFIGURATION
// --------------------------------------

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience is missing.");

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtKey)
);

// --------------------------------------
// AUTHENTICATION: COOKIE + JWT
// --------------------------------------

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Smart";
        options.DefaultChallengeScheme = "Smart";
        options.DefaultForbidScheme = "Smart";
    })
    .AddPolicyScheme("Smart", "Cookie or JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization
                .FirstOrDefault();

            return authorization?.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase
            ) == true
                ? JwtBearerDefaults.AuthenticationScheme
                : IdentityConstants.ApplicationScheme;
        };
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
    });

// --------------------------------------
// MVC AND API
// --------------------------------------

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// --------------------------------------
// CENTRALIZED API VALIDATION
// --------------------------------------

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .Select(x => new
            {
                field = x.Key,
                errors = x.Value!.Errors
                    .Select(e =>
                        string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? "Invalid value."
                            : e.ErrorMessage)
                    .ToArray()
            })
            .ToArray();

        return new BadRequestObjectResult(new
        {
            success = false,
            message = "Validation failed.",
            errors,
            traceId = context.HttpContext.TraceIdentifier
        });
    };
});

// --------------------------------------
// GLOBAL API RATE LIMITING
// --------------------------------------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                success = false,
                message = "Too many requests. Please try again later.",
                statusCode = 429
            },
            cancellationToken
        );
    };

    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext =>
            {
                var path = httpContext.Request.Path;

                var isAuthApi = path.StartsWithSegments("/api/auth");
                var isOtherApi = path.StartsWithSegments("/api");

                // Do not rate-limit normal MVC pages.
                if (!isAuthApi && !isOtherApi)
                {
                    return RateLimitPartition.GetNoLimiter(
                        "non-api"
                    );
                }

                var ipAddress =
                    httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                var category = isAuthApi ? "auth" : "api";

                var partitionKey = $"{category}:{ipAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isAuthApi ? 5 : 100,

                        Window = TimeSpan.FromMinutes(1),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }
                );
            }
        );
});

// --------------------------------------
// SWAGGER
// --------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------------------
// BUILD APPLICATION
// --------------------------------------

var app = builder.Build();

// --------------------------------------
// GLOBAL EXCEPTION HANDLING
// --------------------------------------

app.UseMiddleware<GlobalExceptionMiddleware>();

// --------------------------------------
// ENVIRONMENT
// --------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// --------------------------------------
// MIDDLEWARE PIPELINE
// --------------------------------------

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Rate limiter must run after routing.
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

// --------------------------------------
// ROUTES
// --------------------------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

app.Run();
