// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Core.TestUtilities;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Shortcuts;

public class ShortcutGetCommandTests : CommandUnitTestsBase<ShortcutGetCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("get_shortcut", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
    }

    [Fact]
    public void GetCommand_ReturnsConfiguredCommand()
    {
        Assert.Equal("get_shortcut", CommandDefinition.Name);
        Assert.NotEmpty(CommandDefinition.Options);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsShortcut()
    {
        const string ws = "ws";
        const string item = "item";
        using var doc = JsonDocument.Parse("{\"name\":\"sc1\"}");
        var payload = doc.RootElement.Clone();

        Service.GetShortcutAsync(ws, item, "Files", "sc1", Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", ws, "--item-id", item, "--shortcut-path", "Files", "--shortcut-name", "sc1");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.ShortcutGetCommandResult);
        Assert.Equal("sc1", result.ShortcutName);
    }

    [Theory]
    [InlineData("--shortcut-path")]
    [InlineData("--shortcut-name")]
    public async Task ExecuteAsync_MissingRequired_ReturnsBadRequest(string missing)
    {
        var args = ArgBuilder.BuildArgs(missing,
            ("--workspace-id", "ws"),
            ("--item-id", "item"),
            ("--shortcut-path", "Files"),
            ("--shortcut-name", "sc1"));
        var response = await ExecuteCommandAsync(args);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ShortcutGetCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new ShortcutGetCommand(Logger, null!));
    }
}
