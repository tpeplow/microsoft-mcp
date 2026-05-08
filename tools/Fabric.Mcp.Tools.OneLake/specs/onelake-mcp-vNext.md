# OneLake MCP — vNext spec

This spec defines the next iteration of the OneLake MCP tool set. There are four things going on:

**1. We're adding tools for four new OneLake-adjacent API surfaces.** All four are existing public Fabric REST APIs that the MCP doesn't cover today:

- **OneLake data access security** (Core API) — role-based policies that gate Tables/Files access on an item, plus a *principal access* lookup that returns the consolidated effective access for a given Entra principal across all roles. Tools: `List / Get / Create-or-Update (single) / Delete` data access roles, plus `Get Principal Access` (Preview). The bulk *Create-or-Update* (plural) endpoint is intentionally not exposed — see §1.1.
- **OneLake shortcuts** (Core API) — `List / Get / Create (bulk, with `createOrOverwrite` for upsert) / Delete / Reset cache`. Single-shortcut and bulk are merged into one tool because the bulk endpoint covers both.
- **OneLake settings** (Core API) — workspace-scoped: `Get settings / Modify diagnostics / Modify immutability policy`.
- **OneLake catalog search** (Core API) — cross-workspace free-text search with type filtering. **Already shipped in the hosted Fabric Core MCP server**, and we deliberately **do not** ship a local equivalent (avoid duplicate tools competing for LLM selection). Instead, the descriptions of our `_list_*` tools must steer the agent to the hosted `core_search_catalog` when present. Covered as a worked scenario in §4.

These four categories are independent — each backs a separate REST surface and ships its own tools — so we can **land them incrementally** rather than as a single big release. The docs fix-up (point 2 below) should land first regardless, because every subsequent change touches the same README.

**2. The upstream docs need to catch up with reality.** During GA prep, `onelake_item_create` was moved out of the OneLake tool into the Core namespace as `core_create_item` (now under `Fabric.Mcp.Tools.Core/src/Commands/ItemCreateCommand.cs`) — but the OneLake README still documents it as an OneLake tool. The README also pre-dates the GA renames (verb-prefix naming, `file_read`/`file_write` → `download_file`/`upload_file`, etc.) and the `blob_*` situation is unclear (still present in the running server, but not in the GA naming doc). This is a documentation problem, not a code problem; we'll fix it as part of vNext so the docs match what's actually shipping — and then layer the new categories on top.

**3. We need to land the new tools in the right place from the start.** With `core_create_item` we discovered the placement after the fact and had to move it. The proposal here is to keep the new categories in the **`onelake_` namespace** in `Fabric.Mcp.Tools.OneLake` — they're OneLake-data-plane concepts, even when the underlying REST is on Core, and that's where users will look for them. Rationale in §1.4.

**4. Tool metadata (descriptions, structured annotations) needs to do real work for the LLM.** The biggest gap in the existing OneLake tools isn't missing capabilities — it's that the model picks the wrong tool, or calls a tool without the prerequisite context, and burns turns retrying. We treat description quality as a first-class deliverable on every new and renamed tool. Two examples that drive the pattern:

- *Search vs. enumerate.* If the hosted Fabric Core MCP is installed alongside this server, the agent should reach for `core_search_catalog` to find items by keyword or type — not page through `onelake_list_workspaces` + `onelake_list_items` and grep client-side. Our `_list_*` descriptions explicitly steer to `core_search_catalog` when present, and call out that listing is for full enumeration of a known scope (see §2.4, §4.1).
- *Prerequisite chains.* `onelake_list_tables` requires a `namespace` argument; without prompting, the LLM will optimistically call it, get a 400, and retry. The description for `onelake_list_tables` must say *"Requires a `namespace` — call `onelake_list_table_namespaces` first if you don't already have one"* so the agent chains the calls in the right order on the first attempt (worked example in §4).

The full set of description rules we'll apply is in §3.

The rest of the doc:
- **§1** — the new tools, by category.
- **§2** — what the OneLake MCP tool should look like at vNext, fully re-documented, with **CHANGED / NEW / REMOVED** flags per tool.
- **§3** — tool description quality guidelines, with proposed strings for every new/renamed tool in §3.1.
- **§4** — scenarios (end-to-end agent workflows the design must enable, including the catalog-search pattern).
- **§5** — totals.
- **§6** — the docs fix-up task.
- **§7** — open questions.

---

## 1. Overview of new tools (by category)

vNext adds new categories of OneLake tools, all backed by existing public Fabric REST APIs.

### 1.1 OneLake security

