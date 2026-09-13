namespace GSIP.Web.Models;

public sealed record UatServiceOptionViewModel(
    Guid ServiceId,
    Guid EntityId,
    string EntityCode,
    string EntityNameAr,
    string EntityNameEn,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    bool ContractConfigured,
    bool ConfigurationActive,
    bool ReadyToExecute,
    string Status);

public sealed record UatServiceDetailViewModel(
    Guid ServiceId,
    Guid EntityId,
    Guid EnvironmentId,
    Guid? AuthProfileId,
    string EntityCode,
    string EntityNameAr,
    string EntityNameEn,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    bool ContractConfigured,
    string BaseUrl,
    string RelativePath,
    string HttpMethod,
    string ContentType,
    int TimeoutSeconds,
    bool ValidateServerCertificate,
    string AuthenticationType,
    bool AuthenticationProfileEnabled,
    bool ConfigurationActive,
    bool RequiresUsername,
    bool RequiresPassword,
    bool RequiresApiKey,
    bool RequiresAuxiliaryCredential,
    string AuxiliaryCredentialLabel,
    bool CredentialsConfigured,
    bool ReadyToExecute,
    string TokenPath,
    DateTimeOffset? LastTestedAtUtc,
    string Status,
    bool DirectCredentialEditorSupported);

public sealed record UatConfigurationViewModel(
    IReadOnlyList<UatServiceOptionViewModel> Services,
    UatServiceDetailViewModel? SelectedService,
    string? ErrorMessage,
    string? SuccessMessage);
