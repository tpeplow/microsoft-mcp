// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Fabric.Mcp.Tools.OneLake.Commands.Security;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Mcp.Tests.Client;
using NSubstitute;

namespace Fabric.Mcp.Tools.OneLake.Tests.Commands.Security;

public class DataAccessRoleDeleteCommandTests : CommandUnitTestsBase<DataAccessRoleDeleteCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("delete_data_access_role", Command.Name);
        Assert.True(Command.Metadata.Destructive);
        Assert.True(Command.Metadata.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesRole()
    {
        Service.DeleteDataAccessRoleAsync("ws", "item", "role1", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var response = await ExecuteCommandAsync(
            "--workspace-id", "ws", "--item-id", "item", "--role-name", "role1");

        await Service.Received(1).DeleteDataAccessRoleAsync("ws", "item", "role1", Arg.Any<CancellationToken>());
        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.DataAccessRoleDeleteCommandResult);
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
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleDeleteCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleDeleteCommand(Logger, null!));
    }
}
