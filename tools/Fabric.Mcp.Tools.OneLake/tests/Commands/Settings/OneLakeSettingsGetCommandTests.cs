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

public class OneLakeSettingsGetCommandTests : CommandUnitTestsBase<OneLakeSettingsGetCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("get_settings", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSettings()
    {
        using var doc = JsonDocument.Parse("{\"diagnostics\":{}}");
        var payload = doc.RootElement.Clone();
        Service.GetOneLakeSettingsAsync("ws", Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync("--workspace-id", "ws");
        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.OneLakeSettingsGetCommandResult);
        Assert.Equal("ws", result.Workspace);
    }

    [Fact]
    public async Task ExecuteAsync_MissingWorkspace_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new OneLakeSettingsGetCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new OneLakeSettingsGetCommand(Logger, null!));
    }
}
