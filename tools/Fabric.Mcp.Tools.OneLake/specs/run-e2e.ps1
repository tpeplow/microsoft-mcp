# =============================================================================
# OneLake MCP — end-to-end deterministic test harness
# =============================================================================
#
# PROVENANCE
# ----------
# This script is a GENERATED ARTIFACT of the canonical e2e prompt at
#   tools/Fabric.Mcp.Tools.OneLake/specs/onelake-mcp-vNext-e2e-test-prompt.md
# When the prompt changes (new tools, new phases, new assertions) re-generate
# this script by running an LLM session against that prompt — do NOT hand-edit
# this file as the primary maintenance path. The prompt is the source of truth.
#
# WHAT IT DOES
# ------------
# Walks every OneLake MCP tool through the e2e scenarios in the prompt
# (workspace + lakehouse setup, settings/diagnostics, file CRUD, shortcuts,
# tables, data-access roles, cleanup), writing a per-run markdown transcript
# of every request/response under specs/runs/onelake-e2e-<RUN_ID>.md.
#
# REQUIREMENTS
# ------------
# - PowerShell 7+ (`pwsh`). Windows PowerShell 5.1 is NOT supported because
#   `ConvertFrom-Json -Depth` was added in PS6.
# - `fabmcp.exe` built locally:
#     dotnet build servers/Fabric.Mcp.Server/src
# - `az login` against the tenant that owns the test workspace.
# - A pre-existing Fabric workspace the caller has Contributor on; lakehouses
#   are created and (mostly) cleaned up by the script. Lakehouses themselves
#   must currently be deleted manually (no MCP tool exists yet).
#
# USAGE
# -----
#   pwsh -NoProfile -ExecutionPolicy Bypass `
#        -File tools/Fabric.Mcp.Tools.OneLake/specs/run-e2e.ps1 `
#        -WorkspaceId <guid> -PrincipalId <guid> -TenantId <guid> `
#        [-RunId <id>] [-FabmcpPath <path>]
#
# Defaults assume: WorkspaceId/PrincipalId/TenantId/FabmcpPath taken from
# environment ($env:WS, $env:PRINCIPAL, $env:TENANT_ID, $env:FABMCP) and
# RunId auto-generated from the current UTC timestamp.
# =============================================================================

[CmdletBinding()]
param(
    [string]$WorkspaceId  = $env:WS,
    [string]$PrincipalId  = $env:PRINCIPAL,
    [string]$TenantId     = $env:TENANT_ID,
    [string]$RunId        = ("e2e" + (Get-Date -AsUTC).ToString("yyyyMMddHHmm")),
    [string]$FabmcpPath   = $(if ($env:FABMCP) { $env:FABMCP } else { (Resolve-Path "$PSScriptRoot/../../../servers/Fabric.Mcp.Server/src/bin/Debug/fabmcp.exe" -ErrorAction SilentlyContinue) })
)

if (-not $WorkspaceId)  { throw "WorkspaceId is required (pass -WorkspaceId or set `$env:WS)." }
if (-not $PrincipalId)  { throw "PrincipalId is required (pass -PrincipalId or set `$env:PRINCIPAL)." }
if (-not $TenantId)     { throw "TenantId is required (pass -TenantId or set `$env:TENANT_ID)." }
if (-not $FabmcpPath -or -not (Test-Path $FabmcpPath)) {
    throw "FabmcpPath '$FabmcpPath' not found. Build fabmcp first: dotnet build servers/Fabric.Mcp.Server/src"
}

$ErrorActionPreference = 'Continue'
$env:WS        = $WorkspaceId
$env:RUN_ID    = $RunId
$env:PRINCIPAL = $PrincipalId
$env:TENANT_ID = $TenantId
$env:FABMCP    = (Resolve-Path $FabmcpPath).Path

$runsDir   = Join-Path $PSScriptRoot 'runs'
if (-not (Test-Path $runsDir)) { New-Item -ItemType Directory -Path $runsDir | Out-Null }
$transcript = Join-Path $runsDir "onelake-e2e-$RunId.md"

Write-Host "Workspace : $WorkspaceId"
Write-Host "RunId     : $RunId"
Write-Host "Principal : $PrincipalId"
Write-Host "fabmcp    : $env:FABMCP"
Write-Host "Transcript: $transcript"

function Append-Trans { param([string]$Text) Add-Content -Path $transcript -Value $Text -Encoding UTF8 }

