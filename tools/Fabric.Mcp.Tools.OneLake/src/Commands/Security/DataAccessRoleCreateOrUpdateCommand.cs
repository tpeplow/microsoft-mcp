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
    Id = "aeb78630-3d18-4769-9c9b-2ce8e6a4c2f0",
    Name = "create_or_update_data_access_role",
    Title = "Create Or Update OneLake Data Access Role",
    Description = "Create a new OneLake data access role or replace an existing one. Body specifies decision rules and member principals. Optionally pass `etag` for optimistic concurrency on updates. Requires `OneLake.ReadWrite.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class DataAccessRoleCreateOrUpdateCommand(
    ILogger<DataAccessRoleCreateOrUpdateCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<DataAccessRoleCreateOrUpdateOptions>()
{
    private readonly ILogger<DataAccessRoleCreateOrUpdateCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ItemId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Item.AsOptional());
        command.Options.Add(FabricOptionDefinitions.RoleName.AsRequired());
        command.Options.Add(FabricOptionDefinitions.Definition.AsRequired());
        command.Options.Add(FabricOptionDefinitions.Etag.AsOptional());
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

    protected override DataAccessRoleCreateOrUpdateOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ItemId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
        options.Item = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
        options.RoleName = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.RoleName.Name);
        options.Definition = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Definition.Name);
        options.Etag = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Etag.Name);
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
            using var doc = JsonDocument.Parse(options.Definition!);
            var payload = doc.RootElement.Clone();

            var workspace = !string.IsNullOrWhiteSpace(options.WorkspaceId) ? options.WorkspaceId! : options.Workspace!;
            var item = !string.IsNullOrWhiteSpace(options.ItemId) ? options.ItemId! : options.Item!;

            var response = await _fabricApiService.CreateOrUpdateDataAccessRoleAsync(workspace, item, options.RoleName!, payload, options.Etag, cancellationToken);
            var result = new DataAccessRoleCreateOrUpdateCommandResult
            {
                Workspace = workspace,
                Item = item,
                RoleName = options.RoleName!,
                Response = response
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.DataAccessRoleCreateOrUpdateCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating data access role {RoleName}.", options.RoleName);
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class DataAccessRoleCreateOrUpdateCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty;
        public JsonElement Response { get; init; }
    }
}
