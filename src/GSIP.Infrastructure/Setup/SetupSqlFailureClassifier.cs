using System.Security.Authentication;
using GSIP.Application.Setup;
using Microsoft.Data.SqlClient;

namespace GSIP.Infrastructure.Setup;

internal static class SetupSqlFailureClassifier
{
    private static readonly HashSet<int> AuthenticationNumbers = [18452, 18456];
    private static readonly HashSet<int> NetworkNumbers = [2, 20, 26, 40, 53, 64, 233, 258, 10060, 11001];

    public static SetupOperationResult Classify(SqlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // SqlClient can surface a transport-level number as SqlException.Number while
        // retaining the decisive login error in Errors. Classification must therefore
        // consider the complete sanitized numeric error set and prioritize authentication.
        if (ContainsAny(exception, AuthenticationNumbers))
        {
            return SetupOperationResult.Fail(
                "SQL_AUTHENTICATION_FAILED",
                "SQL Server rejected the login. Verify SQL Authentication mode, username and password.");
        }

        if (ContainsNumber(exception, -2))
        {
            return SetupOperationResult.Fail(
                "SQL_CONNECTION_TIMEOUT",
                "SQL Server connection timed out. Verify reachability, instance/port and the configured timeout.");
        }

        if (IsTlsOrCertificateFailure(exception))
        {
            return SetupOperationResult.Fail(
                "SQL_TLS_CERTIFICATE_FAILED",
                "SQL Server TLS/certificate validation failed. Verify the server certificate and the selected Encrypt/Trust Server Certificate settings; GSIP did not weaken TLS automatically.");
        }

        if (ContainsAny(exception, NetworkNumbers))
        {
            return SetupOperationResult.Fail(
                "SQL_NETWORK_OR_INSTANCE_FAILED",
                "SQL Server could not be reached at the configured server, port or instance. For an explicit TCP port use SQL Server syntax such as tcp:server,1433.");
        }

        return SetupOperationResult.Fail(
            "SQL_CONNECTION_UNKNOWN",
            $"SQL Server returned an unclassified connection error (SQL error {exception.Number}). Review server-side SQL diagnostics without exposing credentials or connection strings.");
    }

    private static bool ContainsAny(SqlException exception, HashSet<int> numbers)
    {
        if (numbers.Contains(exception.Number))
        {
            return true;
        }

        foreach (SqlError error in exception.Errors)
        {
            if (numbers.Contains(error.Number))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNumber(SqlException exception, int number)
    {
        if (exception.Number == number)
        {
            return true;
        }

        foreach (SqlError error in exception.Errors)
        {
            if (error.Number == number)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTlsOrCertificateFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is AuthenticationException)
            {
                return true;
            }

            var message = current.Message;
            if (message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("SSL Provider", StringComparison.OrdinalIgnoreCase)
                || message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
                || message.Contains("trust relationship", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.InnerException is null)
            {
                break;
            }
        }

        return false;
    }
}
