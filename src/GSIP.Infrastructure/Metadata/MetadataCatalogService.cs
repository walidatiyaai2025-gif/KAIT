using System.Text.Json;
using System.Text.RegularExpressions;
using GSIP.Application.Abstractions;
using GSIP.Application.Metadata;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Metadata;

public sealed class MetadataCatalogService(GsipDbContext dbContext, ISystemClock clock) : IMetadataCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    private static readonly HashSet<string> ForbiddenHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization", "X-Api-Key", "Api-Key", "Consumer-Secret", "X-Consumer-Secret"
    };

    public async Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.CatalogEntities
            .AsNoTracking()
            .OrderBy(entity => entity.DisplayOrder)
            .ThenBy(entity => entity.Code)
            .ToListAsync(cancellationToken);
        var services = await dbContext.CatalogServices
            .AsNoTracking()
            .Where(service => service.IsCurrent)
            .Include(service => service.EnvironmentConfigs)
            .Include(service => service.Fields)
            .Include(service => service.ResultMappings)
            .OrderBy(service => service.Code)
            .ToListAsync(cancellationToken);
        var environments = await dbContext.CatalogEnvironments
            .AsNoTracking()
            .OrderBy(environment => environment.DisplayOrder)
            .ToListAsync(cancellationToken);
        return new MetadataCatalogSnapshot(entities, services, environments);
    }

    public async Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default)
    {
        var normalized = ValidateEntity(input);
        if (await dbContext.CatalogEntities.AnyAsync(entity => entity.Code == normalized.Code, cancellationToken))
        {
            throw new InvalidOperationException($"Entity code '{normalized.Code}' already exists.");
        }

        var now = clock.UtcNow;
        var entity = new CatalogEntity
        {
            Id = Guid.NewGuid(),
            Code = normalized.Code,
            NameAr = normalized.NameAr,
            NameEn = normalized.NameEn,
            Logo = normalized.Logo,
            Active = normalized.Active,
            DisplayOrder = normalized.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.CatalogEntities.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default)
    {
        var normalized = ValidateEntity(input);
        var entity = await dbContext.CatalogEntities.SingleOrDefaultAsync(candidate => candidate.Id == entityId, cancellationToken)
            ?? throw new KeyNotFoundException("Entity not found.");
        if (await dbContext.CatalogEntities.AnyAsync(candidate => candidate.Id != entityId && candidate.Code == normalized.Code, cancellationToken))
        {
            throw new InvalidOperationException($"Entity code '{normalized.Code}' already exists.");
        }

        entity.Code = normalized.Code;
        entity.NameAr = normalized.NameAr;
        entity.NameEn = normalized.NameEn;
        entity.Logo = normalized.Logo;
        entity.Active = normalized.Active;
        entity.DisplayOrder = normalized.DisplayOrder;
        entity.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CatalogEntities.SingleOrDefaultAsync(candidate => candidate.Id == entityId, cancellationToken)
            ?? throw new KeyNotFoundException("Entity not found.");
        var hasActiveServices = await dbContext.CatalogServices
            .AnyAsync(service => service.EntityId == entityId && service.IsCurrent && service.Active, cancellationToken);
        if (hasActiveServices)
        {
            throw new InvalidOperationException("An entity with active services cannot be deactivated.");
        }
        entity.Active = false;
        entity.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CatalogEntities.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == entityId, cancellationToken)
            ?? throw new KeyNotFoundException("Entity not found.");
        if (!entity.Active)
        {
            throw new InvalidOperationException("Services cannot be added to an inactive entity.");
        }
        var normalized = ValidateService(input);
        if (await dbContext.CatalogServices.AnyAsync(
                service => service.EntityId == entityId && service.Code == normalized.Code && service.IsCurrent,
                cancellationToken))
        {
            throw new InvalidOperationException($"Service code '{normalized.Code}' already exists for this entity.");
        }

        var now = clock.UtcNow;
        var service = new CatalogService
        {
            Id = Guid.NewGuid(),
            DefinitionKey = Guid.NewGuid(),
            EntityId = entityId,
            Code = normalized.Code,
            NameAr = normalized.NameAr,
            NameEn = normalized.NameEn,
            DescriptionAr = normalized.DescriptionAr,
            DescriptionEn = normalized.DescriptionEn,
            Active = normalized.Active,
            Version = 1,
            IsCurrent = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.CatalogServices.Add(service);
        await AddChildrenAsync(service, normalized, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return service;
    }

    public async Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default)
    {
        var normalized = ValidateService(input);
        var service = await LoadCurrentServiceAsync(serviceId, cancellationToken);
        if (await dbContext.CatalogServices.AnyAsync(
                candidate => candidate.Id != service.Id
                    && candidate.EntityId == service.EntityId
                    && candidate.Code == normalized.Code
                    && candidate.IsCurrent,
                cancellationToken))
        {
            throw new InvalidOperationException($"Service code '{normalized.Code}' already exists for this entity.");
        }

        if (service.FirstUsedAtUtc.HasValue)
        {
            service.IsCurrent = false;
            service.UpdatedAtUtc = clock.UtcNow;
            var revision = CreateRevision(service, normalized, activeOverride: null);
            dbContext.CatalogServices.Add(revision);
            await AddChildrenAsync(revision, normalized, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return revision;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ServiceEnvironmentConfigs.RemoveRange(service.EnvironmentConfigs);
        dbContext.ServiceFieldDefinitions.RemoveRange(service.Fields);
        dbContext.ResultMappingDefinitions.RemoveRange(service.ResultMappings);
        await dbContext.SaveChangesAsync(cancellationToken);

        service.Code = normalized.Code;
        service.NameAr = normalized.NameAr;
        service.NameEn = normalized.NameEn;
        service.DescriptionAr = normalized.DescriptionAr;
        service.DescriptionEn = normalized.DescriptionEn;
        service.Active = normalized.Active;
        service.UpdatedAtUtc = clock.UtcNow;
        await AddChildrenAsync(service, normalized, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return service;
    }

    public async Task<CatalogService> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await LoadCurrentServiceAsync(serviceId, cancellationToken);
        if (!service.FirstUsedAtUtc.HasValue)
        {
            service.Active = false;
            service.UpdatedAtUtc = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return service;
        }

        service.IsCurrent = false;
        service.UpdatedAtUtc = clock.UtcNow;
        var revision = CloneRevision(service, active: false);
        dbContext.CatalogServices.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await dbContext.CatalogServices.SingleOrDefaultAsync(candidate => candidate.Id == serviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Service not found.");
        service.FirstUsedAtUtc ??= clock.UtcNow;
        service.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var environmentCodes = snapshot.Environments.ToDictionary(environment => environment.Id, environment => environment.Code);
        var package = new MetadataPackage
        {
            SchemaVersion = 1,
            Entities = snapshot.Entities.Select(entity => new MetadataEntityPackage
            {
                Code = entity.Code,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                Logo = entity.Logo,
                Active = entity.Active,
                DisplayOrder = entity.DisplayOrder,
                Services = snapshot.Services
                    .Where(service => service.EntityId == entity.Id)
                    .OrderBy(service => service.Code)
                    .Select(service => new MetadataServicePackage
                    {
                        Code = service.Code,
                        NameAr = service.NameAr,
                        NameEn = service.NameEn,
                        DescriptionAr = service.DescriptionAr,
                        DescriptionEn = service.DescriptionEn,
                        Active = service.Active,
                        EnvironmentConfigs = service.EnvironmentConfigs
                            .OrderBy(config => environmentCodes.TryGetValue(config.EnvironmentId, out var code) ? code : string.Empty)
                            .Select(config => new ServiceEnvironmentInput(
                                environmentCodes[config.EnvironmentId],
                                config.BaseUrl,
                                config.RelativePath,
                                config.HttpMethod,
                                config.ContentType,
                                config.NonSecretHeadersJson,
                                config.TimeoutSeconds,
                                config.TlsPolicy,
                                config.ValidateServerCertificate,
                                config.ProxyUrl,
                                config.HealthPath,
                                config.HealthMethod,
                                config.Active,
                                config.AuthProfileId))
                            .ToList(),
                        Fields = service.Fields
                            .OrderBy(field => field.DisplayOrder)
                            .Select(field => new ServiceFieldInput(
                                field.Key,
                                field.LabelAr,
                                field.LabelEn,
                                field.FieldType,
                                field.Required,
                                field.Regex,
                                field.Minimum,
                                field.Maximum,
                                field.MinLength,
                                field.MaxLength,
                                field.OptionsJson,
                                field.DisplayOrder,
                                field.Sensitive,
                                field.Masking))
                            .ToList(),
                        ResultMappings = service.ResultMappings
                            .OrderBy(mapping => mapping.DisplayOrder)
                            .Select(mapping => new ResultMappingInput(
                                mapping.SourcePath,
                                mapping.LabelAr,
                                mapping.LabelEn,
                                mapping.ResultType,
                                mapping.Formatter,
                                mapping.Sensitive,
                                mapping.DisplayOrder))
                            .ToList()
                    })
                    .ToList()
            }).ToList()
        };
        return JsonSerializer.Serialize(package, JsonOptions);
    }

    public async Task<MetadataImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 2_000_000)
        {
            throw new ArgumentException("Metadata JSON is empty or exceeds the 2 MB import limit.", nameof(json));
        }
        MetadataPackage package;
        try
        {
            package = JsonSerializer.Deserialize<MetadataPackage>(json, JsonOptions)
                ?? throw new InvalidOperationException("Metadata package is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Metadata JSON is invalid.", exception);
        }
        if (package.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported metadata schema version '{package.SchemaVersion}'.");
        }
        if (package.Entities.Count > 500)
        {
            throw new InvalidOperationException("Metadata package contains too many entities.");
        }

        var entitiesProcessed = 0;
        var servicesProcessed = 0;
        var revisionsCreated = 0;
        foreach (var entityPackage in package.Entities)
        {
            var entityInput = new EntityInput(
                entityPackage.Code,
                entityPackage.NameAr,
                entityPackage.NameEn,
                entityPackage.Logo,
                entityPackage.Active,
                entityPackage.DisplayOrder);
            var normalizedEntityCode = NormalizeCode(entityPackage.Code, 40, "entity code");
            var existingEntity = await dbContext.CatalogEntities
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Code == normalizedEntityCode, cancellationToken);
            var entity = existingEntity is null
                ? await CreateEntityAsync(entityInput, cancellationToken)
                : await UpdateEntityAsync(existingEntity.Id, entityInput, cancellationToken);
            entitiesProcessed++;

            if (entityPackage.Services.Count > 500)
            {
                throw new InvalidOperationException($"Entity '{entity.Code}' contains too many services.");
            }
            foreach (var servicePackage in entityPackage.Services)
            {
                var input = new ServiceInput(
                    servicePackage.Code,
                    servicePackage.NameAr,
                    servicePackage.NameEn,
                    servicePackage.DescriptionAr,
                    servicePackage.DescriptionEn,
                    servicePackage.Active,
                    servicePackage.EnvironmentConfigs,
                    servicePackage.Fields,
                    servicePackage.ResultMappings);
                var normalizedServiceCode = NormalizeCode(servicePackage.Code, 120, "service code");
                var existingService = await dbContext.CatalogServices
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        service => service.EntityId == entity.Id && service.Code == normalizedServiceCode && service.IsCurrent,
                        cancellationToken);
                if (existingService is null)
                {
                    await CreateServiceAsync(entity.Id, input, cancellationToken);
                }
                else
                {
                    var next = await UpdateServiceAsync(existingService.Id, input, cancellationToken);
                    if (next.Id != existingService.Id)
                    {
                        revisionsCreated++;
                    }
                }
                servicesProcessed++;
            }
        }
        return new MetadataImportResult(entitiesProcessed, servicesProcessed, revisionsCreated);
    }

    private async Task<CatalogService> LoadCurrentServiceAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        return await dbContext.CatalogServices
            .Include(service => service.EnvironmentConfigs)
            .Include(service => service.Fields)
            .Include(service => service.ResultMappings)
            .SingleOrDefaultAsync(service => service.Id == serviceId && service.IsCurrent, cancellationToken)
            ?? throw new KeyNotFoundException("Current service definition not found.");
    }

    private CatalogService CreateRevision(CatalogService previous, ServiceInput input, bool? activeOverride)
    {
        var now = clock.UtcNow;
        return new CatalogService
        {
            Id = Guid.NewGuid(),
            DefinitionKey = previous.DefinitionKey,
            EntityId = previous.EntityId,
            Code = input.Code,
            NameAr = input.NameAr,
            NameEn = input.NameEn,
            DescriptionAr = input.DescriptionAr,
            DescriptionEn = input.DescriptionEn,
            Active = activeOverride ?? input.Active,
            Version = previous.Version + 1,
            IsCurrent = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private CatalogService CloneRevision(CatalogService previous, bool active)
    {
        var now = clock.UtcNow;
        var clone = new CatalogService
        {
            Id = Guid.NewGuid(),
            DefinitionKey = previous.DefinitionKey,
            EntityId = previous.EntityId,
            Code = previous.Code,
            NameAr = previous.NameAr,
            NameEn = previous.NameEn,
            DescriptionAr = previous.DescriptionAr,
            DescriptionEn = previous.DescriptionEn,
            Active = active,
            Version = previous.Version + 1,
            IsCurrent = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        foreach (var config in previous.EnvironmentConfigs)
        {
            clone.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
            {
                Id = Guid.NewGuid(), EnvironmentId = config.EnvironmentId, BaseUrl = config.BaseUrl, RelativePath = config.RelativePath,
                HttpMethod = config.HttpMethod, ContentType = config.ContentType, NonSecretHeadersJson = config.NonSecretHeadersJson,
                TimeoutSeconds = config.TimeoutSeconds, TlsPolicy = config.TlsPolicy, ValidateServerCertificate = config.ValidateServerCertificate,
                ProxyUrl = config.ProxyUrl, HealthPath = config.HealthPath, HealthMethod = config.HealthMethod, Active = config.Active,
                AuthProfileId = config.AuthProfileId
            });
        }
        foreach (var field in previous.Fields)
        {
            clone.Fields.Add(new ServiceFieldDefinition
            {
                Id = Guid.NewGuid(), Key = field.Key, LabelAr = field.LabelAr, LabelEn = field.LabelEn, FieldType = field.FieldType,
                Required = field.Required, Regex = field.Regex, Minimum = field.Minimum, Maximum = field.Maximum,
                MinLength = field.MinLength, MaxLength = field.MaxLength, OptionsJson = field.OptionsJson, DisplayOrder = field.DisplayOrder,
                Sensitive = field.Sensitive, Masking = field.Masking
            });
        }
        foreach (var mapping in previous.ResultMappings)
        {
            clone.ResultMappings.Add(new ResultMappingDefinition
            {
                Id = Guid.NewGuid(), SourcePath = mapping.SourcePath, LabelAr = mapping.LabelAr, LabelEn = mapping.LabelEn,
                ResultType = mapping.ResultType, Formatter = mapping.Formatter, Sensitive = mapping.Sensitive, DisplayOrder = mapping.DisplayOrder
            });
        }
        return clone;
    }

    private async Task AddChildrenAsync(CatalogService service, ServiceInput input, CancellationToken cancellationToken)
    {
        var environments = await dbContext.CatalogEnvironments.AsNoTracking().ToDictionaryAsync(environment => environment.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var config in input.EnvironmentConfigs)
        {
            var code = NormalizeEnvironmentCode(config.EnvironmentCode);
            if (!environments.TryGetValue(code, out var environment) || !environment.Active)
            {
                throw new InvalidOperationException($"Environment '{code}' is not available.");
            }
            var headers = NormalizeHeaders(config.NonSecretHeadersJson);
            ValidateEndpoint(config, code);
            service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
            {
                Id = Guid.NewGuid(),
                EnvironmentId = environment.Id,
                BaseUrl = config.BaseUrl.Trim().TrimEnd('/'),
                RelativePath = NormalizeRelativePath(config.RelativePath),
                HttpMethod = config.HttpMethod.Trim().ToUpperInvariant(),
                ContentType = NormalizeText(config.ContentType, 120, "content type"),
                NonSecretHeadersJson = headers,
                TimeoutSeconds = config.TimeoutSeconds,
                TlsPolicy = NormalizeText(config.TlsPolicy, 80, "TLS policy"),
                ValidateServerCertificate = config.ValidateServerCertificate,
                ProxyUrl = config.ProxyUrl?.Trim() ?? string.Empty,
                HealthPath = string.IsNullOrWhiteSpace(config.HealthPath) ? string.Empty : NormalizeRelativePath(config.HealthPath),
                HealthMethod = string.IsNullOrWhiteSpace(config.HealthMethod) ? "HEAD" : config.HealthMethod.Trim().ToUpperInvariant(),
                Active = config.Active,
                AuthProfileId = config.AuthProfileId
            });
        }

        foreach (var field in input.Fields.OrderBy(field => field.DisplayOrder))
        {
            service.Fields.Add(new ServiceFieldDefinition
            {
                Id = Guid.NewGuid(),
                Key = NormalizeCode(field.Key, 120, "field key"),
                LabelAr = NormalizeText(field.LabelAr, 200, "Arabic field label"),
                LabelEn = NormalizeText(field.LabelEn, 200, "English field label"),
                FieldType = NormalizeText(field.FieldType, 40, "field type").ToLowerInvariant(),
                Required = field.Required,
                Regex = ValidateRegex(field.Regex),
                Minimum = field.Minimum,
                Maximum = field.Maximum,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                OptionsJson = NormalizeOptions(field.OptionsJson),
                DisplayOrder = field.DisplayOrder,
                Sensitive = field.Sensitive,
                Masking = field.Sensitive
                    ? NormalizeText(string.IsNullOrWhiteSpace(field.Masking) ? "Partial" : field.Masking, 40, "masking policy")
                    : "None"
            });
        }
        foreach (var mapping in input.ResultMappings.OrderBy(mapping => mapping.DisplayOrder))
        {
            service.ResultMappings.Add(new ResultMappingDefinition
            {
                Id = Guid.NewGuid(),
                SourcePath = NormalizeText(mapping.SourcePath, 500, "result source path"),
                LabelAr = NormalizeText(mapping.LabelAr, 200, "Arabic result label"),
                LabelEn = NormalizeText(mapping.LabelEn, 200, "English result label"),
                ResultType = NormalizeText(mapping.ResultType, 40, "result type").ToLowerInvariant(),
                Formatter = mapping.Formatter?.Trim() ?? string.Empty,
                Sensitive = mapping.Sensitive,
                DisplayOrder = mapping.DisplayOrder
            });
        }
    }

    private static EntityInput ValidateEntity(EntityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input with
        {
            Code = NormalizeCode(input.Code, 40, "entity code"),
            NameAr = NormalizeText(input.NameAr, 200, "Arabic entity name"),
            NameEn = NormalizeText(input.NameEn, 200, "English entity name"),
            Logo = (input.Logo ?? string.Empty).Trim()
        };
    }

    private static ServiceInput ValidateService(ServiceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input.EnvironmentConfigs ??= [];
        input.Fields ??= [];
        input.ResultMappings ??= [];
        if (input.EnvironmentConfigs.Count != 2
            || input.EnvironmentConfigs.Select(config => NormalizeEnvironmentCode(config.EnvironmentCode)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2
            || !input.EnvironmentConfigs.Any(config => NormalizeEnvironmentCode(config.EnvironmentCode) == CatalogEnvironmentCodes.Uat)
            || !input.EnvironmentConfigs.Any(config => NormalizeEnvironmentCode(config.EnvironmentCode) == CatalogEnvironmentCodes.Production))
        {
            throw new InvalidOperationException("Every service definition must contain exactly one UAT and one Production environment configuration.");
        }
        if (input.Fields.Count > 250 || input.ResultMappings.Count > 250)
        {
            throw new InvalidOperationException("Service metadata exceeds the supported field or result-mapping limit.");
        }
        var duplicateField = input.Fields.GroupBy(field => NormalizeCode(field.Key, 120, "field key"), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateField is not null)
        {
            throw new InvalidOperationException($"Duplicate service field key '{duplicateField.Key}'.");
        }
        foreach (var field in input.Fields)
        {
            if (field.Minimum.HasValue && field.Maximum.HasValue && field.Minimum > field.Maximum)
            {
                throw new InvalidOperationException($"Field '{field.Key}' minimum exceeds maximum.");
            }
            if (field.MinLength.HasValue && field.MaxLength.HasValue && field.MinLength > field.MaxLength)
            {
                throw new InvalidOperationException($"Field '{field.Key}' minimum length exceeds maximum length.");
            }
        }
        return input with
        {
            Code = NormalizeCode(input.Code, 120, "service code"),
            NameAr = NormalizeText(input.NameAr, 200, "Arabic service name"),
            NameEn = NormalizeText(input.NameEn, 200, "English service name"),
            DescriptionAr = input.DescriptionAr?.Trim() ?? string.Empty,
            DescriptionEn = input.DescriptionEn?.Trim() ?? string.Empty
        };
    }

    private static void ValidateEndpoint(ServiceEnvironmentInput config, string environmentCode)
    {
        if (!AllowedMethods.Contains(config.HttpMethod?.Trim() ?? string.Empty))
        {
            throw new InvalidOperationException($"HTTP method '{config.HttpMethod}' is not supported.");
        }
        if (config.TimeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("Timeout must be between 1 and 300 seconds.");
        }
        if (!Uri.TryCreate(config.BaseUrl?.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("BaseUrl must be an absolute HTTP or HTTPS URL.");
        }
        if (environmentCode == CatalogEnvironmentCodes.Production && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Production BaseUrl must use HTTPS.");
        }
        if (environmentCode == CatalogEnvironmentCodes.Production && !config.ValidateServerCertificate)
        {
            throw new InvalidOperationException("Production server certificate validation cannot be disabled.");
        }
        _ = NormalizeRelativePath(config.RelativePath);
        if (!string.IsNullOrWhiteSpace(config.ProxyUrl)
            && (!Uri.TryCreate(config.ProxyUrl.Trim(), UriKind.Absolute, out var proxyUri)
                || (proxyUri.Scheme != Uri.UriSchemeHttps && proxyUri.Scheme != Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException("ProxyUrl must be an absolute HTTP or HTTPS URL.");
        }
        if (!string.IsNullOrWhiteSpace(config.HealthMethod) && !AllowedMethods.Contains(config.HealthMethod.Trim()))
        {
            throw new InvalidOperationException("Health method is not supported.");
        }
    }

    private static string NormalizeHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Non-secret headers must be a JSON object.");
        }
        var headers = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (ForbiddenHeaderNames.Contains(property.Name))
            {
                throw new InvalidOperationException($"Header '{property.Name}' is secret-bearing and cannot be stored as metadata.");
            }
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Non-secret header values must be strings.");
            }
            headers[NormalizeText(property.Name, 120, "header name")] = property.Value.GetString() ?? string.Empty;
        }
        return JsonSerializer.Serialize(headers, JsonOptions);
    }

    private static string NormalizeOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array
            || document.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new InvalidOperationException("Field options must be a JSON array of strings.");
        }
        return JsonSerializer.Serialize(document.RootElement, JsonOptions);
    }

    private static string ValidateRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return string.Empty;
        }
        if (pattern.Length > 500)
        {
            throw new InvalidOperationException("Regex metadata exceeds 500 characters.");
        }
        _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return pattern;
    }

    private static string NormalizeEnvironmentCode(string? value)
    {
        if (string.Equals(value?.Trim(), CatalogEnvironmentCodes.Uat, StringComparison.OrdinalIgnoreCase))
        {
            return CatalogEnvironmentCodes.Uat;
        }
        if (string.Equals(value?.Trim(), CatalogEnvironmentCodes.Production, StringComparison.OrdinalIgnoreCase))
        {
            return CatalogEnvironmentCodes.Production;
        }
        throw new InvalidOperationException($"Unknown environment '{value}'. Only UAT and Production are allowed in P05.");
    }

    private static string NormalizeRelativePath(string? value)
    {
        var path = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.Length > 500 || !path.StartsWith('/', StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("RelativePath must be a relative absolute-path starting with one '/'.");
        }
        return path;
    }

    private static string NormalizeCode(string? value, int maxLength, string fieldName)
    {
        var normalized = NormalizeText(value, maxLength, fieldName).ToUpperInvariant();
        if (!normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            throw new InvalidOperationException($"{fieldName} may contain only ASCII letters, digits, '.', '_' and '-'.");
        }
        return normalized;
    }

    private static string NormalizeText(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{fieldName} must contain 1 to {maxLength} printable characters.");
        }
        return normalized;
    }
}