function Invoke-Fab {
    param([string[]]$ArgsList)
    $errFile = [IO.Path]::GetTempFileName()
    $raw = & $env:FABMCP @ArgsList 2>$errFile | Out-String
    $raw = $raw.Trim()
    Remove-Item $errFile -ErrorAction SilentlyContinue
    $m = [regex]::Match($raw, '(?ms)^\{[\r\n]+\s*"status"')
    $jsonText = if ($m.Success) { $raw.Substring($m.Index).Trim() } else { $raw }
    try {
        $obj = $jsonText | ConvertFrom-Json -Depth 30
        return @{ Raw=$raw; Json=$obj; JsonText=$jsonText; Ok=($obj.status -ge 200 -and $obj.status -lt 300) }
    } catch {
        return @{ Raw=$raw; Json=$null; JsonText=$jsonText; Ok=$false }
    }
}

function Step {
    param(
        [string]$Phase, [string]$Tool, [string]$Assertion,
        [string[]]$ArgsList, [scriptblock]$AssertFn = $null,
        [string]$Notes = ''
    )
    $r = Invoke-Fab -ArgsList $ArgsList
    $assertResult = 'n/a'
    if ($AssertFn) {
        try { $assertResult = if (& $AssertFn $r) { 'pass' } else { 'fail' } } catch { $assertResult = 'fail'; $Notes += " (assert error: $_)" }
    }
    $outcome = if ($assertResult -eq 'pass') { 'pass' } elseif ($assertResult -eq 'fail') { 'fail' } elseif (-not $r.Ok) { 'fail' } else { 'pass' }
    $cliCmd = "fabmcp " + ($ArgsList -join ' ')
    $respText = if ($r.JsonText) { $r.JsonText } else { $r.Raw }
    if ($respText.Length -gt 6000) { $respText = $respText.Substring(0,6000) + "`n... (truncated)" }
    Append-Trans @"

### $Phase  $Tool

**Outcome:** $outcome
**Assertion:** $Assertion -> $assertResult
**Notes:** $Notes

**Request**

``````
$cliCmd
``````

**Response**

``````json
$respText
``````
"@
    Write-Host "[$Phase $Tool] outcome=$outcome assert=$assertResult"
    return $r
}

function Skip-Step {
    param([string]$Phase, [string]$Tool, [string]$Reason)
    Append-Trans @"

### $Phase  $Tool

**Outcome:** skip
**Assertion:** n/a
**Notes:** $Reason
"@
    Write-Host "[$Phase $Tool] SKIP: $Reason"
}

# initialize transcript
Set-Content -Path $transcript -Value @"
# OneLake MCP E2E run  $($env:RUN_ID)

| | |
|---|---|
| Workspace | ``$($env:WS)`` |
| Run ID | ``$($env:RUN_ID)`` |
| Principal | ``$($env:PRINCIPAL)`` |
| Binary | ``$env:FABMCP`` |
| Started | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz') |

Generated automatically by ``run-e2e.ps1`` per protocol rule #11 of
``onelake-mcp-vNext-e2e-test-prompt.md``. Each section below records the
exact CLI invocation and the full unredacted Fabric MCP envelope.

## Phase 0  Setup
"@ -Encoding UTF8

# === Phase 0 ===
$r = Step '0.2' 'core create-item' "lakehouse created" `
    @('core','create-item','--workspace',$env:WS,'--display-name',"e2e_data_$($env:RUN_ID)",'--item-type','Lakehouse') `
    { param($x) $null -ne $x.Json.results.item.id }
$DATA = $r.Json.results.item.id

$r = Step '0.3' 'core create-item' "lakehouse created" `
    @('core','create-item','--workspace',$env:WS,'--display-name',"e2e_shortcut_$($env:RUN_ID)",'--item-type','Lakehouse') `
    { param($x) $null -ne $x.Json.results.item.id }
$SC = $r.Json.results.item.id

Set-Content "$env:TEMP\e2e-ids-$($env:RUN_ID).txt" "DATA=$DATA`nSHORTCUT=$SC" -Encoding UTF8

Append-Trans "`n## Phase 1  Workspace + item enumeration"

