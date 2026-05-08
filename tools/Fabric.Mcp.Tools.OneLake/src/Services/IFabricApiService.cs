// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Fabric.Mcp.Tools.OneLake.Services;

/// <summary>
/// Service for the Fabric Core REST API surface used by OneLake security, shortcut and
/// settings tools (https://api.fabric.microsoft.com/v1). This is intentionally separate
/// from <see cref="IOneLakeService"/>, which owns the OneLake storage data plane (DFS / Blob /
/// Table endpoints, <c>storage.azure.com</c> audience). The two surfaces use different audiences
/// and should not be conflated.
/// </summary>
public interface IFabricApiService
{
    // Shortcuts
    Task<JsonElement> ListShortcutsAsync(string workspaceIdentifier, string itemIdentifier, string? parentPath, string? continuationToken, CancellationToken cancellationToken = default);
    Task<JsonElement> GetShortcutAsync(string workspaceIdentifier, string itemIdentifier, string shortcutPath, string shortcutName, CancellationToken cancellationToken = default);
    Task<JsonElement> CreateOrUpdateShortcutsAsync(string workspaceIdentifier, string itemIdentifier, JsonElement payload, bool createOrOverwrite, CancellationToken cancellationToken = default);
    Task DeleteShortcutAsync(string workspaceIdentifier, string itemIdentifier, string shortcutPath, string shortcutName, CancellationToken cancellationToken = default);
    Task ResetShortcutCacheAsync(string workspaceIdentifier, CancellationToken cancellationToken = default);

    // Data access security
    Task<JsonElement> ListDataAccessRolesAsync(string workspaceIdentifier, string itemIdentifier, string? continuationToken, CancellationToken cancellationToken = default);
    Task<JsonElement> GetDataAccessRoleAsync(string workspaceIdentifier, string itemIdentifier, string roleName, CancellationToken cancellationToken = default);
    Task<JsonElement> CreateOrUpdateDataAccessRoleAsync(string workspaceIdentifier, string itemIdentifier, string roleName, JsonElement definition, string? etag, CancellationToken cancellationToken = default);
    Task DeleteDataAccessRoleAsync(string workspaceIdentifier, string itemIdentifier, string roleName, CancellationToken cancellationToken = default);
    Task<JsonElement> GetPrincipalAccessAsync(string workspaceIdentifier, string itemIdentifier, JsonElement requestPayload, string? continuationToken, int? maxResults, CancellationToken cancellationToken = default);

    // OneLake settings (workspace-scoped)
    Task<JsonElement> GetOneLakeSettingsAsync(string workspaceIdentifier, CancellationToken cancellationToken = default);
    Task<JsonElement> ModifyOneLakeDiagnosticsAsync(string workspaceIdentifier, JsonElement payload, CancellationToken cancellationToken = default);
    Task<JsonElement> ModifyOneLakeImmutabilityPolicyAsync(string workspaceIdentifier, JsonElement payload, CancellationToken cancellationToken = default);
}
