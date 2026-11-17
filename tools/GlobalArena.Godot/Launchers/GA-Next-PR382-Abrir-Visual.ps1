# GA-Next PR382 — automatic Windows launch (PowerShell 5.1+)
# Pinned to reviewed code blobs, preserves main and old PR worktrees.
[CmdletBinding()]
param([string]$RepoPath = (Join-Path $env:USERPROFILE 'source\repos\GlobalArena'))
$ErrorActionPreference = 'Stop'
$ExpectedMainBlob = '1626704d9704a96e67b38cc82e7fc26a88d18430'
$ExpectedSmokeBlob = '8af7721124feb5be6c25c958da14b4362bfa60d6'
$ExpectedTacticalBlob = 'e77130f9d93dceadc20a51e030d534f06c437a06'
$ExpectedFloatingBlob = 'f52954fea971aa4faedf0f8074189ebe3bcfa088'
$ExpectedThemeBlob = '1f2f441d132146641b445b46943e7f568478de59'
$ExpectedRosterBlob = 'd943fc5a884766ba74d9611244f5550d72390542'
$ExpectedV10SmokeBlob = '977f2d20ee5c248f8b5a3a1e7eab77538ce67262'
$ExpectedV10CollectiveBlob = 'f685415546872068c42924ba07f8ac0038e78568'
$ExpectedV10PlacementBlob = 'aa8b3acf4cc217b1301b6c7e5444998fea05720a'
$ExpectedV10MovementBlob = '4074921348764e34ea2f899aa7940490f2f25bd9'
$ExpectedBadgesBlob = 'e7140f431b14c0c7541b1981d6ce0eb2dcb2973d'
$ExpectedClickMenuBlob = '34d71b718f22c3584fbca9ec965e89ac97075b29'
$ExpectedCivilizationIdentityBlob = 'c1ae3daaedeab7e5fd0307be20a044950f85f7c0'
$ExpectedClickMenuSmokeBlob = '37ac64e00ced5cee17d00edbbfb0e50d7272e9f2'
$ExpectedWorkerBoardBlob = '55887452204046af46e31a972016365b557fa4e3'
$ExpectedWorkerDragChipBlob = '368a415b2d843c7397fd4c1818dbefb8d55dfa6b'
$ExpectedWorkerDropZoneBlob = 'ff81e5de17fe88770f1f29d76cdc04b44f9f21b9'
$ExpectedWorkerBoardSmokeBlob = 'f73c43f82262ad5af0267715dd42a88743fad5b9'
$ExpectedInspectorBlob = 'd879a4df63734a65fd10df0fd9c3be3e445e1bfd'
$ExpectedConcentratedScenarioBlob = 'e03273a060d22666d42bc11523261cd82ec5bf2b'
$ExpectedGroupProjectionBlob = 'd6ccafef173a6e5886fcd773b0a89b689cebf11a'
$ExpectedConcentratedSmokeBlob = '1ac7820d83ba3b1324cec951d2f8e8015c69aff5'
$ExpectedV11WorkerBlob = '4fc697dfc090fdf2da6a07cbf0504d4a01c323c5'
$ExpectedV11WorldStateBlob = '9e673e548090795772265d1dca6b4b485866ba4d'
$ExpectedV11HasherBlob = 'f8c625140296b385e2ac1dbf1cc7510550a87eaf'
$PullRef = 'refs/pull/382/head'
$OfficialUrl = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
$OfficialZipSha = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
$ToolsDir = Join-Path $env:LOCALAPPDATA 'GA-Next\Tools\Godot-4.7.2-Mono'
$ZipPath = Join-Path $ToolsDir 'Godot_v4.7.2-stable_mono_win64.zip'
function Step([string]$message) { Write-Host ""; Write-Host "[GA-NEXT PR382] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[GA-NEXT PR382] $message" }
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
    Step 'Obtendo PR382 no GitHub sem tocar na branch principal.'
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
    $actualClickMenuBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/TacticalContextPreview.TileClickMenu.cs" -f $FetchSha)).Trim()
    $actualCivilizationIdentityBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaCivilizationVisualIdentity.cs" -f $FetchSha)).Trim()
    $actualClickMenuSmokeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/FoundationGodotTileClickMenuSmoke.cs" -f $FetchSha)).Trim()
    $actualWorkerBoardBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/TacticalContextPreview.WorkerBoard.cs" -f $FetchSha)).Trim()
    $actualWorkerDragChipBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaWorkerBoardDragChip.cs" -f $FetchSha)).Trim()
    $actualWorkerDropZoneBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/GaWorkerBoardDropZone.cs" -f $FetchSha)).Trim()
    $actualWorkerBoardSmokeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/FoundationGodotWorkerBoardSmoke.cs" -f $FetchSha)).Trim()
    $actualInspectorBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/TacticalContextPreview.WorkerInspection.cs" -f $FetchSha)).Trim()
    $actualConcentratedScenarioBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/Foundation/FoundationConcentratedWorkforceScenario.cs" -f $FetchSha)).Trim()
    $actualGroupProjectionBlob = (& git -C $RepoPath rev-parse ("{0}:GlobalArena.Simulation/Foundation/FoundationWorkforceActivityGroups.cs" -f $FetchSha)).Trim()
    $actualConcentratedSmokeBlob = (& git -C $RepoPath rev-parse ("{0}:tools/GlobalArena.Godot/Scripts/FoundationGodotConcentratedWorkforceSmoke.cs" -f $FetchSha)).Trim()
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
        $actualClickMenuBlob -ne $ExpectedClickMenuBlob -or
        $actualCivilizationIdentityBlob -ne $ExpectedCivilizationIdentityBlob -or
        $actualClickMenuSmokeBlob -ne $ExpectedClickMenuSmokeBlob -or
        $actualWorkerBoardBlob -ne $ExpectedWorkerBoardBlob -or
        $actualWorkerDragChipBlob -ne $ExpectedWorkerDragChipBlob -or
        $actualWorkerDropZoneBlob -ne $ExpectedWorkerDropZoneBlob -or
        $actualWorkerBoardSmokeBlob -ne $ExpectedWorkerBoardSmokeBlob -or
        $actualInspectorBlob -ne $ExpectedInspectorBlob -or
        $actualConcentratedScenarioBlob -ne $ExpectedConcentratedScenarioBlob -or
        $actualGroupProjectionBlob -ne $ExpectedGroupProjectionBlob -or
        $actualConcentratedSmokeBlob -ne $ExpectedConcentratedSmokeBlob -or
        $actualV11WorkerBlob -ne $ExpectedV11WorkerBlob -or
        $actualV11WorldStateBlob -ne $ExpectedV11WorldStateBlob -or
        $actualV11HasherBlob -ne $ExpectedV11HasherBlob) {
        Fail 'Codigo PR382 mudou desde o checkpoint. Nenhum arquivo substituido.'
    }
    $Worktree = Join-Path (Split-Path -Parent $RepoPath) 'GlobalArena-GANEXT-VISUAL-PR382'
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
    Step 'Compilando cliente Godot PR382.'
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
    Write-Host "[GA-NEXT PR382] Godot: $GodotExe" -ForegroundColor Green
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
    Remove-Item Env:\GA_NEXT_CONCENTRATED_WORKFORCE_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_CONCENTRATED_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_CLICK_MENU_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKER_BOARD_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_WORKER_BOARD_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_HOVER_HYPOTHESIS_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_CLICK_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_HOVER_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Step 'Abrindo Global Arena PR382.'
    $Game = Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"{0}"' -f $ProjectDir)) -WorkingDirectory $ProjectDir -PassThru
    Write-Host "[GA-NEXT PR382] JOGO ABERTO: PID $($Game.Id) / SHA $FetchSha" -ForegroundColor Green
    Write-Host '[GA-NEXT PR382] TILE habitado: clique para abrir o menu. WORKERS / ORGANIZE: fichas e slots.'
    Write-Host '[GA-NEXT PR382] M=mapa, TAB=paineis, ESC=fechar, E=turno coletivo; G/P/X e A/V/N continuam.'
    Write-Host '[GA-NEXT PR382] PAINEL LATERAL: clique TAB, role para baixo com mouse/scroll ate as ultimas ordens.'
    Write-Host '[GA-NEXT PR382] Cabecalho e metricas permanecem visiveis; mapa recupera largura ao fechar TAB.'
    Write-Host '[GA-NEXT PR382] Pedidos de trabalho sao PENDENTES ate E; X cancela antes do commit.'
    Write-Host '[GA-NEXT PR382] Validar conteudo inferior em janela de diferentes alturas. Sem hover do menu.'
    Write-Host '[GA-NEXT PR382] Este PR nao altera movimentos, economia ou host autoritativo.'
    Write-Host '[GA-NEXT PR382] Repositorio principal preservado.'
} catch {
    Write-Host ""; Write-Host "[GA-NEXT PR382] ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