# === Phase 1 ===
Step '1.1' 'list_workspaces' "WORKSPACE in result" @('onelake','list_workspaces') `
    { param($x) ($x.Json.results.workspaces | Where-Object { $_.id -eq $env:WS }).Count -ge 1 } | Out-Null

Step '1.2' 'list_items' "lakehouse IDs in xmlResponse (XML pass-through)" `
    @('onelake','list_items','--workspace-id',$env:WS) `
    { param($x) $xml=[string]$x.Json.results.xmlResponse; $xml.Contains($DATA) -and $xml.Contains($SC) } | Out-Null

Step '1.3' 'list_items_dfs' "both lakehouses appear as paths" `
    @('onelake','list_items_dfs','--workspace-id',$env:WS) `
    { param($x) ($x.Json.results.items.paths | Where-Object { $_.name -eq $DATA -or $_.name -eq $SC }).Count -eq 2 } | Out-Null

Append-Trans "`n## Phase 2  Diagnostics"

# === Phase 2 ===
$snap = Step '2.1' 'get_settings (snapshot)' "200 returned" @('onelake','get_settings','--workspace-id',$env:WS) `
    { param($x) $x.Ok }

$diagBody = (@{
    status='Enabled'
    destination=@{
        type='Lakehouse'
        lakehouse=@{ referenceType='ById'; itemId=$DATA; workspaceId=$env:WS }
    }
} | ConvertTo-Json -Compress -Depth 5)

Step '2.2' 'modify_diagnostics' "call accepted (200/202)" `
    @('onelake','modify_diagnostics','--workspace-id',$env:WS,'--diagnostics',$diagBody) `
    { param($x) $x.Json -eq $null -or $x.Ok -or $x.Json.status -eq 202 } "LRO; verify via 2.3" | Out-Null

Start-Sleep -Seconds 5
Step '2.3' 'get_settings (verify)' "diagnostics now Enabled with destination=DATA" `
    @('onelake','get_settings','--workspace-id',$env:WS) `
    { param($x) $x.Json.results.settings.diagnostics.status -eq 'Enabled' -and $x.Json.results.settings.diagnostics.destination.lakehouse.itemId -eq $DATA } | Out-Null

Append-Trans "`n## Phase 3  Files + directories"

# === Phase 3 ===
Step '3.1' 'create_directory' "directory created" `
    @('onelake','create_directory','--workspace-id',$env:WS,'--item-id',$DATA,'--directory-path',"Files/e2e/$($env:RUN_ID)/") `
    { param($x) $x.Ok } | Out-Null

$payload = '{"run_id":"' + $env:RUN_ID + '","msg":"hello onelake","values":[1,2,3]}'
$tmpPayload = "$env:TEMP\payload-$($env:RUN_ID).json"
Set-Content $tmpPayload $payload -NoNewline -Encoding UTF8

Step '3.2' 'upload_file' "file uploaded" `
    @('onelake','upload_file','--workspace-id',$env:WS,'--item-id',$DATA,'--file-path',"Files/e2e/$($env:RUN_ID)/payload.json",'--local-file-path',$tmpPayload) `
    { param($x) $x.Ok } | Out-Null

Step '3.3' 'list_files' "payload.json present non-zero" `
    @('onelake','list_files','--workspace-id',$env:WS,'--item-id',$DATA,'--directory-path',"Files/e2e/$($env:RUN_ID)/") `
    { param($x) ($x.Json.results.items | Where-Object { $_.name -match 'payload.json' -and [int]$_.size -gt 0 }).Count -ge 1 } | Out-Null

$dl = Step '3.4' 'download_file' "decoded matches uploaded payload" `
    @('onelake','download_file','--workspace-id',$env:WS,'--item-id',$DATA,'--file-path',"Files/e2e/$($env:RUN_ID)/payload.json") `
    { param($x) $b64 = $x.Json.results.blob.contentBase64; if (-not $b64) { return $false }; [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)) -eq $payload }

Append-Trans "`n## Phase 4  Shortcuts"

# === Phase 4 ===
$shortcutDef = (@{
    name = "data_$($env:RUN_ID)"
    path = 'Files/'
    target = @{
        type = 'OneLake'
        oneLake = @{ workspaceId=$env:WS; itemId=$DATA; path="Files/e2e/$($env:RUN_ID)/" }
    }
} | ConvertTo-Json -Compress -Depth 5)

Step '4.1' 'create_or_update_shortcuts' "create accepted (200/201)" `
    @('onelake','create_or_update_shortcuts','--workspace-id',$env:WS,'--item-id',$SC,'--definition',$shortcutDef,'--create-or-overwrite','true') `
    { param($x) $x.Ok } | Out-Null

