# Validating OneLake MCP tool descriptions

This is a handoff doc for whoever runs the `ToolDescriptionEvaluator` against the
OneLake tool descriptions shipped on branch `spike/new-onlake-mcp-commands`. The
evaluator is a one-time gate before the vNext PR merges; it just needs an Azure
OpenAI embeddings deployment that this dev box doesn't currently have access to.

## What you're checking

The PR rewrites every OneLake tool description and adds 13 new ones (Shortcuts,
Data Access Security, Settings). The descriptions follow the eight rules in
[`onelake-mcp-vNext.md`](./onelake-mcp-vNext.md) §3 — sibling cross-references,
search-vs-list steering, prerequisite-tool hints, etc. The evaluator scores how
likely an LLM is to pick the *right* tool for each prompt by embedding the
prompt and the tool descriptions and comparing similarities.

**Pass criteria** (per `AGENTS.md` PR checklist):

- Every tool ranks in the **top 3** for every one of its prompts.
- Every tool scores **≥ 0.4 confidence** for every one of its prompts.

If a tool drops below either bar, the description in
`tools/Fabric.Mcp.Tools.OneLake/src/Commands/<area>/*Command.cs` needs a
disambiguating tweak (usually a sibling cross-reference per rule 2 or a
search-vs-list line per rule 3).

## Prerequisites

- An **Azure OpenAI** resource with a **text embedding** deployment (e.g.
  `text-embedding-3-small`, `text-embedding-3-large`, or `text-embedding-ada-002`).
  The evaluator authenticates with an **API key** — `DefaultAzureCredential`
  is *not* supported.
- .NET 10 SDK (already required to build the repo).
- This branch checked out: `spike/new-onlake-mcp-commands`.

If your team already runs this evaluator on PRs, ask for the shared eval
resource rather than provisioning a new one — that's almost certainly cheaper
and matches whatever calibration the existing scores were measured against.

### Standing up an AOAI resource if you need one

```bash
az cognitiveservices account create -n <name> -g <rg> -l eastus \
  --kind OpenAI --sku S0
az cognitiveservices account deployment create -n <name> -g <rg> \
  --deployment-name text-embedding-3-small \
  --model-name text-embedding-3-small --model-version 1 \
  --model-format OpenAI --sku-name Standard --sku-capacity 10
az cognitiveservices account keys list -n <name> -g <rg> --query key1 -o tsv
```

## Run the evaluator

### 1. Build the Fabric MCP server

The evaluator launches `fabmcp tools list` to discover tool names and
descriptions, so a Debug or Release build of `servers/Fabric.Mcp.Server` must
exist in `bin/`.

```bash
dotnet build servers/Fabric.Mcp.Server
```

### 2. Configure credentials

The endpoint URL must be the **full deployments URL** — not the resource base.
The format is documented in `eng/tools/ToolDescriptionEvaluator/README.md`.
Drop a `.env` file in the evaluator's `src/` directory (gitignored):

```bash
cat > eng/tools/ToolDescriptionEvaluator/src/.env <<'EOF'
AOAI_ENDPOINT=https://<your-resource>.openai.azure.com/openai/deployments/<embeddings-deployment-name>/embeddings?api-version=2023-05-15
TEXT_EMBEDDING_API_KEY=<key1-or-key2>
EOF
```

### 3. Run, scoped to OneLake

```bash
cd eng/tools/ToolDescriptionEvaluator/src
dotnet run -- --server Fabric --area onelake
```

Flags:

- `--server Fabric` — discover tools by running the `fabmcp` binary.
- `--area onelake` — only score prompts whose tool name starts with `onelake`
  (this matches `--area` semantics; if our OneLake tools register without a
  prefix, see the *Prompt source* note below).

Results land in `eng/tools/ToolDescriptionEvaluator/src/results/<timestamp>.md`
(or `.txt` with `--text-results`). The summary table calls out any tool that
fails top-3 or the 0.4 confidence threshold.

## Prompt source

The prompts the evaluator scores against live in
[`servers/Fabric.Mcp.Server/docs/e2eTestPrompts.md`](../../../servers/Fabric.Mcp.Server/docs/e2eTestPrompts.md).
Three or more prompts per tool, organized by category to mirror the spec §2
groups and exercise the rule-2 / rule-3 / rule-8 description discipline. If
`--area onelake` returns no prompts (because the runtime tool names have a
different prefix scheme than the file expects), drop `--area` to score every
prompt in the file:

```bash
dotnet run -- --server Fabric \
  --prompts-file ../../../../servers/Fabric.Mcp.Server/docs/e2eTestPrompts.md
```

## What to do with the results

1. Attach the generated results file to the PR (or paste the summary table
   into a PR comment).
2. For any tool below the bar:
   - Open `tools/Fabric.Mcp.Tools.OneLake/src/Commands/<area>/<Tool>Command.cs`.
   - Edit the `[CommandMetadata(Description = ...)]` string. The description
     rules and the strawman strings the descriptions are based on are in
     [`onelake-mcp-vNext.md`](./onelake-mcp-vNext.md) §3 / §3.1.
   - Rebuild and rerun the evaluator. Repeat until every tool passes both bars.
3. Tools most likely to need iteration (closest siblings):
   - `get_data_access_role` ↔ `get_principal_access`
   - `list_items` ↔ `list_items_dfs`
   - `download_file` (file path) — disambiguate from any `read`-style sibling
   - `modify_diagnostics` ↔ `get_settings`
   - `create_or_update_data_access_role` ↔ `create_or_update_shortcuts`

## Pointers

- Evaluator usage: [`eng/tools/ToolDescriptionEvaluator/README.md`](../../../eng/tools/ToolDescriptionEvaluator/README.md)
- Quickstart (env-only setup): [`eng/tools/ToolDescriptionEvaluator/Quickstart.md`](../../../eng/tools/ToolDescriptionEvaluator/Quickstart.md)
- vNext spec (description rules, §3 / §3.1): [`onelake-mcp-vNext.md`](./onelake-mcp-vNext.md)
- Implementation plan + decision log: [`onelake-mcp-vNext-plan.md`](./onelake-mcp-vNext-plan.md)
- Edited tool descriptions live in `tools/Fabric.Mcp.Tools.OneLake/src/Commands/**`
  on this branch (commit `1b7c118a`).
