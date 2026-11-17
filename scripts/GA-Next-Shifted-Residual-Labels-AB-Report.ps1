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
    '### GA-Next Gate 01B.2D-21 — exact shifted BigInteger residual labels vs original',
    '',
    '| Goldberg tiles | Sellers / real markets | Augmentations | Original search ms | Shifted search ms | Original allocated MiB | Shifted allocated MiB | Allocated change |',
    '|---:|---:|---:|---:|---:|---:|---:|---:|'
)
foreach ($case in $cases) {
    $tiles = [int]$case.Tiles
    $sellers = [int]$case.Sellers
    $markets = [int]$case.Markets
    $file = Join-Path $ReportDirectory ("shifted-labels-$tiles.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing exact shifted-labels A/B: $file"
    }
    $r = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($r.Schema -ne 'ga-next.exact-shifted-residual-labels-ab.v1' -or
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
        'ShiftedResidualSearchMilliseconds',
        'OriginalCompleteFlowMilliseconds',
        'ShiftedCompleteFlowMilliseconds')) {
        $ms = [double]$r.$key
        if ([double]::IsNaN($ms) -or
            [double]::IsInfinity($ms) -or $ms -lt 0) {
            throw "Invalid elapsed time $key in $file"
        }
    }
    foreach ($key in @('OriginalResidualSearchAllocatedBytes',
        'ShiftedResidualSearchAllocatedBytes',
        'OriginalCompleteFlowAllocatedBytes',
        'ShiftedCompleteFlowAllocatedBytes')) {
        if ([long]$r.$key -lt 0) {
            throw "Invalid managed allocation counter $key in $file"
        }
    }
    $originalMs = ([double]$r.OriginalResidualSearchMilliseconds).ToString('F2', $fmt)
    $shiftedMs = ([double]$r.ShiftedResidualSearchMilliseconds).ToString('F2', $fmt)
    $originalMiB = ([double]$r.OriginalResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $shiftedMiB = ([double]$r.ShiftedResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $savedBytes = [long]$r.OriginalResidualSearchAllocatedBytes -
        [long]$r.ShiftedResidualSearchAllocatedBytes
    $savedPct = if ([long]$r.OriginalResidualSearchAllocatedBytes -eq 0) {
        'n/a'
    } else {
        (($savedBytes / [double]$r.OriginalResidualSearchAllocatedBytes) *
            100.0).ToString('F2', $fmt) + '%'
    }
    $lines += "| $tiles | $sellers / $markets | $($r.FlowAugmentations) | $originalMs | $shiftedMs | $originalMiB | $shiftedMiB | $savedPct |"
    Write-Host "GA-NEXT SHIFTED A/B tiles=$tiles exact=true originalSearchMs=$originalMs shiftedSearchMs=$shiftedMs originalAllocatedMiB=$originalMiB shiftedAllocatedMiB=$shiftedMiB saved=$savedPct"
}
$lines += @(
    '',
    'The reference and optimized paths BOTH use the same indexed residual heap. Only arithmetic of the BigInteger labels differs: shifted[v]=reducedDistance[v]+potential[v]. The reduced-cost nonnegativity invariant is checked for every positive residual edge; a reduced label is created only when the exact shifted candidate strictly improves its destination.',
    'A/B verifies ordered real seller/buyer deliveries, physical paths and freight, all goods sold, the exact integrated and segmented BigInteger potential, augmentation count, StateHash and NoClosedCycle at every scale. Additional large 240-bit-cost tests cover both heap variants, both workspace modes, ties and fail-closed budget/reachability.',
    'One observed wall-clock reading per platform is NOT exclusive CPU time, a confidence interval or a throughput SLA. Allocated bytes are cumulative managed thread allocations, not peak or live memory. Timing can regress due to GC, JIT or fewer opportunities for rejected relaxations; correctness is the mandatory acceptance gate. No rounding, loss of precision, assumed wallets, invented demand or economic settlement.'
)
$report = $lines -join [Environment]::NewLine
Write-Host $report
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $report
}
