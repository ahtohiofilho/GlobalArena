# GA-VS19B: deterministic Godot Mono editor launch from THIS worktree.
# Run from PowerShell with:
#   powershell -ExecutionPolicy Bypass -File .\tools\launch-vs19b.ps1
# Optional: -GodotExe "C:\path\to\Godot_v4.7.2-stable_mono_win64.exe"
[CmdletBinding()]
param([string]$GodotExe)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectDir = Join-Path $repoRoot 'tools\GlobalArena.Godot'
$mainCode = Join-Path $projectDir 'Scripts\Main.cs'
$labCode = Join-Path $projectDir 'Scripts\Main.NeolithicWorkerLab.cs'
$expectedBranch = 'feature/ga-vs19b-born-civilization-20261030'

if (-not (Test-Path (Join-Path $projectDir 'project.godot'))) {
    throw "This directory does not contain the GA-VS19B Godot project: $projectDir"
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
    throw "This checkout is not the current VS19B branch head. Run git fetch origin then git switch --detach origin/$expectedBranch. Actual: $head Expected: $expectedHead"
}
$main = Get-Content -Path $mainCode -Raw -Encoding UTF8
$lab = Get-Content -Path $labCode -Raw -Encoding UTF8
if (-not $main.Contains('GLOBAL ARENA  |  VS19B') -or
    -not $main.Contains('GetWindow().Title = "Global Arena — VS19B') -or
    -not $lab.Contains('INSPECIONAR POVOAMENTO INICIAL') -or
    $lab.Contains('INICIAR TRABALHO NEOLÍTICO (LAB)')) {
    throw "The C# files do not match the approved VS19B UI. Do not start an older Godot project."
}

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

Write-Host "VS19B VERIFIED: $repoRoot" -ForegroundColor Green
Write-Host "Commit: $head"
Write-Host "Godot executable: $GodotExe"
Write-Host "Godot project: $projectDir"
Write-Host "Building current Godot project..."
& dotnet build (Join-Path $projectDir 'GlobalArena.Godot.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Godot C# build failed; editor not opened." }

Write-Host "Launching this exact VS19B project via --path." -ForegroundColor Green
Write-Host "Close other Godot editors/game windows before pressing F5."
Write-Host "Expected new game title: Global Arena — VS19B | Comunidade desde o início"
Start-Process -FilePath $GodotExe -ArgumentList @(
    '--editor', '--path', ('"' + $projectDir + '"')
) -WorkingDirectory $projectDir