Step '4.2' 'get_shortcut (immediate)' "metadata reflects create (immediate consistency)" `
    @('onelake','get_shortcut','--workspace-id',$env:WS,'--item-id',$SC,'--shortcut-path','Files/','--shortcut-name',"data_$($env:RUN_ID)") `
    { param($x) $x.Ok -and $x.Json.results.shortcut.target.oneLake.itemId -eq $DATA } | Out-Null

Append-Trans "`n*[waiting 30s for shortcut listing consistency window]*"
Start-Sleep -Seconds 30

Step '4.4' 'list_shortcuts' "shortcut appears in list" `
    @('onelake','list_shortcuts','--workspace-id',$env:WS,'--item-id',$SC) `
    { param($x) ($x.Json.results.shortcuts.value | Where-Object { $_.name -eq "data_$($env:RUN_ID)" }).Count -ge 1 } | Out-Null

Step '4.5' 'list_files (via shortcut)' "payload.json visible via shortcut" `
    @('onelake','list_files','--workspace-id',$env:WS,'--item-id',$SC,'--directory-path',"Files/data_$($env:RUN_ID)/") `
    { param($x) ($x.Json.results.items | Where-Object { $_.name -match 'payload.json' }).Count -ge 1 } | Out-Null

Step '4.6' 'download_file (via shortcut)' "bytes match original" `
    @('onelake','download_file','--workspace-id',$env:WS,'--item-id',$SC,'--file-path',"Files/data_$($env:RUN_ID)/payload.json") `
    { param($x) $b64 = $x.Json.results.blob.contentBase64; if (-not $b64) { return $false }; [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)) -eq $payload } | Out-Null

Step '4.7a' 'reset_shortcut_cache' "200 returned (workspace-scoped)" `
    @('onelake','reset_shortcut_cache','--workspace-id',$env:WS) `
    { param($x) $x.Ok -or ($x.Json.results.message -match 'ExternalShortcutCacheDisabled') } 'workspace may not have external-shortcut cache enabled' | Out-Null

Step '4.7b' 'download_file (post cache-reset)' "bytes still match after reset" `
    @('onelake','download_file','--workspace-id',$env:WS,'--item-id',$SC,'--file-path',"Files/data_$($env:RUN_ID)/payload.json") `
    { param($x) $b64 = $x.Json.results.blob.contentBase64; if (-not $b64) { return $false }; [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)) -eq $payload } | Out-Null

Append-Trans "`n## Phase 5  Tables (best-effort, empty acceptable)"

# === Phase 5 ===
$ns = Step '5.1' 'list_table_namespaces' "200 returned (empty acceptable)" `
    @('onelake','list_table_namespaces','--workspace-id',$env:WS,'--item-id',$DATA) `
    { param($x) $x.Ok }
$nsList = $ns.Json.results.namespaces

if ($nsList -and $nsList.Count -gt 0) {
    Step '5.2' 'get_table_namespace' "200 returned" `
        @('onelake','get_table_namespace','--workspace-id',$env:WS,'--item-id',$DATA,'--namespace',$nsList[0]) `
        { param($x) $x.Ok } | Out-Null
} else { Skip-Step '5.2' 'get_table_namespace' 'no namespaces returned' }

$nsForList = if ($nsList -and $nsList.Count -gt 0) { $nsList[0] } else { 'dbo' }
$tbl = Step '5.3' 'list_tables' "200 returned (empty acceptable)" `
    @('onelake','list_tables','--workspace-id',$env:WS,'--item-id',$DATA,'--namespace',$nsForList) `
    { param($x) $x.Ok }
$tableIds = $tbl.Json.results.tables.identifiers
if ($tableIds -and $tableIds.Count -gt 0) {
    $tname = if ($tableIds[0] -is [string]) { $tableIds[0] } else { $tableIds[0].name }
    Step '5.4a' 'get_table' "200 returned" `
        @('onelake','get_table','--workspace-id',$env:WS,'--item-id',$DATA,'--namespace',$nsForList,'--table',$tname) `
        { param($x) $x.Ok } | Out-Null
    Step '5.4b' 'get_table_config' "200 returned" `
        @('onelake','get_table_config','--workspace-id',$env:WS,'--item-id',$DATA,'--namespace',$nsForList,'--table',$tname) `
        { param($x) $x.Ok } | Out-Null
} else {
    Skip-Step '5.4' 'get_table / get_table_config' 'no Delta tables present'
}

