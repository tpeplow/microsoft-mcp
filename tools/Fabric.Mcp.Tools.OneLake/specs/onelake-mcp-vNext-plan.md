# OneLake MCP vNext — Implementation Plan

## Problem
Implement the vNext OneLake MCP tool set per `tools/Fabric.Mcp.Tools.OneLake/specs/onelake-mcp-vNext.md`. Most GA renames are already in place (`list_items`, `list_workspaces`, `download_file`, `upload_file`, `list_files`, `list_items_dfs`); the work is to add 13 new tools across three categories, drop `blob_*`, refresh descriptions, and fix the README.

Existing pattern to mirror (per OneLake table commands):
- `IOneLakeService` adds new methods; `OneLakeService` implements against the Fabric Core API (`https://api.fabric.microsoft.com/v1`) using bearer token via `TokenCredential` (mirror `Fabric.Mcp.Tools.Core/Services/FabricCoreService.cs`).
- One sealed command class per tool in `src/Commands/<Group>/`, primary constructor, `[CommandMetadata]`, `GlobalCommand<TOptions>` base, `RegisterOptions` + `BindOptions` + `ExecuteAsync` with `HandleException`.
- One options POCO per command in `src/Options/`, extending `GlobalOptions`.
- Static `Option<T>` definitions added to `FabricOptionDefinitions.cs` (no readonly fields per repo rules — these are already static readonly which is the established pattern in this tool).
- Result records on the command class; register every new result type in `OneLakeJsonContext`.
- Register the command in `FabricOneLakeSetup.cs` (DI + `AddCommand`).
- One unit-test class per command under `tests/Commands/<Group>/`, extending `CommandUnitTestsBase<TCommand, IOneLakeService>`, using `NSubstitute`. Test the four shapes used by `TableListCommandTests` (metadata, schema, happy path, missing-required-option ⇒ 400, null-arg constructor guard).

## Approach (incremental, can land as separate PRs per §1)

### Phase 0 — Docs + cleanup (lands first per spec §1)
- Update `tools/Fabric.Mcp.Tools.OneLake/README.md`: drop `item create`, point at `core_create_item`; refresh names to GA verb-prefix; update tool count; add Security/Shortcuts/Settings sections; resolve `blob_*`.
- Cross-link from `tools/Fabric.Mcp.Tools.Core/README.md` (document `core_create_item`).
- Drop or rewrite the `CRITICAL:` shouting in current command descriptions (rule 7, §3).
- Remove `blob_*` commands and tests (pending OQ7 — flag in PR description; see Open Questions).
- Refresh `_list_*` descriptions to steer to `core_search_catalog` (§3.1).
- Refresh `_list_tables` description with the prerequisite-chain hint (§4.1).
- `OneLakeJsonContext` cleanup as needed.

