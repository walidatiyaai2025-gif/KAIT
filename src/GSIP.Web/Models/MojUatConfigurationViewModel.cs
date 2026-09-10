namespace GSIP.Web.Models;

public sealed record MojUatConfigurationViewModel(
    Guid EntityId,
    Guid ServiceId,
    Guid EnvironmentId,
    Guid? AuthProfileId,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    string BaseUrl,
    string RelativePath,
    string HttpMethod,
    string ContentType,
    bool ServiceActive,
    bool EnvironmentActive,
    bool ConfigurationActive,
    string AuthenticationType,
    bool AuthenticationProfileEnabled,
    bool ApiKeyConfigured,
    bool ReadyToExecute,
    string Status,
    string? ErrorMessage,
    string? SuccessMessage);
