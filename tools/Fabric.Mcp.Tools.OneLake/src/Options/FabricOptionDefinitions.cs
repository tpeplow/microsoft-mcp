// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;

namespace Fabric.Mcp.Tools.OneLake.Options;

public static class FabricOptionDefinitions
{

    // Workspace options
    public const string WorkspaceName = "workspace";
    public static readonly Option<string> Workspace = new($"--{WorkspaceName}")
    {
        Description = "The name or ID of the Microsoft Fabric workspace.",
        Required = true
    };

    public const string WorkspaceIdName = "workspace-id";
    public static readonly Option<string> WorkspaceId = new($"--{WorkspaceIdName}")
    {
        Description = "The ID of the Microsoft Fabric workspace.",
        Required = true
    };

    // Item options
    public const string ItemName = "item";
    public static readonly Option<string> Item = new($"--{ItemName}")
    {
        Description = "The name or ID of the Fabric item. When using friendly names, MUST include the item type suffix (e.g., 'ItemName.Lakehouse', 'ItemName.Warehouse').",
        Required = false
    };

    public const string ItemIdName = "item-id";
    public static readonly Option<string> ItemId = new($"--{ItemIdName}")
    {
        Description = "The ID of the Fabric item.",
        Required = true
    };

    public const string ItemTypeName = "item-type";
    public static readonly Option<string> ItemType = new($"--{ItemTypeName}")
    {
        Description = "The type of the Fabric item (e.g., Lakehouse, Notebook, etc.).",
        Required = false
    };

    // Lakehouse options
    public const string LakehouseIdName = "lakehouse-id";
    public static readonly Option<string> LakehouseId = new($"--{LakehouseIdName}")
    {
        Description = "The ID of the Lakehouse.",
        Required = true
    };

    // File path options
    public const string FilePathName = "file-path";
    public static readonly Option<string> FilePath = new($"--{FilePathName}")
    {
        Description = "The path to the file in OneLake.",
        Required = true
    };

    public const string DirectoryPathName = "directory-path";
    public static readonly Option<string> DirectoryPath = new($"--{DirectoryPathName}")
    {
        Description = "The path to the directory in OneLake.",
        Required = false
    };

    public const string PathName = "path";
    public static readonly Option<string> Path = new($"--{PathName}")
    {
        Description = "The path to list in OneLake storage (optional, defaults to root).",
        Required = false
    };

    // Data operation options
    public const string RecursiveName = "recursive";
    public static readonly Option<bool> Recursive = new($"--{RecursiveName}")
    {
        Description = "Whether to perform the operation recursively.",
        Required = false
    };

    public const string OverwriteName = "overwrite";
    public static readonly Option<bool> Overwrite = new($"--{OverwriteName}")
    {
        Description = "Whether to overwrite existing files.",
        Required = false
    };

    public const string ContentName = "content";
    public static readonly Option<string> Content = new($"--{ContentName}")
    {
        Description = "The content to write to the file.",
        Required = false
    };

    public const string LocalFilePathName = "local-file-path";
    public static readonly Option<string> LocalFilePath = new($"--{LocalFilePathName}")
    {
        Description = "The path to a local file to upload.",
        Required = false
    };

    public const string DownloadFilePathName = "download-file-path";
    public static readonly Option<string> DownloadFilePath = new($"--{DownloadFilePathName}")
    {
        Description = "Local path to save the downloaded content when running locally.",
        Required = false
    };

    public const string ContentTypeName = "content-type";
    public static readonly Option<string> ContentType = new($"--{ContentTypeName}")
    {
        Description = "MIME content type to set on the uploaded file (e.g., 'application/json'). Defaults to 'application/octet-stream'.",
        Required = false
    };

    // Display options
    public const string DisplayNameName = "display-name";
    public static readonly Option<string> DisplayName = new($"--{DisplayNameName}")
    {
        Description = "The display name for the item.",
        Required = true
    };

    public const string DescriptionName = "description";
    public static readonly Option<string> Description = new($"--{DescriptionName}")
    {
        Description = "The description for the item.",
        Required = false
    };

    // Connection options
    public const string ConnectionIdName = "connection-id";
    public static readonly Option<string> ConnectionId = new($"--{ConnectionIdName}")
    {
        Description = "The connection ID for external data sources.",
        Required = false
    };

