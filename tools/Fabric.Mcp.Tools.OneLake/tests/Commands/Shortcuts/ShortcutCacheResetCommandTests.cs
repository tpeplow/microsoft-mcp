// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Shortcuts;

public class ShortcutCacheResetCommandTests : CommandUnitTestsBase<ShortcutCacheResetCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("reset_shortcut_cache", Command.Name);
        Assert.True(Command.Metadata.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_ResetsCache()
    {
        Service.ResetShortcutCacheAsync("ws", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var response = await ExecuteCommandAsync("--workspace-id", "ws");

        await Service.Received(1).ResetShortcutCacheAsync("ws", Arg.Any<CancellationToken>());
        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.ShortcutCacheResetCommandResult);
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
        Assert.Throws<ArgumentNullException>(() => new ShortcutCacheResetCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new ShortcutCacheResetCommand(Logger, null!));
    }
}
