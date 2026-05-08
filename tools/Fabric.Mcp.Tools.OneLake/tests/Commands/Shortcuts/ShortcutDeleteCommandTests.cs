// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Shortcuts;

public class ShortcutDeleteCommandTests : CommandUnitTestsBase<ShortcutDeleteCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("delete_shortcut", Command.Name);
        Assert.True(Command.Metadata.Destructive);
        Assert.True(Command.Metadata.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesShortcut()
    {
        Service.DeleteShortcutAsync("ws", "item", "Files", "sc1", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item",
            "--shortcut-path", "Files", "--shortcut-name", "sc1");

        await Service.Received(1).DeleteShortcutAsync("ws", "item", "Files", "sc1", Arg.Any<CancellationToken>());
        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.ShortcutDeleteCommandResult);
        Assert.Equal("sc1", result.ShortcutName);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequired_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws", "--item-id", "item");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ShortcutDeleteCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new ShortcutDeleteCommand(Logger, null!));
    }
}
