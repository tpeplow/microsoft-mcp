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

public class ShortcutListCommandTests : CommandUnitTestsBase<ShortcutListCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("list_shortcuts", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
        Assert.True(Command.Metadata.Idempotent);
        Assert.False(Command.Metadata.Destructive);
    }

    [Fact]
    public void GetCommand_ReturnsConfiguredCommand()
    {
        Assert.Equal("list_shortcuts", CommandDefinition.Name);
        Assert.NotEmpty(CommandDefinition.Options);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsShortcuts()
    {
        const string workspaceId = "47242da5-ff3b-46fb-a94f-977909b773d5";
        const string itemId = "0e67ed13-2bb6-49be-9c87-a1105a4ea342";
        using var doc = JsonDocument.Parse("{\"value\":[{\"name\":\"sc1\"}]}");
        var payload = doc.RootElement.Clone();

        Service.ListShortcutsAsync(workspaceId, itemId, null, null, Arg.Any<CancellationToken>())
            .Returns(payload);

        var response = await ExecuteCommandAsync("--workspace-id", workspaceId, "--item-id", itemId);

        await Service.Received(1).ListShortcutsAsync(workspaceId, itemId, null, null, Arg.Any<CancellationToken>());
        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.ShortcutListCommandResult);
        Assert.Equal(workspaceId, result.Workspace);
        Assert.Equal(itemId, result.Item);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequired_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ShortcutListCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new ShortcutListCommand(Logger, null!));
    }
}
