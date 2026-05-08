// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Shortcuts;

public class ShortcutCreateOrUpdateCommandTests : CommandUnitTestsBase<ShortcutCreateOrUpdateCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("create_or_update_shortcuts", Command.Name);
        Assert.True(Command.Metadata.Idempotent);
        Assert.False(Command.Metadata.ReadOnly);
    }

    [Fact]
    public async Task ExecuteAsync_PostsDefinition()
    {
        using var resp = JsonDocument.Parse("{\"created\":1}");
        var clone = resp.RootElement.Clone();
        Service.CreateOrUpdateShortcutsAsync("ws", "item", Arg.Any<JsonElement>(), true, Arg.Any<CancellationToken>())
            .Returns(clone);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws",
            "--item-id", "item",
            "--definition", "{\"shortcuts\":[]}",
            "--create-or-overwrite", "true");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.ShortcutCreateOrUpdateCommandResult);
        Assert.True(result.CreateOrOverwrite);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDefinition_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws", "--item-id", "item");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ShortcutCreateOrUpdateCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new ShortcutCreateOrUpdateCommand(Logger, null!));
    }
}
