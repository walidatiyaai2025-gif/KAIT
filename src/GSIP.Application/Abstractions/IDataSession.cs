namespace GSIP.Application.Abstractions;

/// <summary>
/// Persistence boundary intentionally independent of EF Core. The concrete DbContext/migrations belong to P02.
/// </summary>
public interface IDataSession
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
