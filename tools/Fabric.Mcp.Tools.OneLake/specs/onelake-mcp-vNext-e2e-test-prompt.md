# OneLake MCP — End-to-end test prompt

A single self-contained prompt you can paste into any MCP-enabled chat (Copilot
Chat, Claude Code, Cursor) that has the **Fabric MCP server** attached. It
exercises every OneLake tool shipped on `spike/new-onlake-mcp-commands` against
a real Fabric workspace, then emits a **structured JSON report** designed to be
fed back into the same loop ("ralph loop") so the LLM-driven test → fix-bugs →
re-run cycle has the signal it needs to converge.

## Why this exists

The vNext PR ships 13 new tools and rewrites every existing description.
Unit tests cover argument parsing and JSON contract shape, but they don't
catch:

- Wrong REST URL templates (some are still `TODO(vNext)` markers).
- PATCH-vs-PUT mismatches on settings endpoints.
- Description ambiguity that causes the LLM to pick the wrong tool.
- Friendly-name → ID resolution edge cases.
- Anything that only fails against a live Fabric tenant.

Running the prompt below is the cheapest way to flush those out.

## Inputs you'll plug into the prompt

| Variable | What it is | How to get it |
|----------|-----------|---------------|
| `<workspace>` | Fabric workspace ID **(GUID)** or name to run against | Fabric portal → workspace settings, or `list_workspaces` |
| `<principal_object_id>` | Your Entra object ID — used as the role member for the security validation | `az ad signed-in-user show --query id -o tsv` |
| `<run_id>` | Short unique tag (e.g. `e2e20260508a`) so artifact names don't collide across reruns | Pick anything; bump per run |
| `<sample_file_local_path>` *(optional)* | Path to a small (≤ 1 KB) text/CSV/JSON file the LLM should upload | Defaults to inline-generated content if omitted |

## How to run

1. Make sure both the **Fabric MCP server** and (ideally) the **hosted Fabric
   Core MCP server** are wired into your chat client. The hosted core MCP gives
   the LLM `core_search_catalog` for the search-vs-list test cases — without
   it the assertions in §"Description checks" can't be validated.
2. Substitute the four placeholders above into the prompt below.
3. Paste the whole thing as a single user turn.
4. Wait for the JSON report at the end. Save it.
5. For the **ralph loop**: feed any `status: "fail"` entries (with their
   `bug_kind` and `error` fields) into a follow-up turn that asks the LLM to
   fix the underlying tool / description / test, rebuild, re-run this same
   prompt with a new `<run_id>`, and diff the reports.

## Cleanup expectations

The prompt asks the LLM to delete every artifact it created (shortcuts, files,
directories, data access roles, lakehouses) at the end. **Lakehouse delete is
not currently exposed as a Fabric MCP tool** — the LLM will report the two
lakehouse IDs in the cleanup section so you can drop them via the Fabric portal
manually. The prompt also explicitly avoids `modify_immutability_policy`
because immutable-data cleanup is a tenant-admin headache.

---

## The prompt — paste from here

