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
$requiredStages = @(
    'worker-market-preflight',
    'physical-graph-build',
    'source-shortest-paths',
    'seller-market-arc-build',
    'residual-network-setup',
    'residual-augmenting-search',
    'residual-conservation-and-utility',
    'delivery-route-materialization',
    'independent-final-physical-audit'
)
$summary = @(
    '### GA-Next Gate 01B.2D-19 — real global market internals, four Goldberg sizes',
    '',
    '| Fine tiles | Sellers / real markets | Possible seller-market pairs | Residual augmentations | Total profiled wall ms | Residual search wall ms | Residual share of instrumented phases |',
    '|---:|---:|---:|---:|---:|---:|---:|'
)
$subphases = @(
    '',
    '**Phase-level current-thread managed allocations (MiB), single observed sample**',
    '',
    '| Tiles | Preflight | Paths | Seller-market arcs | Residual network | Residual search | Final audit | Total profiled allocation |',
    '|---:|---:|---:|---:|---:|---:|---:|---:|'
)

foreach ($case in $cases) {
    $tiles = [int]$case.Tiles
    $sellers = [int]$case.Sellers
    $markets = [int]$case.Markets
    $file = Join-Path $ReportDirectory ("global-flow-internals-$tiles.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing full worker-backed global market internal profile: $file"
    }
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($data.Schema -ne 'ga-next.global-flow-internal-scaling.v1' -or
        [int]$data.TileCount -ne $tiles -or
        [int]$data.SellerCount -ne $sellers -or
        [int]$data.MarketCount -ne $markets -or
        [int64]$data.SellerMarketPairsUpperBound -ne
            [int64]($sellers * $markets) -or
        [int]$data.PhysicalEdgeCount -le $tiles -or
        [int]$data.SegmentsPerAverageMarket -ne 4 -or
        [int]$data.FlowAugmentations -lt 1 -or
        [int]$data.FlowAugmentations -gt 8192 -or
        [uint64]$data.TotalAvailableGoodsMicro -ne
            [uint64]($sellers * 1000000) -or
        [uint64]$data.TotalAllocatedGoodsMicro -ne
            [uint64]$data.TotalAvailableGoodsMicro -or
        $data.ExactlyEquivalentWithoutProfiler -ne $true -or
        $data.StateHashUnchanged -ne $true -or
        $data.NoClosedCycle -ne $true -or
        $data.EconomicallySettled -ne $false) {
        throw "Wrong real Goldberg size, conservation or state boundary in $file"
    }
    foreach ($key in @('ProfiledElapsedMilliseconds',
                       'UnprofiledElapsedMilliseconds')) {
        $value = [double]$data.$key
        if ([double]::IsNaN($value) -or
            [double]::IsInfinity($value) -or $value -lt 0) {
            throw "Invalid elapsed wall time $key in $file"
        }
    }
    foreach ($key in @('ProfiledAllocatedBytesCurrentThread',
                       'UnprofiledAllocatedBytesCurrentThread')) {
        if ([long]$data.$key -lt 0) {
            throw "Invalid cumulative managed allocation $key in $file"
        }
    }
    if ($data.TimingKind -ne 'Stopwatch.wall-clock.single-observation' -or
        $data.AllocationKind -ne
            'GC.GetAllocatedBytesForCurrentThread.cumulative') {
        throw "Profile lacks explicit interpretation of timing or allocations"
    }

    $stages = @($data.Stages)
    if ($stages.Count -ne $requiredStages.Count -or
        (@($stages | Select-Object -ExpandProperty Phase -Unique).Count -ne
            $requiredStages.Count)) {
        throw "Global flow internal stages absent or duplicated in $file"
    }
    $byName = @{}
    $totalInstrumentedMs = 0.0
    foreach ($stage in $stages) {
        $phase = [string]$stage.Phase
        $ms = [double]$stage.ElapsedMilliseconds
        $bytes = [long]$stage.AllocatedBytesCurrentThread
        if ($phase -notin $requiredStages -or
            [int]$stage.Samples -lt 1 -or
            [double]::IsNaN($ms) -or
            [double]::IsInfinity($ms) -or
            $ms -lt 0 -or $bytes -lt 0) {
            throw "Invalid or invented global flow internal stage in $file"
        }
        $byName[$phase] = $stage
        $totalInstrumentedMs += $ms
    }
    foreach ($required in $requiredStages) {
        if (-not $byName.ContainsKey($required)) {
            throw "Missing phase $required in $file"
        }
    }

    $residualMs = [double]$byName['residual-augmenting-search'].ElapsedMilliseconds
    $trackedShare = if ($totalInstrumentedMs -eq 0) { 'n/a' } else {
        ($residualMs * 100.0 / $totalInstrumentedMs).ToString('F1', $fmt) + '%'
    }
    $totalMs = ([double]$data.ProfiledElapsedMilliseconds).ToString('F2', $fmt)
    $searchMs = $residualMs.ToString('F2', $fmt)
    $summary += "| $tiles | $sellers / $markets | $($sellers * $markets) | $($data.FlowAugmentations) | $totalMs | $searchMs | $trackedShare |"
    $readings = @('worker-market-preflight', 'source-shortest-paths',
        'seller-market-arc-build', 'residual-network-setup',
        'residual-augmenting-search', 'independent-final-physical-audit')
    $alloc = foreach ($phase in $readings) {
        ([double]$byName[$phase].AllocatedBytesCurrentThread /
            1048576.0).ToString('F2', $fmt)
    }
    $overall = ([double]$data.ProfiledAllocatedBytesCurrentThread /
        1048576.0).ToString('F2', $fmt)
    $subphases += "| $tiles | $($alloc -join ' | ') | $overall |"
    Write-Host "GA-NEXT GLOBAL INTERNAL SCALE tiles=$tiles sellers=$sellers markets=$markets augmentations=$($data.FlowAugmentations) profiledMs=$totalMs residualMs=$searchMs trackedResidualShare=$trackedShare PASS"
}
$summary += $subphases
$summary += @(
    '',
    'The nine opt-in measurement phases are non-overlapping but do not cover all orchestration. The residual share is a fraction of **instrumented phase time**, not CPU share or total wall time. The full flow is also recomputed once without the profiler in each size; complete candidate allocations, ordered source and destination routes, edge loads, exact BigInteger objective, augmentations and conserved goods must match.',
    'The profile is observation-only and never used by price, freight, market allocation or convergence. No new optimization or CPU budget policy is introduced by this gate.',
    'Timings are one wall-clock run per GitHub runner, not exclusive CPU time; allocations are cumulative managed bytes on one executing thread, not process RAM or peak heap. No statistical SLA or proof of thousand-node scalability. Performance counters do not gate correctness.',
    'No market was economically settled, no user-facing price was changed and the authoritative StateHash remained unchanged.'
)
$report = $summary -join [Environment]::NewLine
Write-Host $report
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $report
}
