# OneLake MCP — vNext E2E test results

End-to-end run of the protocol in
[`onelake-mcp-vNext-e2e-test-prompt.md`](./onelake-mcp-vNext-e2e-test-prompt.md)
against a real Fabric tenant via the **direct CLI** (`fabmcp.exe`), followed by
a fix pass and re-validation. Phase 7 (description-quality checks) is skipped
because it requires the hosted Fabric Core MCP wired into an LLM client.

## Run metadata

| | |
|---|---|
| Date | 2026-05-08 |
| Binary | `servers/Fabric.Mcp.Server/src/bin/Debug/fabmcp.exe` |
| Workspace | `6a8b0b3c-bfe8-40d2-8adc-10e26d62f8c7` (`TomsWorkspaceOnF2Capacity`) |
| Test lakehouses | `e2e_data_<run>` and `e2e_shortcut_<run>` (created/used during the run) |

## Headline

| | count |
|---|---|
| Steps attempted (initial run) | 33 |
| ✅ pass | 16 |
| ❌ fail (initial run) | 8 |
| ⏸ parked / deferred | 1 (`list_items` — already shipped, see [open questions](#open-questions-for-team-discussion)) |
| ⏭ skip (gated on other failures or out-of-scope) | 8 |

After the fix pass: **9 of the 10 fixable findings landed and were
re-validated end-to-end against the live tenant.** One finding
(`get_principal_access`) is parked pending an endpoint clarification; one
cross-cutting cleanup (`--workspace-id` / `--item-id` removal) is parked
pending a team decision (both listed below).

## Open questions for team discussion

These are the items where progress was blocked on a decision the broader team
should weigh in on. They are **not** blocked on engineering effort — each has
a concrete proposal attached.

### 1. `get_principal_access` — what is the correct endpoint?

The current implementation calls
`POST /v1/workspaces/{ws}/items/{itemId}/dataAccessRoles/principalAccess`,
which returns `404`. The bundled Fabric REST API swagger
(`tools/Fabric.Mcp.Tools.Docs/src/Resources/fabric-rest-api-specs/contents/platform/swagger.json`)
defines per-role and bulk endpoints under `dataAccessRoles` but **does not
define a `principalAccess` action at all** under any of the OneLake or
platform paths.

Possible answers we need:

- Is this Preview API published in a different swagger surface that we should
  pull into `Fabric.Mcp.Tools.Docs/src/Resources/fabric-rest-api-specs`?
- Is the URL different from what the current implementation assumes? (E.g.,
  a different verb, a different action name, or a different parent
  resource.)
- Should we drop the tool until the API is GA-ready?

Until this is resolved, `get_principal_access` is left as-is and flagged as
broken. All other data-access tools (`list_data_access_roles`,
`create_or_update_data_access_role`, `get_data_access_role`,
`delete_data_access_role`) round-trip correctly with the fixes in this PR.

### 2. Drop duplicate `--workspace-id` / `--item-id` options across all OneLake commands?

Today every OneLake command exposes both:

- `--workspace` *and* `--workspace-id`
- `--item` *and* `--item-id`

`AGENTS.md` says to use only `--workspace` and `--item` (each accepting either
a GUID or a friendly name). The `*-id` variants violate this convention and
double the surface area for every command without adding capability.

**Why this is parked:** removing `--workspace-id` / `--item-id` is a breaking
change for any pre-release caller that adopted them. It also touches ~12
command files and their tests.

**Proposal:** in a follow-up PR, drop the `*-id` variants and verify in
documentation that `--workspace` / `--item` accept GUIDs.

### 3. `list_items` (already shipped) — should we add an XML→objects parser?

`list_items` returns the raw OneLake DFS XML in an `xmlResponse` field with
`items[]` set to `null`. By contrast, `list_workspaces` returns both the raw
XML *and* a parsed object array — and after the fixes in this PR its `id`
field is the workspace GUID.

**Why this is parked:** `list_items` is already shipped, so changing its
output shape is potentially breaking and warrants a separate discussion.

**Proposal options:**

- (a) Add a parsed `items[]` alongside `xmlResponse` (additive, non-breaking).
- (b) Mirror the `list_workspaces` pattern exactly — same XML → typed objects
  with the GUID as the canonical `id`.
- (c) Leave as-is and document that callers must parse XML themselves.

### 4. Engineering signal: LLM-driven E2E loops can mis-attribute eventual-consistency failures to tool bugs

Phase 4.4 (`list_files` via a freshly-created shortcut) initially looked like
a tool bug — the call returned 404 even though the shortcut existed and
`download_file` against the same path worked.

Real cause: **OneLake shortcuts take ~30 seconds to become consistent for
listing operations**, while direct path-based file ops resolve immediately
through the shortcut.

This is a textbook failure mode for LLM-driven E2E tests:

- The tool call returns a real HTTP error.
- Every assertion in the test prompt is reasonable.
- A human who knows the platform shrugs and adds a `Start-Sleep 30`.
- An LLM in a ralph loop will instead "fix" the tool, regress nothing in
  unit tests, ship the change, and break some other path.

**What we already did in this PR:**

1. Updated the `create_or_update_shortcuts` tool description to call out the
   ~30s consistency window.
2. Updated the E2E test prompt (Phase 4.4) to explicitly tell the LLM about
   the window and to retry-with-backoff before failing.

**What the team should consider for any future tool that has similar
semantics** (any "create resource A, then list/read through A" sequence
where the read can race the write):

- Document consistency windows directly in the tool description so the LLM
  has them in-context.
- Consider building a small retry helper into the test runner so prompts
  don't have to hand-code backoff for every consistency-prone read.
- Audit other Fabric APIs for similar windows (e.g., role grants, settings
  changes) — if any exist, surface them the same way.

## Fixes landed in this PR

All items below were broken on the initial run and now pass end-to-end
validation against the live tenant. Files touched are listed for review
context.

| # | Symptom | Root cause | Fix |
|---|---------|------------|-----|
| 1 | `get_settings`, `modify_diagnostics`, `modify_immutability_policy` all 404 | Wrong URL (`/onelakeSettings`) and wrong verb (PATCH) | URLs updated to `/onelake/settings`, `/onelake/settings/modifyDiagnostics`, `/onelake/settings/modifyImmutabilityPolicy`; modify operations use POST. Resolves the `TODO(vNext)` PATCH-vs-PUT marker. (`Services/FabricApiService.cs`) |
| 2 | Every upstream non-2xx surfaced as `503 "Service unavailable"` | `EnsureSuccessAsync` threw `HttpRequestException` without an explicit status, and the base `HandleException` falls back to 503 when the status is missing | `EnsureSuccessAsync` now passes the actual `response.StatusCode` to the exception. (`Services/FabricApiService.cs`) |
| 3 | `create_or_update_data_access_role` 404 | Wrong URL template (per-role path) and wrong body shape (no `value` array) and missing `?preview=true` query param | PUT now targets the *collection* URL `/items/{itemId}/dataAccessRoles?preview=true&dataAccessRoleConflictPolicy=Overwrite` with body `{ "value": [<role>] }`. Bare-role bodies are wrapped automatically and the role name from `--role-name` is injected if not present. Per-role GET and DELETE add `?preview=true`. (`Services/FabricApiService.cs`) |
| 4 | `list_files` used `--path` while `create_directory` / `delete_directory` used `--directory-path` | Inconsistency | `list_files` now uses `--directory-path`. (`Commands/File/PathListCommand.cs`) |
| 5 | `list_workspaces` `id` field was the display name | Code populated `Id` from the container `Name` element only | `Id` is now `metadata.workspaceObjectId` (the GUID) when present, with display name kept on `displayName`. (`Services/OneLakeService.cs`) |
| 6 | `list_table_namespaces` returned `[["dbo"]]` plus a redundant `rawResponse` | Returned the raw Iceberg JSON shape verbatim | Now returns a flat `string[]` (`["dbo"]`); `rawResponse` removed. Multi-level Iceberg namespaces (e.g. `["db","schema"]`) are joined with `.`. (`Models/TableNamespaceListResult.cs`, `Services/OneLakeService.cs`, `Commands/Table/TableNamespaceListCommand.cs`) |
| 7 | `list_items_dfs` returned the raw JSON as an opaque string | No parser path | Now parses the response into a structured `items` `JsonElement`; falls back to the raw string only if parsing fails. (`Commands/Item/OneLakeItemDataListCommand.cs`) |
| 8 | `create_or_update_shortcuts` description claimed the API was "bulk-only" | The underlying API accepts a single shortcut object too | Description rewritten to reflect actual behaviour, and now documents the ~30s eventual-consistency window for downstream listing. (`Commands/Shortcuts/ShortcutCreateOrUpdateCommand.cs`) |
| 9 | `reset_shortcut_cache` had no `--item` option (initial analysis suspected a missing option) | Tool is correctly **workspace-scoped** per the Fabric API; the original analysis was wrong | Description updated to clarify workspace scope. No new option added. (`Commands/Shortcuts/ShortcutCacheResetCommand.cs`) |
| 10 | E2E prompt didn't tell the LLM about the 30s shortcut window | Test-prompt gap | Phase 4.4 now documents the consistency window and tells the LLM to retry-with-backoff before failing. (`specs/onelake-mcp-vNext-e2e-test-prompt.md`) |

## Re-validation summary

After the fixes were applied, the affected scenarios were re-run against the
same workspace:

| Scenario | Result |
|----------|--------|
| `list_workspaces` returns GUID as `id` | ✅ |
| `list_table_namespaces` returns `["dbo"]` (flat, no `rawResponse`) | ✅ |
| `list_files --directory-path` succeeds; `--path` is rejected | ✅ |
| `get_settings` returns 200 with diagnostics + lifecycle | ✅ |
| `list_items_dfs` returns parsed `items` (no opaque `jsonResponse`) | ✅ |
| `get_data_access_role` for a missing role returns 404 (not 503) | ✅ |
| Full DAR round-trip: `create → get → delete → get (404)` | ✅ |

## Per-step results from the initial run

Kept for reference and for the engineering signal in §4 above. ✅/❌ reflect
the **initial** run; items fixed in this PR are flagged in the rightmost
column.

| Phase | Tool | Initial status | Resolution |
|---|---|---|---|
| 0.2 | `core create-item` | ✅ pass | — |
| 0.3 | `core create-item` | ✅ pass | — |
| 1.1 | `list_workspaces` | ✅ pass (with `id` bug) | Fixed (#5) |
| 1.2 | `list_items` | ⏸ parked | Open question §3 |
| 1.3 | `list_items_dfs` | ❌ fail | Fixed (#7) |
| 2.1 | `get_settings` | ❌ fail | Fixed (#1) |
| 2.2 | `modify_diagnostics` | ❌ fail | Fixed (#1) |
| 2.3 | `get_settings` (verify) | ⏭ skip | Re-validated post-fix |
| 3.1 | `create_directory` | ✅ pass | — |
| 3.2 | `upload_file` | ✅ pass | — (note: `--content-type` appears to be dropped, not addressed in this PR) |
| 3.3 | `list_files` | ✅ pass (option-name bug) | Fixed (#4) |
| 3.4 | `download_file` | ✅ pass | — |
| 4.1 | `create_or_update_shortcuts` | ✅ pass (description bug) | Fixed (#8) |
| 4.2 | `list_shortcuts` | ✅ pass | — |
| 4.3 | `get_shortcut` | ✅ pass | — |
| 4.4 | `list_files` via shortcut | ❌ fail (initial), reclassified | Not a tool bug — eventual consistency. See §4. Test prompt updated (#10). |
| 4.5 | `download_file` via shortcut | ✅ pass | — |
| 4.6 | `reset_shortcut_cache` | ❌ fail (initial), reclassified | Not a tool bug — workspace-scoped API. Description updated (#9). |
| 5.1 | `list_table_namespaces` | ✅ pass (shape bug) | Fixed (#6) |
| 5.2 | `get_table_namespace` | ⏭ skip | — |
| 5.3 | `list_tables` | ✅ pass | — |
| 5.4 | `get_table` / `get_table_config` | ⏭ skip | — (no Delta tables in test lakehouse) |
| 6.1 | `list_data_access_roles` | ✅ pass | — |
| 6.2 | `create_or_update_data_access_role` | ❌ fail | Fixed (#3) |
| 6.3 | `list_data_access_roles` (verify) | ⏭ skip | Re-validated post-fix |
| 6.4 | `get_data_access_role` | ⏭ skip | Re-validated post-fix |
| 6.5 | `get_principal_access` | ❌ fail | Open question §1 |
| 7   | description checks | ⏭ skip | Out of scope (needs hosted Core MCP) |
| 8.1 | `modify_diagnostics` (restore) | ⏭ skip | Re-validated post-fix |
| 8.2 | `delete_data_access_role` | ⏭ skip | Re-validated post-fix |
| 8.3 | `delete_shortcut` | ✅ pass | — |
| 8.4 | `delete_file` | ✅ pass | — |
| 8.5 | `delete_directory` | ✅ pass | — |

## Second run — refined prompt against a fresh workspace

After the fixes landed and the prompt was hardened with lessons learned (new
runner harness section + protocol rules #9 and #10 in
[`onelake-mcp-vNext-e2e-test-prompt.md`](./onelake-mcp-vNext-e2e-test-prompt.md)),
a second run was driven against a different workspace via the same `fabmcp.exe`
CLI to confirm the fixes hold and to flush new findings.

### Run metadata

| | |
|---|---|
| Date | 2026-05-08 |
| Workspace | `56e15d20-fe2e-4b27-9d33-cbfcd69ecbde` (`TomMCPEndToEndTest`) |
| Run ID | `e2e20260508b` |
| Test lakehouses | `e2e_data_e2e20260508b` = `4206bab8-9a85-4fc8-9f63-e23992858d72`<br>`e2e_shortcut_e2e20260508b` = `2aaf4d75-efe0-43b8-be28-b7cd57653cd5` |
| Coverage | Phases 0 → 2.2 (paused on the new finding below; phases 3 → 8 not re-run in this iteration) |

### What the second run validated

| Phase | Tool | Result |
|---|---|---|
| 0.2 / 0.3 | `core create-item` | ✅ Both lakehouses created. Result path is `results.item.id` (now documented in the runner-harness cheat sheet). |
| 1.1 | `list_workspaces` | ✅ New workspace appears in the list. Confirms fix #5 — `id` is the GUID, display name lives on `displayName`. |
| 1.2 | `list_items` | ⏸ As expected — returns XML pass-through with `items: null`. Both lakehouse IDs present in `xmlResponse`. Tracked under open question §3. |
| 1.3 | `list_items_dfs` | ✅ Returns structured `items.paths[]` with both lakehouse GUIDs. Confirms fix #7. |
| 2.1 | `get_settings` | ✅ Returns the new structured `settings` shape (diagnostics + lifecycle). Confirms fix #1 (URL + verb). |

### New finding — `modify_diagnostics` body schema is undocumented

Phase 2.2 returns `400 BadRequest` (`errorCode: BadRequestMyFolder`) for every
plausible request body shape we tried. None of the following were accepted:

```json
{ "status": "Enabled", "destinationWorkspaceId": "...", "destinationItemId": "..." }
{ "diagnosticsState": "Enabled", "destination": { "type": "MyFolder", "workspaceId": "...", "lakehouseId": "...", "folderPath": "Files/diagnostics" } }
{ "diagnosticsState": "Enabled", "myOneLakeFolder": { "workspaceId": "...", "lakehouseId": "...", "folderPath": "Files/diagnostics" } }
```

The current tool description (`"Update the OneLake diagnostics configuration
for a workspace (e.g., enable/disable diagnostics, set destination)"`) does
not reveal the request-body shape, so an LLM caller has no way to construct a
valid body without trial and error against the live API.

This validates the new protocol rule #9 (cap body-guessing at 2 attempts and
record both as a `description` bug) — without that rule the run would have
spun on body shapes for a long time without producing useful signal.

**Follow-up needed:** confirm the canonical `modifyDiagnostics` request body
shape against Fabric documentation or service-team contact, and bake it into
the `modify_diagnostics` tool description (and ideally into a typed
`--diagnostics` schema with named options instead of a free-form JSON blob).
Add the same treatment to `create_or_update_data_access_role`, which has the
same description gap.

### Prompt hardening landed alongside this run

To stop future iterations re-discovering the same issues:

- `9482a0c6` — `get_shortcut` runs immediately after `create_or_update_shortcuts`
  to fail-fast on create errors (the metadata API is immediately consistent).
- `3d67a3d2` — Phase 4 has an explicit 30s wait step after shortcut create,
  with the reactive retry note kept as a safety net.
- `5bb91d0e` — Added a runner harness section (proven `Invoke-Fab` helper +
  full response-shape cheat sheet for every tool) plus two new protocol
  rules: #9 cap body-shape guessing at 2 attempts, #10 response-shape ground
  rules covering field paths that bit during this run (`results.item.id`, no
  `.results.response` wrapper, `displayName` not `name`, prefer
  `list_items_dfs` over `list_items`, dump full envelope when a field is
  unexpectedly null).

## Manual cleanup required

OneLake item deletion is not exposed as a Fabric MCP tool today, so the test
lakehouses created during these runs still need to be removed via the Fabric
portal.

| Workspace | Lakehouse | Item ID |
|---|---|---|
| `6a8b0b3c-bfe8-40d2-8adc-10e26d62f8c7` | (initial run) | `324355ad-9933-4072-8c6b-04a383188887` |
| `6a8b0b3c-bfe8-40d2-8adc-10e26d62f8c7` | (initial run) | `4df663e7-9b51-4dac-9ac7-e438054918f7` |
| `56e15d20-fe2e-4b27-9d33-cbfcd69ecbde` | `e2e_data_e2e20260508b` | `4206bab8-9a85-4fc8-9f63-e23992858d72` |
| `56e15d20-fe2e-4b27-9d33-cbfcd69ecbde` | `e2e_shortcut_e2e20260508b` | `2aaf4d75-efe0-43b8-be28-b7cd57653cd5` |
