namespace GSIP.Application.Setup;

public static class DatabaseAuthenticationBinding
{
    public const string WindowsMode = "windows";
    public const string SqlMode = "sql";

    public static SetupOperationResult Apply(string? postedMode, DatabaseSetupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(postedMode, WindowsMode, StringComparison.OrdinalIgnoreCase))
        {
            options.UseWindowsAuthentication = true;
            options.Username = string.Empty;
            options.Password = string.Empty;
            return SetupOperationResult.Ok("SQL_AUTH_MODE_WINDOWS", "Windows Authentication selected.");
        }

        if (string.Equals(postedMode, SqlMode, StringComparison.OrdinalIgnoreCase))
        {
            options.UseWindowsAuthentication = false;
            return SetupOperationResult.Ok("SQL_AUTH_MODE_SQL", "SQL Authentication selected.");
        }

        return SetupOperationResult.Fail(
            "SQL_AUTH_MODE_INVALID",
            "Choose either Windows Authentication or SQL Authentication explicitly.");
    }
}
