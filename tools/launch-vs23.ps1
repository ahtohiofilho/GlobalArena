# GA-VS23: deterministic DIRECT game launch from THIS worktree.
# No editor selection or F5 required. Build Debug for Godot Mono runtime.
# Run from PowerShell with:
#   powershell -ExecutionPolicy Bypass -File .\tools\launch-vs23.ps1
# Optional: -GodotExe "C:\path\to\Godot_v4.7.2-stable_mono_win64.exe"
# Optional: -Editor opens project editor rather than the game directly.
[CmdletBinding()]
param(
    [string]$GodotExe,
    [switch]$Editor
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectDir = Join-Path $repoRoot 'tools\GlobalArena.Godot'
$mainCode = Join-Path $projectDir 'Scripts\Main.cs'
$labCode = Join-Path $projectDir 'Scripts\Main.NeolithicWorkerLab.cs'
$expectedBranch = 'feature/ga-vs23-multicycle-economic-outcomes-20261031'

if (-not (Test-Path (Join-Path $projectDir 'project.godot'))) {
    throw "This directory does not contain the GA-VS23 Godot project: $projectDir"
}
$repoName = (git -C $repoRoot remote get-url origin 2>$null)
if ($LASTEXITCODE -ne 0 -or $repoName -notmatch 'GlobalArena') {
    throw "Expected the GlobalArena Git worktree; found $repoRoot"
}
$head = (git -C $repoRoot rev-parse HEAD).Trim()
$expectedHead = (git -C $repoRoot rev-parse "origin/$expectedBranch" 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw "Branch origin/$expectedBranch missing. Run git fetch origin first."
}
$expectedHead = $expectedHead.Trim()
if ($head -ne $expectedHead) {
    throw "This checkout is not the current VS23 branch head. Run git fetch origin then git switch --detach origin/$expectedBranch. Actual: $head Expected: $expectedHead"
}
$main = [System.IO.File]::ReadAllText($mainCode, [System.Text.Encoding]::UTF8)
$lab = [System.IO.File]::ReadAllText($labCode, [System.Text.Encoding]::UTF8)
# Match stable ASCII runtime/behavior signatures. Visual titles with
# diacritics are not reliable guards on every Windows PowerShell code page.
# Report the exact missing signature rather than a generic UI mismatch.
$signatures = [ordered]@{
    'VS23 C# runtime marker' = $main.Contains('GA_VS23_RUNTIME_ACTIVE')
    'VS23 Godot window title' = $main.Contains('GetWindow().Title = "Global Arena') -and $main.Contains('VS23 | Resultados')
    'VS23 tactical HUD' = $lab.Contains('RESULTADOS ECON') -and $lab.Contains('VS23')
    'VS23 bounded five-cycle control' = $lab.Contains('AdvanceOfflineLabFiveEconomicCycles')
    'VS23 canonical five-cycle composer' = $lab.Contains('PhysicalEconomicCycleProgression.AdvanceBatch')
    'VS22 next-cycle command preserved' = $lab.Contains('AdvanceOfflineLabToNextEconomicCycle')
}
foreach ($check in $signatures.GetEnumerator()) {
    if (-not $check.Value) {
        throw "VS23 worktree verification failed: $($check.Key) missing. Review checked-out sources before launching."
    }
}
Write-Host 'VS23 runtime and canonical-cycle signatures verified.' -ForegroundColor Green

if (-not $GodotExe) {
    # The current Godot editor/game is the most reliable source for the
    # exact version and Mono executable already installed on this Windows PC.
    $running = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^Godot.*\.exe$' -and
                       $_.ExecutablePath -and
                       (Test-Path $_.ExecutablePath) } |
        Select-Object -ExpandProperty ExecutablePath -Unique)
    $godotExeCandidates = @($running | Where-Object { $_ -match '(mono|\.net)' })
    if ($godotExeCandidates.Count -eq 0) {
        $godotExeCandidates = @($running)
    }
    if ($godotExeCandidates.Count -gt 0) {
        $GodotExe = $godotExeCandidates[0]
    } else {
        $onPath = @(Get-Command 'godot*' -CommandType Application -ErrorAction SilentlyContinue |
            Where-Object { $_.Source -match '(mono|\.net)' })
        if ($onPath.Count -gt 0) {
            $GodotExe = $onPath[0].Source
        }
    }
}
if (-not $GodotExe -or -not (Test-Path $GodotExe)) {
    throw "Cannot find a running Godot Mono executable. Keep the existing Godot open before running this script, or pass -GodotExe with the full path to Godot 4.7.2 Mono."
}

Write-Host "VS23 VERIFIED: $repoRoot" -ForegroundColor Green
Write-Host "Commit: $head"
Write-Host "Godot executable: $GodotExe"
Write-Host "Godot project: $projectDir"
Write-Host "Building VS23 Debug (used by Godot F5 and direct game launch)..."
& dotnet build (Join-Path $projectDir 'GlobalArena.Godot.csproj') -c Debug
if ($LASTEXITCODE -ne 0) { throw "Godot C# Debug build failed; game not opened." }

Write-Host "Build completed. Checked VS23 source and Git SHA." -ForegroundColor Green
Write-Host "Starting Godot from this EXACT folder: $projectDir"
Write-Host "Expected side panel: GLOBAL ARENA | VS23"
Write-Host "Expected runtime log: GA_VS23_RUNTIME_ACTIVE"
Write-Host "Do not press F5 in any other open Godot editor." -ForegroundColor Yellow
if ($Editor) {
    Write-Host "Explicit -Editor option: opening THIS worktree's editor."
    & $GodotExe --editor --path $projectDir
} else {
    Write-Host "Launching VS23 GAME DIRECTLY (no editor, no F5 required)." -ForegroundColor Green
    & $GodotExe --path $projectDir --verbose
}
if ($LASTEXITCODE -ne 0) {
    throw "Godot exited with code $LASTEXITCODE. See its log above."
}
