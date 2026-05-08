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
    Id = "bf8d8764-1cf0-4d39-bba7-3d3a1cf8dc84",
    Name = "get_principal_access",
    Title = "Get OneLake Principal Access (Preview)",
    Description = "Preview: report the effective OneLake permissions a given principal has on an item, optionally scoped to `Tables` or `Files`. Useful for auditing — answers \"what can this user actually read?\". Requires `OneLake.Read.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = true,
    Secret = false)]
public sealed class PrincipalAccessGetCommand(
    ILogger<PrincipalAccessGetCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<PrincipalAccessGetOptions>()
{
    private readonly ILogger<PrincipalAccessGetCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ItemId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Item.AsOptional());
        command.Options.Add(FabricOptionDefinitions.PrincipalId.AsRequired());
        command.Options.Add(FabricOptionDefinitions.PrincipalType.AsRequired());
        command.Options.Add(FabricOptionDefinitions.InputPath.AsOptional());
        command.Options.Add(FabricOptionDefinitions.MaxResults.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ContinuationToken.AsOptional());
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

    protected override PrincipalAccessGetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ItemId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ItemId.Name);
        options.Item = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Item.Name);
        options.PrincipalId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.PrincipalId.Name);
        options.PrincipalType = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.PrincipalType.Name);
        options.InputPath = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.InputPath.Name);
        options.MaxResults = parseResult.GetValueOrDefault<int?>(FabricOptionDefinitions.MaxResults.Name);
        options.ContinuationToken = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ContinuationToken.Name);
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

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("principal");
                writer.WriteString("id", options.PrincipalId);
                writer.WriteString("type", options.PrincipalType);
                writer.WriteEndObject();
                if (!string.IsNullOrWhiteSpace(options.InputPath))
                {
                    writer.WriteString("inputPath", options.InputPath);
                }
                writer.WriteEndObject();
            }
            ms.Position = 0;
            using var doc = JsonDocument.Parse(ms);
            var payload = doc.RootElement.Clone();

            var data = await _fabricApiService.GetPrincipalAccessAsync(workspace, item, payload, options.ContinuationToken, options.MaxResults, cancellationToken);
            var result = new PrincipalAccessGetCommandResult
            {
                Workspace = workspace,
                Item = item,
                PrincipalId = options.PrincipalId!,
                PrincipalType = options.PrincipalType!,
                Access = data
            };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.PrincipalAccessGetCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting principal access for {PrincipalId}.", options.PrincipalId);
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class PrincipalAccessGetCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public string Item { get; init; } = string.Empty;
        public string PrincipalId { get; init; } = string.Empty;
        public string PrincipalType { get; init; } = string.Empty;
        public JsonElement Access { get; init; }
    }
}
