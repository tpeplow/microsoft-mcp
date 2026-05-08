// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Mcp.Core.Commands;

namespace Fabric.Mcp.Tools.OneLake.Tests;

public class FabricOneLakeSetupTests
{
    [Fact]
    public void ConfigureServices_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var setup = new FabricOneLakeSetup();

        // Act
        setup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, s => s.ServiceType == typeof(IOneLakeService));
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Arrange
        var setup = new FabricOneLakeSetup();

        // Act & Assert
        Assert.Equal("onelake", setup.Name);
    }

    [Fact]
    public void RegisterCommands_RegistersAllOneLakeCommands()
    {
        // Arrange
        var services = new ServiceCollection();
        var setup = new FabricOneLakeSetup();
        setup.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        // Act
        var rootGroup = setup.RegisterCommands(provider);

        // Assert - flat structure with verb_object naming
        Assert.True(rootGroup.Commands.ContainsKey("list_workspaces"), "Should have list_workspaces command");
        Assert.True(rootGroup.Commands.ContainsKey("list_items"), "Should have list_items command");
        Assert.True(rootGroup.Commands.ContainsKey("list_items_dfs"), "Should have list_items_dfs command");
        Assert.True(rootGroup.Commands.ContainsKey("list_files"), "Should have list_files command");
        Assert.True(rootGroup.Commands.ContainsKey("download_file"), "Should have download_file command");
        Assert.True(rootGroup.Commands.ContainsKey("upload_file"), "Should have upload_file command");
        Assert.True(rootGroup.Commands.ContainsKey("delete_file"), "Should have delete_file command");
        Assert.True(rootGroup.Commands.ContainsKey("create_directory"), "Should have create_directory command");
        Assert.True(rootGroup.Commands.ContainsKey("delete_directory"), "Should have delete_directory command");

        // Table commands
        Assert.True(rootGroup.Commands.ContainsKey("get_table_config"), "Should have get_table_config command");
        Assert.True(rootGroup.Commands.ContainsKey("list_tables"), "Should have list_tables command");
        Assert.True(rootGroup.Commands.ContainsKey("get_table"), "Should have get_table command");
        Assert.True(rootGroup.Commands.ContainsKey("list_table_namespaces"), "Should have list_table_namespaces command");
        Assert.True(rootGroup.Commands.ContainsKey("get_table_namespace"), "Should have get_table_namespace command");

        // Shortcuts
        Assert.True(rootGroup.Commands.ContainsKey("list_shortcuts"), "Should have list_shortcuts command");
        Assert.True(rootGroup.Commands.ContainsKey("get_shortcut"), "Should have get_shortcut command");
        Assert.True(rootGroup.Commands.ContainsKey("create_or_update_shortcuts"), "Should have create_or_update_shortcuts command");
        Assert.True(rootGroup.Commands.ContainsKey("delete_shortcut"), "Should have delete_shortcut command");
        Assert.True(rootGroup.Commands.ContainsKey("reset_shortcut_cache"), "Should have reset_shortcut_cache command");

        // Security
        Assert.True(rootGroup.Commands.ContainsKey("list_data_access_roles"), "Should have list_data_access_roles command");
        Assert.True(rootGroup.Commands.ContainsKey("get_data_access_role"), "Should have get_data_access_role command");
        Assert.True(rootGroup.Commands.ContainsKey("create_or_update_data_access_role"), "Should have create_or_update_data_access_role command");
        Assert.True(rootGroup.Commands.ContainsKey("delete_data_access_role"), "Should have delete_data_access_role command");
        Assert.True(rootGroup.Commands.ContainsKey("get_principal_access"), "Should have get_principal_access command");

        // Settings
        Assert.True(rootGroup.Commands.ContainsKey("get_settings"), "Should have get_settings command");
        Assert.True(rootGroup.Commands.ContainsKey("modify_diagnostics"), "Should have modify_diagnostics command");
        Assert.True(rootGroup.Commands.ContainsKey("modify_immutability_policy"), "Should have modify_immutability_policy command");

        // Removed legacy commands
        Assert.False(rootGroup.Commands.ContainsKey("blob_list"), "blob_list should be removed");
        Assert.False(rootGroup.Commands.ContainsKey("blob_delete"), "blob_delete should be removed");
    }
}
