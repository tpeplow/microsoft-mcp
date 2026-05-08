// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Commands.Settings;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Settings;

public class OneLakeDiagnosticsModifyCommandTests : CommandUnitTestsBase<OneLakeDiagnosticsModifyCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("modify_diagnostics", Command.Name);
        Assert.True(Command.Metadata.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_PatchesDiagnostics()
    {
        using var doc = JsonDocument.Parse("{\"ok\":true}");
        var payload = doc.RootElement.Clone();
        Service.ModifyOneLakeDiagnosticsAsync("ws", Arg.Any<JsonElement>(), Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws",
            "--diagnostics", "{\"enabled\":true}");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.OneLakeDiagnosticsModifyCommandResult);
        Assert.Equal("ws", result.Workspace);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDiagnostics_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new OneLakeDiagnosticsModifyCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new OneLakeDiagnosticsModifyCommand(Logger, null!));
    }
}
