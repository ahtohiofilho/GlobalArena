# GA-Next PR372 — automatic Windows launch (PowerShell 5.1+)
# Pinned to reviewed code blobs, preserves main and old PR worktrees.
[CmdletBinding()]
param([string]$RepoPath = (Join-Path $env:USERPROFILE 'source\repos\GlobalArena'))
$ErrorActionPreference = 'Stop'
$ExpectedMainBlob = 'd4c9132fecaf0c76d1889307cf12978ef627e082'
$ExpectedSmokeBlob = '5fe8d2e0643cb1025def1fb612b02b008bf05027'
$ExpectedTacticalBlob = '814d978edd4eb58f546e3d24041c4c1d7588155f'
$ExpectedFloatingBlob = 'f52954fea971aa4faedf0f8074189ebe3bcfa088'
$ExpectedThemeBlob = '1f2f441d132146641b445b46943e7f568478de59'
$ExpectedRosterBlob = 'd943fc5a884766ba74d9611244f5550d72390542'
$ExpectedV10SmokeBlob = 'aa634caf27e7b142b9567ce8d927a360f6c9d07c'
$ExpectedV10CollectiveBlob = '26a023957589a0fefd489c2929abc94b8304828c'
$ExpectedV10PlacementBlob = 'aa8b3acf4cc217b1301b6c7e5444998fea05720a'
$ExpectedV10MovementBlob = 'a28c32a42365c83c03e474e31affacd7de9049b2'
$ExpectedBadgesBlob = '7a25a3122064a548298a6da519230039204dcaa8'
$ExpectedV11WorkerBlob = '23ccae7932d8bc39bb347df7f0c846c5c2e6db79'
$ExpectedV11WorldStateBlob = '9e673e548090795772265d1dca6b4b485866ba4d'
$ExpectedV11HasherBlob = 'f8c625140296b385e2ac1dbf1cc7510550a87eaf'
$PullRef = 'refs/pull/372/head'
$OfficialUrl = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
$OfficialZipSha = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
$ToolsDir = Join-Path $env:LOCALAPPDATA 'GA-Next\Tools\Godot-4.7.2-Mono'
$ZipPath = Join-Path $ToolsDir 'Godot_v4.7.2-stable_mono_win64.zip'
function Step([string]$message) { Write-Host ""; Write-Host "[GA-NEXT PR372] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[GA-NEXT PR372] $message" }
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
    Step 'Obtendo PR372 no GitHub sem tocar na branch principal.'
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
    $actualV11WorkerBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Runtime/PhysicalWorkerRuntimeState.cs" -f $FetchSha)).Trim()
    $actualV11WorldStateBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Runtime/WorldState.cs" -f $FetchSha)).Trim()
    $actualV11HasherBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/CanonicalWorldStateHasher.cs" -f $FetchSha)).Trim()
    if ($actualMainBlob -ne $ExpectedMainBlob -or $actualSmokeBlob -ne $ExpectedSmokeBlob -or
        $actualTacticalBlob -ne $ExpectedTacticalBlob -or
        $actualFloatingBlob -ne $ExpectedFloatingBlob -or $actualThemeBlob -ne $ExpectedThemeBlob -or
        $actualRosterBlob -ne $ExpectedRosterBlob -or
        $actualV10SmokeBlob -ne $ExpectedV10SmokeBlob -or
        $actualV10CollectiveBlob -ne $ExpectedV10CollectiveBlob -or
        $actualV10PlacementBlob -ne $ExpectedV10PlacementBlob -or
        $actualV10MovementBlob -ne $ExpectedV10MovementBlob -or
        $actualBadgesBlob -ne $ExpectedBadgesBlob -or
        $actualV11WorkerBlob -ne $ExpectedV11WorkerBlob -or
        $actualV11WorldStateBlob -ne $ExpectedV11WorldStateBlob -or
        $actualV11HasherBlob -ne $ExpectedV11HasherBlob) {
        Fail 'Codigo PR372 mudou desde o checkpoint. Nenhum arquivo substituido.'
    }
    $Worktree = Join-Path (Split-Path -Parent $RepoPath) 'GlobalArena-GANEXT-VISUAL-PR372'
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
    Step 'Compilando cliente Godot PR372.'
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
    Write-Host "[GA-NEXT PR372] Godot: $GodotExe" -ForegroundColor Green
    Step 'Importando recursos automaticamente.'
    & $GodotExe --headless --path $ProjectDir --editor --import --quit
    if ($LASTEXITCODE -ne 0) { Fail "Importacao Godot falhou: $LASTEXITCODE" }
    Remove-Item Env:\GA_FOUNDATION_GODOT_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKSPACE_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKSPACE_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V10_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V10_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V11_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TACTICAL_V11_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Step 'Abrindo Global Arena PR372.'
    $Game = Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"{0}"' -f $ProjectDir)) -WorkingDirectory $ProjectDir -PassThru
    Write-Host "[GA-NEXT PR372] JOGO ABERTO: PID $($Game.Id) / SHA $FetchSha" -ForegroundColor Green
    Write-Host '[GA-NEXT PR372] Clique na bolinha para selecionar; clique no hex adjacente + G=andar; P=produzir; X=cancelar.'
    Write-Host '[GA-NEXT PR372] M=mapa TAB=paineis ESC=fechar E=turno.'
    Write-Host '[GA-NEXT PR372] Marcadores coplanares VS16: passe o mouse para detalhes; clique sobre a bolinha para selecionar.'
    Write-Host '[GA-NEXT PR372] Ocupacoes produtivas: verde Agricultura, marrom Extracao Vegetal, cinza Mineracao.'
    Write-Host '[GA-NEXT PR372] Profissao e estado de deslocamento sao fatos separados do individuo.'
    Write-Host '[GA-NEXT PR372] Repositorio principal preservado.'
} catch {
    Write-Host ""; Write-Host "[GA-NEXT PR372] ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
