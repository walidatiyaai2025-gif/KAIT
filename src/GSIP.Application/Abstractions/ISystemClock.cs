namespace GSIP.Application.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