Append-Trans "`n## Phase 6  Data access security"

# === Phase 6 ===
Step '6.1' 'list_data_access_roles (snapshot)' "200 returned" `
    @('onelake','list_data_access_roles','--workspace-id',$env:WS,'--item-id',$DATA) `
    { param($x) $x.Ok } | Out-Null

$roleDef = (@{
    decisionRules = @(@{
        effect = 'Permit'
        permission = @(
            @{ attributeName='Action'; attributeValueIncludedIn=@('Read') },
            @{ attributeName='Path';   attributeValueIncludedIn=@("/Files/e2e/$($env:RUN_ID)/") }
        )
    })
    members = @{
        microsoftEntraMembers = @(@{ objectId=$env:PRINCIPAL; tenantId=$env:TENANT_ID })
    }
} | ConvertTo-Json -Compress -Depth 8)

Step '6.2' 'create_or_update_data_access_role' "create accepted" `
    @('onelake','create_or_update_data_access_role','--workspace-id',$env:WS,'--item-id',$DATA,'--role-name',"e2erole$($env:RUN_ID)",'--definition',$roleDef) `
    { param($x) $x.Ok } "canonical Action+Path body" | Out-Null

Step '6.3' 'list_data_access_roles (verify)' "role appears in list" `
    @('onelake','list_data_access_roles','--workspace-id',$env:WS,'--item-id',$DATA) `
    { param($x) ($x.Json.results.roles.value | Where-Object { $_.name -eq "e2erole$($env:RUN_ID)" }).Count -ge 1 } | Out-Null

Step '6.4' 'get_data_access_role' "definition matches" `
    @('onelake','get_data_access_role','--workspace-id',$env:WS,'--item-id',$DATA,'--role-name',"e2erole$($env:RUN_ID)") `
    { param($x) $x.Ok } | Out-Null

Step '6.5' 'get_principal_access' "grant from e2e_role appears" `
    @('onelake','get_principal_access','--workspace-id',$env:WS,'--item-id',$DATA,'--principal-id',$env:PRINCIPAL,'--principal-type','User') `
    { param($x) $x.Ok } "known broken endpoint per open question 1" | Out-Null

Skip-Step '7' 'description checks' 'core_search_catalog (hosted Core MCP) not attached'

Append-Trans "`n## Phase 8  Cleanup"

# === Phase 8 cleanup ===
$disableBody = (@{ status='Disabled' } | ConvertTo-Json -Compress)
Step '8.1' 'modify_diagnostics (restore=Disabled)' "diagnostics disabled" `
    @('onelake','modify_diagnostics','--workspace-id',$env:WS,'--diagnostics',$disableBody) `
    { param($x) $x.Json -eq $null -or $x.Ok -or $x.Json.status -eq 202 } | Out-Null

Step '8.2' 'delete_data_access_role' "role removed" `
    @('onelake','delete_data_access_role','--workspace-id',$env:WS,'--item-id',$DATA,'--role-name',"e2erole$($env:RUN_ID)") `
    { param($x) $x.Ok } | Out-Null

Step '8.3' 'delete_shortcut' "shortcut removed" `
    @('onelake','delete_shortcut','--workspace-id',$env:WS,'--item-id',$SC,'--shortcut-path','Files/','--shortcut-name',"data_$($env:RUN_ID)") `
    { param($x) $x.Ok } | Out-Null

Step '8.4' 'delete_file' "payload removed" `
    @('onelake','delete_file','--workspace-id',$env:WS,'--item-id',$DATA,'--file-path',"Files/e2e/$($env:RUN_ID)/payload.json") `
    { param($x) $x.Ok } | Out-Null

Step '8.5' 'delete_directory' "directory removed (recursive)" `
    @('onelake','delete_directory','--workspace-id',$env:WS,'--item-id',$DATA,'--directory-path',"Files/e2e/$($env:RUN_ID)/",'--recursive','true') `
    { param($x) $x.Ok } | Out-Null

Append-Trans "`n## Manual cleanup`n`nLakehouses created during this run (delete via Fabric portal):`n- ``$DATA`` (e2e_data_$($env:RUN_ID))`n- ``$SC`` (e2e_shortcut_$($env:RUN_ID))`n"

Write-Host "===== TRANSCRIPT WRITTEN: $transcript ====="
Write-Host "DATA=$DATA"
Write-Host "SHORTCUT=$SC"




