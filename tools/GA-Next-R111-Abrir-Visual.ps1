# GA-Next R111 — isolated visual review; no hard-coded local repository path.
# Run from any directory inside the local GlobalArena Git checkout.
param(
    [ValidateSet('Gameplay', 'Geography')]
    [string]$Mode = 'Gameplay'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$branch = 'feature/ga-next-r111-multi-order-board-visual-review-20261120'
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
    Invoke-WebRequest -Uri $url -OutFile $archive
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
Write-Host '[GA-NEXT VISUAL] Fetching R111 into a separate worktree (your current branch stays untouched).'
& git -C $repoRoot fetch --no-tags origin "refs/heads/$branch"
Assert-LastCommand 'Fetch R111'
# Avoid rev-parse/PowerShell pipeline exit-code ambiguity on Windows.
# The remote tracking ref was updated by the successful fetch above.
# Git itself resolves this ref directly when creating/updating the worktree.
$visualRoot = Join-Path $env:LOCALAPPDATA 'GA-Next\visual-r111-inspection'
$remoteRef = "refs/remotes/origin/$branch"
if (Test-Path -LiteralPath $visualRoot) {
    if (-not (Test-Path -LiteralPath (Join-Path $visualRoot '.git'))) {
        throw 'The inspection location exists but is not a Git worktree. No files were removed.'
    }
    & git -C $visualRoot switch --detach $remoteRef
    Assert-LastCommand 'Update isolated R111 worktree'
} else {
    New-Item -ItemType Directory -Path (Split-Path $visualRoot -Parent) -Force | Out-Null
    & git -C $repoRoot worktree add --detach $visualRoot $remoteRef
    Assert-LastCommand 'Create isolated R111 worktree'
}
if (-not (Test-Path -LiteralPath (Join-Path $visualRoot $projectRelative))) {
    throw 'The isolated R111 worktree does not contain the Godot project.'
}
$projectDir = Join-Path $visualRoot $projectRelative
Write-Host "[GA-NEXT VISUAL] Building isolated R111 review worktree"
& dotnet build (Join-Path $projectDir 'GlobalArena.Godot.csproj') -c Debug
Assert-LastCommand 'Build Godot Mono client'
$godot = Locate-Godot
$oldPreview = [Environment]::GetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', 'Process')
$oldSmoke = [Environment]::GetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', 'Process')
try {
    # Disable CI-only automatic closures; leave the window open for human visual review.
    [Environment]::SetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', $null, 'Process')
    if ($Mode -eq 'Geography') {
        [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', '1', 'Process')
    } else {
        [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', $null, 'Process')
    }
    Write-Host "[GA-NEXT VISUAL] Opening $Mode — Godot 4.7.2 Mono; press M for planetary map, E for turn."
    & $godot --path $projectDir
    Assert-LastCommand 'Run Godot client'
} finally {
    [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', $oldPreview, 'Process')
    [Environment]::SetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', $oldSmoke, 'Process')
}
) {
    throw "Git returned an invalid R111 commit SHA: $target"
}
$visualRoot = Join-Path $env:LOCALAPPDATA ('GA-Next\visual-r111-' + $target.Substring(0, 12))
if (Test-Path -LiteralPath $visualRoot) {
    $existing = (& git -C $visualRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0 -or $existing -ne $target) {
        throw 'A different worktree already occupies the R111 review location; no files were removed.'
    }
} else {
    New-Item -ItemType Directory -Path (Split-Path $visualRoot -Parent) -Force | Out-Null
    & git -C $repoRoot worktree add --detach $visualRoot $target
    Assert-LastCommand 'Create isolated review worktree'
}
$projectDir = Join-Path $visualRoot $projectRelative
Write-Host "[GA-NEXT VISUAL] Building immutable review commit $target"
& dotnet build (Join-Path $projectDir 'GlobalArena.Godot.csproj') -c Debug
Assert-LastCommand 'Build Godot Mono client'
$godot = Locate-Godot
$oldPreview = [Environment]::GetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', 'Process')
$oldSmoke = [Environment]::GetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', 'Process')
try {
    # Disable CI-only automatic closures; leave the window open for human visual review.
    [Environment]::SetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', $null, 'Process')
    if ($Mode -eq 'Geography') {
        [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', '1', 'Process')
    } else {
        [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', $null, 'Process')
    }
    Write-Host "[GA-NEXT VISUAL] Opening $Mode — Godot 4.7.2 Mono; press M for planetary map, E for turn."
    & $godot --path $projectDir
    Assert-LastCommand 'Run Godot client'
} finally {
    [Environment]::SetEnvironmentVariable('GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW', $oldPreview, 'Process')
    [Environment]::SetEnvironmentVariable('GA_NEXT_WORKER_BOARD_SMOKE', $oldSmoke, 'Process')
}