### Phase 1 — Wire Fabric Core API client
- Decide: extend `OneLakeService` with Core API methods + Fabric scope, OR add a sibling `IOneLakeFabricApiService` in this project. Recommend a single internal helper inside `OneLakeService` for `SendFabricApiRequestAsync(...)` mirroring `FabricCoreService`, to keep one DI registration. (Open Question Q-IMPL.)
- Add a `FabricEndpoints` constants class (or reuse Core's pattern) for `api.fabric.microsoft.com/v1` and the Fabric scopes.
- Resolution: keep using existing `ResolveItemIdentifierAsync` / workspace identifier handling so security/shortcut/settings tools accept friendly names too (per §2.3).

### Phase 2 — Shortcuts (5 tools)
`onelake_list_shortcuts`, `onelake_get_shortcut`, `onelake_create_or_update_shortcuts`, `onelake_delete_shortcut`, `onelake_reset_shortcut_cache`.
- REST: `…/workspaces/{w}/items/{i}/shortcuts` and `…/shortcuts/{path}/{name}` per Fabric docs.
- New options: `--shortcut-path`, `--shortcut-name`, `--create-or-overwrite` (bool), payload JSON for create/update (accept `--definition` JSON string for AOT-friendly inline body).
- New models: `Shortcut`, `ShortcutTarget`, `ShortcutListResponse`, command result records.

### Phase 3 — Data access security (5 tools)
`onelake_list_data_access_roles`, `onelake_get_data_access_role`, `onelake_create_or_update_data_access_role`, `onelake_delete_data_access_role`, `onelake_get_principal_access`.
- REST: `…/items/{i}/dataAccessRoles` (single PUT/GET/DELETE variant only — bulk PUT intentionally not exposed, §1.1).
- Principal access: `…/items/{i}/dataAccessRoles/principalAccess` (Preview); options for `--principal-id`, `--principal-type`, `--input-path` (Tables|Files), `--max-results`, `--continuation-token`. Description must include the literal word "Preview" (rule 6).
- New options: `--role-name`, `--definition` (JSON), `--principal-id`, `--principal-type`, `--input-path`, `--max-results`.
- New models: `DataAccessRole`, `DataAccessRoleMember`, `DecisionRule`, `PrincipalAccessResult`.

### Phase 4 — Settings (3 tools)
`onelake_get_settings`, `onelake_modify_diagnostics`, `onelake_modify_immutability_policy`.
- REST: `…/workspaces/{w}/onelakeSettings` per Fabric docs.
- Workspace-scoped (no `--item`).
- New options: `--diagnostics` (JSON), `--immutability-policy` (JSON).
- New models: `OneLakeSettings`, `DiagnosticsConfiguration`, `ImmutabilityPolicy`.

### Phase 5 — Description polish + final review
- Apply the §3.1 strings verbatim (or near-verbatim) to every new + renamed tool's `[CommandMetadata(Description = …)]`.
- Walk `_list_*` again to confirm the `core_search_catalog` steer is consistent.
- Final docs sync, `dotnet build`, `dotnet format`, spelling check.

## Test strategy
- One unit-test class per command under `tests/Commands/<Group>/` mirroring `TableListCommandTests`:
  - metadata assertions
  - `CommandDefinition.Options` non-empty
  - happy-path returns expected payload
  - one missing-required-option per option ⇒ `BadRequest`
  - null-arg constructor guards
- Service-level tests: extend `OneLakeServiceTests` with `CapturingHttpMessageHandler` cases for each new endpoint family — verb, URL, headers (bearer, user-agent), payload shape.
- `FabricOneLakeSetupTests` — assert each new command type is registered.

## Spec gaps / things to flag to the user
1. **Tool name punctuation drift.** Spec writes `onelake_list_items-dfs` (with `-dfs` dash); existing code already uses `list_items_dfs` (underscore, GA-consistent). Spec also writes `onelake_get_principal_access` as a child of "data access roles" but the underlying URL family suggests it could equally sit in its own `principal-access` command group. Recommend keeping current `_dfs` (underscore) and grouping principal-access under Security. Confirm.
2. **CLI option schemas for new tools are not in the spec.** §3.1 explicitly says "arg schemas are out of scope". I'll propose the option list above; flag any that should be different (e.g., should `_create_or_update_shortcuts` accept inline JSON via `--definition` or a file via `--definition-file`? both?).
3. **Request/response payload shapes** for create/update bodies (data access roles, shortcuts, diagnostics, immutability) are not enumerated — I'll mirror the Fabric REST docs verbatim. Worth confirming we want the strict typed models vs. pass-through `JsonElement` (existing table commands return `JsonElement`).
4. **`blob_list` / `blob_delete`** — §7 OQ. Plan above assumes removal. Confirm before deletion.
5. **`onelake_get_principal_access` Preview flag** — §7 OQ. Plan above includes the literal word "Preview" per rule 6. Confirm.
6. **Hosted-MCP cross-references** in descriptions assume `core_search_catalog` as the exact tool name on the hosted Fabric Core MCP. Worth confirming the hosted name has not drifted.
7. **No mention of live/recorded tests.** The OneLake project today ships unit tests only (no `LiveTests` csproj, no `assets.json`, no `test-resources.bicep`). Repo guidelines say Azure tools must have live tests, but Fabric tools have not adopted that pattern here. Confirm we stay unit-test-only for vNext, or add live tests.
8. **`OneLakePrompts.cs`** still references old guidance (e.g., `onelake_list_items` semantics from before search-vs-list discipline). Should be refreshed in Phase 0/5 — spec doesn't call this out.
9. **Audience wiring (§2.3.1).** Spec says "no change needed beyond wiring the new HTTP clients to the right credential scope" — confirm we add Fabric scope (`https://api.fabric.microsoft.com/.default`) alongside the existing OneLake storage scope without breaking existing tools.
10. **AOT.** All new types must be in `OneLakeJsonContext`; the spec doesn't call this out but it's a hard repo rule.
11. **Immutability policy is one-way** per §3.1's `_modify_immutability_policy` description. No tooling-side guard is proposed (it just says "confirm with the user before applying"). Confirm we don't need a `--confirm` flag.

## Open questions for user
- Q-IMPL: extend `OneLakeService` or create new `OneLakeFabricApiService`?
- Q-PR: one big PR or per-category PRs (spec suggests incremental)?
- Q-BLOB: remove `blob_list` / `blob_delete` now or wait on platform team?
- Q-PROMPTS: refresh `OneLakePrompts.cs` as part of vNext?
- Q-SHAPES: typed models vs. `JsonElement` pass-through for write payloads?

## Decision log

| # | Decision | Rationale |
|---|---|---|
| D1 | Tool names use underscores throughout (`list_items_dfs`, not `list_items-dfs`). | Matches code already shipped; spec used a dash inconsistently. Keep GA-consistent verb_noun style. |
| D2 | Remove `blob_list` and `blob_delete` now. Keep `BlobGetCommand` / `BlobPutCommand` classes — they're already registered as `download_file` / `upload_file` and renaming the C# class files would balloon the diff. | Spec §2.4 marks the two `blob_*` tools REMOVED. The "rename" tools are not actually removed; the file-name confusion is cosmetic and out of scope. |
| D3 | Stay unit-test-only for vNext. No `LiveTests` csproj, no `assets.json`, no Bicep. | The OneLake tool ships unit-only today; Fabric tools haven't adopted the live-test pattern. Out of scope to change here. |
| D4 | One big PR (not per-category). | User direction. The plan still groups work by category for review clarity. |
| D5 | Refresh `OneLakePrompts.cs` as part of vNext. | User direction. Prompts must reflect GA naming and the search-vs-list / prerequisite-chain discipline; otherwise descriptions and prompts disagree. |
| D6 | Include the literal word **"Preview"** in `onelake_get_principal_access`'s description (rule 6 of §3). | User confirmed. Lets agents flag the user / avoid production paths. |
| D7 | No `--confirm` flag on `onelake_modify_immutability_policy`. The description warns the LLM the change is one-way. | Spec §3.1 and §7. Tooling-side gates fight the agent; the description is the right place. |
| D8 | Move all Fabric Core REST calls (shortcuts, data access security, settings) into a **new `IFabricApiService` / `FabricApiService`**, separate from `OneLakeService`. | User direction. `OneLakeService` is for the OneLake storage data plane (DFS / Blob / Table endpoints, `storage.azure.com` audience). The new tools talk to `api.fabric.microsoft.com` (Fabric audience). Different audience, different surface — separating them keeps responsibilities clear and avoids one service holding two TokenCredential scopes' worth of concerns. |
| D9 | Duplicate the `FabricEndpoints` constants locally (don't take a `Fabric.Mcp.Tools.Core` project ref). | Avoids a new cross-project dependency for two URLs and one scope; the values are stable. |
| D10 | **Outputs:** always `JsonElement` pass-through (no typed response models for new tools). **Inputs:** `JsonElement` for resource bodies that mirror typed REST resources (data access role `definition`, shortcut `target`, diagnostics, immutability policy). Named CLI options for *operation arguments* — IDs, enums, paging knobs (e.g., `--principal-id`, `--principal-type`, `--input-path`, `--max-results`, `--shortcut-name`, `--create-or-overwrite`). | User direction (pass-through over coded contracts so API additions don't require a release). Split keeps us release-free where REST schemas drift, while preserving named LLM-discoverable options for the simple operation knobs that don't drift and that the model genuinely benefits from seeing at tool-selection time. |
| D11 | Use a fresh GUID for each new command's `[CommandMetadata(Id = "...")]`. | Standard convention; Ids are stable identifiers. |
| D12 | `_create_or_update_*` tools have `Idempotent=true, Destructive=false, ReadOnly=false`. `_delete_*` have `Destructive=true, Idempotent=true`. `_get_principal_access` is `ReadOnly=true, Idempotent=true` despite using POST. | Matches semantic intent rather than HTTP verb; consistent with how existing OneLake commands set these flags. |
| D13 | Endpoint paths for Fabric REST that aren't unambiguously documented in the spec are implemented per Microsoft Learn templates with a `// TODO(vNext):` comment so a reviewer can re-verify before merge. | Avoids blocking on doc lookups; flags reverification clearly. |
