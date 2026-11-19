
using AcxiomCRM.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options
) : IdentityDbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // CUSTOMERS
        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.Phone).IsUnique();

            entity.Property(c => c.CustomerCode)
                .IsRequired().HasMaxLength(20);

            entity.Property(c => c.CustomerName)
                .IsRequired().HasMaxLength(150);
        });

        // LEADS
        builder.Entity<Lead>(entity =>
        {
            entity.HasIndex(l => l.Email).IsUnique();
            entity.HasIndex(l => l.Phone).IsUnique();

            entity.Property(l => l.LeadCode)
                .IsRequired().HasMaxLength(20);

            entity.Property(l => l.LeadName)
                .IsRequired().HasMaxLength(150);

            entity.Property(l => l.ExpectedValue)
                .HasPrecision(18, 2);
        });

        // FOLLOW UPS
        builder.Entity<FollowUp>(entity =>
        {
            entity.Property(f => f.FollowUpType)
                .IsRequired().HasMaxLength(30);

            entity.Property(f => f.Status)
                .IsRequired().HasMaxLength(30);

            entity.Property(f => f.Remarks)
                .HasMaxLength(2000);

            entity.HasIndex(f => f.FollowUpDate);
            entity.HasIndex(f => f.Status);
            entity.HasIndex(f => f.AssignedTo);
        });

        // OPPORTUNITIES
        builder.Entity<Opportunity>(entity =>
        {
            entity.Property(o => o.OpportunityName)
                .IsRequired().HasMaxLength(150);

            entity.Property(o => o.Amount)
                .HasPrecision(18, 2);

            entity.Property(o => o.Stage)
                .IsRequired().HasMaxLength(30);

            entity.Property(o => o.Status)
                .IsRequired().HasMaxLength(30);

            entity.HasIndex(o => o.ExpectedCloseDate);
            entity.HasIndex(o => o.Stage);
            entity.HasIndex(o => o.AssignedTo);
        });

        // ACTIVITIES
        builder.Entity<Activity>(entity =>
        {
            entity.Property(a => a.ActivityType)
                .IsRequired().HasMaxLength(30);

            entity.Property(a => a.Subject)
                .IsRequired().HasMaxLength(150);

            entity.Property(a => a.Description)
                .HasMaxLength(2000);

            entity.Property(a => a.Status)
                .IsRequired().HasMaxLength(30);

            entity.HasIndex(a => a.ActivityDate);
            entity.HasIndex(a => a.ActivityType);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.AssignedTo);
        });

        // AUDIT LOGS
        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(a => a.Action)
                .IsRequired().HasMaxLength(50);

            entity.Property(a => a.EntityName)
                .IsRequired().HasMaxLength(100);

            entity.Property(a => a.RecordId)
                .IsRequired().HasMaxLength(100);

            entity.Property(a => a.IpAddress)
                .HasMaxLength(100);

            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.EntityName);
            entity.HasIndex(a => a.CreatedDate);

            entity.HasIndex(a => new
            {
                a.EntityName,
                a.RecordId
            });
        });

        // REFRESH TOKENS
        builder.Entity<RefreshToken>(entity =>
        {
            // Id is automatically recognized as the primary key.

            entity.Property(x => x.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.CreatedByIp)
                .HasMaxLength(100);

            entity.Property(x => x.RevokedByIp)
                .HasMaxLength(100);

            entity.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(128);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAt);

            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // AUDIT LOG PROTECTION
    private void ProtectAuditLogs()
    {
        var modifiedOrDeletedLogs = ChangeTracker
            .Entries<AuditLog>()
            .Where(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted);

        if (modifiedOrDeletedLogs.Any())
        {
            throw new InvalidOperationException(
                "Audit logs are append-only. Existing audit records cannot be modified or deleted.");
        }
    }

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        ProtectAuditLogs();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectAuditLogs();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }
}
