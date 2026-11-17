param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$lines = @(
    '### GA-Next Gate 01B.2D-18 — unused sequential market seed A/B',
    '',
    'Complete global 112-tile Goldberg market: 8 actual producer lots, 104 real worker-demand markets, 8,000,000 micro-goods. The old sequential marginal solve never constrained the global allocation. This gate replaces it with complete worker, commodity and quote preflight.',
    '',
    '| Segments / average market | Global augmentations | Full reference time (ms) | Seedless time (ms) | Reference allocated (MiB) | Seedless allocated (MiB) | Observed allocation change |',
    '|---:|---:|---:|---:|---:|---:|---:|'
)
foreach ($n in @(2, 4, 8)) {
    $file = Join-Path $ReportDirectory ("seed-elision-segments-$n.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing exact global seed A/B evidence: $file"
    }
    $r = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($r.Schema -ne 'ga-next.global-seed-elision-ab.v1' -or
        [int]$r.SellerCount -ne 8 -or
        [int]$r.MarketCount -ne 104 -or
        [int]$r.SegmentsPerAverageMarket -ne $n -or
        [int]$r.FlowAugmentations -lt 1 -or
        [uint64]$r.TotalAvailableGoodsMicro -ne 8000000 -or
        [uint64]$r.TotalAllocatedGoodsMicro -ne 8000000 -or
        $r.ExactlyEquivalent -ne $true -or
        $r.StateHashUnchanged -ne $true -or
        $r.NoClosedCycle -ne $true -or
        $r.CandidateSettledEconomically -ne $false) {
        throw "Exact global market, physical conservation or StateHash audit failed: $file"
    }
    $refMs = [double]$r.ReferenceElapsedMilliseconds
    $newMs = [double]$r.OptimizedElapsedMilliseconds
    $refBytes = [long]$r.ReferenceAllocatedBytesCurrentThread
    $newBytes = [long]$r.OptimizedAllocatedBytesCurrentThread
    if ([double]::IsNaN($refMs) -or [double]::IsInfinity($refMs) -or
        [double]::IsNaN($newMs) -or [double]::IsInfinity($newMs) -or
        $refMs -lt 0 -or $newMs -lt 0 -or
        $refBytes -le 0 -or $newBytes -le 0) {
        throw "Invalid single-run time or managed allocated bytes: $file"
    }
    $fmt = [System.Globalization.CultureInfo]::InvariantCulture
    $refTime = $refMs.ToString('F3', $fmt)
    $newTime = $newMs.ToString('F3', $fmt)
    $refMiB = ($refBytes / 1048576.0).ToString('F2', $fmt)
    $newMiB = ($newBytes / 1048576.0).ToString('F2', $fmt)
    $change = ((($refBytes - $newBytes) / [double]$refBytes) * 100).ToString('F2', $fmt)
    $lines += "| $n | $($r.FlowAugmentations) | $refTime | $newTime | $refMiB | $newMiB | $change% |"
    Write-Host "GA-NEXT GLOBAL SEED A/B segments=$n exact=true referenceMs=$refTime seedlessMs=$newTime referenceBytes=$refBytes seedlessBytes=$newBytes"
}

$lines += @(
    '',
    'Every sample must pass strict identity of supply lots, market totals, ordered seller/buyer physical deliveries and routes, flow augmentation count, segmented/integrated BigInteger potential, edge freight evidence, StateHash and NoClosedCycle. Invalid quotes, duplicate markets, invalid roads and augmentation-budget exhaustion are also tested to fail closed in BOTH modes.',
    '',
    'Time is elapsed wall-clock, not CPU exclusive. Allocations are managed bytes accumulated by the executing thread, not heap retained or process peak RAM. Each stage is one run per runner: performance changes are observations without confidence intervals or guaranteed throughput.',
    'The full concave flow solver, its search workspace, market prices, quartic congestion, tariffs, source worker allocations and shipment audit remain unchanged. No economic settlement is published.',
    'This is a 112-tile benchmark, NOT a demonstration of thousand-node scalability.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
