using GSIP.Application.Abstractions;
using GSIP.Application.Authorization;
using GSIP.Application.Identity;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
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
    public static IServiceCollection AddGsipInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var security = configuration.GetSection(IdentitySecurityOptions.SectionName).Get<IdentitySecurityOptions>()
            ?? new IdentitySecurityOptions();

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IPasswordHasher<BootstrapAdministrator>, PasswordHasher<BootstrapAdministrator>>();
        services.Configure<IdentitySecurityOptions>(configuration.GetSection(IdentitySecurityOptions.SectionName));
        services.AddSingleton<IRuntimeDatabaseConnection>(serviceProvider =>
            new RuntimeDatabaseConnection(
                serviceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
                environment,
                configuration));
        services.AddDbContext<GsipDbContext>((serviceProvider, options) =>
        {
            var connection = serviceProvider.GetRequiredService<IRuntimeDatabaseConnection>();
            options.UseSqlServer(
                connection.GetRequiredConnectionString(),
                sql => sql.MigrationsAssembly(typeof(GsipDbContext).Assembly.FullName));
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
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
            })
            .AddEntityFrameworkStores<GsipDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "GSIP.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/access-denied";
            options.SlidingExpiration = false;
        });
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(1);
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization(GsipAuthorizationPolicyRegistration.AddPolicies);
        services.AddScoped<IGsipPermissionEvaluator, GsipPermissionEvaluator>();
        services.AddScoped<IAuthorizationHandler, GsipPermissionAuthorizationHandler>();
        services.AddScoped<IAccountSecurityPolicyProvider, AccountSecurityPolicyProvider>();
        services.AddScoped<IAuthenticationAuditWriter, AuthenticationAuditWriter>();
        services.AddScoped<IAccountAuthenticationService, AccountAuthenticationService>();
        services.AddHostedService<IdentityDatabaseMigrationService>();
        services.AddHostedService<RbacBootstrapService>();
        return services;
    }

    private sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
