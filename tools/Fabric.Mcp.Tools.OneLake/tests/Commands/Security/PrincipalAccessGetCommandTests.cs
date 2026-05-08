// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Commands.Security;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Core.TestUtilities;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Security;

public class PrincipalAccessGetCommandTests : CommandUnitTestsBase<PrincipalAccessGetCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("get_principal_access", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
        Assert.Contains("Preview", Command.Description);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAccess()
    {
        using var doc = JsonDocument.Parse("{\"value\":[]}");
        var payload = doc.RootElement.Clone();
        Service.GetPrincipalAccessAsync("ws", "item", Arg.Any<JsonElement>(), null, null, Arg.Any<CancellationToken>())
            .Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item",
            "--principal-id", "p1", "--principal-type", "User");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.PrincipalAccessGetCommandResult);
        Assert.Equal("p1", result.PrincipalId);
        Assert.Equal("User", result.PrincipalType);
    }

    [Theory]
    [InlineData("--principal-id")]
    [InlineData("--principal-type")]
    public async Task ExecuteAsync_MissingRequired_ReturnsBadRequest(string missing)
    {
        var args = ArgBuilder.BuildArgs(missing,
            ("--workspace-id", "ws"),
            ("--item-id", "item"),
            ("--principal-id", "p1"),
            ("--principal-type", "User"));
        var response = await ExecuteCommandAsync(args);
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new PrincipalAccessGetCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new PrincipalAccessGetCommand(Logger, null!));
    }
}
