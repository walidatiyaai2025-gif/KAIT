using System.Globalization;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("execute")]
public sealed class ServiceExecutionController(
    IMetadataCatalogService catalog,
    IGsipPermissionEvaluator permissions,
    IServiceExecutionEngine executionEngine) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid? entityId,
        Guid? serviceId,
        Guid? environmentId,
        CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(
            entityId,
            serviceId,
            environmentId,
            submittedInputs: null,
            validationErrors: null,
            executionResult: null,
            cancellationToken);
        return View("~/Views/Execution/Index.cshtml", model);
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(
        [FromForm] ServiceExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EntityId == Guid.Empty || request.ServiceId == Guid.Empty || request.EnvironmentId == Guid.Empty)
        {
            return Forbid();
        }

        var selection = await BuildModelAsync(
            request.EntityId,
            request.ServiceId,
            request.EnvironmentId,
            request.Inputs,
            validationErrors: null,
            executionResult: null,
            cancellationToken);

        if (selection.SelectedEntityId != request.EntityId
            || selection.SelectedServiceId != request.ServiceId
            || selection.SelectedEnvironmentId != request.EnvironmentId)
        {
            return Forbid();
        }

        try
        {
            var result = await executionEngine.ExecuteAsync(
                new ServiceExecutionCommand(User, request.ServiceId, request.EnvironmentId, request.Inputs),
                cancellationToken);

            var model = await BuildModelAsync(
                request.EntityId,
                request.ServiceId,
                request.EnvironmentId,
                request.Inputs,
                validationErrors: null,
                executionResult: result,
                cancellationToken);
            return View("~/Views/Execution/Index.cshtml", model);
        }
        catch (ServiceExecutionValidationException exception)
        {
            var model = await BuildModelAsync(
                request.EntityId,
                request.ServiceId,
                request.EnvironmentId,
                request.Inputs,
                exception.Errors,
                executionResult: null,
                cancellationToken);
            return View("~/Views/Execution/Index.cshtml", model);
        }
        catch (ServiceExecutionRejectedException)
        {
            return Forbid();
        }
    }

    private async Task<ServiceExecutionViewModel> BuildModelAsync(
        Guid? entityId,
        Guid? serviceId,
        Guid? environmentId,
        IReadOnlyDictionary<string, string?>? submittedInputs,
        IReadOnlyDictionary<string, string>? validationErrors,
        ServiceExecutionResult? executionResult,
        CancellationToken cancellationToken)
    {
        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        var isArabic = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        var activeServices = snapshot.Services
            .Where(service => service.Active && service.IsCurrent)
            .OrderBy(service => service.Code, StringComparer.Ordinal)
            .ToArray();

        var authorizedServices = new List<GSIP.Domain.Metadata.CatalogService>();
        foreach (var service in activeServices)
        {
            if (await permissions.HasServicePermissionAsync(
                    User,
                    service.Code,
                    GsipPermissions.ServicesExecute,
                    cancellationToken))
            {
                authorizedServices.Add(service);
            }
        }

        var authorizedEntityIds = authorizedServices.Select(service => service.EntityId).ToHashSet();
        var entities = snapshot.Entities
            .Where(entity => entity.Active && authorizedEntityIds.Contains(entity.Id))
            .OrderBy(entity => entity.DisplayOrder)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .Select(entity => new ExecutionOptionViewModel(
                entity.Id,
                entity.Code,
                isArabic ? entity.NameAr : entity.NameEn))
            .ToArray();

        var selectedEntityId = entityId is Guid requestedEntityId && authorizedEntityIds.Contains(requestedEntityId)
            ? requestedEntityId
            : entities.FirstOrDefault()?.Id;

        var serviceOptions = authorizedServices
            .Where(service => selectedEntityId is null || service.EntityId == selectedEntityId)
            .Select(service => new ExecutionServiceOptionViewModel(
                service.Id,
                service.EntityId,
                service.Code,
                isArabic ? service.NameAr : service.NameEn))
            .ToArray();

        var selectedService = serviceId is Guid requestedServiceId
            ? authorizedServices.SingleOrDefault(service => service.Id == requestedServiceId && service.EntityId == selectedEntityId)
            : null;
        selectedService ??= authorizedServices.FirstOrDefault(service => service.EntityId == selectedEntityId);

        var activeEnvironmentIds = selectedService?.EnvironmentConfigs
            .Where(config => config.Active)
            .Select(config => config.EnvironmentId)
            .ToHashSet() ?? [];
        var environments = snapshot.Environments
            .Where(environment => environment.Active && activeEnvironmentIds.Contains(environment.Id))
            .OrderBy(environment => environment.DisplayOrder)
            .ThenBy(environment => environment.Code, StringComparer.Ordinal)
            .Select(environment => new ExecutionOptionViewModel(
                environment.Id,
                environment.Code,
                isArabic && !string.IsNullOrWhiteSpace(environment.NameAr) ? environment.NameAr :
                !isArabic && !string.IsNullOrWhiteSpace(environment.NameEn) ? environment.NameEn : environment.Code))
            .ToArray();

        var selectedEnvironmentId = environmentId is Guid requestedEnvironmentId
            && activeEnvironmentIds.Contains(requestedEnvironmentId)
                ? requestedEnvironmentId
                : environments.FirstOrDefault()?.Id;

        ExecutionServiceDetailsViewModel? details = null;
        if (selectedService is not null && selectedEnvironmentId is Guid exactEnvironmentId)
        {
            var config = selectedService.EnvironmentConfigs.SingleOrDefault(item =>
                item.Active && item.EnvironmentId == exactEnvironmentId);
            var entity = snapshot.Entities.Single(item => item.Id == selectedService.EntityId);
            var environment = snapshot.Environments.Single(item => item.Id == exactEnvironmentId);
            if (config is not null)
            {
                details = new ExecutionServiceDetailsViewModel(
                    isArabic ? entity.NameAr : entity.NameEn,
                    isArabic ? selectedService.NameAr : selectedService.NameEn,
                    isArabic ? selectedService.DescriptionAr : selectedService.DescriptionEn,
                    environment.Code,
                    config.HttpMethod,
                    config.RelativePath,
                    config.ContentType,
                    config.TimeoutSeconds,
                    config.AuthProfileId.HasValue,
                    selectedService.Fields
                        .OrderBy(field => field.DisplayOrder)
                        .Select(field => new ExecutionFieldViewModel(
                            field.Key,
                            isArabic ? field.LabelAr : field.LabelEn,
                            field.FieldType,
                            field.Required,
                            field.MinLength,
                            field.MaxLength,
                            field.Minimum,
                            field.Maximum,
                            field.Regex,
                            field.Sensitive))
                        .ToArray());
            }
        }

        var safeSubmittedValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (selectedService is not null && submittedInputs is not null)
        {
            foreach (var field in selectedService.Fields.Where(field => !field.Sensitive))
            {
                if (submittedInputs.TryGetValue(field.Key, out var value))
                {
                    safeSubmittedValues[field.Key] = value;
                }
            }
        }

        return new ServiceExecutionViewModel
        {
            Entities = entities,
            Services = serviceOptions,
            Environments = environments,
            SelectedEntityId = selectedEntityId,
            SelectedServiceId = selectedService?.Id,
            SelectedEnvironmentId = selectedEnvironmentId,
            SelectedService = details,
            SubmittedValues = safeSubmittedValues,
            ValidationErrors = validationErrors ?? new Dictionary<string, string>(),
            ExecutionResult = executionResult
        };
    }
}
