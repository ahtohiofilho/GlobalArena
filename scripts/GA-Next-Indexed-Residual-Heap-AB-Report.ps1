param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

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
    '### GA-Next Gate 01B.2D-20 — exact indexed heap vs legacy residual priority queue',
    '',
    '| Goldberg connected tiles | Sellers / real markets | Augmentations | Legacy search ms | Indexed search ms | Legacy search allocated MiB | Indexed search allocated MiB | Search allocated change |',
    '|---:|---:|---:|---:|---:|---:|---:|---:|'
)
$totalSaves = 0
foreach ($c in $cases) {
    $tiles = [int]$c.Tiles
    $sellers = [int]$c.Sellers
    $markets = [int]$c.Markets
    $file = Join-Path $ReportDirectory ("indexed-heap-$tiles.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing indexed residual A/B report $file"
    }
    $r = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($r.Schema -ne 'ga-next.indexed-residual-heap-ab.v1' -or
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
        throw "Indexed heap violated exact market identity, conservation or state boundary: $file"
    }
    foreach ($key in @('OriginalResidualSearchMilliseconds',
            'IndexedResidualSearchMilliseconds',
            'OriginalCompleteFlowMilliseconds',
            'IndexedCompleteFlowMilliseconds')) {
        $value = [double]$r.$key
        if ([double]::IsNaN($value) -or
            [double]::IsInfinity($value) -or $value -lt 0) {
            throw "Invalid phase time $key in $file"
        }
    }
    foreach ($key in @('OriginalResidualSearchAllocatedBytes',
            'IndexedResidualSearchAllocatedBytes',
            'OriginalCompleteFlowAllocatedBytes',
            'IndexedCompleteFlowAllocatedBytes')) {
        if ([long]$r.$key -lt 0) {
            throw "Invalid allocated bytes $key in $file"
        }
    }
    $oldMs = ([double]$r.OriginalResidualSearchMilliseconds).ToString('F2', $fmt)
    $newMs = ([double]$r.IndexedResidualSearchMilliseconds).ToString('F2', $fmt)
    $oldMiB = ([double]$r.OriginalResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $newMiB = ([double]$r.IndexedResidualSearchAllocatedBytes /
        1048576.0).ToString('F2', $fmt)
    $saved = [long]$r.OriginalResidualSearchAllocatedBytes -
        [long]$r.IndexedResidualSearchAllocatedBytes
    $totalSaves += $saved
    $savedPct = if ([long]$r.OriginalResidualSearchAllocatedBytes -eq 0) {
        'n/a'
    } else {
        (($saved / [double]$r.OriginalResidualSearchAllocatedBytes) *
            100.0).ToString('F2', $fmt) + '%'
    }
    $lines += "| $tiles | $sellers / $markets | $($r.FlowAugmentations) | $oldMs | $newMs | $oldMiB | $newMiB | $savedPct |"
    Write-Host "GA-NEXT INDEXED HEAP A/B tiles=$tiles exact=true legacySearchMs=$oldMs indexedSearchMs=$newMs legacyAllocatedMiB=$oldMiB indexedAllocatedMiB=$newMiB saved=$savedPct"
}

$lines += @(
    '',
    'The original PriorityQueue implementation with duplicate/stale queue entries remains the exact A/B oracle. Every run certifies the same ordered seller/buyer allocations, actual physical routes, integrated and segmented BigInteger potential, flow augmentation count, conserved goods, freight loads, StateHash and NoClosedCycle. Constrained-seller reassignment, equal-cost ties, budget exhaustion and unreachable buyers are tested in both modes.',
    '',
    'The optimized indexed heap maintains one live entry per node and decreases its key in place while preserving exact (BigInteger distance, node-id) ordering. No money, route, tariff, real demand, congestion parameter or economic settlement is changed.',
    'Timings are ONE observed wall-clock measurement per GitHub runner (not exclusive CPU); allocation is cumulative managed bytes on the current thread, not peak RAM. Differences may be affected by JIT, GC and CI scheduling. If measured search time regresses, the data remain valid but **the optimization is NOT a proven latency improvement**. No statistical SLA or proof for thousands of nodes.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
