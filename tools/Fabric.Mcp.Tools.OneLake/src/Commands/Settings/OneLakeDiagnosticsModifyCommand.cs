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
    Id = "aead3a70-5cee-4e8e-bcf6-7e1d4c59f0d5",
    Name = "modify_diagnostics",
    Title = "Modify OneLake Diagnostics Settings",
    Description = "Update the OneLake diagnostics configuration for a workspace. Pass `--diagnostics` with a JSON body matching the Fabric `modifyDiagnostics` API shape: `{ \"status\": \"Enabled\"|\"Disabled\", \"destination\": { \"type\": \"Lakehouse\", \"lakehouse\": { \"referenceType\": \"ById\", \"itemId\": \"<lakehouse-guid>\", \"workspaceId\": \"<workspace-guid>\" } } }`. To disable, only `status` is required. To replace the destination, send the full destination block. The destination workspace must be in the same capacity as the source. Requires admin role on the source workspace and contributor (or above) on the destination, plus `OneLake.ReadWrite.All`. Returns 202 (long-running operation) on success.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class OneLakeDiagnosticsModifyCommand(
    ILogger<OneLakeDiagnosticsModifyCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<OneLakeDiagnosticsModifyOptions>()
{
    private readonly ILogger<OneLakeDiagnosticsModifyCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Diagnostics.AsRequired());
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

    protected override OneLakeDiagnosticsModifyOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.Diagnostics = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Diagnostics.Name);
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
            using var doc = JsonDocument.Parse(options.Diagnostics!);
            var payload = doc.RootElement.Clone();
            var workspace = !string.IsNullOrWhiteSpace(options.WorkspaceId) ? options.WorkspaceId! : options.Workspace!;

            var response = await _fabricApiService.ModifyOneLakeDiagnosticsAsync(workspace, payload, cancellationToken);
            var result = new OneLakeDiagnosticsModifyCommandResult { Workspace = workspace, Response = response };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.OneLakeDiagnosticsModifyCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying OneLake diagnostics.");
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class OneLakeDiagnosticsModifyCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public JsonElement Response { get; init; }
    }
}
