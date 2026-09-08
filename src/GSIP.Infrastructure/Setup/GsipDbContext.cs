using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Authorization;
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
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleServicePermission> RoleServicePermissions => Set<RoleServicePermission>();
    public DbSet<CatalogEntity> CatalogEntities => Set<CatalogEntity>();
    public DbSet<CatalogEnvironment> CatalogEnvironments => Set<CatalogEnvironment>();
    public DbSet<CatalogService> CatalogServices => Set<CatalogService>();
    public DbSet<ServiceEnvironmentConfig> ServiceEnvironmentConfigs => Set<ServiceEnvironmentConfig>();
    public DbSet<ServiceFieldDefinition> ServiceFieldDefinitions => Set<ServiceFieldDefinition>();
    public DbSet<ResultMappingDefinition> ResultMappingDefinitions => Set<ResultMappingDefinition>();
    public DbSet<SecretVaultEntry> SecretVaultEntries => Set<SecretVaultEntry>();
    public DbSet<AuthProfile> AuthProfiles => Set<AuthProfile>();
    public DbSet<AuthProfileBinding> AuthProfileBindings => Set<AuthProfileBinding>();
    public DbSet<AuthProfileSecret> AuthProfileSecrets => Set<AuthProfileSecret>();

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
            entity.ToTable("AuthenticationAuditEvents"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAtUtc); entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResultCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions"); entity.HasKey(x => new { x.RoleId, x.PermissionKey });
            entity.Property(x => x.PermissionKey).HasMaxLength(120).IsRequired(); entity.Property(x => x.IsAllowed).IsRequired();
            entity.HasIndex(x => x.PermissionKey);
            entity.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleServicePermission>(entity =>
        {
            entity.ToTable("RoleServicePermissions"); entity.HasKey(x => new { x.RoleId, x.ServiceCode, x.PermissionKey });
            entity.Property(x => x.ServiceCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PermissionKey).HasMaxLength(120).IsRequired(); entity.Property(x => x.IsAllowed).IsRequired();
            entity.HasIndex(x => new { x.ServiceCode, x.PermissionKey });
            entity.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemSetupRecord>(entity =>
        {
            entity.ToTable("SystemSetup"); entity.HasKey(x => x.Id);
            entity.Property(x => x.OrganizationNameEn).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OrganizationNameAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PrimaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DefaultEnvironment).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<BootstrapAdministrator>(entity =>
        {
            entity.ToTable("BootstrapAdministrators"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.NormalizedUsername).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired(); entity.Property(x => x.Username).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedUsername).HasMaxLength(120).IsRequired(); entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        });

        modelBuilder.Entity<ServiceEnvironmentPlaceholder>(entity =>
        {
            entity.ToTable("ServiceEnvironmentPlaceholders"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EntityCode, x.ServiceCode, x.Environment }).IsUnique();
            entity.Property(x => x.EntityCode).HasMaxLength(40).IsRequired(); entity.Property(x => x.ServiceCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Environment).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<CatalogEntity>(entity =>
        {
            entity.ToTable("CatalogEntities"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired(); entity.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.NameEn).HasMaxLength(200).IsRequired(); entity.Property(x => x.Logo).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<CatalogEnvironment>(entity =>
        {
            entity.ToTable("CatalogEnvironments"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired(); entity.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            entity.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<CatalogService>(entity =>
        {
            entity.ToTable("CatalogServices"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DefinitionKey, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.EntityId, x.Code }).IsUnique().HasFilter("[IsCurrent] = 1");
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired(); entity.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.NameEn).HasMaxLength(200).IsRequired(); entity.Property(x => x.DescriptionAr).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.DescriptionEn).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Entity).WithMany(x => x.Services).HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceEnvironmentConfig>(entity =>
        {
            entity.ToTable("ServiceEnvironmentConfigs"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServiceId, x.EnvironmentId }).IsUnique();
            entity.Property(x => x.BaseUrl).HasMaxLength(1000).IsRequired(); entity.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(16).IsRequired(); entity.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NonSecretHeadersJson).HasMaxLength(4000).IsRequired(); entity.Property(x => x.TlsPolicy).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ProxyUrl).HasMaxLength(1000).IsRequired(); entity.Property(x => x.HealthPath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HealthMethod).HasMaxLength(16).IsRequired(); entity.Property(x => x.LastTestStatus).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Service).WithMany(x => x.EnvironmentConfigs).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Environment).WithMany(x => x.ServiceConfigurations).HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AuthProfileBinding>().WithOne()
                .HasForeignKey<ServiceEnvironmentConfig>(x => new { x.ServiceId, x.EnvironmentId, x.AuthProfileId })
                .HasPrincipalKey<AuthProfileBinding>(x => new { x.ServiceId, x.EnvironmentId, x.AuthProfileId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceFieldDefinition>(entity =>
        {
            entity.ToTable("ServiceFieldDefinitions"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ServiceId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired(); entity.Property(x => x.LabelAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LabelEn).HasMaxLength(200).IsRequired(); entity.Property(x => x.FieldType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Regex).HasMaxLength(500).IsRequired(); entity.Property(x => x.Minimum).HasPrecision(18, 4);
            entity.Property(x => x.Maximum).HasPrecision(18, 4); entity.Property(x => x.OptionsJson).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Masking).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Service).WithMany(x => x.Fields).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResultMappingDefinition>(entity =>
        {
            entity.ToTable("ResultMappingDefinitions"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ServiceId, x.DisplayOrder });
            entity.Property(x => x.SourcePath).HasMaxLength(500).IsRequired(); entity.Property(x => x.LabelAr).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LabelEn).HasMaxLength(200).IsRequired(); entity.Property(x => x.ResultType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Formatter).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.Service).WithMany(x => x.ResultMappings).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecretVaultEntry>(entity =>
        {
            entity.ToTable("SecretVaultEntries"); entity.HasKey(x => x.Reference);
            entity.Property(x => x.Reference).HasMaxLength(SecretRef.MaxLength).IsRequired();
            entity.Property(x => x.ProtectedPayload).HasColumnType("varbinary(max)").IsRequired();
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Generation).IsRequired();
        });

        modelBuilder.Entity<AuthProfile>(entity =>
        {
            entity.ToTable("AuthProfiles"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerServiceId, x.OwnerEnvironmentId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.AuthType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(160).IsRequired();
            entity.HasOne<CatalogService>().WithMany().HasForeignKey(x => x.OwnerServiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CatalogEnvironment>().WithMany().HasForeignKey(x => x.OwnerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuthProfileBinding>(entity =>
        {
            entity.ToTable("AuthProfileBindings"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServiceId, x.EnvironmentId }).IsUnique();
            entity.HasAlternateKey(x => new { x.ServiceId, x.EnvironmentId, x.AuthProfileId });
            entity.Property(x => x.DecisionBy).HasMaxLength(160).IsRequired();
            entity.Property(x => x.DecisionReason).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.AuthProfile).WithMany(x => x.Bindings).HasForeignKey(x => x.AuthProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CatalogService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CatalogEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuthProfileSecret>(entity =>
        {
            entity.ToTable("AuthProfileSecrets"); entity.HasKey(x => new { x.AuthProfileId, x.SecretName });
            entity.Property(x => x.SecretName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SecretReference).HasMaxLength(SecretRef.MaxLength).IsRequired();
            entity.HasOne(x => x.AuthProfile).WithMany(x => x.Secrets).HasForeignKey(x => x.AuthProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SecretVaultEntry>().WithMany().HasForeignKey(x => x.SecretReference).OnDelete(DeleteBehavior.Restrict);
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
