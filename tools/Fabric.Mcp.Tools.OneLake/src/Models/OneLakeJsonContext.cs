// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fabric.Mcp.Tools.OneLake.Commands.File;
using Fabric.Mcp.Tools.OneLake.Commands.Item;
using Fabric.Mcp.Tools.OneLake.Commands.Security;
using Fabric.Mcp.Tools.OneLake.Commands.Settings;
using Fabric.Mcp.Tools.OneLake.Commands.Shortcuts;
using Fabric.Mcp.Tools.OneLake.Commands.Table;
using Fabric.Mcp.Tools.OneLake.Commands.Workspace;

namespace Fabric.Mcp.Tools.OneLake.Models;

[JsonSerializable(typeof(Workspace))]
[JsonSerializable(typeof(WorkspaceProperties))]
[JsonSerializable(typeof(WorkspaceMetadata))]
[JsonSerializable(typeof(OneLakeItem))]
[JsonSerializable(typeof(OneLakeItemMetadata))]
[JsonSerializable(typeof(Lakehouse))]
[JsonSerializable(typeof(OneLakeFileInfo))]
[JsonSerializable(typeof(FileSystemItem))]
[JsonSerializable(typeof(CreateItemRequest))]
[JsonSerializable(typeof(UpdateItemRequest))]
[JsonSerializable(typeof(WorkspacesResponse))]
[JsonSerializable(typeof(ItemsResponse))]
[JsonSerializable(typeof(LakehousesResponse))]
[JsonSerializable(typeof(OneLakeEndpoint))]
[JsonSerializable(typeof(OneLakeEnvironmentEndpoints))]
[JsonSerializable(typeof(OneLakeWorkspaceListCommand.OneLakeWorkspaceListCommandResult))]
[JsonSerializable(typeof(OneLakeItemListCommand.OneLakeItemListCommandResult))]
[JsonSerializable(typeof(OneLakeItemDataListCommand.OneLakeItemDataListCommandResult))]
[JsonSerializable(typeof(FileReadCommand.FileReadCommandResult))]
[JsonSerializable(typeof(FileWriteCommand.FileWriteCommandResult))]
[JsonSerializable(typeof(FileDeleteCommand.FileDeleteCommandResult))]
[JsonSerializable(typeof(BlobPutCommand.BlobPutCommandResult))]
[JsonSerializable(typeof(BlobGetCommand.BlobGetCommandResult))]
[JsonSerializable(typeof(PathListCommand.PathListResult))]
[JsonSerializable(typeof(DirectoryCreateCommand.DirectoryCreateCommandResult))]
[JsonSerializable(typeof(DirectoryDeleteCommand.DirectoryDeleteCommandResult))]
[JsonSerializable(typeof(TableConfigGetCommand.TableConfigGetCommandResult))]
[JsonSerializable(typeof(TableListCommand.TableListCommandResult))]
[JsonSerializable(typeof(TableGetCommand.TableGetCommandResult))]
[JsonSerializable(typeof(TableNamespaceGetCommand.TableNamespaceGetCommandResult))]
[JsonSerializable(typeof(TableNamespaceListCommand.TableNamespaceListCommandResult))]
[JsonSerializable(typeof(BlobPutResult))]
[JsonSerializable(typeof(BlobGetResult))]
[JsonSerializable(typeof(TableConfigurationResult))]
[JsonSerializable(typeof(TableListResult))]
[JsonSerializable(typeof(TableGetResult))]
[JsonSerializable(typeof(TableNamespaceGetResult))]
[JsonSerializable(typeof(TableNamespaceListResult))]
// Security
[JsonSerializable(typeof(DataAccessRoleListCommand.DataAccessRoleListCommandResult))]
[JsonSerializable(typeof(DataAccessRoleGetCommand.DataAccessRoleGetCommandResult))]
[JsonSerializable(typeof(DataAccessRoleCreateOrUpdateCommand.DataAccessRoleCreateOrUpdateCommandResult))]
[JsonSerializable(typeof(DataAccessRoleDeleteCommand.DataAccessRoleDeleteCommandResult))]
[JsonSerializable(typeof(PrincipalAccessGetCommand.PrincipalAccessGetCommandResult))]
// Shortcuts
[JsonSerializable(typeof(ShortcutListCommand.ShortcutListCommandResult))]
[JsonSerializable(typeof(ShortcutGetCommand.ShortcutGetCommandResult))]
[JsonSerializable(typeof(ShortcutCreateOrUpdateCommand.ShortcutCreateOrUpdateCommandResult))]
[JsonSerializable(typeof(ShortcutDeleteCommand.ShortcutDeleteCommandResult))]
[JsonSerializable(typeof(ShortcutCacheResetCommand.ShortcutCacheResetCommandResult))]
// Settings
[JsonSerializable(typeof(OneLakeSettingsGetCommand.OneLakeSettingsGetCommandResult))]
[JsonSerializable(typeof(OneLakeDiagnosticsModifyCommand.OneLakeDiagnosticsModifyCommandResult))]
[JsonSerializable(typeof(OneLakeImmutabilityPolicyModifyCommand.OneLakeImmutabilityPolicyModifyCommandResult))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(IEnumerable<Workspace>))]
[JsonSerializable(typeof(IEnumerable<OneLakeItem>))]
[JsonSerializable(typeof(IEnumerable<Lakehouse>))]
[JsonSerializable(typeof(IEnumerable<OneLakeFileInfo>))]
[JsonSerializable(typeof(IEnumerable<FileSystemItem>))]
[JsonSerializable(typeof(List<Workspace>))]
[JsonSerializable(typeof(List<OneLakeItem>))]
[JsonSerializable(typeof(List<Lakehouse>))]
[JsonSerializable(typeof(List<OneLakeFileInfo>))]
[JsonSerializable(typeof(List<FileSystemItem>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
internal partial class OneLakeJsonContext : JsonSerializerContext;
