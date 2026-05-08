// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Mcp.Core.Options;

namespace Fabric.Mcp.Tools.OneLake.Options;

public sealed class PrincipalAccessGetOptions : GlobalOptions
{
    public string? WorkspaceId { get; set; }
    public string? Workspace { get; set; }
    public string? ItemId { get; set; }
    public string? Item { get; set; }
    public string? PrincipalId { get; set; }
    public string? PrincipalType { get; set; }
    public string? InputPath { get; set; }
    public string? ContinuationToken { get; set; }
    public int? MaxResults { get; set; }
}
