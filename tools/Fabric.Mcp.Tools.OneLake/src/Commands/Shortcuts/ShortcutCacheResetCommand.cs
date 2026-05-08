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
    Id = "4883d2f8-ab5d-4f37-8ac2-9e0e000a5881",
    Name = "reset_shortcut_cache",
    Title = "Reset OneLake Shortcut Cache",
    Description = "Drop cached shortcut reads for an item, forcing the next read to re-resolve from the destination. Use sparingly — primarily for debugging stale-cache issues. Requires `OneLake.ReadWrite.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class ShortcutCacheResetCommand(
    ILogger<ShortcutCacheResetCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<ShortcutCacheResetOptions>()
{
    private readonly ILogger<ShortcutCacheResetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Validators.Add(result =>
        {
            var workspaceId = result.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
            var workspace = result.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
            if (string.IsNullOrWhiteSpace(workspaceId) && string.IsNullOrWhiteSpace(workspace))
            {
                result.AddError("Workspace identifier is required. Provide --workspace or --workspace-id.");
            }
        });
    }

    protected override ShortcutCacheResetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
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
            await _fabricApiService.ResetShortcutCacheAsync(workspace, cancellationToken);
            var result = new ShortcutCacheResetCommandResult { Workspace = workspace, Message = "Shortcut cache reset." };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.ShortcutCacheResetCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting shortcut cache.");
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class ShortcutCacheResetCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
