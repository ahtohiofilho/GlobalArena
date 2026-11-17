param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.Numerics -ErrorAction SilentlyContinue

$expected = [ordered]@{
    'eight-sellers-104-markets-mild' = @{ Sellers = 8; Markets = 104; Tiles = 112; RequireStable = $true }
    'eight-sellers-104-markets-severe' = @{ Sellers = 8; Markets = 104; Tiles = 112; RequireStable = $false }
    'sixteen-sellers-192-markets-scale' = @{ Sellers = 16; Markets = 192; Tiles = 208; RequireStable = $false }
}

$lines = @(
    '### GA-Next — joint market/route stress outcomes (not settled trade)',
    '',
    '| Scenario | Real sellers | Real markets | Connected tiles | Outcome | Completed / budget passes | Accepted proposals | Duration (ms) |',
    '|---|---:|---:|---:|---|---:|---:|---:|'
)

foreach ($name in $expected.Keys) {
    $file = Join-Path $ReportDirectory ($name + '.json')
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing mandatory adversarial stress outcome: $file. Cannot approve an ambiguous result."
    }
    $item = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $spec = $expected[$name]
    if ($item.Schema -ne 'ga-next.joint-stress.v1' -or
        $item.Scenario -ne $name -or
        [int]$item.SellerCount -ne $spec.Sellers -or
        [int]$item.MarketCount -ne $spec.Markets -or
        [int]$item.TileCount -ne $spec.Tiles -or
        $item.CandidateSettledEconomically -ne $false) {
        throw "Incorrect scenario, schema, market count, or settlement state in $file"
    }
    if (@('BLOCK_STATIONARY', 'BUDGET_EXHAUSTED') -notcontains [string]$item.Outcome) {
        throw "Unknown convergence outcome in $file"
    }
    if ($spec.RequireStable -and $item.Outcome -ne 'BLOCK_STATIONARY') {
        throw "Moderate-regime convergence gate was not met in $file"
    }
    $passBudget = [int]$item.PassBudget
    $passes = [int]$item.CompletePasses
    $accepted = [int]$item.AcceptedProposals
    if ($passBudget -lt 1 -or $passes -lt 1 -or $passes -gt $passBudget -or $accepted -lt 0) {
        throw "Invalid budget or accepted proposal count in $file"
    }
    if ($item.Outcome -eq 'BUDGET_EXHAUSTED' -and
        ($passes -ne $passBudget -or $accepted -lt 1)) {
        throw "Exhaustion must consume the pass budget with accepted improvements in $file"
    }
    $history = @($item.PotentialHistoryScaled)
    if ($history.Count -ne $accepted + 1) {
        throw "Accepted potential trace length mismatch in $file"
    }
    for ($i = 1; $i -lt $history.Count; $i++) {
        $before = [System.Numerics.BigInteger]::Parse([string]$history[$i - 1])
        $after = [System.Numerics.BigInteger]::Parse([string]$history[$i])
        if ($after -le $before) {
            throw "Non-increasing integrated potential trace in $file"
        }
    }
    $duration = [long]$item.ElapsedMilliseconds
    if ($duration -lt 0) { throw "Negative benchmark duration in $file" }

    $lines += "| $name | $($spec.Sellers) | $($spec.Markets) | $($spec.Tiles) | $($item.Outcome) | $passes / $passBudget | $accepted | $duration |"
    Write-Host "GA-NEXT STRESS $name : $($item.Outcome); passes=$passes/$passBudget; accepted=$accepted; durationMs=$duration"
}
$lines += @(
    '',
    'BLOCK_STATIONARY means no strictly improving proposal in one complete declared block pass; it is not proof of global economic equilibrium.',
    'BUDGET_EXHAUSTED means no converged plan was returned or published. Both outcomes require the physical auditor and unchanged NoClosedCycle state.',
    'Measured CI runner times are observations only, not performance guarantees for very large worlds.'
)
$markdown = $lines -join [Environment]::NewLine
Write-Host $markdown
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $markdown
}
