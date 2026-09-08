namespace GSIP.Infrastructure.Metadata;

internal static class StringComparisonExtensions
{
    public static bool StartsWith(this string value, char prefix, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.StartsWith(prefix.ToString(), comparisonType);
    }
}
