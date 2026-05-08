// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Fabric.Mcp.Tools.OneLake.Models;
using Fabric.Mcp.Tools.OneLake.Options;
using Fabric.Mcp.Tools.OneLake.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Extensions;
using Microsoft.Mcp.Core.Models.Option;

namespace Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;

[CommandMetadata(
    Id = "1641b33e-230a-43fd-8d83-9e7aaefa405a",
    Name = "delete_shortcut",
    Title = "Delete OneLake Shortcut",
    Description = "Delete a single shortcut from an item. Destructive but the destination data is preserved — only the shortcut reference is removed. Requires `OneLake.ReadWrite.All`.",
    Destructive = true,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class ShortcutDeleteCommand(
    ILogger<ShortcutDeleteCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<ShortcutDeleteOptions>()
{
    private readonly ILogger<ShortcutDeleteCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    protected override ShortcutDeleteOptions BindOptions(ParseResult parseResult)
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
            await _fabricApiService.DeleteShortcutAsync(workspace, item, options.ShortcutPath!, options.ShortcutName!, cancellationToken);
            var result = new ShortcutDeleteCommandResult
            {
                Workspace = workspace,
                Item = item,
                ShortcutPath = options.ShortcutPath!,
                ShortcutName = options.ShortcutName!,
                Message = "Shortcut deleted successfully."
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.ShortcutDeleteCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shortcut {ShortcutName}.", options.ShortcutName);
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class ShortcutDeleteCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public string ShortcutPath { get; init; } = string.Empty;
        public string ShortcutName { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
