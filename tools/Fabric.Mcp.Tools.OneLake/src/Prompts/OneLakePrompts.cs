// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Fabric.Mcp.Tools.OneLake.Prompts;

[McpServerPromptType]
public sealed class OneLakePrompts
{
    [McpServerPrompt(Name = "onelake_browse_item")]
    [Description("Guide an agent to browse files inside a known OneLake item (lakehouse/warehouse) safely.")]
    public static ChatMessage[] BrowseItem(
        [Description("Fabric workspace ID or name")] string workspace,
        [Description("Item name (with type suffix, e.g. 'Sales.Lakehouse') or item ID")] string item,
        [Description("Folder path inside the item (e.g. 'Files/sales')")] string path)
    {
        var header =
$@"You will browse files inside an existing OneLake item.
- To find an item by name, prefer `core_search_catalog` from the hosted Fabric Core MCP server (tenant-wide, server-side, supports OData `Type` filter). Avoid client-side list-then-grep.
- Once you have a workspace + item, call `onelake_list_files` (or `onelake_list_items_dfs`) with the path. Handle 404s gracefully.
- Treat all writes (`onelake_upload_file`, `onelake_delete_file`, `onelake_create_directory`, `onelake_delete_directory`) as destructive; confirm before running.";

        var instruction =
$@"Browse:
workspace={workspace}, item={item}, path={path}";

        return new[]
        {
            new ChatMessage(ChatRole.User, header),
            new ChatMessage(ChatRole.User, instruction)
        };
    }

    [McpServerPrompt(Name = "onelake_inspect_tables")]
    [Description("Guide an agent through inspecting OneLake table API metadata for a known item.")]
    public static ChatMessage[] InspectTables(
        [Description("Fabric workspace ID or name")] string workspace,
        [Description("Item name (with type suffix) or item ID")] string item)
    {
        var guide =
$@"To explore tables exposed by the OneLake table API for a known item:
1. Start with `onelake_get_table_config` to confirm the item supports the table API and learn the catalog/namespaces shape.
2. Call `onelake_list_table_namespaces` (or call `onelake_get_table_namespace` once you know the name).
3. Call `onelake_list_tables` with the chosen namespace.
4. Use `onelake_get_table` for column-level schema on a specific table.
Never use OneLake list tools to *find* an item by name — use `core_search_catalog` instead.";

        var exec =
$@"Inspect:
workspace={workspace}, item={item}";

        return new[]
        {
            new ChatMessage(ChatRole.User, guide),
            new ChatMessage(ChatRole.User, exec)
        };
    }

    [McpServerPrompt(Name = "onelake_best_practices")]
    [Description("Context & usage tips for OneLake tools: discovery, auth, paging, destructive operations.")]
    public static ChatMessage[] BestPractices()
    {
        const string content =
    """
    When using OneLake tools:
    - For discovery (finding items, workspaces, anything by name/keyword/type), prefer `core_search_catalog` from the hosted Fabric Core MCP server. It is tenant-wide, server-side, and far cheaper than list-then-grep.
    - The `onelake_list_*` tools are scoped to a single workspace/item you already know — use them for inventory, not search.
    - Authenticate via the server's configured credential flow; never embed secrets in prompts.
    - Use paging (`continuation-token`, `max-results`) for large result sets.
    - Treat shortcut, role, file, and directory write/delete tools as destructive — confirm intent and prefer surgical scopes (single shortcut, single role).
    - Immutability policy is irreversible once enabled — confirm before calling `onelake_modify_immutability_policy`.
    - For shortcut create/update, prefer the bulk `onelake_create_or_update_shortcuts` — it is the only API and accepts arrays.
    """;

        return new[] { new ChatMessage(ChatRole.User, content) };
    }

    [McpServerPrompt(Name = "onelake_confirm_delete")]
    [Description("Ask the user to confirm destructive OneLake delete operations before invoking tools.")]
    public static ChatMessage[] ConfirmDelete(
        [Description("Fabric workspace ID or name")] string workspace,
        [Description("Item name or ID (when applicable)")] string item,
        [Description("Resource path or name that will be deleted")] string target,
        [Description("Operation description (file, directory, shortcut, role, etc.)")] string operation,
        [Description("Set to true when the delete will run recursively")] bool recursive = false)
    {
        var message =
$@"Confirm with the user before deleting a OneLake resource.

Target:
- Workspace: {workspace}
- Item: {item}
- Resource: {target}
- Operation: {operation}{(recursive ? " (recursive)" : string.Empty)}

Ask the user explicitly if they are sure they want to proceed. Require a clear affirmative response (yes/confirm) before calling any delete tool. If they decline or stay silent, stop and report that the deletion was cancelled.";

        return new[]
        {
            new ChatMessage(ChatRole.User, message)
        };
    }
}
