# GA-Next PR434 — Open the REAL Godot game locally without changing main.
# Windows PowerShell 5.1+, .NET 10 SDK and Git. Never merges or resets.
# EconomyLab uses an EXPLICIT QA balance pack, not approved game economics.
[CmdletBinding()]
param(
    [ValidateSet('EconomyLab', 'Workers')]
    [string]$Mode = 'EconomyLab',
    [string]$RepoPath = (Join-Path $env:USERPROFILE 'source\repos\GlobalArena')
)
$ErrorActionPreference = 'Stop'
$ExpectedAncestor = '65c868a2abcd8f74bbdeabda204dae16555e850a'
$PullRef = 'refs/pull/434/head'
$ExpectedCodeBlobs = @{
    'tools/GlobalArena.Godot/Scripts/Main.cs' = '8b3f35ce65dd64bad3e9a73d4565fa10db971186'
    'tools/GlobalArena.Godot/Scripts/Main.InteractiveEconomyPreview.cs' = '530e52ebef582e8d95be5a38ac6653cde26fa690'
    'tools/GlobalArena.Godot/Scripts/GaExplicitEconomicQaConfiguration.cs' = 'd0eff1907fbe69814b1c60a67a0b1217a962152b'
    'GlobalArena.Simulation/Foundation/FoundationAuthoritativeEconomicSalesSettlementPhase.cs' = '0feda919196aab74b9a58746a6235136d869f3d0'
    'GlobalArena.Simulation/Foundation/FoundationGameplayEconomicBootstrap.cs' = '2e6dd2ff661344135d32c6d4f30306efea4b157b'
    'tools/GlobalArena.Godot/Scripts/TacticalContextPreview.SettledTradeRoutes.cs' = '86ecb9b8ef52262ff59d45d66e735badb3b1b8b5'
    'tools/GlobalArena.Godot/Scripts/TacticalContextPreview.cs' = '0c01e9fcb45f04a405721bdf3884cde859a74968'
}
$OfficialGodotUrl = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip'
$OfficialGodotSha = 'a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0'
$ToolsDir = Join-Path $env:LOCALAPPDATA 'GA-Next\Tools\Godot-4.7.2-Mono'
$ZipPath = Join-Path $ToolsDir 'Godot_v4.7.2-stable_mono_win64.zip'

function Step([string]$message) {
    Write-Host ''
    Write-Host "[GA-NEXT PR434] $message" -ForegroundColor Cyan
}
function Fail([string]$message) { throw "[GA-NEXT PR434] $message" }
function Find-Godot([string[]]$roots) {
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        $found = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.exe' -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -match '(?i)^Godot.*4[.]7[.]2.*mono.*win64[.]exe$' -and
                $_.Name -notmatch '(?i)console[.]exe$'
            } | Sort-Object FullName)
        foreach ($candidate in $found) {
            if (Test-Path -LiteralPath (Join-Path $candidate.DirectoryName 'GodotSharp')) {
                return [string]$candidate.FullName
            }
        }
        if ($found.Count -gt 0) { return [string]$found[0].FullName }
    }
    return $null
}

