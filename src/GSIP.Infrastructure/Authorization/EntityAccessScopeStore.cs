using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Authorization;

public sealed record EntityAccessScope(bool IsUnrestricted, IReadOnlySet<string> EntityCodes)
{
    public bool Allows(string? entityCode) =>
        IsUnrestricted
        || (!string.IsNullOrWhiteSpace(entityCode)
            && EntityCodes.Contains(entityCode.Trim().ToUpperInvariant()));
}

public static class EntityAccessScopeStore
{
    public const string LoginProvider = "GSIP.EntityScope";
    public const string TokenName = "AllowedEntities";
    private const string NoneValue = "__NONE__";

    private static readonly IReadOnlySet<string> EmptyCodes =
        new HashSet<string>(StringComparer.Ordinal);

    public static async Task<EntityAccessScope> GetForUserAsync(
        GsipDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (userId == Guid.Empty)
        {
            return new EntityAccessScope(false, EmptyCodes);
        }

        var token = await dbContext.Set<IdentityUserToken<Guid>>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.UserId == userId
                    && row.LoginProvider == LoginProvider
                    && row.Name == TokenName,
                cancellationToken);

        if (token is null)
        {
            // Backward compatibility: users created before entity scoping remain unrestricted.
            return new EntityAccessScope(true, EmptyCodes);
        }

        return new EntityAccessScope(false, Parse(token.Value));
    }

    public static async Task<IReadOnlyDictionary<Guid, EntityAccessScope>> GetForUsersAsync(
        GsipDbContext dbContext,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, EntityAccessScope>();
        }

        var rows = await dbContext.Set<IdentityUserToken<Guid>>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.UserId)
                && row.LoginProvider == LoginProvider
                && row.Name == TokenName)
            .Select(row => new { row.UserId, row.Value })
            .ToListAsync(cancellationToken);

        var explicitScopes = rows.ToDictionary(
            row => row.UserId,
            row => new EntityAccessScope(false, Parse(row.Value)));

        return ids.ToDictionary(
            id => id,
            id => explicitScopes.TryGetValue(id, out var scope)
                ? scope
                : new EntityAccessScope(true, EmptyCodes));
    }

    public static async Task SetForUserAsync(
        GsipDbContext dbContext,
        Guid userId,
        bool allEntities,
        IEnumerable<string>? entityCodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (userId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var row = await dbContext.Set<IdentityUserToken<Guid>>()
            .SingleOrDefaultAsync(
                item => item.UserId == userId
                    && item.LoginProvider == LoginProvider
                    && item.Name == TokenName,
                cancellationToken);

        if (allEntities)
        {
            if (row is not null)
            {
                dbContext.Remove(row);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var normalized = NormalizeCodes(entityCodes);
        var value = normalized.Count == 0 ? NoneValue : string.Join('|', normalized);

        if (row is null)
        {
            dbContext.Add(new IdentityUserToken<Guid>
            {
                UserId = userId,
                LoginProvider = LoginProvider,
                Name = TokenName,
                Value = value
            });
        }
        else
        {
            row.Value = value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlySet<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, NoneValue, StringComparison.Ordinal))
        {
            return EmptyCodes;
        }

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Where(code => code.Length is > 0 and <= 40)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> NormalizeCodes(IEnumerable<string>? entityCodes) =>
        (entityCodes ?? [])
            .Select(code => code?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(code => code.Length is > 0 and <= 40 && !code.Contains('|'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
}
