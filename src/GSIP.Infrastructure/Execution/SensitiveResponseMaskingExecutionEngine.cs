using System.Text.Json;
using System.Text.Json.Nodes;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Domain.Metadata;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Enforces response-side masking before execution results cross the infrastructure boundary.
/// Sensitive result mappings are redacted from RawResponse as well as StructuredResult.
/// Any ambiguity while sensitive mappings exist suppresses RawResponse rather than failing open.
/// </summary>
public sealed class SensitiveResponseMaskingExecutionEngine(
    IServiceExecutionEngine inner,
    IMetadataCatalogService metadataCatalog) : IServiceExecutionEngine
{
    private const string Mask = "[MASKED]";

    public async Task<ServiceExecutionResult> ExecuteAsync(
        ServiceExecutionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await inner.ExecuteAsync(command, cancellationToken);
        if (string.IsNullOrEmpty(result.RawResponse))
            return result;

        try
        {
            var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
            var matches = snapshot.Services
                .Where(service => service.Id == command.ServiceId && service.IsCurrent && service.Active)
                .Take(2)
                .ToArray();

            if (matches.Length != 1)
                return result with { RawResponse = string.Empty };

            var sensitiveMappings = matches[0].ResultMappings
                .Where(mapping => mapping.Sensitive)
                .OrderBy(mapping => mapping.DisplayOrder)
                .ToArray();

            if (sensitiveMappings.Length == 0)
                return result;

            return result with
            {
                RawResponse = MaskSensitiveRawResponse(result.RawResponse, sensitiveMappings)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A response governed by sensitive mappings must never be returned unredacted when
            // metadata lookup or redaction cannot be completed safely.
            return result with { RawResponse = string.Empty };
        }
    }

    private static string MaskSensitiveRawResponse(
        string rawResponse,
        IReadOnlyList<ResultMappingDefinition> sensitiveMappings)
    {
        try
        {
            var root = JsonNode.Parse(rawResponse);
            if (root is null)
                return string.Empty;

            foreach (var mapping in sensitiveMappings)
            {
                if (!TryMaskPath(root, mapping.SourcePath))
                    return string.Empty;
            }

            return root.ToJsonString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static bool TryMaskPath(JsonNode root, string sourcePath)
    {
        if (!TryParsePath(sourcePath, out var tokens) || tokens.Count == 0)
            return false;

        JsonNode? current = root;
        for (var index = 0; index < tokens.Count - 1; index++)
        {
            if (!TryGetChild(current, tokens[index], out current) || current is null)
                return false;
        }

        return TryReplaceChild(current, tokens[^1]);
    }

    private static bool TryParsePath(string sourcePath, out IReadOnlyList<PathToken> tokens)
    {
        var parsed = new List<PathToken>();
        tokens = parsed;

        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        var normalized = sourcePath.Trim();
        if (string.Equals(normalized, "$", StringComparison.Ordinal))
            return false;
        if (normalized.StartsWith("$.", StringComparison.Ordinal))
            normalized = normalized[2..];
        else if (normalized.StartsWith('$'))
            return false;

        if (normalized.Length == 0)
            return false;

        foreach (var segment in normalized.Split('.', StringSplitOptions.None))
        {
            if (segment.Length == 0)
                return false;

            var bracket = segment.IndexOf('[');
            var propertyName = bracket >= 0 ? segment[..bracket] : segment;
            if (propertyName.Length > 0)
                parsed.Add(PathToken.Property(propertyName));

            var position = bracket;
            while (position >= 0)
            {
                if (segment[position] != '[')
                    return false;

                var end = segment.IndexOf(']', position + 1);
                if (end < 0
                    || !int.TryParse(segment[(position + 1)..end], out var arrayIndex)
                    || arrayIndex < 0)
                    return false;

                parsed.Add(PathToken.Index(arrayIndex));
                position = end + 1;
                if (position == segment.Length)
                    break;
                if (segment[position] != '[')
                    return false;
            }
        }

        return parsed.Count > 0;
    }

    private static bool TryGetChild(JsonNode? parent, PathToken token, out JsonNode? child)
    {
        child = null;
        if (token.PropertyName is not null)
        {
            if (parent is not JsonObject obj || !obj.TryGetPropertyValue(token.PropertyName, out child))
                return false;
            return true;
        }

        if (parent is not JsonArray array
            || token.ArrayIndex is not int arrayIndex
            || arrayIndex >= array.Count)
            return false;

        child = array[arrayIndex];
        return true;
    }

    private static bool TryReplaceChild(JsonNode? parent, PathToken token)
    {
        if (token.PropertyName is not null)
        {
            if (parent is not JsonObject obj || !obj.ContainsKey(token.PropertyName))
                return false;
            obj[token.PropertyName] = Mask;
            return true;
        }

        if (parent is not JsonArray array
            || token.ArrayIndex is not int arrayIndex
            || arrayIndex >= array.Count)
            return false;

        array[arrayIndex] = Mask;
        return true;
    }

    private sealed record PathToken(string? PropertyName, int? ArrayIndex)
    {
        public static PathToken Property(string name) => new(name, null);
        public static PathToken Index(int index) => new(null, index);
    }
}
