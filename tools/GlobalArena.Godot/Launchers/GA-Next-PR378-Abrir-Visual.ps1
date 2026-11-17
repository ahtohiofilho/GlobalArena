# GA-Next PR378 — automatic Windows launch (PowerShell 5.1+)
# Pinned to reviewed code blobs, preserves main and old PR worktrees.
[CmdletBinding()]
param([string]$RepoPath = (Join-Path $env:USERPROFILE 'source\repos\GlobalArena'))
$ErrorActionPreference = 'Stop'
$ExpectedMainBlob = '9dbcd8dab47c756cae92d74dfc919b7d9f52014e'
$ExpectedSmokeBlob = '5fe8d2e0643cb1025def1fb612b02b008bf05027'
$ExpectedTacticalBlob = 'e77130f9d93dceadc20a51e030d534f06c437a06'
$ExpectedFloatingBlob = 'f52954fea971aa4faedf0f8074189ebe3bcfa088'
$ExpectedThemeBlob = '1f2f441d132146641b445b46943e7f568478de59'
$ExpectedRosterBlob = 'd943fc5a884766ba74d9611244f5550d72390542'
$ExpectedV10SmokeBlob = '977f2d20ee5c248f8b5a3a1e7eab77538ce67262'
$ExpectedV10CollectiveBlob = 'dddcfdc6aa6518e27e0d3bbb6ffca0344b0ed628'
$ExpectedV10PlacementBlob = 'aa8b3acf4cc217b1301b6c7e5444998fea05720a'
$ExpectedV10MovementBlob = '4074921348764e34ea2f899aa7940490f2f25bd9'
$ExpectedBadgesBlob = 'e7140f431b14c0c7541b1981d6ce0eb2dcb2973d'
$ExpectedClickMenuBlob = '1f8b769a3f9c31bb3a2053ab70ab851984577d42'
$ExpectedCivilizationIdentityBlob = 'c1ae3daaedeab7e5fd0307be20a044950f85f7c0'
$ExpectedClickMenuSmokeBlob = '37ac64e00ced5cee17d00edbbfb0e50d7272e9f2'
$ExpectedInspectorBlob = 'd879a4df63734a65fd10df0fd9c3be3e445e1bfd'
$ExpectedConcentratedScenarioBlob = 'e03273a060d22666d42bc11523261cd82ec5bf2b'
$ExpectedGroupProjectionBlob = 'd6ccafef173a6e5886fcd773b0a89b689cebf11a'
$ExpectedConcentratedSmokeBlob = '1ac7820d83ba3b1324cec951d2f8e8015c69aff5'
$ExpectedV11WorkerBlob = '4fc697dfc090fdf2da6a07cbf0504d4a01c323c5'
$ExpectedV11WorldStateBlob = '9e673e548090795772265d1dca6b4b485866ba4d'
$ExpectedV11HasherBlob = 'f8c625140296b385e2ac1dbf1cc7510550a87eaf'
$PullRef = 'refs/pull/378/head'
$OfficialUrl = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
$OfficialZipSha = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
$ToolsDir = Join-Path $env:LOCALAPPDATA 'GA-Next\Tools\Godot-4.7.2-Mono'
$ZipPath = Join-Path $ToolsDir 'Godot_v4.7.2-stable_mono_win64.zip'
function Step([string]$message) { Write-Host ""; Write-Host "[GA-NEXT PR378] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[GA-NEXT PR378] $message" }
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
    Step 'Obtendo PR378 no GitHub sem tocar na branch principal.'
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
        $actualInspectorBlob -ne $ExpectedInspectorBlob -or
        $actualConcentratedScenarioBlob -ne $ExpectedConcentratedScenarioBlob -or
        $actualGroupProjectionBlob -ne $ExpectedGroupProjectionBlob -or
        $actualConcentratedSmokeBlob -ne $ExpectedConcentratedSmokeBlob -or
        $actualV11WorkerBlob -ne $ExpectedV11WorkerBlob -or
        $actualV11WorldStateBlob -ne $ExpectedV11WorldStateBlob -or
        $actualV11HasherBlob -ne $ExpectedV11HasherBlob) {
        Fail 'Codigo PR378 mudou desde o checkpoint. Nenhum arquivo substituido.'
    }
    $Worktree = Join-Path (Split-Path -Parent $RepoPath) 'GlobalArena-GANEXT-VISUAL-PR378'
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
    Step 'Compilando cliente Godot PR378.'
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
    Write-Host "[GA-NEXT PR378] Godot: $GodotExe" -ForegroundColor Green
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
    Remove-Item Env:\GA_NEXT_TILE_HOVER_HYPOTHESIS_SMOKE -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_CLICK_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\GA_NEXT_TILE_HOVER_SCREENSHOT_DIR -ErrorAction SilentlyContinue
    Step 'Abrindo Global Arena PR378.'
    $Game = Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"{0}"' -f $ProjectDir)) -WorkingDirectory $ProjectDir -PassThru
    Write-Host "[GA-NEXT PR378] JOGO ABERTO: PID $($Game.Id) / SHA $FetchSha" -ForegroundColor Green
    Write-Host '[GA-NEXT PR378] Clique na bolinha para selecionar; clique no hex adjacente + G=andar; P=produzir; X=cancelar.'
    Write-Host '[GA-NEXT PR378] M=mapa TAB=paineis ESC=fechar E=turno.'
    Write-Host '[GA-NEXT PR378] Marcadores coplanares VS16: passe o mouse para detalhes; clique sobre a bolinha para selecionar.'
    Write-Host '[GA-NEXT PR378] Ocupacoes produtivas: verde Agricultura, marrom Extracao Vegetal, cinza Mineracao.'
    Write-Host '[GA-NEXT PR378] Profissao e estado de deslocamento sao fatos separados do individuo.'
    Write-Host '[GA-NEXT PR378] Hover: cartao contextual compacto, clique seleciona, rolamento e arrasto preservados.'
    Write-Host '[GA-NEXT PR378] Inicio: 6 trabalhadores em um tile por civilizacao (3 atividades de 2).' 
    Write-Host '[GA-NEXT PR378] Clique repetido na mesma bolinha alterna individuos do grupo; G move apenas o selecionado.'
    Write-Host '[GA-NEXT PR378] Realocacao de um trabalhador selecionado: A=AGRICULTURA V=EXTRACAO VEGETAL N=MINERACAO.'
    Write-Host '[GA-NEXT PR378] X cancela a ordem antes do turno; E confirma no journal coletivo e atualiza as bolinhas.'
    Write-Host '[GA-NEXT PR378] HIPOTESE UX: passe o cursor no tile habitado para ver tres atividades.'
    Write-Host '[GA-NEXT PR378] Passe sobre a atividade para ver trabalhadores; clique no individuo para seleciona-lo.'
    Write-Host '[GA-NEXT PR378] FIXAR/SOLTAR mantem o menu; Esc fecha. A/V/N, G/P/X/E permanecem no motor v11.'
    Write-Host '[GA-NEXT PR378] Clique no hex habitado para abrir menu; clique numa atividade para ver individuos.'
    Write-Host '[GA-NEXT PR378] Clique no trabalhador para seleciona-lo; Fechar/Esc/click outro tile fecha. SEM HOVER.'
    Write-Host '[GA-NEXT PR378] Numero sempre de cabeca para cima na camera inclinada e nas rotacoes.'
    Write-Host '[GA-NEXT PR378] Identidade da civilizacao: aro externo RGB (vermelho/azul/verde); cor interna = atividade.'
    Write-Host '[GA-NEXT PR378] Servidor autoritativo/clientes: contrato documentado; sem rede prematura.'
    Write-Host '[GA-NEXT PR378] Repositorio principal preservado.'
} catch {
    Write-Host ""; Write-Host "[GA-NEXT PR378] ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
