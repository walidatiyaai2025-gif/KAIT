using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using GSIP.Application.Authentication;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class CompositeAuthContractChecks
{
    private const string ApiKey = "SYNTHETIC_P08_COMPOSITE_API_KEY";
    private const string Username = "synthetic-composite-user";
    private const string Password = "SYNTHETIC_P08_COMPOSITE_PASSWORD";
    private const string AccessToken = "eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.synthetic-composite";
    private const string CivilId = "synthetic-civil-id";
    private const string TokenMetadataKey = "X-GSIP-TokenEndpointPath";

    [ModuleInitializer]
    internal static void Initialize() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var serviceId = Guid.Parse("8a000000-0000-0000-0000-000000000901");
        var profileId = Guid.Parse("8a000000-0000-0000-0000-000000000902");
        var apiKeyRef = Ref('K');
        var usernameRef = Ref('U');
        var passwordRef = Ref('P');
        var service = new CatalogService
        {
            Id = serviceId,
            DefinitionKey = Guid.Parse("8a000000-0000-0000-0000-000000000903"),
            EntityId = Guid.Parse("8a000000-0000-0000-0000-000000000904"),
            Code = "SYNTH-P08-MOJ-COMPOSITE",
            NameAr = "عقد مصادقة مركب اصطناعي",
            NameEn = "Synthetic MOJ composite auth contract",
            DescriptionAr = "اختبار اصطناعي فقط",
            DescriptionEn = "Synthetic test only",
            Active = true,
            IsCurrent = true,
            Version = 1,
            Fields =
            [
                new ServiceFieldDefinition
                {
                    Id = Guid.Parse("8a000000-0000-0000-0000-000000000905"),
                    ServiceId = serviceId,
                    Key = "civilId",
                    LabelAr = "الرقم المدني الاصطناعي",
                    LabelEn = "Synthetic Civil ID",
                    FieldType = "text",
                    Required = true,
                    MaxLength = 64,
                    DisplayOrder = 1,
                    Sensitive = true,
                    Masking = "Full"
                }
            ],
            ResultMappings = []
        };
        var profile = new AuthProfileDescriptor(
            profileId,
            serviceId,
            CatalogEnvironmentCodes.UatId,
            "Synthetic composite token profile",
            AuthProfileType.TokenEndpoint,
            true,
            21,
            "synthetic",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [
                new AuthProfileSecretDescriptor("x-api-key", apiKeyRef, 9),
                new AuthProfileSecretDescriptor("username", usernameRef, 4),
                new AuthProfileSecretDescriptor("password", passwordRef, 7)
            ],
            [new AuthProfileBindingDescriptor(serviceId, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
        var binding = new AuthorizedServiceExecutionBinding(
            serviceId,
            service.Code,
            CatalogEnvironmentCodes.UatId,
            CatalogEnvironmentCodes.Uat,
            "https://synthetic.invalid/WSWEB/WS/v1/marriage",
            "/marriageCasesAPIGEE",
            "POST",
            "application/x-www-form-urlencoded",
            5,
            "SystemDefault",
            true,
            string.Empty,
            profileId,
            profile.Version,
            $"{{\"{TokenMetadataKey}\":\"/genToken\"}}");
        var metadata = new CompositeMetadata(new MetadataCatalogSnapshot(
            [],
            [service],
            [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
        var resolver = new CompositeSecretResolver(
            serviceId,
            CatalogEnvironmentCodes.UatId,
            profileId,
            new Dictionary<SecretRef, string>
            {
                [apiKeyRef] = ApiKey,
                [usernameRef] = Username,
                [passwordRef] = Password
            });
        var handler = new CompositeHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/WSWEB/WS/v1/marriage/genToken")
            {
                Check(request.Method == HttpMethod.Post, "Composite token request must use POST.");
                Check(request.Headers.TryGetValues("x-api-key", out var keyValues) && keyValues.Single() == ApiKey,
                    "Composite token request did not carry the exact x-api-key secret.");
                Check(request.Headers.Authorization is null, "Bearer must not be attached to /genToken.");
                Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                    "Composite token request must be form-urlencoded.");
                var form = await request.Content!.ReadAsStringAsync();
                Check(FormContains(form, "username", Username) && FormContains(form, "password", Password),
                    "Composite token request omitted username/password.");
                return Json(HttpStatusCode.OK, $"{{\"data\":\"{AccessToken}\"}}");
            }

            Check(path == "/WSWEB/WS/v1/marriage/marriageCasesAPIGEE",
                "Marriage target did not preserve the configured UAT base-path prefix.");
            Check(request.Headers.TryGetValues("x-api-key", out var targetKeyValues) && targetKeyValues.Single() == ApiKey,
                "Marriage target did not carry x-api-key.");
            Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == AccessToken,
                "Marriage target did not carry the acquired Bearer token.");
            Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                "Marriage target must be form-urlencoded.");
            var targetForm = await request.Content!.ReadAsStringAsync();
            Check(FormContains(targetForm, "civilId", CivilId), "Marriage target omitted the synthetic civilId field.");
            return Json(HttpStatusCode.OK, "{\"ok\":true}");
        });
        var cache = new CompositeTokenCache();
        var logger = new CompositeLogger<GenericServiceExecutionEngine>();
        var engine = new GenericServiceExecutionEngine(
            new CompositeSecurityGate(binding),
            metadata,
            new CompositeAuthProfiles(profile),
            resolver,
            new CompositeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
            Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }),
            logger,
            cache);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "8a000000-0000-0000-0000-000000000906")],
            "SyntheticCompositeP08"));

        var result = await engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            serviceId,
            CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = CivilId }));

        Check(result.Outcome == ServiceExecutionOutcome.Success, "Composite MOJ authentication execution failed.");
        Check(handler.TokenCalls == 1 && handler.TargetCalls == 1, "Composite flow must call token and target exactly once.");
        Check(resolver.Calls.Count == 3, "Composite flow must resolve exactly API key, username, and password.");
        Check(resolver.Calls.All(call => call.ServiceId == serviceId
                                         && call.EnvironmentId == CatalogEnvironmentCodes.UatId
                                         && call.AuthProfileId == profileId),
            "Composite secret resolution escaped exact Service + Environment + AuthProfile scope.");
        Check(cache.LastIdentity is not null
              && cache.LastIdentity.ServiceId == serviceId
              && cache.LastIdentity.EnvironmentId == CatalogEnvironmentCodes.UatId
              && cache.LastIdentity.AuthProfileId == profileId
              && cache.LastIdentity.AuthProfileVersion == profile.Version
              && cache.LastIdentity.SecretGeneration == 9,
            "API-key generation did not participate in token-cache scope validity.");
        var diagnostics = string.Join('\n', logger.Messages) + "\n" + result;
        Check(!diagnostics.Contains(ApiKey, StringComparison.Ordinal)
              && !diagnostics.Contains(Username, StringComparison.Ordinal)
              && !diagnostics.Contains(Password, StringComparison.Ordinal)
              && !diagnostics.Contains(AccessToken, StringComparison.Ordinal)
              && !diagnostics.Contains(CivilId, StringComparison.Ordinal),
            "Composite credential/token/personal synthetic input leaked to diagnostics.");

        Console.WriteLine("P08 MOJ composite authentication contract check passed.");
    }

    private static SecretRef Ref(char fill) => SecretRef.Parse("sr1_" + new string(fill, 43));

    private static bool FormContains(string body, string name, string value) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Any(parts => parts.Length == 2
                          && Uri.UnescapeDataString(parts[0].Replace('+', ' ')) == name
                          && Uri.UnescapeDataString(parts[1].Replace('+', ' ')) == value);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record SecretCall(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string Name);

    private sealed class CompositeSecretResolver(
        Guid serviceId,
        Guid environmentId,
        Guid profileId,
        IReadOnlyDictionary<SecretRef, string> values) : ISecretMaterialResolver
    {
        public List<SecretCall> Calls { get; } = [];

        public async Task<TResult> UseSecretAsync<TResult>(
            Guid requestedServiceId,
            Guid requestedEnvironmentId,
            Guid requestedProfileId,
            string secretName,
            SecretRef secretRef,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SecretCall(requestedServiceId, requestedEnvironmentId, requestedProfileId, secretName));
            if (requestedServiceId != serviceId
                || requestedEnvironmentId != environmentId
                || requestedProfileId != profileId
                || !values.TryGetValue(secretRef, out var value))
                throw new SecretReferenceRejectedException();

            var material = Encoding.UTF8.GetBytes(value);
            try
            {
                return await operation(material, cancellationToken);
            }
            finally
            {
                Array.Clear(material);
            }
        }
    }

    private sealed class CompositeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        public int TargetCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/genToken", StringComparison.Ordinal) == true)
                TokenCalls++;
            else
                TargetCalls++;
            return await responder(request);
        }
    }

    private sealed class CompositeTokenCache : ITokenCache
    {
        public TokenCacheIdentity? LastIdentity { get; private set; }

        public async Task<TokenCacheValue> GetOrRefreshAsync(
            TokenCacheIdentity identity,
            Func<CancellationToken, Task<TokenCacheValue>> refreshFactory,
            CancellationToken cancellationToken = default)
        {
            LastIdentity = identity;
            return await refreshFactory(cancellationToken);
        }
    }

    private sealed class CompositeSecurityGate(AuthorizedServiceExecutionBinding binding) : IServiceExecutionSecurityGate
    {
        public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(
            ClaimsPrincipal principal,
            Guid serviceId,
            Guid environmentId,
            CancellationToken cancellationToken = default) =>
            serviceId == binding.ServiceId && environmentId == binding.EnvironmentId
                ? Task.FromResult(binding)
                : Task.FromException<AuthorizedServiceExecutionBinding>(new ServiceExecutionRejectedException());
    }

    private sealed class CompositeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
    {
        public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogService> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ExportJsonAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MetadataImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CompositeAuthProfiles(AuthProfileDescriptor profile) : IAuthProfileService
    {
        public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
            authProfileId == profile.Id
                ? Task.FromResult(profile)
                : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
        public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthProfileDescriptor?>(profile);
        public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CompositeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            name == "GSIP.Execution" ? client : throw new InvalidOperationException("Unexpected HttpClient name.");
    }

    private sealed class CompositeLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
