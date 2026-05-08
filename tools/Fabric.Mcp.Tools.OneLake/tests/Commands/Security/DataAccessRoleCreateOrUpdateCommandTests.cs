// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Commands.Security;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Security;

public class DataAccessRoleCreateOrUpdateCommandTests : CommandUnitTestsBase<DataAccessRoleCreateOrUpdateCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("create_or_update_data_access_role", Command.Name);
        Assert.True(Command.Metadata.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_PutsRole()
    {
        using var doc = JsonDocument.Parse("{\"ok\":true}");
        var payload = doc.RootElement.Clone();
        Service.CreateOrUpdateDataAccessRoleAsync("ws", "item", "role1", Arg.Any<JsonElement>(), null, Arg.Any<CancellationToken>())
            .Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item",
            "--role-name", "role1",
            "--definition", "{\"members\":[]}");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.DataAccessRoleCreateOrUpdateCommandResult);
        Assert.Equal("role1", result.RoleName);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDefinition_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item", "--role-name", "role1");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleCreateOrUpdateCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleCreateOrUpdateCommand(Logger, null!));
    }
}