try {
    Step "Preflight Git, .NET, repository and launcher mode $Mode."
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'Git nao encontrado.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail '.NET nao encontrado.' }
    if (-not (Test-Path -LiteralPath $RepoPath -PathType Container)) {
        Fail "Repositorio nao encontrado: $RepoPath"
    }
    $RepoPath = [System.IO.Path]::GetFullPath($RepoPath)
    $origin = (& git -C $RepoPath remote get-url origin 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or
        $origin -notmatch '^(https://github[.]com/|git@github[.]com:)ahtohiofilho/GlobalArena([.]git)?$') {
        Fail "Origem Git inesperada, sem executar codigo externo: $origin"
    }

    Step 'Obtendo PR434 sem alterar a branch atual ou a pasta principal.'
    & git -C $RepoPath fetch --no-tags origin $PullRef
    if ($LASTEXITCODE -ne 0) { Fail 'Git fetch falhou.' }
    $fetchSha = (& git -C $RepoPath rev-parse FETCH_HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $fetchSha -notmatch '^[0-9a-f]{40}$') {
        Fail 'Git retornou commit invalido.'
    }
    & git -C $RepoPath merge-base --is-ancestor $ExpectedAncestor $fetchSha
    if ($LASTEXITCODE -ne 0) { Fail 'O PR mudou de base; sem alterar o repositorio.' }

    foreach ($path in ($ExpectedCodeBlobs.Keys | Sort-Object)) {
        $blob = (& git -C $RepoPath rev-parse ("{0}:{1}" -f $fetchSha, $path) | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $blob -ne $ExpectedCodeBlobs[$path]) {
            Fail "O codigo foi alterado desde a homologacao: $path. Revalidar o PR antes de executar."
        }
    }

    # A new immutable worktree per commit: never reset or overwrite local work.
    $worktree = Join-Path (Split-Path -Parent $RepoPath) (
        'GlobalArena-GANEXT-VISUAL-PR434-' + $fetchSha.Substring(0, 12))
    if (-not (Test-Path -LiteralPath $worktree -PathType Container)) {
        Step 'Criando worktree isolada no commit verificado.'
        & git -C $RepoPath worktree add --detach $worktree $fetchSha
        if ($LASTEXITCODE -ne 0) { Fail 'Nao foi possivel criar worktree.' }
    }
    $checkedSha = (& git -C $worktree rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $checkedSha -ne $fetchSha) {
        Fail 'Worktree existente nao corresponde ao commit certificado; sem reset.'
    }
    $project = Join-Path $worktree 'tools\GlobalArena.Godot'
    $csproj = Join-Path $project 'GlobalArena.Godot.csproj'
    if (-not (Test-Path -LiteralPath $csproj -PathType Leaf)) {
        Fail 'Projeto Godot C# nao encontrado no worktree.'
    }
    Step 'Compilando o jogo C# real (sem arquivos artificiais).'
    & dotnet build $csproj -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { Fail 'Compilacao C# falhou.' }

    Step 'Localizando Godot 4.7.2 Mono.'
    $roots = @($ToolsDir, (Join-Path $env:USERPROFILE 'Downloads'),
        (Join-Path $env:USERPROFILE 'Desktop'),
        (Join-Path $env:USERPROFILE 'source'))
    $godot = Find-Godot $roots
    if ([string]::IsNullOrWhiteSpace($godot)) {
        Step 'Baixando Godot Mono oficial com hash SHA-256 verificado.'
        New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
        if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
            Invoke-WebRequest -Uri $OfficialGodotUrl -OutFile $ZipPath -UseBasicParsing
        }
        $zipSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $ZipPath).Hash.ToLowerInvariant()
        if ($zipSha -ne $OfficialGodotSha) { Fail 'Hash do Godot diferente do pacote oficial esperado.' }
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $ToolsDir -Force
        $godot = Find-Godot @($ToolsDir)
    }
    if ([string]::IsNullOrWhiteSpace($godot)) { Fail 'Godot Mono 4.7.2 nao localizado.' }
    Step "Godot verificado: $godot"
    & $godot --headless --path $project --editor --import --quit
    if ($LASTEXITCODE -ne 0) { Fail 'Importacao de recursos Godot falhou.' }

    # Drop all historic automated-smoke environment flags inherited from
    # PowerShell so that Godot will never auto-press E or auto-close the game.
    foreach ($name in @(
        'GA_FOUNDATION_GODOT_SMOKE',
        'GA_NEXT_WORKSPACE_SMOKE',
        'GA_NEXT_TACTICAL_V10_SMOKE',
        'GA_NEXT_TACTICAL_V11_SMOKE',
        'GA_NEXT_CONCENTRATED_WORKFORCE_SMOKE',
        'GA_NEXT_TILE_CLICK_MENU_SMOKE',
        'GA_NEXT_WORKER_BOARD_SMOKE',
        'GA_NEXT_TILE_HOVER_HYPOTHESIS_SMOKE',
        'GA_NEXT_SETTLED_TRADE_VISUAL_SMOKE',
        'GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW'
    )) {
        [System.Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }

    if ($Mode -eq 'EconomyLab') {
        [System.Environment]::SetEnvironmentVariable(
            'GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW', '1', 'Process')
        Step 'Abrindo LAB ECONOMICO INTERATIVO: valores apenas para QA.'
    } else {
        Step 'Abrindo demonstracao normal de trabalhadores (sem economia QA).'
    }
    $game = Start-Process -FilePath $godot -ArgumentList @(
        '--path', ('"{0}"' -f $project)
    ) -WorkingDirectory $project -PassThru
    if ($null -eq $game -or $game.Id -le 0) { Fail 'Godot nao iniciou.' }
    Write-Host "[GA-NEXT PR434] JOGO ABERTO: PID $($game.Id) / commit $fetchSha" -ForegroundColor Green
    Write-Host '[GA-NEXT PR434] E = fechar um turno; TAB = operacoes; M = mapa; G/P/X = ordens reais.'
    Write-Host '[GA-NEXT PR434] A/V/N = mudar profissao de trabalhador; clique em tiles; rotas aparecem apos E.'
    Write-Host '[GA-NEXT PR434] O jogo permanece aberto para uso manual; nenhuma sequencia de testes fecha a janela.'
    if ($Mode -eq 'EconomyLab') {
        Write-Host '[GA-NEXT PR434] ATENCAO: parametros experimentais de teste, NAO sao o balanceamento final.' -ForegroundColor Yellow
        Write-Host '[GA-NEXT PR434] O caixa inicial e zero; E liquida producao real, mostrando caixa e rotas na tela.'
    }
    Write-Host '[GA-NEXT PR434] A main e as demais worktrees permanecem intactas.'
} catch {
    Write-Host ''
    Write-Host "[GA-NEXT PR434] ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
