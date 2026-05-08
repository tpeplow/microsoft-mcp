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

namespace Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;

[CommandMetadata(
    Id = "19df63c4-26f0-457f-b8b0-cc446741dc44",
    Name = "get_shortcut",
    Title = "Get OneLake Shortcut",
    Description = "Get the properties of a single shortcut (name, path, target, configuration). Requires `OneLake.Read.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = true,
    Secret = false)]
public sealed class ShortcutGetCommand(
    ILogger<ShortcutGetCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<ShortcutGetOptions>()
{
    private readonly ILogger<ShortcutGetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ItemId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Item.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ShortcutPath.AsRequired());
        command.Options.Add(FabricOptionDefinitions.ShortcutName.AsRequired());
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

    protected override ShortcutGetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ItemId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
        options.Item = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
        options.ShortcutPath = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ShortcutPath.Name);
        options.ShortcutName = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ShortcutName.Name);
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
            var data = await _fabricApiService.GetShortcutAsync(workspace, item, options.ShortcutPath!, options.ShortcutName!, cancellationToken);
            var result = new ShortcutGetCommandResult
            {
                Workspace = workspace,
                Item = item,
                ShortcutPath = options.ShortcutPath!,
                ShortcutName = options.ShortcutName!,
                Shortcut = data
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.ShortcutGetCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shortcut {ShortcutName}.", options.ShortcutName);
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class ShortcutGetCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public string ShortcutPath { get; init; } = string.Empty;
        public string ShortcutName { get; init; } = string.Empty;
        public JsonElement Shortcut { get; init; }
    }
}
