using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GSIP.Infrastructure.Execution;

const string Username = "SYNTHETIC_MOE_USER";
const string Password = "SYNTHETIC_MOE_PASSWORD";

await ConvertsProtectedPairToBasicAsync();
await MissingCredentialFailsClosedAsync();
await ExistingAuthorizationFailsClosedAsync();
await InvalidUsernameFailsClosedAsync();
await UnrelatedRequestPassesThroughAsync();

Console.WriteLine("P17_MOE_BASIC_AUTH_ACCEPTANCE=PASS");
return;

async Task ConvertsProtectedPairToBasicAsync()
{
    var inner = new RecordingHandler();
    using var client = Client(inner);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://synthetic.invalid/lastactive");
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.UsernameHeader, Username);
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.PasswordHeader, Password);

    using var response = await client.SendAsync(request);

    Check(response.StatusCode == HttpStatusCode.OK, "Valid Basic pair did not reach transport.");
    Check(inner.Calls == 1, "Valid Basic pair must issue exactly one outbound request.");
    Check(!inner.SawInternalUsernameHeader && !inner.SawInternalPasswordHeader,
        "Internal Basic credential-carrier headers escaped to transport.");
    Check(inner.AuthorizationScheme == "Basic", "Authorization scheme was not Basic.");
    var expected = Convert.ToBase64String(Encoding.ASCII.GetBytes(Username + ":" + Password));
    Check(inner.AuthorizationParameter == expected, "Basic Authorization payload was not composed from the protected credential pair.");
    Check(request.Headers.Authorization is null, "Authorization header was not cleared after transport.");
}

async Task MissingCredentialFailsClosedAsync()
{
    var inner = new RecordingHandler();
    using var client = Client(inner);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://synthetic.invalid/last");
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.UsernameHeader, Username);

    using var response = await client.SendAsync(request);

    Check(response.StatusCode == HttpStatusCode.Unauthorized, "Partial Basic credentials did not fail closed.");
    Check(inner.Calls == 0, "Partial Basic credentials reached outbound transport.");
    Check(!request.Headers.Contains(BasicAuthenticationTransformHandler.UsernameHeader)
          && !request.Headers.Contains(BasicAuthenticationTransformHandler.PasswordHeader),
        "Internal Basic credential-carrier headers were retained after fail-closed rejection.");
}

async Task ExistingAuthorizationFailsClosedAsync()
{
    var inner = new RecordingHandler();
    using var client = Client(inner);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://synthetic.invalid/lastsuccess");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "SYNTHETIC_EXISTING_TOKEN");
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.UsernameHeader, Username);
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.PasswordHeader, Password);

    using var response = await client.SendAsync(request);

    Check(response.StatusCode == HttpStatusCode.Unauthorized, "Conflicting Authorization did not fail closed.");
    Check(inner.Calls == 0, "Conflicting Authorization reached outbound transport.");
}

async Task InvalidUsernameFailsClosedAsync()
{
    var inner = new RecordingHandler();
    using var client = Client(inner);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://synthetic.invalid/lastactive");
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.UsernameHeader, "bad:user");
    request.Headers.TryAddWithoutValidation(BasicAuthenticationTransformHandler.PasswordHeader, Password);

    using var response = await client.SendAsync(request);

    Check(response.StatusCode == HttpStatusCode.Unauthorized, "Invalid Basic username did not fail closed.");
    Check(inner.Calls == 0, "Invalid Basic username reached outbound transport.");
}

async Task UnrelatedRequestPassesThroughAsync()
{
    var inner = new RecordingHandler();
    using var client = Client(inner);
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://synthetic.invalid/health");

    using var response = await client.SendAsync(request);

    Check(response.StatusCode == HttpStatusCode.OK && inner.Calls == 1,
        "Non-Basic execution traffic was altered by the Basic transform handler.");
    Check(inner.AuthorizationScheme is null && inner.AuthorizationParameter is null,
        "Basic transform invented Authorization for unrelated traffic.");
}

static HttpClient Client(RecordingHandler inner)
{
    var handler = new BasicAuthenticationTransformHandler { InnerHandler = inner };
    return new HttpClient(handler, disposeHandler: true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class RecordingHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool SawInternalUsernameHeader { get; private set; }
    public bool SawInternalPasswordHeader { get; private set; }
    public string? AuthorizationScheme { get; private set; }
    public string? AuthorizationParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        SawInternalUsernameHeader = request.Headers.Contains(BasicAuthenticationTransformHandler.UsernameHeader);
        SawInternalPasswordHeader = request.Headers.Contains(BasicAuthenticationTransformHandler.PasswordHeader);
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }
}