```
You are running an end-to-end test of the Fabric MCP OneLake tool surface
against a real Fabric tenant. This is a structured test, not a free-form
conversation. Follow the protocol exactly. Do not ask me clarifying questions
during the run — make the most reasonable assumption, record it in the report,
and keep going.

# Inputs

- WORKSPACE = <workspace>
- PRINCIPAL_OBJECT_ID = <principal_object_id>
- RUN_ID = <run_id>
- SAMPLE_FILE_LOCAL_PATH = <sample_file_local_path>   # may be empty; if so, generate inline content

# Protocol — read all of this before starting

1. Run the phases below in order. Within a phase, run the steps in order.
2. **Never abort on a tool failure.** Record the failure in the report and
   continue. The whole point is to flush out bugs across the entire surface
   in one run.
3. Use stable, deterministic names derived from RUN_ID so a retry does not
   collide and so I can grep logs:
     - data lakehouse name:     `e2e_data_${RUN_ID}`
     - shortcut lakehouse name: `e2e_shortcut_${RUN_ID}`
     - working directory:       `Files/e2e/${RUN_ID}/`
     - sample file name:        `payload.json`
     - shortcut name:           `data_${RUN_ID}`
     - data access role name:   `e2e_role_${RUN_ID}`
4. Prefer the Fabric MCP tools shipped on this branch over any general-purpose
   tools. If a tool fails with what looks like a bug, do **not** fall back to
   workarounds (e.g. shelling out to az/curl) — that hides the bug. Record the
   failure and move on.
5. **Do NOT call `modify_immutability_policy` under any circumstances** — it is
   irreversible and creates cleanup pain. Mark it `skip` in the report.
6. Validate behavior at each step with explicit assertions (listed inline
   below). When an assertion fails, that's a `fail` even if the tool call
   itself returned 200.
7. For every tool call, capture both the inputs you used and the relevant
   slice of the output so I can review even when the call succeeded.
8. Do not redact error messages in the report. I need raw error strings to
   debug the underlying tool.

# Phase 0 — Setup

0.1  Resolve WORKSPACE to a workspace ID if a name was passed (use
     `list_workspaces`). Record the resolved ID.
0.2  Use `create-item` (Fabric Core) to create a Lakehouse named
     `e2e_data_${RUN_ID}` in WORKSPACE. Capture its item ID.
0.3  Use `create-item` (Fabric Core) to create a second Lakehouse named
     `e2e_shortcut_${RUN_ID}` in WORKSPACE. Capture its item ID.
0.4  If SAMPLE_FILE_LOCAL_PATH is empty, generate this content in memory and
     treat it as the payload bytes:
         {"run_id":"${RUN_ID}","msg":"hello onelake","values":[1,2,3]}

# Phase 1 — Workspace + item enumeration

1.1  Call `list_workspaces`.
     ASSERT: WORKSPACE appears in the result.
1.2  Call `list_items` for WORKSPACE.
     ASSERT: both lakehouses created in Phase 0 appear, with the IDs returned
     by `create-item`.
1.3  Call `list_items_dfs` for WORKSPACE.
     ASSERT: both lakehouses appear, returned as DFS paths.

# Phase 2 — Diagnostics (write & verify)

2.1  Call `get_settings` for WORKSPACE. Snapshot the response so we can
     restore later.
2.2  Call `modify_diagnostics` for WORKSPACE to enable diagnostics, with the
     destination set to the data lakehouse (`e2e_data_${RUN_ID}`). Use a
     minimal-but-valid `--diagnostics` JSON body. If you don't know the
     exact body shape, attempt a best-guess body, capture the request and
     the server's error response, and mark the step `fail` with
     `bug_kind: "tool"` (the description should have told you the shape).
2.3  Call `get_settings` for WORKSPACE again.
     ASSERT: diagnostics is now enabled and the destination matches what 2.2
     wrote. Capture the diff between 2.1 and 2.3.

# Phase 3 — Files + directories on the data lakehouse

3.1  Call `create_directory` to create `Files/e2e/${RUN_ID}/` on the data
     lakehouse. (Nested path — exercises intermediate-dir creation.)
3.2  Call `upload_file` to upload the payload bytes to
     `Files/e2e/${RUN_ID}/payload.json` on the data lakehouse.
3.3  Call `list_files` for `Files/e2e/${RUN_ID}/` on the data lakehouse.
     ASSERT: `payload.json` is present with non-zero size.
3.4  Call `download_file` for `Files/e2e/${RUN_ID}/payload.json` on the data
     lakehouse.
     ASSERT: decoded text matches the bytes sent in 3.2 byte-for-byte.

# Phase 4 — Shortcuts (cross-lakehouse read)

4.1  Call `create_or_update_shortcuts` on the **shortcut** lakehouse to create
     a single shortcut named `data_${RUN_ID}` at path `Files/` whose target is
     `Files/e2e/${RUN_ID}/` on the data lakehouse. (Both items live in the
     same workspace — this is a OneLake → OneLake shortcut, no external
     credentials needed.) Use `createOrOverwrite=true`.
4.2  Call `get_shortcut` for `data_${RUN_ID}` at `Files/` on the shortcut
     lakehouse. This validates the create succeeded — the shortcut
     metadata API is immediately consistent (only the listing path has
     the 30s window).
     ASSERT: target matches what 4.1 sent.
4.3  Wait 30 seconds before exercising the shortcut for reads. Newly
     created shortcuts have a ~30s eventual-consistency window before
     listing operations against them are reliable. Reads through the
     shortcut path still work immediately, but `list_files` at the
     shortcut root can return empty/404 during this window.
4.4  Call `list_shortcuts` on the shortcut lakehouse.
     ASSERT: `data_${RUN_ID}` appears with the expected target.
4.5  Call `list_files` for `Files/data_${RUN_ID}/` on the **shortcut**
     lakehouse.
     NOTE: if this still returns empty/404 despite the 30s wait in 4.3,
     sleep another 30s and retry once before failing.
     ASSERT: `payload.json` shows up via the shortcut.
4.6  Call `download_file` for `Files/data_${RUN_ID}/payload.json` on the
     shortcut lakehouse.
     ASSERT: bytes match the original payload from 3.2.
4.7  Call `reset_shortcut_cache` on the shortcut lakehouse. Repeat 4.6.
     ASSERT: still works (cache reset shouldn't break reads).

# Phase 5 — Tables (best-effort — empty is acceptable)

These tools are useful even when there is no Delta data yet; an empty list is
a valid pass as long as the call returned successfully.

5.1  Call `list_table_namespaces` on the data lakehouse. Record the result.
5.2  If 5.1 returned at least one namespace, call `get_table_namespace` for
     the first one.
5.3  Call `list_tables` for the namespace from 5.1 (or `dbo` if empty).
     Empty result is acceptable.
5.4  If 5.3 returned at least one table, call `get_table` and `get_table_config`
     for it. If 5.3 was empty, mark 5.4 `skip` with reason "no Delta tables
     present" — do not fabricate Delta tables.

# Phase 6 — Data access security (self-grant + verify)

6.1  Call `list_data_access_roles` on the data lakehouse. Snapshot.
6.2  Call `create_or_update_data_access_role` on the data lakehouse with role
     name `e2e_role_${RUN_ID}`, granting Read access to
     `Files/e2e/${RUN_ID}/` and adding PRINCIPAL_OBJECT_ID (type `User`) as a
     member. Use a minimal-but-valid `--definition` JSON body. If the body
     shape is unclear, best-guess, record the failure with `bug_kind: "tool"`
     (description should have told you the shape).
6.3  Call `list_data_access_roles` on the data lakehouse.
     ASSERT: `e2e_role_${RUN_ID}` is present.
6.4  Call `get_data_access_role` for `e2e_role_${RUN_ID}`.
     ASSERT: role definition matches what 6.2 sent (member set, decision
     rules, scope).
6.5  Call `get_principal_access` for PRINCIPAL_OBJECT_ID on the data
     lakehouse.
     ASSERT: the response includes the grant from `e2e_role_${RUN_ID}` —
     i.e. Read on `Files/e2e/${RUN_ID}/`. (We are not testing whether
     access actually works under another identity — too much overhead for
     this test. We are validating that the role we just wrote is reflected
     in the effective-access query for the granted principal.)

# Phase 7 — Description checks (only if hosted Fabric Core MCP is attached)

These are LLM-judgment tests of the description-quality work in §3.1 of the
spec. Skip the whole phase with reason "core MCP not attached" if you don't
see `core_search_catalog` in your tool list.

7.1  When asked "find lakehouses named e2e in my tenant", you should reach for
     `core_search_catalog` from the hosted Core MCP, not `list_items` /
     `list_items_dfs` / `list_workspaces`. Report which tool you would have
     picked. Pass = `core_search_catalog`.
7.2  When asked "show me what tables exist in `e2e_data_${RUN_ID}`", report
     the chain you would call. Pass = `list_table_namespaces` →
     `list_tables` (in that order).
7.3  When asked "what can <PRINCIPAL_OBJECT_ID> read on this lakehouse?",
     report which tool you'd pick. Pass = `get_principal_access`. Fail =
     `get_data_access_role` or `list_data_access_roles`.

# Phase 8 — Cleanup

Reverse-order teardown. Record each cleanup tool call's pass/fail too — those
are part of the test surface.

8.1  Call `modify_diagnostics` to restore the diagnostics state captured in
     2.1. If you can't reconstruct the prior body, leave diagnostics enabled
     and note this in the report so a human can clean up.
8.2  Call `delete_data_access_role` for `e2e_role_${RUN_ID}`.
8.3  Call `delete_shortcut` for `data_${RUN_ID}` on the shortcut lakehouse.
8.4  Call `delete_file` for `Files/e2e/${RUN_ID}/payload.json` on the data
     lakehouse.
8.5  Call `delete_directory` for `Files/e2e/${RUN_ID}/` on the data lakehouse
     with `recursive=true`.
8.6  Lakehouse deletion is **not** exposed as a Fabric MCP tool today. List
     the two lakehouse IDs explicitly in the report under
     `manual_cleanup_required` so the human running this can drop them via
     the Fabric portal.

# Final output — emit ONLY this JSON, nothing else after the closing brace

After all phases finish, emit a single fenced ```json``` block with this
shape. No prose before, after, or between. Keep the JSON valid — no trailing
commas, no comments. Truncate any single string field to ~2000 chars but
preserve full error messages verbatim up to that limit.

{
  "run_id": "<RUN_ID>",
  "workspace_id": "<resolved workspace id>",
  "data_lakehouse_id": "<id from 0.2>",
  "shortcut_lakehouse_id": "<id from 0.3>",
  "summary": {
    "tools_attempted": <int>,
    "tools_passed": <int>,
    "tools_failed": <int>,
    "tools_skipped": <int>
  },
  "results": [
    {
      "phase": "1.1",
      "tool": "list_workspaces",
      "status": "pass" | "fail" | "skip",
      "inputs": { ... what you sent, sanitized only of secrets ... },
      "output_summary": "...one-line summary, or null on fail...",
      "assertion": "WORKSPACE appears in result",
      "assertion_result": "pass" | "fail" | "n/a",
      "error": "...verbatim error text or null...",
      "bug_kind": "tool" | "description" | "test_setup" | "transient" | null,
      "notes": "...anything the human needs to triage this..."
    }
    // one entry per numbered step, including cleanup and skipped phases
  ],
  "description_check": {
    "ran": true | false,
    "results": [
      { "phase": "7.1", "expected": "core_search_catalog", "picked": "...", "status": "pass" | "fail" }
    ]
  },
  "manual_cleanup_required": [
    { "kind": "lakehouse", "workspace_id": "...", "item_id": "...", "name": "..." }
  ]
}
```

