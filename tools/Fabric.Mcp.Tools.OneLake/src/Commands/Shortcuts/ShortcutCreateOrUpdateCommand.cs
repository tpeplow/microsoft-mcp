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
    Id = "afc1d19d-3d1b-49cb-8191-2a7808ea8a11",
    Name = "create_or_update_shortcuts",
    Title = "Create Or Update OneLake Shortcuts",
    Description = "Create one or more shortcuts in a single call. Pass `--definition` with a JSON body containing either a single shortcut object or an array of shortcuts. By default, fails if any shortcut already exists; pass `createOrOverwrite=true` to upsert. Use this for both initial creation and updates. Note: newly created shortcuts can take ~30 seconds to become consistent for listing operations (e.g. `list_files` against the shortcut path), though direct path-based reads work immediately. Requires `OneLake.ReadWrite.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class ShortcutCreateOrUpdateCommand(
    ILogger<ShortcutCreateOrUpdateCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<ShortcutCreateOrUpdateOptions>()
{
    private readonly ILogger<ShortcutCreateOrUpdateCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ItemId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Item.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Definition.AsRequired());
        command.Options.Add(FabricOptionDefinitions.CreateOrOverwrite.AsOptional());
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

    protected override ShortcutCreateOrUpdateOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ItemId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
        options.Item = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
        options.Definition = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Definition.Name);
        options.CreateOrOverwrite = parseResult.GetValueOrDefault<bool>(FabricOptionDefinitions.CreateOrOverwrite.Name);
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

            var response = await _fabricApiService.CreateOrUpdateShortcutsAsync(workspace, item, payload, options.CreateOrOverwrite, cancellationToken);
            var result = new ShortcutCreateOrUpdateCommandResult
            {
                Workspace = workspace,
                Item = item,
                CreateOrOverwrite = options.CreateOrOverwrite,
                Response = response
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.ShortcutCreateOrUpdateCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or updating shortcuts.");
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class ShortcutCreateOrUpdateCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public bool CreateOrOverwrite { get; init; }
        public JsonElement Response { get; init; }
    }
}
