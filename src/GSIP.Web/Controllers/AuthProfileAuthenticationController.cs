using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
[Route("auth-profiles")]
public sealed class AuthProfileAuthenticationController(IAuthenticationProbeService authenticationProbe) : Controller
{
    [HttpPost("{authProfileId:guid}/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTokenGeneration(
        Guid authProfileId,
        Guid serviceId,
        Guid environmentId,
        string? culture,
        CancellationToken cancellationToken)
    {
        try
        {
            await authenticationProbe.TestTokenGenerationAsync(
                serviceId,
                environmentId,
                authProfileId,
                cancellationToken);
            TempData["AuthProfileSuccess"] = "AuthenticationTestPassed";
        }
        catch (AuthenticationProbeRejectedException)
        {
            TempData["AuthProfileError"] = "AuthenticationTestFailed";
        }

        var normalizedCulture = string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase)
            ? "ar-KW"
            : "en";
        return Redirect($"/auth-profiles?culture={normalizedCulture}#profile-{authProfileId:D}");
    }
}