    // Pagination options
    public const string ContinuationTokenName = "continuation-token";
    public static readonly Option<string> ContinuationToken = new($"--{ContinuationTokenName}")
    {
        Description = "Token for retrieving the next page of results.",
        Required = false
    };

    // Endpoint options
    public const string EndpointTypeName = "endpoint-type";
    public static readonly Option<string> EndpointType = new($"--{EndpointTypeName}")
    {
        Description = "The endpoint type to use for listing items (fabric-api, blob, auto). Default is 'auto' which uses blob endpoint when appropriate.",
        Required = false
    };

    // Table namespace options
    public const string NamespaceName = "namespace";
    public static readonly Option<string> Namespace = new($"--{NamespaceName}")
    {
        Description = "The table namespace (schema) to inspect within the OneLake table API.",
        Required = true
    };

    public const string SchemaName = "schema";
    public static readonly Option<string> Schema = new($"--{SchemaName}")
    {
        Description = "Alias for --namespace when specifying table schemas in the OneLake table API.",
        Required = false
    };

    public const string TableName = "table";
    public static readonly Option<string> Table = new($"--{TableName}")
    {
        Description = "The table name exposed by the OneLake table API.",
        Required = true
    };

    // Shortcuts
    public const string ShortcutPathName = "shortcut-path";
    public static readonly Option<string> ShortcutPath = new($"--{ShortcutPathName}")
    {
        Description = "The parent path of the shortcut (relative to the item root, e.g. 'Files/folder').",
        Required = false
    };

    public const string ShortcutNameName = "shortcut-name";
    public static readonly Option<string> ShortcutName = new($"--{ShortcutNameName}")
    {
        Description = "The shortcut name.",
        Required = false
    };

    public const string CreateOrOverwriteName = "create-or-overwrite";
    public static readonly Option<bool> CreateOrOverwrite = new($"--{CreateOrOverwriteName}")
    {
        Description = "When true, existing shortcuts at the same path are replaced. When false (default), the call fails on conflict.",
        Required = false
    };

    // Definition payloads (raw JSON for shortcut create/update, role definitions, etc.)
    public const string DefinitionName = "definition";
    public static readonly Option<string> Definition = new($"--{DefinitionName}")
    {
        Description = "Inline JSON payload describing the resource definition (e.g. shortcut creation body, role members and decision rules).",
        Required = false
    };

    // Data access security
    public const string RoleNameName = "role-name";
    public static readonly Option<string> RoleName = new($"--{RoleNameName}")
    {
        Description = "The data access role name.",
        Required = false
    };

    public const string PrincipalIdName = "principal-id";
    public static readonly Option<string> PrincipalId = new($"--{PrincipalIdName}")
    {
        Description = "The Entra principal object ID.",
        Required = false
    };

    public const string PrincipalTypeName = "principal-type";
    public static readonly Option<string> PrincipalType = new($"--{PrincipalTypeName}")
    {
        Description = "The Entra principal type (e.g. 'User', 'Group', 'ServicePrincipal').",
        Required = false
    };

    public const string InputPathName = "input-path";
    public static readonly Option<string> InputPath = new($"--{InputPathName}")
    {
        Description = "The OneLake input path scope for principal-access lookups: 'Tables' or 'Files'.",
        Required = false
    };

    public const string MaxResultsName = "max-results";
    public static readonly Option<int?> MaxResults = new($"--{MaxResultsName}")
    {
        Description = "Maximum number of results to return per page (server may impose its own cap).",
        Required = false
    };

    public const string EtagName = "etag";
    public static readonly Option<string> Etag = new($"--{EtagName}")
    {
        Description = "Optional If-Match ETag to enable optimistic concurrency on updates.",
        Required = false
    };

    // Settings
    public const string DiagnosticsName = "diagnostics";
    public static readonly Option<string> Diagnostics = new($"--{DiagnosticsName}")
    {
        Description = "Inline JSON describing the OneLake diagnostics configuration.",
        Required = false
    };

    public const string ImmutabilityPolicyName = "immutability-policy";
    public static readonly Option<string> ImmutabilityPolicy = new($"--{ImmutabilityPolicyName}")
    {
        Description = "Inline JSON describing the OneLake immutability policy. Once enabled, immutability cannot be disabled.",
        Required = false
    };
}
