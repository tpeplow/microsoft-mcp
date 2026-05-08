// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Fabric.Mcp.Tools.OneLake.Services;

/// <summary>
/// Fabric Core API constants used by the OneLake security/shortcut/settings tools.
/// Mirrors the constants in Fabric.Mcp.Tools.Core; we duplicate rather than take a
/// project reference to avoid a circular dependency between the OneLake and Core tools.
/// The OneLake-specific endpoint values continue to live in OneLakeEndpoints (Models/OneLakeModels.cs).
/// </summary>
internal static class FabricEndpoints
{
    public const string FabricApiBaseUrl = "https://api.fabric.microsoft.com/v1";
    public const string FabricScope = "https://api.fabric.microsoft.com/.default";
    public static readonly string[] FabricScopes = [FabricScope];
}
