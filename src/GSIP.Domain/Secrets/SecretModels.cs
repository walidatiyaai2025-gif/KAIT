namespace GSIP.Domain.Secrets;

public enum SecretLifecycleState
{
    Active = 1,
    Revoked = 2,
    Staged = 3
}

public enum AuthProfileType
{
    None = 0,
    ApiKeyHeader = 1,
    StaticBearer = 2,
    TokenEndpoint = 3,
    ApiKeyPlusBearer = 4,
    CustomHeaders = 5
}

public readonly record struct SecretRef
{
    public const string Prefix = "sr1_";
    public const int TokenLength = 43;
    public const int MaxLength = 64;

    public SecretRef(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException("Secret reference format is invalid.", nameof(value));
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public static SecretRef Parse(string value) => new(value);

    public static bool TryParse(string? value, out SecretRef secretRef)
    {
        if (IsValid(value))
        {
            secretRef = new SecretRef(value!);
            return true;
        }

        secretRef = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != Prefix.Length + TokenLength || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        for (var index = Prefix.Length; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'A' and <= 'Z'
                  || character is >= 'a' and <= 'z'
                  || character is >= '0' and <= '9'
                  || character is '-' or '_'))
                return false;
        }

        return true;
    }
}

public sealed class SecretVaultEntry
{
    public string Reference { get; set; } = string.Empty;
    public byte[] ProtectedPayload { get; set; } = [];
    public SecretLifecycleState State { get; set; } = SecretLifecycleState.Active;
    public int Generation { get; set; } = 1;
    public Guid OwnerServiceId { get; set; }
    public Guid OwnerEnvironmentId { get; set; }
    public Guid? OwnerAuthProfileId { get; set; }
    public string SecretName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class AuthProfile
{
    public Guid Id { get; set; }
    public Guid OwnerServiceId { get; set; }
    public Guid OwnerEnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AuthProfileType AuthType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long Version { get; set; } = 1;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<AuthProfileBinding> Bindings { get; set; } = new List<AuthProfileBinding>();
    public ICollection<AuthProfileSecret> Secrets { get; set; } = new List<AuthProfileSecret>();
}

public sealed class AuthProfileBinding
{
    public Guid Id { get; set; }
    public Guid AuthProfileId { get; set; }
    public AuthProfile? AuthProfile { get; set; }
    public Guid ServiceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public bool IsShared { get; set; }
    public string DecisionBy { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public DateTimeOffset DecisionAtUtc { get; set; }
}

public sealed class AuthProfileSecret
{
    public Guid AuthProfileId { get; set; }
    public AuthProfile? AuthProfile { get; set; }
    public string SecretName { get; set; } = string.Empty;
    public string SecretReference { get; set; } = string.Empty;
    public int Generation { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
