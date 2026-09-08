using GSIP.Application.Abstractions;
using GSIP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Setup;

public sealed class GsipDbContext(DbContextOptions<GsipDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataSession
{
    public DbSet<SystemSetupRecord> SystemSetup => Set<SystemSetupRecord>();
    public DbSet<BootstrapAdministrator> BootstrapAdministrators => Set<BootstrapAdministrator>();
    public DbSet<ServiceEnvironmentPlaceholder> ServiceEnvironmentPlaceholders => Set<ServiceEnvironmentPlaceholder>();
    public DbSet<AuthenticationAuditEvent> AuthenticationAuditEvents => Set<AuthenticationAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.IsEnabled).IsRequired();
            entity.Property(x => x.IsPrivileged).IsRequired();
            entity.Property(x => x.MustChangePassword).IsRequired();
        });

        modelBuilder.Entity<AuthenticationAuditEvent>(entity =>
        {
            entity.ToTable("AuthenticationAuditEvents");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResultCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SystemSetupRecord>(entity =>
        {
            entity.ToTable("SystemSetup");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrganizationNameEn).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OrganizationNameAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PrimaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DefaultEnvironment).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<BootstrapAdministrator>(entity =>
        {
            entity.ToTable("BootstrapAdministrators");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedUsername).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        });

        modelBuilder.Entity<ServiceEnvironmentPlaceholder>(entity =>
        {
            entity.ToTable("ServiceEnvironmentPlaceholders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EntityCode, x.ServiceCode, x.Environment }).IsUnique();
            entity.Property(x => x.EntityCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ServiceCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Environment).HasMaxLength(40).IsRequired();
        });
    }
}

public sealed class SystemSetupRecord
{
    public int Id { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string OrganizationNameEn { get; set; } = string.Empty;
    public string OrganizationNameAr { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public int SessionTimeoutMinutes { get; set; }
    public int LockoutMinutes { get; set; }
    public int MaxFailedAccessAttempts { get; set; }
    public bool RequireMfaForPrivilegedAccounts { get; set; }
    public string DefaultEnvironment { get; set; } = string.Empty;
    public int IntegrationTimeoutSeconds { get; set; }
    public bool ValidateServerCertificate { get; set; }
}

public sealed class BootstrapAdministrator
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
}

public sealed class ServiceEnvironmentPlaceholder
{
    public Guid Id { get; set; }
    public string EntityCode { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
