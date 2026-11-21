# GA-Next R133 - experimental fine-first coastal A/B visual review, not main.
# EconomyLab and Geography are separate modes by the Godot QA contract.
# Run from any directory inside the local GlobalArena Git checkout.
param(
    [ValidateSet('EconomyLab', 'EconomyShallow', 'Geography', 'Workers')]
    [string]$Mode = 'EconomyLab'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$branch = 'feature/ga-next-r133-centered-tactical-site-markers-20261121'
$projectRelative = 'tools/GlobalArena.Godot'
function Assert-LastCommand([string]$Description) {
    if ($LASTEXITCODE -ne 0) { throw "$Description failed (exit $LASTEXITCODE)." }
}
function Locate-Godot() {
    if (-not [string]::IsNullOrWhiteSpace($env:GA_NEXT_GODOT_EXE) -and
        (Test-Path -LiteralPath $env:GA_NEXT_GODOT_EXE -PathType Leaf)) {
        return $env:GA_NEXT_GODOT_EXE
    }
    foreach ($name in @('godot', 'godot-mono', 'Godot_v4.7.2-stable_mono_win64.exe')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command -and $command.Source) { return $command.Source }
    }
    foreach ($folder in @(
        (Join-Path $env:USERPROFILE 'Downloads'),
        (Join-Path $env:USERPROFILE 'Desktop'),
        (Join-Path $env:LOCALAPPDATA 'GA-Next')
    )) {
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) { continue }
        $found = Get-ChildItem -LiteralPath $folder -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^Godot_v4\.7\.2-stable_mono_win64\.exe$' } |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    # Controlled fallback to the official version used by the CI gates.
    $cache = Join-Path $env:LOCALAPPDATA 'GA-Next\godot-4.7.2-mono'
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $archive = Join-Path $cache 'runtime.zip'
    $url = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
    Write-Host '[GA-NEXT VISUAL] Obtaining official Godot Mono 4.7.2...'
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    Expand-Archive -LiteralPath $archive -DestinationPath $cache -Force
    $found = Get-ChildItem -LiteralPath $cache -Recurse -File |
        Where-Object { $_.Name -eq 'Godot_v4.7.2-stable_mono_win64.exe' } |
        Select-Object -First 1
    if (-not $found) { throw 'Official Godot download does not contain expected Mono runtime.' }
    return $found.FullName
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is not installed or available in PATH.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK is required.' }
$repoRoot = (& git rev-parse --show-toplevel 2>$null)
Assert-LastCommand 'Find Git checkout'
if (-not $repoRoot) { throw 'Run this script from inside your GlobalArena Git checkout.' }
$repoRoot = ($repoRoot | Select-Object -First 1).Trim()
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $projectRelative))) {
    throw 'Current Git checkout does not contain the GlobalArena Godot project.'
}
Write-Host '[GA-NEXT VISUAL] Fetching R133 into a separate worktree (your current branch stays untouched).'
$origin = (& git -C $repoRoot remote get-url origin 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $origin -notmatch '^(https://github[.]com/|git@github[.]com:)ahtohiofilho/GlobalArena([.]git)?$') {
    throw "Unexpected repository origin: $origin"
}
& git -C $repoRoot fetch --no-tags origin ("refs/heads/{0}:refs/remotes/origin/{0}" -f $branch)
Assert-LastCommand 'Fetch R133'
# Avoid rev-parse/PowerShell pipeline exit-code ambiguity on Windows.
# The remote tracking ref was updated by the successful fetch above.
# Git itself resolves this ref directly when creating/updating the worktree.
$visualRoot = Join-Path $env:LOCALAPPDATA ('GA-Next\visual-r133-' + $Mode.ToLowerInvariant())
$remoteRef = "refs/remotes/origin/$branch"
if (Test-Path -LiteralPath $visualRoot) {
    if (-not (Test-Path -LiteralPath (Join-Path $visualRoot '.git'))) {
        throw 'The inspection location exists but is not a Git worktree. No files were removed.'
    }
    & git -C $visualRoot switch --detach $remoteRef
    Assert-LastCommand 'Update isolated R133 worktree'
} else {
    New-Item -ItemType Directory -Path (Split-Path $visualRoot -Parent) -Force | Out-Null
    & git -C $repoRoot worktree add --detach $visualRoot $remoteRef
    Assert-LastCommand 'Create isolated R133 worktree'
}
if (-not (Test-Path -LiteralPath (Join-Path $visualRoot $projectRelative))) {
    throw 'The isolated R133 worktree does not contain the Godot project.'
}
$projectDir = Join-Path $visualRoot $projectRelative
Write-Host "[GA-NEXT VISUAL] Building isolated R133 review worktree"
& dotnet build (Join-Path $projectDir 'GlobalArena.Godot.csproj') -c Debug
Assert-LastCommand 'Build Godot Mono client'
$godot = Locate-Godot
Write-Host "[GA-NEXT VISUAL] Godot executable: $godot"
# PowerShell can receive no version text from native executables even if they run.
# Do not reject an executable on an empty version probe; import/run are decisive.
try {
    $versionLines = @(& $godot --headless --version 2>&1)
    $reportedVersion = ($versionLines | ForEach-Object { [string]$_ }) -join ' '
    if ($reportedVersion -match '4\\.7\\.2') {
        Write-Host "[GA-NEXT VISUAL] Version: $reportedVersion"
    } elseif ([string]::IsNullOrWhiteSpace($reportedVersion)) {
        Write-Warning 'Godot provided no version text; attempting real project import/run.'
    } else {
        Write-Warning "Godot reports $reportedVersion; attempting compatibility check."
    }
} catch {
    Write-Warning 'Godot version probe failed; proceeding with real project launch.'
}
$allFlags = @(
    'GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW',
    'GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW',
    'GA_NEXT_QA_SHALLOW_PORTS',
    'GA_NEXT_QA_LOCAL_SITE_MARKERS',
    'GA_NEXT_WORKER_BOARD_SMOKE',
    'GA_NEXT_SETTLED_TRADE_VISUAL_SMOKE',
    'GA_NEXT_FINE_FIRST_VISUAL_SMOKE',
    'GA_FOUNDATION_GODOT_SMOKE',
    'GA_NEXT_WORKSPACE_SMOKE',
    'GA_NEXT_TACTICAL_V10_SMOKE',
    'GA_NEXT_TACTICAL_V11_SMOKE',
    'GA_NEXT_CONCENTRATED_WORKFORCE_SMOKE',
    'GA_NEXT_TILE_CLICK_MENU_SMOKE',
    'GA_NEXT_CONTINUOUS_NAVIGATION_SMOKE',
    'GA_NEXT_GOLDBERG_RESOLUTION_SMOKE'
)
$originalFlags = @{}
foreach ($flag in $allFlags) {
    $originalFlags[$flag] = [Environment]::GetEnvironmentVariable($flag, 'Process')
}
try {
    # Never run a CI smoke in an interactive inspection session.
    foreach ($flag in $allFlags) {
        [Environment]::SetEnvironmentVariable($flag, $null, 'Process')
    }
    if ($Mode -eq 'Geography') {
        [Environment]::SetEnvironmentVariable(
            'GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', '1', 'Process')
    } elseif ($Mode -eq 'EconomyLab' -or $Mode -eq 'EconomyShallow') {
        # Explicit manual combined mode: signed fine-first G18 field drives
        # BOTH game freight and tactical coloring, with QA treasury UI.
        [Environment]::SetEnvironmentVariable(
            'GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', '1', 'Process')
        [Environment]::SetEnvironmentVariable(
            'GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW', '1', 'Process')
        [Environment]::SetEnvironmentVariable(
            'GA_NEXT_QA_LOCAL_SITE_MARKERS', '1', 'Process')
        if ($Mode -eq 'EconomyShallow') {
            [Environment]::SetEnvironmentVariable(
                'GA_NEXT_QA_SHALLOW_PORTS', '1', 'Process')
        }
    }
    Write-Host "[GA-NEXT VISUAL] Opening $Mode - Godot 4.7.2 Mono." 
    Write-Host '[GA-NEXT VISUAL] M = map; TAB = operations; E = close next turn.'
    if ($Mode -eq 'EconomyLab' -or $Mode -eq 'EconomyShallow') {
        Write-Warning 'Experimental QA economy only: prices and balances are NOT final calibration.'
        Write-Host '[GA-NEXT VISUAL] E settles real sales; route lines represent last closed cycle.'
        Write-Host '[GA-NEXT VISUAL] Baseline = EconomyLab; candidate = EconomyShallow. Use same number of turns.'
        Write-Host '[GA-NEXT VISUAL] TAB -> PORT INVESTMENT QA; capital, construction and freight use one abstract money unit.'
        Write-Host '[GA-NEXT VISUAL] USED means last-cycle port trade. AVAILABLE has no demonstrated route effect.'
        Write-Host '[GA-NEXT VISUAL] Semantic zoom: distant view suppresses sites, nearer view progressively shows labels.'
        Write-Host '[GA-NEXT VISUAL] No individual worker circles on cartographic map; use administration for workers.'
        Write-Host '[GA-NEXT VISUAL] Local Center / Trade: current unit freight is not the historical site-paid amount.'
        Write-Host '[GA-NEXT VISUAL] All tactical sites stay visible at any zoom; dots sit at polygon centers.'
        Write-Host '[GA-NEXT VISUAL] Click the dot or its Site name to open Local Center.'
    }
    # Leave Godot running for manual screenshots; no scripted gameplay or auto-quit.
    $game = Start-Process -FilePath $godot -ArgumentList @(
        '--path', ('"{0}"' -f $projectDir)
    ) -WorkingDirectory $projectDir -PassThru
    if (-not $game -or $game.Id -le 0) {
        throw 'Godot failed to start.'
    }
    Write-Host "[GA-NEXT VISUAL] Godot started: PID $($game.Id); isolated branch $branch"
} finally {
    foreach ($flag in $allFlags) {
        [Environment]::SetEnvironmentVariable(
            $flag, $originalFlags[$flag], 'Process')
    }
}
