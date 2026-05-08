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
    Id = "0effcc83-1eaa-4b3a-9aa4-1c80ce41f6ab",
    Name = "modify_immutability_policy",
    Title = "Modify OneLake Immutability Policy",
    Description = "Update the OneLake immutability policy for a workspace. WARNING: enabling immutability is irreversible — once enabled it cannot be disabled. Requires `Workspace.ReadWrite.All`.",
    Destructive = false,
    Idempotent = true,
    LocalRequired = false,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false)]
public sealed class OneLakeImmutabilityPolicyModifyCommand(
    ILogger<OneLakeImmutabilityPolicyModifyCommand> logger,
    IFabricApiService fabricApiService) : GlobalCommand<OneLakeImmutabilityPolicyModifyOptions>()
{
    private readonly ILogger<OneLakeImmutabilityPolicyModifyCommand> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFabricApiService _fabricApiService = fabricApiService ?? throw new ArgumentNullException(nameof(fabricApiService));

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(FabricOptionDefinitions.WorkspaceId.AsOptional());
        command.Options.Add(FabricOptionDefinitions.Workspace.AsOptional());
        command.Options.Add(FabricOptionDefinitions.ImmutabilityPolicy.AsRequired());
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

    protected override OneLakeImmutabilityPolicyModifyOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.WorkspaceId = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.WorkspaceId.Name);
        options.Workspace = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.Workspace.Name);
        options.ImmutabilityPolicy = parseResult.GetValueOrDefault<string>(FabricOptionDefinitions.ImmutabilityPolicy.Name);
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
            using var doc = JsonDocument.Parse(options.ImmutabilityPolicy!);
            var payload = doc.RootElement.Clone();
            var workspace = !string.IsNullOrWhiteSpace(options.WorkspaceId) ? options.WorkspaceId! : options.Workspace!;

            var response = await _fabricApiService.ModifyOneLakeImmutabilityPolicyAsync(workspace, payload, cancellationToken);
            var result = new OneLakeImmutabilityPolicyModifyCommandResult { Workspace = workspace, Response = response };
            context.Response.Results = ResponseResult.Create(result, OneLakeJsonContext.Default.OneLakeImmutabilityPolicyModifyCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying OneLake immutability policy.");
            HandleException(context, ex);
        }
        return context.Response;
    }

    public sealed class OneLakeImmutabilityPolicyModifyCommandResult
    {
        public string Workspace { get; init; } = string.Empty;
        public JsonElement Response { get; init; }
    }
}
