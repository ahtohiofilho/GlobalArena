param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$file = Join-Path $ReportDirectory 'progressive-cost-104.json'
if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
    throw "Missing worker-backed progressive cost report: $file"
}
$item = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
$phases = @(
    'initial-cold-solve',
    'market-proposal',
    'route-proposal',
    'quote-warm-rebase',
    'quote-cold-solve',
    'post-quote-market-proposal',
    'road-warm-reroute',
    'road-cold-solve',
    'post-road-market-proposal'
)
if ($item.Schema -ne 'ga-next.progressive-cost-profile.v1' -or
    [int]$item.TileCount -ne 112 -or
    [int]$item.SellerCount -ne 8 -or
    [int]$item.MarketCount -ne 104 -or
    [uint64]$item.TotalAvailableGoodsMicro -ne 8000000 -or
    [uint64]$item.TotalAllocatedGoodsMicro -ne 8000000 -or
    $item.StateHashUnchanged -ne $true -or
    $item.NoClosedCycle -ne $true -or
    $item.EconomicallySettled -ne $false -or
    $item.QuoteWarmCandidateReused -ne $true -or
    $item.RoadWarmMarketAllocationReused -ne $true -or
    $item.TimingKind -ne 'Stopwatch.wall-clock.single-observation' -or
    $item.AllocationKind -ne 'GC.GetAllocatedBytesForCurrentThread.cumulative' -or
    @($item.Samples).Count -ne $phases.Count) {
    throw "Invalid worker census, sample schema, audit or unsettled-market boundary in $file"
}

$lines = @(
    '### GA-Next — progressive market proposal cost observability (Gate 01B.2D-15)',
    '',
    'One measured 112-node / 8-seller / 104-worker-market connected Goldberg scenario. All quantities were conserved; StateHash and NoClosedCycle unchanged.',
    '',
    '| Executed stage | Wall time (ms) | Managed allocations (KiB) | Accepted potential gain (scaled) |',
    '|---|---:|---:|---:|'
)
foreach ($phase in $phases) {
    $matches = @($item.Samples | Where-Object { $_.Phase -ceq $phase })
    if ($matches.Count -ne 1) {
        throw "Missing or duplicated stage $phase in $file"
    }
    $sample = $matches[0]
    $ms = [double]$sample.ElapsedMilliseconds
    $bytes = [long]$sample.AllocatedBytesCurrentThread
    if ([double]::IsNaN($ms) -or [double]::IsInfinity($ms) -or
        $ms -lt 0 -or $bytes -lt 0 -or
        $sample.PhysicalConservationPassed -ne $true -or
        [string]::IsNullOrWhiteSpace([string]$sample.PotentialScaled)) {
        throw "Invalid audited measurement at $phase"
    }
    if ($phase -like '*proposal') {
        if ([string]::IsNullOrWhiteSpace([string]$sample.AcceptedGainScaled) -or
            $sample.AcceptedGainScaled -eq 'not-comparable-across-epochs') {
            throw "Missing same-epoch exact potential gain at $phase"
        }
    }
    elseif ($sample.AcceptedGainScaled -ne 'not-comparable-across-epochs') {
        throw "Cross-epoch potential mistakenly compared at $phase"
    }
    $time = $ms.ToString('F3', [System.Globalization.CultureInfo]::InvariantCulture)
    $kib = ($bytes / 1024.0).ToString('F1', [System.Globalization.CultureInfo]::InvariantCulture)
    $gain = [string]$sample.AcceptedGainScaled
    $lines += "| $phase | $time | $kib | $gain |"
    Write-Host "GA-NEXT PROGRESSIVE PROFILE phase=$phase elapsedMs=$time allocatedBytes=$bytes potentialGainScaled=$gain conserved=true"
}

$quoteWarm = @($item.Samples | Where-Object { $_.Phase -eq 'quote-warm-rebase' })[0]
$quoteCold = @($item.Samples | Where-Object { $_.Phase -eq 'quote-cold-solve' })[0]
$roadWarm = @($item.Samples | Where-Object { $_.Phase -eq 'road-warm-reroute' })[0]
$roadCold = @($item.Samples | Where-Object { $_.Phase -eq 'road-cold-solve' })[0]
$lines += @(
    '',
    'Warm/cold comparisons use identical **inputs** within each category but do not claim the same optimization result. A warm candidate is physically audited, not necessarily the cold solver optimum.',
    "Quote-only rebase: warm $([math]::Round([double]$quoteWarm.ElapsedMilliseconds, 3)) ms versus fresh $([math]::Round([double]$quoteCold.ElapsedMilliseconds, 3)) ms (observed, not an SLA).",
    "Road/tariff rebase: warm $([math]::Round([double]$roadWarm.ElapsedMilliseconds, 3)) ms versus fresh $([math]::Round([double]$roadCold.ElapsedMilliseconds, 3)) ms (observed, not an SLA).",
    '',
    'The reported time is elapsed wall-clock duration (not process CPU time); allocated bytes are cumulative current-thread managed allocations (not peak RAM). One run per stage, no statistical significance or hard performance pass/fail threshold. Potential gains are valid ONLY for continuation proposals in the same frozen economic epoch; warm/cold seeds are never compared across epochs.',
    'This benchmark does not simulate thousands of nodes. It is an audited baseline to decide which expensive proposal substage to profile and optimize next. No market settlement, interface, prices, toll rules or trade publication were changed.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
