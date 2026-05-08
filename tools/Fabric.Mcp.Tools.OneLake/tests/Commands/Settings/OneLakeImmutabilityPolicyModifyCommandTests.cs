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

public class OneLakeImmutabilityPolicyModifyCommandTests : CommandUnitTestsBase<OneLakeImmutabilityPolicyModifyCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("modify_immutability_policy", Command.Name);
        Assert.True(Command.Metadata.Idempotent);
        Assert.Contains("irreversible", Command.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AppliesPolicy()
    {
        using var doc = JsonDocument.Parse("{\"ok\":true}");
        var payload = doc.RootElement.Clone();
        Service.ModifyOneLakeImmutabilityPolicyAsync("ws", Arg.Any<JsonElement>(), Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws",
            "--immutability-policy", "{\"enabled\":true}");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.OneLakeImmutabilityPolicyModifyCommandResult);
        Assert.Equal("ws", result.Workspace);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPolicy_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new OneLakeImmutabilityPolicyModifyCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new OneLakeImmutabilityPolicyModifyCommand(Logger, null!));
    }
}
