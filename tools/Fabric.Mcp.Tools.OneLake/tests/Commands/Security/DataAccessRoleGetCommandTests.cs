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

public class DataAccessRoleGetCommandTests : CommandUnitTestsBase<DataAccessRoleGetCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("get_data_access_role", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRole()
    {
        using var doc = JsonDocument.Parse("{\"name\":\"role1\"}");
        var payload = doc.RootElement.Clone();
        Service.GetDataAccessRoleAsync("ws", "item", "role1", Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item", "--role-name", "role1");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.DataAccessRoleGetCommandResult);
        Assert.Equal("role1", result.RoleName);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRoleName_ReturnsBadRequest()
    {
        var response = await ExecuteCommandAsync("--workspace-id", "ws", "--item-id", "item");
        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleGetCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleGetCommand(Logger, null!));
    }
}
