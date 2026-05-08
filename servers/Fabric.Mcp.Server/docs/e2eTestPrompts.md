# Fabric MCP End-to-End Test Prompts

This file contains prompts used for end-to-end testing and tool-description validation
(via `eng/tools/ToolDescriptionEvaluator`) to ensure each tool is invoked correctly by
MCP clients. The tables are organized by Fabric MCP Server areas in alphabetical order,
with Tool Names sorted alphabetically within each table.

The prompts exercise the description-quality rules in
`tools/Fabric.Mcp.Tools.OneLake/specs/onelake-mcp-vNext.md` §3 — in particular sibling
disambiguation (rule 2), search-over-list steering (rule 3), and prerequisite-tool
chaining (rule 8). Where two tools could plausibly answer the same question (e.g.
`get_data_access_role` vs `get_principal_access`, `list_items` vs `list_items_dfs`,
`download_file` vs `read`), prompts are intentionally phrased to disambiguate.

## OneLake — Data access security

| Tool Name | Test Prompt |
|:----------|:----------|
| create_or_update_data_access_role | Create a new data access role on lakehouse <item> in workspace <workspace> from this definition |
| create_or_update_data_access_role | Apply this data access role definition to lakehouse <item> in workspace <workspace> |
| create_or_update_data_access_role | Update the data access role <role-name> on the lakehouse <item> in workspace <workspace> |
| delete_data_access_role | Delete the data access role <role-name> on lakehouse <item> in workspace <workspace> |
| delete_data_access_role | Remove data access role <role-name> from <item> in workspace <workspace> |
| get_data_access_role | Show me the definition of data access role <role-name> on lakehouse <item> in workspace <workspace> |
| get_data_access_role | Get the rules in data access role <role-name> on <item> in workspace <workspace> |
| get_data_access_role | What does the data access role <role-name> grant on <item> in workspace <workspace>? |
| get_principal_access | What data access does user <principal-id> have on lakehouse <item> in workspace <workspace>? |
| get_principal_access | List the effective data access for principal <principal-id> on <item> in workspace <workspace> |
| get_principal_access | Show me what folders and tables <principal-id> can read on lakehouse <item> in workspace <workspace> |
| list_data_access_roles | List all data access roles defined on lakehouse <item> in workspace <workspace> |
| list_data_access_roles | Show me the data access roles configured on <item> in workspace <workspace> |
| list_data_access_roles | What data access roles exist on lakehouse <item> in workspace <workspace>? |

## OneLake — Files and directories

| Tool Name | Test Prompt |
|:----------|:----------|
| create_directory | Create a new directory <path> in lakehouse <item> in workspace <workspace> |
| create_directory | Make the folder <path> under <item> in workspace <workspace> |
| delete_directory | Delete the directory <path> from lakehouse <item> in workspace <workspace> |
| delete_directory | Remove the folder <path> from <item> in workspace <workspace> |
| delete_file | Delete the file <path> from lakehouse <item> in workspace <workspace> |
| delete_file | Remove the file <path> in <item> in workspace <workspace> |
| download_file | Download the file <path> from lakehouse <item> in workspace <workspace> to <local-path> |
| download_file | Copy <path> from OneLake item <item> in workspace <workspace> to my local disk at <local-path> |
| download_file | Save the OneLake file <path> in <item> in workspace <workspace> to a local file <local-path> |
| list_files | List the files and folders under <path> in lakehouse <item> in workspace <workspace> |
| list_files | Show me what's in the Files section of <item> in workspace <workspace> |
| list_files | Enumerate the contents of <path> in <item> in workspace <workspace> |
| upload_file | Upload my local file <local-path> to <path> in lakehouse <item> in workspace <workspace> |
| upload_file | Copy the local file <local-path> into OneLake at <path> in <item> in workspace <workspace> |
| upload_file | Send the file <local-path> to OneLake path <path> in <item> in workspace <workspace> |

## OneLake — Items

| Tool Name | Test Prompt |
|:----------|:----------|
| list_items | List all items in workspace <workspace> |
| list_items | Show me the items in workspace <workspace> |
| list_items | Enumerate every item in the workspace <workspace> |
| list_items_dfs | List all items in workspace <workspace> using the DFS data API |
| list_items_dfs | Show items in workspace <workspace> via the OneLake DFS endpoint |