---

## Iterating in a ralph loop

Once you have the JSON report:

1. Filter to `status == "fail"`.
2. Bucket by `bug_kind`:
   - `tool` → fix the implementation in
     `tools/Fabric.Mcp.Tools.OneLake/src/Commands/**` or
     `tools/Fabric.Mcp.Tools.OneLake/src/Services/FabricApiService.cs`
     (resolve any lingering `TODO(vNext)` markers).
   - `description` → tighten the `[CommandMetadata(Description = ...)]` per
     spec §3.1; rerun `ToolDescriptionEvaluator` (see
     [`onelake-mcp-vNext-validation.md`](./onelake-mcp-vNext-validation.md)).
   - `test_setup` → fix this prompt (e.g. wrong assertion, wrong assumption
     about table seed data).
   - `transient` → re-run with a fresh `RUN_ID`; if it persists, reclassify.
3. Rebuild the Fabric server: `dotnet build servers/Fabric.Mcp.Server`.
4. Re-paste the prompt with a fresh `RUN_ID`.
5. Diff the new report against the previous — every previously-failing tool
   should now be `pass`.

Stop when **all phases except 7 (gated on hosted Core MCP) are `pass` or
`skip`**, and `manual_cleanup_required` is empty (or you've cleaned the
lakehouses by hand).
