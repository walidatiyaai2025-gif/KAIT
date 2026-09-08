namespace GSIP.Web.Models;

public sealed class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class MfaEnrollmentViewModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public sealed class MfaVerificationViewModel
{
    public string Code { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public sealed class PasswordChangeViewModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