## OneLake — Settings

| Tool Name | Test Prompt |
|:----------|:----------|
| get_settings | Get the OneLake settings for lakehouse <item> in workspace <workspace> |
| get_settings | Show me the diagnostics and immutability settings on <item> in workspace <workspace> |
| get_settings | What are the OneLake settings configured on lakehouse <item> in workspace <workspace>? |
| modify_diagnostics | Update the diagnostics settings on lakehouse <item> in workspace <workspace> |
| modify_diagnostics | Configure diagnostic logs for OneLake on <item> in workspace <workspace> |
| modify_diagnostics | Change the OneLake diagnostics policy on lakehouse <item> in workspace <workspace> |
| modify_immutability_policy | Set an immutability policy on lakehouse <item> in workspace <workspace> |
| modify_immutability_policy | Update the OneLake immutability policy on <item> in workspace <workspace> |
| modify_immutability_policy | Configure write-once-read-many retention on lakehouse <item> in workspace <workspace> |

## OneLake — Shortcuts

| Tool Name | Test Prompt |
|:----------|:----------|
| create_or_update_shortcuts | Create a OneLake shortcut on lakehouse <item> in workspace <workspace> from this definition |
| create_or_update_shortcuts | Add a shortcut to <item> in workspace <workspace> pointing at the external path |
| create_or_update_shortcuts | Update the shortcut <shortcut-name> on lakehouse <item> in workspace <workspace> |
| delete_shortcut | Delete the shortcut <shortcut-name> at <path> on lakehouse <item> in workspace <workspace> |
| delete_shortcut | Remove the shortcut <shortcut-name> from <item> in workspace <workspace> |
| get_shortcut | Show me the shortcut <shortcut-name> at <path> on lakehouse <item> in workspace <workspace> |
| get_shortcut | Get the target of shortcut <shortcut-name> on <item> in workspace <workspace> |
| get_shortcut | What does the shortcut <shortcut-name> in <item> in workspace <workspace> point at? |
| list_shortcuts | List all shortcuts in lakehouse <item> in workspace <workspace> |
| list_shortcuts | Show me the OneLake shortcuts under <path> in <item> in workspace <workspace> |
| list_shortcuts | Enumerate shortcuts on <item> in workspace <workspace> |
| reset_shortcut_cache | Reset the shortcut cache on lakehouse <item> in workspace <workspace> |
| reset_shortcut_cache | Invalidate the cached shortcut data on <item> in workspace <workspace> |
| reset_shortcut_cache | Clear the OneLake shortcut cache for <item> in workspace <workspace> |

## OneLake — Tables

| Tool Name | Test Prompt |
|:----------|:----------|
| get_table | Show me the schema of table <table> in namespace <namespace> on lakehouse <item> in workspace <workspace> |
| get_table | Get the columns of table <table> in namespace <namespace> in <item> in workspace <workspace> |
| get_table | What does table <table> in namespace <namespace> look like in <item> in workspace <workspace>? |
| get_table_config | Get the table configuration for <item> in workspace <workspace> |
| get_table_config | Show the OneLake table service configuration on lakehouse <item> in workspace <workspace> |
| get_table_namespace | Show me the details of namespace <namespace> on lakehouse <item> in workspace <workspace> |
| get_table_namespace | Get the table namespace <namespace> in <item> in workspace <workspace> |
| list_tables | List all tables in namespace <namespace> on lakehouse <item> in workspace <workspace> |
| list_tables | Show me the tables under namespace <namespace> in <item> in workspace <workspace> |
| list_tables | What tables are in namespace <namespace> on lakehouse <item> in workspace <workspace>? |
| list_table_namespaces | List all table namespaces in lakehouse <item> in workspace <workspace> |
| list_table_namespaces | Show me the table namespaces (schemas) on <item> in workspace <workspace> |
| list_table_namespaces | What table namespaces exist on lakehouse <item> in workspace <workspace>? |

## OneLake — Workspaces

| Tool Name | Test Prompt |
|:----------|:----------|
| list_workspaces | List all my Microsoft Fabric workspaces |
| list_workspaces | Show me the Fabric workspaces I have access to |
| list_workspaces | Enumerate every workspace in my Fabric tenant |