Wraps the [OneLake Data Access Security](https://learn.microsoft.com/en-us/rest/api/fabric/core/onelake-data-access-security) operations on the Core endpoint, plus the [Authorized access for principal](https://learn.microsoft.com/en-us/fabric/onelake/security/authorized-access-for-principal-reference) lookup. The role-based tools let agents define and inspect the policies that gate Tables/Files for an item; the principal-access lookup returns the consolidated *effective* access for a given Entra principal across all roles on an item.

| New tool | Backing API | Purpose |
|---|---|---|
| `onelake_list_data_access_roles` | List Data Access Roles | Enumerate all data access roles defined on an item. |
| `onelake_get_data_access_role` | Get Data Access Role (single) | Retrieve one role's definition (members + decision rules). |
| `onelake_create_or_update_data_access_role` | Create Or Update Single Data Access Role | Upsert a single role on an item. |
| `onelake_delete_data_access_role` | Delete Data Access Role | Remove a single role from an item. |
| `onelake_get_principal_access` | Get Principal Access | Effective Tables-or-Files access for a given Entra principal on an item. Supports `inputPath` (Tables\|Files), pagination via `continuationToken` / `maxResults`. **Preview**. |

> **No bulk create-or-update tool (intentional).** The underlying API has a plural `Create Or Update Data Access Roles` operation that replaces the entire role set on an item in one PUT — any role missing from the request is deleted. We deliberately do **not** expose this as an MCP tool: the safe usage pattern requires the caller to first read every existing role, merge the change in, and send the full set back, and an LLM that skips or partially performs that read-merge step will silently wipe roles it never intended to touch. Agents that need to make multiple role changes should loop over the single-role `onelake_create_or_update_data_access_role` instead — slower, but each call has clear single-role semantics and can't accidentally remove unrelated roles.

> **Workspace-role authorization — admins and members only.** Every tool in this category requires the calling principal to hold the **Admin** or **Member** workspace role on the workspace that owns the item. Contributor and Viewer roles are *not* sufficient — the security surface is intentionally narrower than the data-access surface it controls. This is enforced server-side by the underlying APIs; agents calling these tools as a Contributor/Viewer principal will see 403 regardless of the Entra scope on the token. The descriptions in §3.1 say this explicitly.

### 1.2 Shortcuts

Wraps the [OneLake Shortcuts](https://learn.microsoft.com/en-us/rest/api/fabric/core/onelake-shortcuts) operations on the Core endpoint.

| New tool | Backing API | Purpose |
|---|---|---|
| `onelake_list_shortcuts` | List Shortcuts | List shortcuts for an item, recursing through subfolders. |
| `onelake_get_shortcut` | Get Shortcut | Return one shortcut's properties. |
| `onelake_create_or_update_shortcuts` | Create Or Update Shortcuts | Create or update one or more shortcuts in a single call. Set `createOrOverwrite=true` to upsert (default `false` fails on conflict). Accepts batches of one, so use this for both single and bulk creates. |
| `onelake_delete_shortcut` | Delete Shortcut | Delete the shortcut (does not delete the destination storage folder). |
| `onelake_reset_shortcut_cache` | Reset Shortcut Cache | Drop cached files read through shortcuts. |

> **Why one create tool, not two:** the bulk API supports both create-only and upsert via the `createOrOverwrite` flag and accepts batches of size 1, so it covers the single-shortcut use case as well. Using only the bulk endpoint keeps the surface smaller, removes a tool the LLM could pick wrongly, and means shortcut changes are *always* expressed as a batch — which matches how agents naturally phrase the work ("create these shortcuts").

### 1.3 OneLake settings

Wraps the [OneLake Settings](https://learn.microsoft.com/en-us/rest/api/fabric/core/onelake-settings) operations. Workspace-scoped; controls diagnostics and immutability policy.

| New tool | Backing API | Purpose |
|---|---|---|
| `onelake_get_settings` | Get Settings | Retrieve the OneLake settings for a workspace (diagnostics + immutability policy). |
| `onelake_modify_diagnostics` | Modify Diagnostics | Update diagnostic logging configuration for OneLake at the workspace scope. |
| `onelake_modify_immutability_policy` | Modify Immutability Policy | Update the workspace-level OneLake immutability policy. |

### 1.4 Placement / namespace

All of the above sit under the `onelake_` namespace in the **local OneLake MCP tool** (`Fabric.Mcp.Tools.OneLake`). They are OneLake-data-plane concepts and do not belong under Core, even where the underlying REST is served from `api.fabric.microsoft.com` (Core). Rationale:

- They are the OneLake security / shortcut / settings surface — discoverability under `onelake_` is what users will look for.
- The earlier `core_create_item` move was driven by the fact that item creation is a generic Core concern that applies to all workloads, not OneLake specifically. That argument doesn't apply here.
- **The audience split (see §2.3.1) doesn't change this.** The OneLake namespace already spans two audiences today (Fabric for the moved `core_create_item`-style management calls, storage for file IO); the new tools just extend the existing pattern. Audience is an implementation detail picked by the tool, not by the agent, so it shouldn't drive the namespace boundary.

---

## 2. The OneLake MCP tool — re-documented as of vNext

> **Note:** This is what the upstream README *should* say once vNext ships. Items marked **CHANGED**, **NEW**, or **REMOVED** call out the deltas vs. the [current README](https://github.com/microsoft/mcp/blob/main/tools/Fabric.Mcp.Tools.OneLake/README.md).

### 2.1 Overview

Microsoft Fabric OneLake MCP (Model Context Protocol) Tools — manage and interact with OneLake data lake storage through AI agents and MCP clients.

OneLake is Microsoft Fabric's built-in data lake providing unified storage for all analytics workloads. This MCP tool lets AI agents:

- Manage OneLake folders and files
- Browse items, tables and namespaces
- **NEW** Configure OneLake data access security (role-based)
- **NEW** Inspect a principal's effective access on an item
- **NEW** Create, list and manage shortcuts (including bulk + cache reset)
- **NEW** Read and modify workspace-level OneLake settings (diagnostics, immutability)

**Tool count:** 31 OneLake tools (was 19). Catalog search is intentionally not shipped locally — it's owned by the hosted Fabric Core MCP server (see §4.1). Item creation is no longer included here either — it ships as `core_create_item` under [Fabric.Mcp.Tools.Core](https://github.com/microsoft/mcp/tree/main/tools/Fabric.Mcp.Tools.Core/src).

### 2.2 Endpoints

```
OneLake Data Plane: https://api.onelake.fabric.microsoft.com
OneLake DFS API:    https://onelake.dfs.fabric.microsoft.com
OneLake Blob API:   https://onelake.blob.fabric.microsoft.com
OneLake Table API:  https://onelake.table.fabric.microsoft.com
Fabric Core API:    https://api.fabric.microsoft.com/v1
```

Security, shortcut and settings tools all call the **Fabric Core API**. All other tools target the OneLake data plane / DFS / Blob / Table endpoints as before.

### 2.3 Authentication, identifiers, MCP client config

Unchanged from the current README:

- Azure auth via `az login` (or managed identity in hosted scenarios).
- Friendly-name support for `--workspace` / `--item` (as `<itemName>.<itemType>`); GUIDs still accepted via `--workspace-id` / `--item-id`.
- Table tools also accept `--namespace` / `--schema`.
- **CHANGED**: the README caveat that `item create` requires GUIDs is no longer relevant here — that command was moved out of the OneLake tool to Core during GA prep; the README just hasn't been updated yet.

### 2.3.1 Token audiences **NEW**

The OneLake namespace fans out across two distinct Entra audiences. The tool implementation picks the right one per call — agents never select an audience directly — but it matters for hosted scenarios (consent + on-behalf-of flows) and for diagnosing 401s.

| Endpoint family | Audience (resource) | Typical Entra scope | Tools that use it |
|---|---|---|---|
| Fabric Core API (`api.fabric.microsoft.com`) | `https://api.fabric.microsoft.com` | `OneLake.ReadWrite.All`, `Workspace.ReadWrite.All`, etc. | All **data access security**, **shortcuts**, and **settings** tools, plus the existing `core_create_item` (in the Core namespace). Catalog search uses the same audience but is owned by the hosted Core MCP, not this local server. |
| OneLake DFS / Blob data plane (`onelake.dfs.fabric.microsoft.com`, `onelake.blob.fabric.microsoft.com`) | `https://storage.azure.com/` | Storage RBAC + OneLake roles (no Entra scope per call) | All file / directory IO: `download_file`, `upload_file`, `delete_file`, `list_files`, `create_directory`, `delete_directory`, `list_workspaces`, `list_items`, `list_items-dfs`. |
| OneLake Table API (`onelake.table.fabric.microsoft.com`) | `https://storage.azure.com/` | Storage RBAC + OneLake roles | All `*_table*` tools. |
| **Hosted Fabric Core MCP server** (`api.fabric.microsoft.com/v1/mcp/core`) | `https://api.fabric.microsoft.com` | Per-tool delegated scopes (e.g. `Workspace.ReadWrite.All`, `Catalog.Read.All`) acquired via browser-based OAuth 2.0 / OBO. | The MCP transport itself is hosted on the Fabric resource, so the access token used by the MCP client is a Fabric-audience token. This is the same audience as Core API calls, which is why the server can call the Fabric REST API on the user's behalf without a second token exchange. |

Practical implications:

- The local stdio MCP already requests both audiences via `DefaultAzureCredential` / Azure CLI; no change needed beyond wiring the new HTTP clients to the right credential scope.

### 2.4 Tool catalog

Naming follows the GA convention: `onelake_<verb>_<noun>` (verb-prefix). Renames listed in §5.

The tables below are a **human-readable inventory** — one-line summaries so a reader can scan what ships. They are *not* the strings we register with MCP. The actual `description` text the LLM sees at tool-selection time lives in §3.1, where each entry is a full multi-sentence string written against the eight rules in §3 (sibling cross-refs, scope, prerequisite-tool hints, scope/role caveats, `OneLake.Read.All` etc.). PR reviewers should focus on §3.1; this catalog is for orientation only.

#### Workspace operations

| Tool | Description | Status |
|---|---|---|
| `onelake_list_workspaces` | List all Fabric workspaces accessible via OneLake data plane API. | **CHANGED** (was `onelake_workspace_list`) |

#### Item operations

| Tool | Description | Status |
|---|---|---|
| `onelake_list_items` | Enumerate OneLake items in a known workspace using the high-level OneLake API. **For finding items by name, description, or type — across workspaces or within one — use `core_search_catalog` from the hosted Fabric Core MCP server instead** (it's tenant-wide and server-side; this listing tool is for full enumeration of a known scope). | **CHANGED** (was `onelake_item_list`; description steered toward hosted catalog search for keyword queries) |
| `onelake_list_items-dfs` | Enumerate OneLake items in a known workspace using the OneLake DFS data API. **For finding items by name, description, or type, use `core_search_catalog` from the hosted Fabric Core MCP server instead.** | **CHANGED** (was `onelake_item_list-data`; suffix `-data` → `-dfs`; description steered toward hosted catalog search for keyword queries) |
| ~~`onelake_item_create`~~ | Already moved to `core_create_item`; remove from the README. | **DOC FIX** (code already removed) |

#### File operations

| Tool | Description | Status |
|---|---|---|
| `onelake_download_file` | Download a file from OneLake; returns base64 content, metadata and text where applicable. | **CHANGED** (was `onelake_file_read`) |
| `onelake_upload_file` | Upload a file from inline content or local path, with overwrite control. | **CHANGED** (was `onelake_file_write`; existing `onelake_upload_file` consolidated into this) |
| `onelake_delete_file` | Delete a file from OneLake. | **CHANGED** (was `onelake_file_delete`) |
| `onelake_list_files` | Filesystem-style hierarchical listing of files and directories. | **CHANGED** (was `onelake_file_list`) |

#### Directory operations

| Tool | Description | Status |
|---|---|---|
| `onelake_create_directory` | Create a directory (supports nested paths). | **CHANGED** (was `onelake_directory_create`) |
| `onelake_delete_directory` | Delete a directory; `--recursive` for non-empty. | **CHANGED** (was `onelake_directory_delete`) |

#### Blob operations

| Tool | Description | Status |
|---|---|---|
| ~~`onelake_blob_list`~~ | Blob-format listing. | **REMOVED** — covered by `onelake_list_files`; current docs already omit it (pending review with the platform team). |
| ~~`onelake_blob_delete`~~ | Blob-endpoint delete. | **REMOVED** — covered by `onelake_delete_file`. |

#### Table operations

| Tool | Description | Status |
|---|---|---|
| `onelake_list_tables` | List tables exposed by the OneLake Table API for a namespace. | **CHANGED** (was `onelake_table_list`) |
| `onelake_get_table` | Retrieve table schema/metadata (columns, types, row counts, statistics). | **CHANGED** (was `onelake_table_get`) |
| `onelake_get_table_config` | Retrieve table API configuration metadata. | **CHANGED** (was `onelake_table_config_get`) |
| `onelake_list_table_namespaces` | Enumerate namespaces (schemas). | **CHANGED** (was `onelake_table_namespace_list`) |
| `onelake_get_table_namespace` | Retrieve metadata for a single namespace. | **CHANGED** (was `onelake_table_namespace_get`) |

> Note: the `CRITICAL:` warnings about `<itemName>.<itemType>` suffixes are dropped from descriptions and rolled into general guidance in the README.

#### Security — data access roles **NEW**

| Tool | Description |
|---|---|
| `onelake_list_data_access_roles` | List all data access roles on an item. |
| `onelake_get_data_access_role` | Get a single data access role definition. |
| `onelake_create_or_update_data_access_role` | Upsert a single data access role. |
| `onelake_delete_data_access_role` | Delete a data access role. |
| `onelake_get_principal_access` | Effective Tables/Files access for an Entra principal on an item. Preview API; supports pagination. |

#### Shortcuts **NEW**

| Tool | Description |
|---|---|
| `onelake_list_shortcuts` | List shortcuts for an item, recursing through subfolders. |
| `onelake_get_shortcut` | Get a single shortcut's properties. |
| `onelake_create_or_update_shortcuts` | Create or update one or more shortcuts in a single call. Use `createOrOverwrite` to upsert. |
| `onelake_delete_shortcut` | Delete a shortcut (preserves destination storage). |
| `onelake_reset_shortcut_cache` | Drop cached shortcut reads. |

#### Settings **NEW**

| Tool | Description |
|---|---|
| `onelake_get_settings` | Get OneLake settings for a workspace. |
| `onelake_modify_diagnostics` | Modify diagnostic logging configuration. |
| `onelake_modify_immutability_policy` | Modify the workspace-level OneLake immutability policy. |

#### Catalog search — not shipped locally

We deliberately do **not** ship a `onelake_search_catalog` tool in the local OneLake MCP. The hosted [Fabric Core remote MCP server](https://learn.microsoft.com/en-us/rest/api/fabric/articles/mcp-servers/core-remote/get-started-core) already exposes `search_catalog`, and shipping a parallel local tool would create two tools competing for LLM selection with no real benefit.

Instead, the **`_list_workspaces`, `_list_items`, and `_list_items-dfs` descriptions** in this server explicitly direct the agent to use `core_search_catalog` (from the hosted Core MCP) when the intent is "find items by name/description/type" rather than "enumerate everything in a known scope". This way:

- Agents that have **only** the local OneLake MCP connected fall back to `_list_*` and may need client-side filtering for keyword queries — acceptable, because that's the only option available to them.
- Agents that have **both** the local OneLake MCP and the hosted Core MCP connected (the expected configuration for most users) route keyword/type queries to `core_search_catalog` automatically, with no duplicate tool to disambiguate.

See §4.1 for the worked scenario and the exact description strings.

---

---

## 3. Tool description quality

For LLMs to pick the right tool first time, the descriptions need to do real work. Examples across the new categories: `_get_data_access_role` vs `_get_principal_access` are easy to confuse, as are `_modify_diagnostics` vs `_get_settings`, and `_create_or_update_shortcuts` needs to be clear about the `createOrOverwrite` flag so the agent doesn't try to call a non-existent "update" variant. The biggest failure mode of all is **search-vs-list-and-filter** — agents will reach for `_list_items` and grep client-side unless we explicitly steer them to `core_search_catalog` on the hosted Core MCP server.

Description guidelines we'll apply to every new tool in this spec:

1. **Lead with the user intent**, not the API name. *"Use this when the user needs to grant a principal access to specific Tables in an item"* beats *"Wraps the Create Or Update Single Data Access Role API"*.
2. **Disambiguate sibling tools explicitly.** Where two tools could plausibly answer the same question, each description should reference the other and say when to prefer it.
3. **Call out search-over-list explicitly.** Every `_list_*` description in a category that has a search peer must include a "for finding by keyword/type, use `_search_*` instead — listing is for full enumeration of a known scope" line. Without this, agents default to list+grep.
4. **State the destructive / write semantics up front.** Bulk-replace, create-only-on-conflict, recursive delete — these are LLM bear traps. Say it in the first sentence.
5. **Name the required Entra scope** (e.g. `OneLake.ReadWrite.All`, `Catalog.Read.All`) in one short sentence at the end. Don't list audiences or hosts — that's noise to the LLM.
6. **Flag Preview APIs** (e.g. `onelake_get_principal_access`) with the literal word *"Preview"* so agents can warn the user / avoid production paths.
7. **Avoid the `CRITICAL:` shouting** the existing table tools use. We're dropping that pattern across the namespace; descriptions should be calm, declarative, and informative.
8. **Document required prerequisite calls.** Where a tool needs context that another tool produces (an ID, a namespace, a table identifier), the description must say so explicitly: *"Requires a `namespace` value — call `onelake_list_table_namespaces` first if you don't already have one."* Without this, the LLM will optimistically call the inner tool, get a 400, and retry — burning a turn and a tool call. See §4 for a worked example of this failure shape and the chain we want the agent to follow.

We should write these descriptions as part of the implementation work (not after the fact) and review them as a set so sibling-tool cross-references stay coherent.

### 3.1 Proposed tool descriptions

These are the **actual strings we propose to register as the MCP `description` field** for each tool — i.e. what the LLM sees at tool-selection time, *not* the human-readable one-liners in §2.4 (those are inventory only). Each entry below applies rules 1–8 from §3: lead with intent, name sibling tools the LLM might confuse this with, point at the prerequisite tool when one exists, state the per-call scope and any workspace-role caveat, flag Preview, list the required Entra scope. Treat these as a strawman for review — not gospel — but ship something close to this rather than re-deriving from rules at PR time. Description text only; arg schemas are out of scope for this section.

#### Workspace + item enumeration (the search-vs-list hot path)

```text
onelake_list_workspaces:
  Enumerate Fabric workspaces accessible via the OneLake data plane API. Use this
  when the user wants the full list of workspaces they can reach. For finding a
  workspace by name or keyword, prefer `core_search_catalog` from the hosted
  Fabric Core MCP server (filter on `Type eq 'Workspace'`) — it is tenant-wide
  and server-side. Requires `OneLake.Read.All`.

onelake_list_items:
  Enumerate OneLake items in a single, known workspace. Use this only when the
  user has already named a workspace and wants its full inventory. For finding
  items by name, description, or item type — across workspaces or within one —
  use `core_search_catalog` from the hosted Fabric Core MCP server instead; it
  is tenant-wide, server-side, and supports an OData `Type` filter. Avoid the
  list-then-grep pattern. Requires `OneLake.Read.All`.

onelake_list_items-dfs:
  DFS-style enumeration of OneLake items in a single workspace, returning DFS
  paths suitable for downstream file IO. Use only when you need DFS paths and
  you already know the workspace. For finding items by name/description/type,
  use `core_search_catalog` from the hosted Fabric Core MCP server instead.
  Requires `OneLake.Read.All`.
```

#### Table operations (prerequisite-chain hot path)

```text
onelake_list_table_namespaces:
  List the namespaces (schemas) exposed by an item's OneLake Table API. Call
  this first whenever you don't already know the namespace, then pass the
  result to `onelake_list_tables`. Most Lakehouses return a single namespace
  (`dbo`); Warehouses can have several. Requires `OneLake.Read.All`.

onelake_get_table_namespace:
  Retrieve metadata for a single table namespace. Rarely needed in normal
  flows — prefer `onelake_list_table_namespaces` for discovery. Requires
  `OneLake.Read.All`.

onelake_list_tables:
  List the tables exposed by the OneLake Table API for a single namespace
  within an item (typically a Lakehouse or Warehouse). Requires a `namespace`
  argument — call `onelake_list_table_namespaces` first if you don't already
  have one. For schema/columns/row counts on a specific table, follow up with
  `onelake_get_table`. Requires `OneLake.Read.All`.

onelake_get_table:
  Get schema and metadata for a single table — columns, types, row counts,
  statistics — without reading data. Requires a fully-qualified table
  identifier; call `onelake_list_tables` first if needed. Requires
  `OneLake.Read.All`.

onelake_get_table_config:
  Retrieve the OneLake Table API configuration for a single table (storage
  format, partitioning, file layout). Read-only; intended for diagnostics, not
  normal data access. Requires `OneLake.Read.All`.
```

#### File and directory operations

```text
onelake_list_files:
  Filesystem-style hierarchical listing of files and directories under a
  OneLake path. Use for browsing file IO inside a known item; for finding
  *items* by name/keyword, use `core_search_catalog` from the hosted Fabric
  Core MCP server instead. Requires `OneLake.Read.All`.

onelake_download_file:
  Download a single file from OneLake. Returns content as base64 plus
  content-type, and decoded text where the file is text/JSON/CSV. Requires
  `OneLake.Read.All`.

onelake_upload_file:
  Upload a file to OneLake from inline content or a local path. Overwrites by
  default — pass `overwrite=false` to fail-on-conflict instead. Requires
  `OneLake.ReadWrite.All`.

onelake_delete_file:
  Delete a single file from OneLake. Destructive and not recoverable from the
  data plane. For directories, use `onelake_delete_directory`. Requires
  `OneLake.ReadWrite.All`.

onelake_create_directory:
  Create a directory in OneLake; supports nested paths (intermediate
  directories are created). Requires `OneLake.ReadWrite.All`.

onelake_delete_directory:
  Delete a directory from OneLake. Destructive. Pass `recursive=true` to
  delete non-empty directories — without it, non-empty deletes fail.
  Requires `OneLake.ReadWrite.All`.
```

#### Data access security — roles **NEW**

> All tools in this section require the caller to be a workspace **Admin** or **Member** on the workspace that owns the item — Contributor / Viewer get 403 regardless of token scope. This appears literally in each description below.

```text
onelake_list_data_access_roles:
  List all data access roles defined on a single item (Lakehouse / Warehouse) —
  the role-based policies that gate Tables/Files access for that item. Scoped
  to one item per call; to inspect roles across multiple items, call once per
  item. For looking up a specific role by name, fetch the list and pick by
  `name`; there is no server-side search. Caller must be a workspace Admin
  or Member on the item's workspace. Requires `OneLake.Read.All`.

onelake_get_data_access_role:
  Get the full definition of a single data access role on a single item —
  members, permissions, decision rules. Scoped to one role on one item per
  call. Use after `onelake_list_data_access_roles` once you know which role
  you need on which item. Distinct from `onelake_get_principal_access`,
  which returns the *effective* (resolved) access for a given principal
  across all roles on an item. Caller must be a workspace Admin or Member
  on the item's workspace. Requires `OneLake.Read.All`.

onelake_create_or_update_data_access_role:
  Upsert a single data access role on a single item. Scoped to one role on
  one item per call — does not affect other roles on the item or any roles
  on other items, so it's safe to call in a loop when multiple roles or
  multiple items need changing. There is no bulk variant: the underlying
  PUT-all API was intentionally not exposed because partial reads would
  silently delete roles. Caller must be a workspace Admin or Member on the
  item's workspace. Requires `OneLake.ReadWrite.All`.

onelake_delete_data_access_role:
  Delete a single data access role from a single item. Scoped to one role
  on one item per call. Destructive — principals that gained access only
  via this role lose it on this item. Does not affect roles on other items.
  Caller must be a workspace Admin or Member on the item's workspace.
  Requires `OneLake.ReadWrite.All`.

onelake_get_principal_access:
  Preview API. Return the *effective* OneLake Tables/Files access for a
  given Entra principal on a single item — i.e. the resolved permissions
  across every data access role on that item. Scoped to one principal +
  one item per call; to check access across multiple items, call once per
  item. Use to answer "what can user X actually see on this lakehouse?",
  not to inspect role definitions (use `onelake_get_data_access_role` for
  that). Supports pagination. Caller must be a workspace Admin or Member
  on the item's workspace. Requires `OneLake.Read.All`.
```

#### Shortcuts **NEW**

```text
onelake_list_shortcuts:
  List shortcuts defined within an item, recursing through subfolders.
  Returns each shortcut's path and target. Requires `OneLake.Read.All`.

onelake_get_shortcut:
  Get the properties of a single shortcut (name, path, target,
  configuration). Requires `OneLake.Read.All`.

onelake_create_or_update_shortcuts:
  Create one or more shortcuts in a single call (the underlying API is
  bulk-only — there is no separate single-shortcut create). By default,
  fails if any shortcut already exists; pass `createOrOverwrite=true` to
  upsert. Use this for both initial creation and updates. Requires
  `OneLake.ReadWrite.All`.

onelake_delete_shortcut:
  Delete a single shortcut from an item. Destructive but the destination
  data is preserved — only the shortcut reference is removed. Requires
  `OneLake.ReadWrite.All`.

onelake_reset_shortcut_cache:
  Drop cached shortcut reads for an item, forcing the next read to
  re-resolve from the destination. Use sparingly — primarily for debugging
  stale-cache issues. Requires `OneLake.ReadWrite.All`.
```

#### Settings **NEW**

```text
onelake_get_settings:
  Get the OneLake settings for a workspace — diagnostics configuration and
  immutability policy. Read-only; for changes use `onelake_modify_diagnostics`
  or `onelake_modify_immutability_policy`. Requires `OneLake.Read.All`.

onelake_modify_diagnostics:
  Modify the diagnostic logging configuration for OneLake at the workspace
  scope. Replaces the existing diagnostics block; fetch with
  `onelake_get_settings` first if you want to merge. Requires
  `OneLake.ReadWrite.All`.

onelake_modify_immutability_policy:
  Modify the workspace-level OneLake immutability policy. Once enabled,
  immutability cannot be disabled — confirm with the user before applying.
  Requires `OneLake.ReadWrite.All`.
```

Review notes for the set:

- Every `_list_*` that has a catalog-search peer points at `core_search_catalog` (rule 3 / 4th intro point).
- Every tool whose call is gated on a value another tool produces names that producer (rule 8) — `_list_tables` → `_list_table_namespaces`, `_get_table` → `_list_tables`, `_get_data_access_role` → `_list_data_access_roles`, `_modify_diagnostics` → `_get_settings` (read-modify-write hint).
- Destructive / bulk-replace / overwrite semantics are stated in the first or second sentence (rule 4): `_delete_*`, `_modify_immutability_policy`, `_upload_file`, `_delete_directory`.
- Sibling pairs cross-reference each other (rule 2): `_get_data_access_role` ↔ `_get_principal_access`. The `_create_or_update_data_access_role` description also explains why no plural variant exists.
- "Preview" appears literally on `_get_principal_access` (rule 6).
- Required scope appears in one short sentence at the end of every description (rule 5); audience/host details are *not* in the description.
- No `CRITICAL:` shouting (rule 7).

---

## 4. Scenarios

End-to-end flows the design must enable. These are the workflows we'll judge the tool surface against — if an agent can't complete them in a small number of well-typed calls, the descriptions or the tool boundaries are wrong.

### 4.1 Cross-workspace catalog search → drill-down

The headline scenario — and the one that motivates the description-discipline work entirely. The Catalog Search API ([docs](https://learn.microsoft.com/en-us/rest/api/fabric/core/catalog/search), [Fabric blog](https://blog.fabric.microsoft.com/en/blog/discover-fabric-items-across-workspaces-with-the-onelake-catalog-search-api-mcp-and-cli-tools-preview)) does cross-workspace, free-text search of catalog items with type filtering and paging. Without it, agents reach for `_list_workspaces` → `_list_items` per workspace → client-side string match — slow, wasteful, and tenant-walk on every prompt.

**We do not ship a local search tool.** The [hosted Fabric Core remote MCP server](https://learn.microsoft.com/en-us/rest/api/fabric/articles/mcp-servers/core-remote/get-started-core) (`https://api.fabric.microsoft.com/v1/mcp/core`, preview) already exposes `search_catalog`. Shipping a parallel `onelake_search_catalog` in the local server would just give the LLM two tools that do the same thing. Instead, our list tools' descriptions explicitly defer to the hosted tool when present (see §2.4).

**Worked example — search → list namespaces → list tables.** Real session against the user's tenant:

> Prompt: *"How many cricket lakehouses do I have, and do they have the same data?"*
>
> 1. **Hosted Core MCP** → `core_search_catalog("cricket")` returns 2 cricket lakehouses across 2 workspaces (no enumeration; tenant-wide).
> 2. **Local OneLake MCP** → `onelake_list_table_namespaces(item, workspace)` returns `["dbo"]` for each.
> 3. **Local OneLake MCP** → `onelake_list_tables(item, workspace, namespace="dbo")` returns the table list per lakehouse, allowing schema comparison.
>
> First attempt failed: the LLM tried `onelake_list_tables` directly without the namespace and got a 400. Adding the `list_table_namespaces` step fixed it. The tool descriptions need to make this chain obvious so the agent gets it right first time. Specifically:
>
> - `onelake_list_tables` — *"Lists tables under a given namespace. Requires a `namespace` value — call `onelake_list_table_namespaces` first if you don't already have one."*
> - `onelake_list_workspaces` / `onelake_list_items` / `onelake_list_items_dfs` — each must include *"For finding items by name, description, or type, use `core_search_catalog` from the hosted Fabric Core MCP server instead — listing is for full enumeration of a known scope."*
> - `onelake_list_files` keeps its existing "explore a known item" framing — file/path search is not in the catalog API's scope.
>
> If we get those strings right, the LLM runs the chain in three calls with no errors and never reaches for list+grep. **Agents that don't have the hosted Core MCP connected** will fall back to `_list_*` + client-side filtering, which is acceptable because no other option is available to them.

**Search-vs-list discipline.** This worked example is the model for every future search/list pair we add. Codified in §3 rule #3 and rule #8.

### 4.2 Other scenarios (sketches)

These are the other workflows the §1 tool set should make trivial.

- **Onboard a new shortcut target** — `core_search_catalog` (hosted) for the source item → `_list_shortcuts` to check for conflicts → `_create_or_update_shortcuts` → verify with `_get_shortcut`.
- **Grant role-based access to an item** — `_list_data_access_roles` → `_create_or_update_data_access_role` → `_get_principal_access` (verify effective access).
- **Audit effective access for a principal** — `core_search_catalog` (hosted) or `_list_items` to scope the items → `_get_principal_access` per item → consolidate.
- **Configure OneLake diagnostics for a workspace** — `_get_settings` → `_modify_diagnostics` → re-fetch to confirm.

---

## 5. Summary of changes vs. shipping tool

Compared to the live tool surface today:

- **Renames** (14): per the GA naming convention. Verb moves to prefix; `file_read`/`file_write` → `download_file`/`upload_file`; `-data` suffix → `-dfs`.
- **Removed from OneLake namespace**: `onelake_item_create` was already removed during GA prep (now `core_create_item`); `onelake_blob_list` and `onelake_blob_delete` to be removed/clarified pending platform team reply.
- **New** (13): 5 data access security (4 role-based + 1 principal-access), 5 shortcut, 3 settings. The bulk `Create Or Update Data Access Roles` operation is intentionally omitted (see §1.1). Catalog search is intentionally not shipped locally — it's owned by the hosted Fabric Core MCP (see §4.1).

Net: 19 → **31** tools in the OneLake namespace.

---

## 6. Docs fix-up task (in scope for vNext release)

The upstream docs need a pass:

1. **OneLake README** (<https://github.com/microsoft/mcp/blob/main/tools/Fabric.Mcp.Tools.OneLake/README.md>):
   - Remove the `item create` section and the friendly-name caveat referencing it; replace with a pointer to `core_create_item` in `Fabric.Mcp.Tools.Core`.
   - Update tool count (currently advertises "19 comprehensive OneLake commands"). Will become **31** with vNext, or **17** if landing the renames + removals before the new tool work.
   - Apply the new naming throughout examples and the "Available Commands" section.
   - Add sections for Security, Shortcuts, Settings.
   - Decide what to do about `blob_list` / `blob_delete` (open question with platform team): either remove them upstream officially, or document them.
2. **Core README** (under [`tools/Fabric.Mcp.Tools.Core`](https://github.com/microsoft/mcp/tree/main/tools/Fabric.Mcp.Tools.Core/src)):
   - Document `core_create_item` (the source already lives there in `Commands/ItemCreateCommand.cs`).
3. **Cross-link** the two READMEs so users following the OneLake docs land on Core for item creation, and vice versa.

This work should ride along with the vNext PR(s) so the docs ship in lockstep with the renames + new tools.

---

## 7. Open questions

- **`blob_list` / `blob_delete`**: officially remove from the local MCP, or restore them in the upstream docs? (Under review.)
- **`onelake_get_principal_access` Preview status**: do we want to flag this in the tool description so agents don't lean on it for production flows?
