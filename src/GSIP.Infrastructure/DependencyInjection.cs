using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authentication;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Identity;
using GSIP.Application.Metadata;
using GSIP.Application.Operations;
using GSIP.Application.Secrets;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Auditing;
using GSIP.Infrastructure.Authentication;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Operations;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGsipInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var security = configuration.GetSection(IdentitySecurityOptions.SectionName).Get<IdentitySecurityOptions>() ?? new IdentitySecurityOptions();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<TokenCacheOptions>();
        services.AddSingleton<ITokenCache, InMemoryTokenCache>();
        services.AddSingleton(new AdminOperationsRuntimeState(DateTimeOffset.UtcNow));
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IMetadataCatalogService, MetadataCatalogService>();
        services.AddScoped<MojMetadataSeedService>();
        services.AddScoped<IServiceExecutionSecurityGate, ServiceExecutionSecurityGate>();
        services.Configure<ServiceExecutionRuntimeOptions>(configuration.GetSection("ServiceExecutionRuntime"));
        services.Configure<RequestHistoryOptions>(configuration.GetSection(RequestHistoryOptions.SectionName));
        services.Configure<AuditTrailOptions>(configuration.GetSection(AuditTrailOptions.SectionName));
        services.Configure<AdminOperationsOptions>(configuration.GetSection(AdminOperationsOptions.SectionName));
        services.AddHttpClient("GSIP.Execution")
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        services.AddScoped<GenericServiceExecutionEngine>();
        services.AddScoped<IAuthenticationProbeService, MojAuthenticationProbeService>();
        services.AddScoped<SensitiveResponseMaskingExecutionEngine>(serviceProvider => new SensitiveResponseMaskingExecutionEngine(
            serviceProvider.GetRequiredService<GenericServiceExecutionEngine>(),
            serviceProvider.GetRequiredService<IMetadataCatalogService>()));
        services.AddScoped<IRequestHistoryStore, RequestHistoryStore>();
        services.AddScoped<IRequestHistoryService, RequestHistoryService>();
        services.AddScoped<IServiceExecutionEngine>(serviceProvider => new RequestHistoryExecutionEngine(
            serviceProvider.GetRequiredService<SensitiveResponseMaskingExecutionEngine>(),
            serviceProvider.GetRequiredService<IServiceExecutionSecurityGate>(),
            serviceProvider.GetRequiredService<IRequestHistoryStore>(),
            serviceProvider.GetRequiredService<IAuditTrailWriter>()));
        services.AddScoped<DataProtectionSecretVault>();
        services.AddScoped<ISecretVault>(serviceProvider => serviceProvider.GetRequiredService<DataProtectionSecretVault>());
        services.AddScoped<ISecretMaterialResolver>(serviceProvider => serviceProvider.GetRequiredService<DataProtectionSecretVault>());
        services.AddScoped<IAuthProfileService, AuthProfileService>();
        services.AddScoped<SecretRotationPersistenceAdapter>();
        services.AddScoped<IPasswordHasher<BootstrapAdministrator>, PasswordHasher<BootstrapAdministrator>>();
        services.Configure<IdentitySecurityOptions>(configuration.GetSection(IdentitySecurityOptions.SectionName));
        services.AddSingleton<IRuntimeDatabaseConnection>(serviceProvider => new RuntimeDatabaseConnection(
            serviceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(), environment, configuration));
        services.AddDbContext<GsipDbContext>((serviceProvider, options) =>
        {
            var connection = serviceProvider.GetRequiredService<IRuntimeDatabaseConnection>();
            options.UseSqlServer(connection.GetRequiredConnectionString(), sql => sql.MigrationsAssembly(typeof(GsipDbContext).Assembly.FullName));
        });

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequiredLength = Math.Clamp(security.PasswordRequiredLength, 12, 128);
            options.Password.RequireUppercase = security.PasswordRequireUppercase;
            options.Password.RequireLowercase = security.PasswordRequireLowercase;
            options.Password.RequireDigit = security.PasswordRequireDigit;
            options.Password.RequireNonAlphanumeric = security.PasswordRequireNonAlphanumeric;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = true;
            options.Stores.MaxLengthForKeys = 128;
        }).AddEntityFrameworkStores<GsipDbContext>().AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "GSIP.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/access-denied";
            options.SlidingExpiration = false;
        });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddHttpContextAccessor();
        services.AddAuthorization(GsipAuthorizationPolicyRegistration.AddPolicies);
        services.AddScoped<IGsipPermissionEvaluator, GsipPermissionEvaluator>();
        services.AddScoped<IAuthorizationHandler, GsipPermissionAuthorizationHandler>();
        services.AddScoped<IAccountSecurityPolicyProvider, AccountSecurityPolicyProvider>();
        services.AddScoped<AuditTrailWriter>();
        services.AddScoped<IAuditTrailWriter>(serviceProvider => serviceProvider.GetRequiredService<AuditTrailWriter>());
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();
        services.AddScoped<IAdminOperationalStateService, AdminOperationalStateService>();
        services.AddScoped<IAuthenticationAuditWriter, AuthenticationAuditWriter>();
        services.AddScoped<IAccountAuthenticationService, AccountAuthenticationService>();
        services.AddHostedService<IdentityDatabaseMigrationService>();
        services.AddHostedService<RbacBootstrapService>();
        services.AddHostedService<MojMetadataBootstrapService>();
        return services;
    }

    private sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
