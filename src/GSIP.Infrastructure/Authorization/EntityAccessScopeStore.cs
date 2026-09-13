using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Authorization;

public sealed record UserAccessScope(
    bool AllEntities,
    IReadOnlySet<string> EntityCodes,
    bool AllServices,
    IReadOnlySet<string> ServiceKeys)
{
    public bool AllowsEntity(string? entityCode) =>
        AllEntities
        || (!string.IsNullOrWhiteSpace(entityCode)
            && EntityCodes.Contains(NormalizeCode(entityCode)));

    public bool AllowsService(string? entityCode, string? serviceCode)
    {
        if (!AllowsEntity(entityCode))
        {
            return false;
        }

        return AllServices
            || (!string.IsNullOrWhiteSpace(entityCode)
                && !string.IsNullOrWhiteSpace(serviceCode)
                && ServiceKeys.Contains(ServiceKey(entityCode, serviceCode)));
    }

    public static string ServiceKey(string entityCode, string serviceCode) =>
        $"{NormalizeCode(entityCode)}::{NormalizeCode(serviceCode)}";

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

public static class EntityAccessScopeStore
{
    public const string LoginProvider = "GSIP.UserAccessScope";
    public const string EntityTokenName = "AllowedEntities";
    public const string ServiceTokenName = "AllowedServices";
    private const string NoneValue = "__NONE__";
    private const int MaximumServiceKeyLength = 200;

    private static readonly IReadOnlySet<string> EmptyValues =
        new HashSet<string>(StringComparer.Ordinal);

    public static async Task<UserAccessScope> GetForUserAsync(
        GsipDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (userId == Guid.Empty)
        {
            return new UserAccessScope(false, EmptyValues, false, EmptyValues);
        }

        var rows = await dbContext.Set<IdentityUserToken<Guid>>()
            .AsNoTracking()
            .Where(row => row.UserId == userId
                && row.LoginProvider == LoginProvider
                && (row.Name == EntityTokenName || row.Name == ServiceTokenName))
            .Select(row => new { row.Name, row.Value })
            .ToListAsync(cancellationToken);

        var entityRow = rows.SingleOrDefault(row => row.Name == EntityTokenName);
        var serviceRow = rows.SingleOrDefault(row => row.Name == ServiceTokenName);

        // Backward compatibility: absence of a token means unrestricted for that dimension.
        return new UserAccessScope(
            entityRow is null,
            entityRow is null ? EmptyValues : ParseValues(entityRow.Value, maximumLength: 40),
            serviceRow is null,
            serviceRow is null ? EmptyValues : ParseValues(serviceRow.Value, MaximumServiceKeyLength));
    }

    public static async Task SetForUserAsync(
        GsipDbContext dbContext,
        Guid userId,
        bool allEntities,
        IEnumerable<string>? entityCodes,
        bool allServices,
        IEnumerable<(string EntityCode, string ServiceCode)>? services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (userId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var rows = await dbContext.Set<IdentityUserToken<Guid>>()
            .Where(row => row.UserId == userId
                && row.LoginProvider == LoginProvider
                && (row.Name == EntityTokenName || row.Name == ServiceTokenName))
            .ToListAsync(cancellationToken);

        var normalizedEntities = NormalizeCodes(entityCodes, 40);
        var normalizedServices = NormalizeServiceKeys(services);

        ApplyToken(
            dbContext,
            rows.SingleOrDefault(row => row.Name == EntityTokenName),
            userId,
            EntityTokenName,
            allEntities,
            normalizedEntities);
        ApplyToken(
            dbContext,
            rows.SingleOrDefault(row => row.Name == ServiceTokenName),
            userId,
            ServiceTokenName,
            allServices,
            normalizedServices);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyToken(
        GsipDbContext dbContext,
        IdentityUserToken<Guid>? existing,
        Guid userId,
        string tokenName,
        bool unrestricted,
        IReadOnlyList<string> values)
    {
        if (unrestricted)
        {
            if (existing is not null)
            {
                dbContext.Remove(existing);
            }
            return;
        }

        var value = values.Count == 0 ? NoneValue : string.Join('|', values);
        if (existing is null)
        {
            dbContext.Add(new IdentityUserToken<Guid>
            {
                UserId = userId,
                LoginProvider = LoginProvider,
                Name = tokenName,
                Value = value
            });
        }
        else
        {
            existing.Value = value;
        }
    }

    private static IReadOnlySet<string> ParseValues(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, NoneValue, StringComparison.Ordinal))
        {
            return EmptyValues;
        }

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToUpperInvariant())
            .Where(item => item.Length is > 0 && item.Length <= maximumLength)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> NormalizeCodes(IEnumerable<string>? codes, int maximumLength) =>
        (codes ?? [])
            .Select(code => code?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(code => code.Length is > 0 && code.Length <= maximumLength && !code.Contains('|'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> NormalizeServiceKeys(
        IEnumerable<(string EntityCode, string ServiceCode)>? services) =>
        (services ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.EntityCode) && !string.IsNullOrWhiteSpace(item.ServiceCode))
            .Select(item => UserAccessScope.ServiceKey(item.EntityCode, item.ServiceCode))
            .Where(key => key.Length <= MaximumServiceKeyLength && !key.Contains('|'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
}
