// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace Fabric.Mcp.Tools.OneLake.Services;

/// <summary>
/// Calls the Fabric Core REST API for OneLake security, shortcut and settings operations.
///
/// Workspace and item identifiers are resolved through <see cref="IOneLakeService"/> so that
/// callers can continue to pass friendly names (e.g. <c>MyLakehouse.Lakehouse</c>) rather than
/// GUIDs — this matches the behaviour of the existing OneLake table tools.
///
/// TODO(vNext): the exact Fabric REST URL templates here are best-effort and should be re-verified
/// against the current Microsoft Fabric REST API docs before merging — particularly the
/// workspace-scoped settings endpoints (PATCH vs PUT semantics) and the principal-access
/// continuation/maxResults query parameter names.
/// </summary>
public class FabricApiService(HttpClient httpClient, IOneLakeService oneLakeService, TokenCredential? credential = null) : IFabricApiService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly IOneLakeService _oneLakeService = oneLakeService ?? throw new ArgumentNullException(nameof(oneLakeService));
    private readonly TokenCredential _credential = credential ?? new DefaultAzureCredential();

    private const string UserAgentHeaderName = "User-Agent";
    private const string UserAgentHeaderValue = "Fabric API MCP";
    private const string ContentTypeJson = "application/json";

    // ------------------- Shortcuts -------------------

    public async Task<JsonElement> ListShortcutsAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string? parentPath,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/shortcuts";

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            query.Add($"parentPath={Uri.EscapeDataString(parentPath)}");
        }
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            query.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        return await SendJsonAsync(HttpMethod.Get, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> GetShortcutAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string shortcutPath,
        string shortcutName,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var encodedPath = Uri.EscapeDataString(shortcutPath.Trim('/'));
        var encodedName = Uri.EscapeDataString(shortcutName);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/shortcuts/{encodedPath}/{encodedName}";
        return await SendJsonAsync(HttpMethod.Get, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> CreateOrUpdateShortcutsAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        JsonElement payload,
        bool createOrOverwrite,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/shortcuts";
        if (createOrOverwrite)
        {
            url += "?createOrOverwrite=true";
        }

        return await SendJsonAsync(HttpMethod.Post, url, payload.GetRawText(), headers: null, cancellationToken);
    }

    public async Task DeleteShortcutAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string shortcutPath,
        string shortcutName,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var encodedPath = Uri.EscapeDataString(shortcutPath.Trim('/'));
        var encodedName = Uri.EscapeDataString(shortcutName);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/shortcuts/{encodedPath}/{encodedName}";
        await SendNoContentAsync(HttpMethod.Delete, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task ResetShortcutCacheAsync(
        string workspaceIdentifier,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = NormalizeWorkspace(workspaceIdentifier);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/onelake/resetShortcutCache";
        await SendNoContentAsync(HttpMethod.Post, url, jsonContent: null, headers: null, cancellationToken);
    }

    // ------------------- Data access security -------------------

    public async Task<JsonElement> ListDataAccessRolesAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/dataAccessRoles";
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            url += $"?continuationToken={Uri.EscapeDataString(continuationToken)}";
        }
        return await SendJsonAsync(HttpMethod.Get, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> GetDataAccessRoleAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/dataAccessRoles/{Uri.EscapeDataString(roleName)}?preview=true";
        return await SendJsonAsync(HttpMethod.Get, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> CreateOrUpdateDataAccessRoleAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string roleName,
        JsonElement definition,
        string? etag,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        // The single-role create-or-update PUT targets the *collection* URL, not the per-role URL.
        // Role identity is carried in the body's `value[0].name` field.
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/dataAccessRoles?preview=true&dataAccessRoleConflictPolicy=Overwrite";

        // The Fabric API expects the role wrapped in a `value` array: { "value": [ <role> ] }.
        // Accept either shape from the caller — if they already wrapped, ensure name matches; if
        // they passed a bare role object, wrap it for them and inject the name from the path arg.
        string body;
        if (definition.ValueKind == JsonValueKind.Object && definition.TryGetProperty("value", out var existingValue) && existingValue.ValueKind == JsonValueKind.Array)
        {
            body = definition.GetRawText();
        }
        else
        {
            var hasName = definition.ValueKind == JsonValueKind.Object && definition.TryGetProperty("name", out _);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                writer.WriteStartArray();
                if (hasName)
                {
                    definition.WriteTo(writer);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", roleName);
                    foreach (var prop in definition.EnumerateObject())
                    {
                        prop.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            body = Encoding.UTF8.GetString(stream.ToArray());
        }

        Dictionary<string, string>? headers = null;
        if (!string.IsNullOrWhiteSpace(etag))
        {
            headers = new Dictionary<string, string> { ["If-Match"] = etag };
        }

        return await SendJsonAsync(HttpMethod.Put, url, body, headers, cancellationToken);
    }

    public async Task DeleteDataAccessRoleAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/dataAccessRoles/{Uri.EscapeDataString(roleName)}?preview=true";
        await SendNoContentAsync(HttpMethod.Delete, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> GetPrincipalAccessAsync(
        string workspaceIdentifier,
        string itemIdentifier,
        JsonElement requestPayload,
        string? continuationToken,
        int? maxResults,
        CancellationToken cancellationToken = default)
    {
        var (workspaceId, itemId) = await ResolveAsync(workspaceIdentifier, itemIdentifier, cancellationToken);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/dataAccessRoles/principalAccess";

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            query.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }
        if (maxResults.HasValue)
        {
            query.Add($"maxResults={maxResults.Value}");
        }
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        return await SendJsonAsync(HttpMethod.Post, url, requestPayload.GetRawText(), headers: null, cancellationToken);
    }

    // ------------------- Workspace settings -------------------

    public async Task<JsonElement> GetOneLakeSettingsAsync(
        string workspaceIdentifier,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = NormalizeWorkspace(workspaceIdentifier);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/onelake/settings";
        return await SendJsonAsync(HttpMethod.Get, url, jsonContent: null, headers: null, cancellationToken);
    }

    public async Task<JsonElement> ModifyOneLakeDiagnosticsAsync(
        string workspaceIdentifier,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = NormalizeWorkspace(workspaceIdentifier);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/onelake/settings/modifyDiagnostics";
        return await SendJsonAsync(HttpMethod.Post, url, payload.GetRawText(), headers: null, cancellationToken);
    }

    public async Task<JsonElement> ModifyOneLakeImmutabilityPolicyAsync(
        string workspaceIdentifier,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = NormalizeWorkspace(workspaceIdentifier);
        var url = $"{FabricEndpoints.FabricApiBaseUrl}/workspaces/{workspaceId}/onelake/settings/modifyImmutabilityPolicy";
        return await SendJsonAsync(HttpMethod.Post, url, payload.GetRawText(), headers: null, cancellationToken);
    }

    // ------------------- Helpers -------------------

    private async Task<(string WorkspaceId, string ItemId)> ResolveAsync(string workspaceIdentifier, string itemIdentifier, CancellationToken cancellationToken)
    {
        var workspaceId = NormalizeWorkspace(workspaceIdentifier);
        var itemId = await _oneLakeService.ResolveItemIdentifierAsync(workspaceId, itemIdentifier, cancellationToken);
        return (workspaceId, itemId);
    }

    private static string NormalizeWorkspace(string workspaceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(workspaceIdentifier))
        {
            throw new ArgumentException("Workspace identifier is required.", nameof(workspaceIdentifier));
        }

        return workspaceIdentifier.Trim();
    }

    private async Task<JsonElement> SendJsonAsync(
        HttpMethod method,
        string url,
        string? jsonContent,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, url, jsonContent, headers, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (stream is null || stream.Length == 0)
        {
            return EmptyJsonObject;
        }

        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return doc.RootElement.Clone();
    }

    private static readonly JsonElement EmptyJsonObject = JsonDocument.Parse("{}").RootElement.Clone();

    private async Task SendNoContentAsync(
        HttpMethod method,
        string url,
        string? jsonContent,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, url, jsonContent, headers, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method,
        string url,
        string? jsonContent,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var tokenContext = new TokenRequestContext(FabricEndpoints.FabricScopes);
        var token = await _credential.GetTokenAsync(tokenContext, cancellationToken);

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation(UserAgentHeaderName, UserAgentHeaderValue);

        if (headers is not null)
        {
            foreach (var kvp in headers)
            {
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }

        if (!string.IsNullOrEmpty(jsonContent))
        {
            request.Content = new StringContent(jsonContent, Encoding.UTF8, ContentTypeJson);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Fabric API request failed with status {(int)response.StatusCode} ({response.StatusCode}): {content}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}
