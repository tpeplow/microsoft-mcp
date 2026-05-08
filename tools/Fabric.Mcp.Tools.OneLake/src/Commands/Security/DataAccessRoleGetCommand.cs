// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Options;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Extensions;
using Microsoft.Mcp.Core.Models.Option;

namespace Fabric.Mcp.Tools.OneLake.Commands.Security;

[CommandMetadata(
    Id = "13ed680e-5c14-4d23-9f25-2ba0f5e2cb24",
    Name = "get_data_access_role",
    Title = "Get OneLake Data Access Role",
    Description = "Get the full definition of a single OneLake data access role, including decision rules and member principals. Requires `OneLake.Read.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = true,
    Secret = false)]
public sealed class DataAccessRoleGetCommand(
    ILogger<DataAccessRoleGetCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<DataAccessRoleGetOptions>()
{
    private readonly ILogger<DataAccessRoleGetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ItemId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Item.AsOptional());
        command.Options.Add(FabricOptionDefinitions.RoleName.AsRequired());
        command.Validators.Add(result =>
        {
            var workspaceId = result.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
            var workspace = result.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
            var itemId = result.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
            var item = result.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
            if (string.IsNullOrWhiteSpace(workspaceId) && string.IsNullOrWhiteSpace(workspace))
            {
                result.AddError("Workspace identifier is required. Provide --workspace or --workspace-id.");
            }
            if (string.IsNullOrWhiteSpace(item) && string.IsNullOrWhiteSpace(itemId))
            {
                result.AddError("Item identifier is required. Provide --item or --item-id.");
            }
        });
    }

    protected override DataAccessRoleGetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ItemId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
        options.Item = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
        options.RoleName = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.RoleName.Name);
        return options;
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!Validate(parseResult.CommandResult, context.Response).IsValid)
        {
            return context.Response;
        }

        var options = BindOptions(parseResult);
        try
        {
            var workspace = !string.IsNullOrWhiteSpace(options.WorkspaceId) ? options.WorkspaceId! : options.Workspace!;
            var item = !string.IsNullOrWhiteSpace(options.ItemId) ? options.ItemId! : options.Item!;
            var data = await _fabricApiService.GetDataAccessRoleAsync(workspace, item, options.RoleName!, cancellationToken);
            var result = new DataAccessRoleGetCommandResult
            {
                Workspace = workspace,
                Item = item,
                RoleName = options.RoleName!,
                Role = data
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.DataAccessRoleGetCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data access role {RoleName}.", options.RoleName);
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class DataAccessRoleGetCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty;
        public JsonElement Role { get; init; }
    }
}
