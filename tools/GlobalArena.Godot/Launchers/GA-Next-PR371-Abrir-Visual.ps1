# GA-Next PR371 — automatic Windows launch (PowerShell 5.1+)
# Pinned to reviewed code blobs, preserves main and old PR worktrees.
[CmdletBinding()]
param([string]$RepoPath = (Join-Path $env:USERPROFILE 'source\repos\GlobalArena'))
$ErrorActionPreference = 'Stop'
$ExpectedMainBlob = '0c1a8f373a1a2526918c6038e2adc0eb4dd1c875'
$ExpectedSmokeBlob = '5fe8d2e0643cb1025def1fb612b02b008bf05027'
$ExpectedTacticalBlob = '814d978edd4eb58f546e3d24041c4c1d7588155f'
$ExpectedFloatingBlob = 'f52954fea971aa4faedf0f8074189ebe3bcfa088'
$ExpectedThemeBlob = '1f2f441d132146641b445b46943e7f568478de59'
$ExpectedRosterBlob = '9f75c08e78770fdb4408aba1a12d6ec80a9294df'
$ExpectedV10SmokeBlob = '4e1e65ae2bbe17cdf685e03028a4e3167028d99b'
$ExpectedV10CollectiveBlob = '5a0c8616bf23936f197fe5bd6a4a96df00d1245d'
$ExpectedV10PlacementBlob = '2a437d8dcb9ff7f170be50bba5b627e7e7e55435'
$ExpectedV10MovementBlob = 'e4193505695b01a6aac85749a5734f54ecd008ac'
$ExpectedBadgesBlob = '99fe00475c773a126dfdbe1087f1613eb40546d1'
$PullRef = 'refs/pull/371/head'
$OfficialUrl = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
$OfficialZipSha = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
$ToolsDir = Join-Path $env:LOCALAPPDATA 'GA-Next\Tools\Godot-4.7.2-Mono'
$ZipPath = Join-Path $ToolsDir 'Godot_v4.7.2-stable_mono_win64.zip'
function Step([string]$message) { Write-Host ""; Write-Host "[GA-NEXT PR371] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[GA-NEXT PR371] $message" }
function Is-GodotMono([string]$path) {
    $name = [System.IO.Path]::GetFileName($path)
    return ($name -match '(?i)^godot.*4[.]7[.]2.*mono.*win64[.]exe$' -and
        $name -notmatch '(?i)console[.]exe$')
}
function Find-Godot([string[]]$roots) {
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        $candidates = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.exe' -ErrorAction SilentlyContinue |
            Where-Object { Is-GodotMono $_.FullName } | Sort-Object FullName)
        foreach ($candidate in $candidates) {
            if (Test-Path -LiteralPath (Join-Path $candidate.DirectoryName 'GodotSharp')) {
                return [string]$candidate.FullName
            }
        }
        if ($candidates.Count -gt 0) { return [string]$candidates[0].FullName }
    }
    return $null
}
try {
    Step 'Verificando Git, .NET e repositorio de origem.'
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'Git nao encontrado.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail '.NET nao encontrado.' }
    if (-not (Test-Path -LiteralPath $RepoPath -PathType Container)) { Fail "Repositorio nao encontrado: $RepoPath" }
    $RepoPath = [System.IO.Path]::GetFullPath($RepoPath)
    Step 'Obtendo PR371 no GitHub sem tocar na branch principal.'
    & git -C $RepoPath fetch --no-tags origin $PullRef
    if ($LASTEXITCODE -ne 0) { Fail 'Git fetch falhou.' }
    $FetchSha = (& git -C $RepoPath rev-parse FETCH_HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $FetchSha -notmatch '^[0-9a-f]{40}$') { Fail 'HEAD invalido.' }
    $actualMainBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/Main.cs" -f $FetchSha)).Trim()
    $actualSmokeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/FoundationGodotWorkspaceSmoke.cs" -f $FetchSha)).Trim()
    $actualTacticalBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/TacticalContextPreview.cs" -f $FetchSha)).Trim()
    $actualFloatingBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaFloatingWindow.cs" -f $FetchSha)).Trim()
    $actualThemeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaVisualFoundation.cs" -f $FetchSha)).Trim()
    $actualRosterBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaOperationalRoster.cs" -f $FetchSha)).Trim()
    $actualV10SmokeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/FoundationGodotTacticalV10Smoke.cs" -f $FetchSha)).Trim()
    $actualV10CollectiveBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/Foundation/FoundationTacticalHumanAiTurnHost.cs" -f $FetchSha)).Trim()
    $actualV10PlacementBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/Foundation/FoundationTacticalInitialDeployment.cs" -f $FetchSha)).Trim()
    $actualV10MovementBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/Foundation/PhysicalTacticalWorkerMovementTurnPhase.cs" -f $FetchSha)).Trim()
    $actualBadgesBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/TacticalContextPreview.WorkerBadges.cs" -f $FetchSha)).Trim()
    if ($actualMainBlob -ne $ExpectedMainBlob -or $actualSmokeBlob -ne $ExpectedSmokeBlob -or
        $actualTacticalBlob -ne $ExpectedTacticalBlob -or
        $actualFloatingBlob -ne $ExpectedFloatingBlob -or $actualThemeBlob -ne $ExpectedThemeBlob -or
        $actualRosterBlob -ne $ExpectedRosterBlob -or
        $actualV10SmokeBlob -ne $ExpectedV10SmokeBlob -or
        $actualV10CollectiveBlob -ne $ExpectedV10CollectiveBlob -or
        $actualV10PlacementBlob -ne $ExpectedV10PlacementBlob -or
        $actualV10MovementBlob -ne $ExpectedV10MovementBlob -or
        $actualBadgesBlob -ne $ExpectedBadgesBlob) {
        Fail 'Codigo PR371 mudou desde o checkpoint. Nenhum arquivo substituido.'
    }
    $Worktree = Join-Path (Split-Path -Parent $RepoPath) 'GlobalArena-GANEXT-VISUAL-PR371'
    if (-not (Test-Path -LiteralPath $Worktree -PathType Container)) {
        Step 'Criando worktree isolada.'
        & git -C $RepoPath worktree add --detach $Worktree $FetchSha
        if ($LASTEXITCODE -ne 0) { Fail 'Worktree falhou.' }
    }
    $currentSha = (& git -C $Worktree rev-parse HEAD).Trim()
    if ($currentSha -ne $FetchSha) { Fail "Worktree divergente: $currentSha. Sem reset." }
    $ProjectDir = Join-Path $Worktree 'tools\GlobalArena.Godot'
    $CsProj = Join-Path $ProjectDir 'GlobalArena.Godot.csproj'
    if (-not (Test-Path -LiteralPath $CsProj -PathType Leaf)) { Fail 'Projeto Godot nao localizado.' }
    Step 'Compilando cliente Godot PR371.'
    & dotnet build $CsProj -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { Fail 'Build falhou.' }
    Step 'Localizando Godot 4.7.2 Mono automaticamente.'
    $searchRoots = @($ToolsDir, (Join-Path $env:USERPROFILE 'Downloads'),
        (Join-Path $env:USERPROFILE 'Desktop'), (Join-Path $env:USERPROFILE 'source'))
    $GodotExe = Find-Godot $searchRoots
    if ([string]::IsNullOrWhiteSpace($GodotExe)) {
        Step 'Godot ausente; recuperando pacote oficial SHA256.'
        New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
        if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
            Invoke-WebRequest -Uri $OfficialUrl -OutFile $ZipPath -UseBasicParsing
        }
        $gotSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $ZipPath).Hash.ToLowerInvariant()
        if ($gotSha -ne $OfficialZipSha) { Fail "SHA256 Godot invalido: $gotSha" }
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $ToolsDir -Force
        $GodotExe = Find-Godot @($ToolsDir)
    }
    if ([string]::IsNullOrWhiteSpace($GodotExe)) { Fail 'Executavel Godot Mono nao localizado.' }
    Write-Host "[GA-NEXT PR371] Godot: $GodotExe" -ForegroundColor Green
    Step 'Importando recursos automaticamente.'
    & $GodotExe --headless --path $ProjectDir --editor --import --quit
    if ($LASTEXITCODE -ne 0) { Fail "Importacao Godot falhou: $LASTEXITCODE" }
    Remove-Item Env:\GA_FOUNDATION_GODOT_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKSPACE_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKSPACE_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V10_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V10_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Step 'Abrindo Global Arena PR371.'
    $Game = Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"{0}"' -f $ProjectDir)) -WorkingDirectory $ProjectDir -PassThru
    Write-Host "[GA-NEXT PR371] JOGO ABERTO: PID $($Game.Id) / SHA $FetchSha" -ForegroundColor Green
    Write-Host '[GA-NEXT PR371] Clique na bolinha para selecionar; clique no hex adjacente + G=andar; P=produzir; X=cancelar.'
    Write-Host '[GA-NEXT PR371] M=mapa TAB=paineis ESC=fechar E=turno.'
    Write-Host '[GA-NEXT PR371] Marcadores coplanares VS16: passe o mouse para detalhes; clique sobre a bolinha para selecionar.'
    Write-Host '[GA-NEXT PR371] Repositorio principal preservado.'
} catch {
    Write-Host ""; Write-Host "[GA-NEXT PR371] ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
