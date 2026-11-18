param([Parameter(Mandatory = $true)][string] $ReportDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fmt = [System.Globalization.CultureInfo]::InvariantCulture
$cases = @(
    @{ Tiles = 112; Sellers = 8; Markets = 104 }
    @{ Tiles = 208; Sellers = 16; Markets = 192 }
    @{ Tiles = 320; Sellers = 24; Markets = 288 }
    @{ Tiles = 416; Sellers = 32; Markets = 384 }
)
$lines = @(
    '### GA-Next Gate 01B.2D-22 — exact dominance BigInteger residual labels vs original',
    '',
    '| Goldberg tiles | Sellers / real markets | Augmentations | Original search ms | Dominance search ms | Original allocated MiB | Dominance allocated MiB | Allocated change |',
    '|---:|---:|---:|---:|---:|---:|---:|---:|'
)
foreach ($case in $cases) {
    $tiles = [int]$case.Tiles
    $sellers = [int]$case.Sellers
    $markets = [int]$case.Markets
    $file = Join-Path $ReportDirectory ("residual-dominance-$tiles.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing exact dominance-labels A/B: $file"
    }
    $r = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($r.Schema -ne 'ga-next.exact-residual-dominance-ab.v1' -or
        [int]$r.TileCount -ne $tiles -or
        [int]$r.SellerCount -ne $sellers -or
        [int]$r.MarketCount -ne $markets -or
        [long]$r.SellerMarketPairsUpperBound -ne
            [long]($sellers * $markets) -or
        [int]$r.PhysicalEdgeCount -le $tiles -or
        [int]$r.SegmentsPerAverageMarket -ne 4 -or
        [int]$r.FlowAugmentations -lt 1 -or
        [int]$r.FlowAugmentations -gt 8192 -or
        [uint64]$r.TotalAvailableGoodsMicro -ne
            [uint64]($sellers * 1000000) -or
        [uint64]$r.TotalAllocatedGoodsMicro -ne
            [uint64]$r.TotalAvailableGoodsMicro -or
        $r.ExactlyEquivalent -ne $true -or
        $r.StateHashUnchanged -ne $true -or
        $r.NoClosedCycle -ne $true -or
        $r.EconomicallySettled -ne $false) {
        throw "Invalid real Goldberg scope or conservation: $file"
    }
    foreach ($key in @('OriginalResidualSearchMilliseconds',
        'DominanceResidualSearchMilliseconds',
        'OriginalCompleteFlowMilliseconds',
        'DominanceCompleteFlowMilliseconds')) {
        $ms = [double]$r.$key
        if ([double]::IsNaN($ms) -or
            [double]::IsInfinity($ms) -or $ms -lt 0) {
            throw "Invalid elapsed time $key in $file"
        }
    }
    foreach ($key in @('OriginalResidualSearchAllocatedBytes',
        'DominanceResidualSearchAllocatedBytes',
        'OriginalCompleteFlowAllocatedBytes',
        'DominanceCompleteFlowAllocatedBytes')) {
        if ([long]$r.$key -lt 0) {
            throw "Invalid managed allocation counter $key in $file"
        }
    }
    $originalMs = ([double]$r.OriginalResidualSearchMilliseconds).ToString('F2', $fmt)
    $dominanceMs = ([double]$r.DominanceResidualSearchMilliseconds).ToString('F2', $fmt)
    $originalMiB = ([double]$r.OriginalResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $dominanceMiB = ([double]$r.DominanceResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $savedBytes = [long]$r.OriginalResidualSearchAllocatedBytes -
        [long]$r.DominanceResidualSearchAllocatedBytes
    $savedPct = if ([long]$r.OriginalResidualSearchAllocatedBytes -eq 0) {
        'n/a'
    } else {
        (($savedBytes / [double]$r.OriginalResidualSearchAllocatedBytes) *
            100.0).ToString('F2', $fmt) + '%'
    }
    $lines += "| $tiles | $sellers / $markets | $($r.FlowAugmentations) | $originalMs | $dominanceMs | $originalMiB | $dominanceMiB | $savedPct |"
    Write-Host "GA-NEXT SHIFTED A/B tiles=$tiles exact=true originalSearchMs=$originalMs dominanceSearchMs=$dominanceMs originalAllocatedMiB=$originalMiB dominanceAllocatedMiB=$dominanceMiB saved=$savedPct"
}
$lines += @(
    '',
    'Both paths use identical shifted BigInteger labels and the same indexed queue. The optimized branch proves reduced-cost nonnegativity from nonnegative edge cost and monotone potentials, and proves candidate rejection from nonnegative cost and shifted-source label >= shifted-destination label. Every edge still satisfies the exact nonnegative reduced-cost invariant. The disabled fast path retains all original BigInteger operations as an A/B oracle.',
    'A/B verifies ordered real seller/buyer deliveries, physical paths and freight, all goods sold, the exact integrated and segmented BigInteger potential, augmentation count, StateHash and NoClosedCycle at every scale. Additional adversarial 240-bit freight tests cover both heaps, both workspace modes, zero/equal costs, reverse edges, and fail-closed budget/reachability.',
    'One observed wall-clock reading per platform is NOT exclusive CPU time, a confidence interval or a throughput SLA. Allocated bytes are cumulative managed thread allocations, not peak or live memory. Timing can regress due to GC, JIT or fewer opportunities for rejected relaxations; correctness is the mandatory acceptance gate. No rounding, loss of precision, assumed wallets, invented demand or economic settlement.'
)
$report = $lines -join [Environment]::NewLine
Write-Host $report
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $report
}
