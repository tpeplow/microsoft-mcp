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

public class DataAccessRoleListCommandTests : CommandUnitTestsBase<DataAccessRoleListCommand, IFabricApiService>
{
    [Fact]
    public void Constructor_InitializesMetadata()
    {
        Assert.Equal("list_data_access_roles", Command.Name);
        Assert.True(Command.Metadata.ReadOnly);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRoles()
    {
        using var doc = JsonDocument.Parse("{\"value\":[]}");
        var payload = doc.RootElement.Clone();
        Service.ListDataAccessRolesAsync("ws", "item", null, Arg.Any<CancellationToken>()).Returns(payload);

        var response = await ExecuteCommandAsync("--workspace-id", "ws", "--item-id", "item");

        var result = ValidateAndDeserializeResponse(response, OneLakeJsonContext.Default.DataAccessRoleListCommandResult);
        Assert.Equal("ws", result.Workspace);
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
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleListCommand(null!, Service));
        Assert.Throws<ArgumentNullException>(() => new DataAccessRoleListCommand(Logger, null!));
    }
}
