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

namespace Fabric.Mcp.Tools.OneLake.Commands.Settings;

[CommandMetadata(
    Id = "d290417e-4d0a-4b86-9b6b-3e2c0c8b71a8",
    Name = "get_settings",
    Title = "Get OneLake Workspace Settings",
    Description = "Get OneLake-level workspace settings (diagnostics, immutability policy, etc.) for a workspace. Read-only — use the modify_* tools to change individual sections. Requires `Workspace.Read.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = true,
    Secret = false)]
public sealed class OneLakeSettingsGetCommand(
    ILogger<OneLakeSettingsGetCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<OneLakeSettingsGetOptions>()
{
    private readonly ILogger<OneLakeSettingsGetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    protected override OneLakeSettingsGetOptions BindOptions(ParseResult parseResult)
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
            var data = await _fabricApiService.GetOneLakeSettingsAsync(workspace, cancellationToken);
            var result = new OneLakeSettingsGetCommandResult { Workspace = workspace, Settings = data };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.OneLakeSettingsGetCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OneLake settings.");
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class OneLakeSettingsGetCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public JsonElement Settings { get; init; }
    }
}
