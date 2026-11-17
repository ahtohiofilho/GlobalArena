param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$cases = [ordered]@{
    'eight-sellers-104-markets' = @{ Tiles = 112; Sellers = 8; Markets = 104 }
    'sixteen-sellers-192-markets' = @{ Tiles = 208; Sellers = 16; Markets = 192 }
    'twentyfour-sellers-288-markets' = @{ Tiles = 320; Sellers = 24; Markets = 288 }
}
$stageNames = @(
    'GlobalSellerMarketFlow',
    'FixedMarketMultipathRouting',
    'IndependentPhysicalFreightAudit',
    'CompleteJointBlockSearch'
)

$lines = @(
    '### GA-Next — separate costs of global market, multipath, freight audit and joint passes',
    '',
    '| Scenario | Seller×market pairs | Global flow ms | Multipath ms | Physical audit ms | Joint search ms | Outcome |',
    '|---|---:|---:|---:|---:|---:|---|'
)
$allocations = @(
    '',
    '**Observed .NET managed thread allocations (KiB, not peak RAM)**',
    '',
    '| Scenario | Global flow | Multipath | Physical audit | Joint search |',
    '|---|---:|---:|---:|---:|'
)

foreach ($name in $cases.Keys) {
    $file = Join-Path $ReportDirectory ($name + '.json')
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing required joint profiling report: $file"
    }
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $expected = $cases[$name]
    if ($data.Schema -ne 'ga-next.joint-stage-profiling.v1' -or
        $data.Scenario -ne $name -or
        [int]$data.TileCount -ne $expected.Tiles -or
        [int]$data.SellerCount -ne $expected.Sellers -or
        [int]$data.MarketCount -ne $expected.Markets -or
        [int64]$data.SellerMarketPairsUpperBound -ne
            [int64]($expected.Sellers * $expected.Markets) -or
        [uint64]$data.AvailableGoodsMicro -ne
            [uint64]($expected.Sellers * 1000000) -or
        [uint64]$data.AllocatedGoodsMicro -ne
            [uint64]$data.AvailableGoodsMicro -or
        $data.CandidateSettledEconomically -ne $false) {
        throw "Wrong scenario, supply conservation or settlement flag in $file"
    }
    if ($data.JointOutcome -notin @('BLOCK_STATIONARY', 'BUDGET_EXHAUSTED')) {
        throw "Unknown joint outcome in $file"
    }
    if ([int]$data.GlobalFlowAugmentations -le 0 -or
        [int]$data.MultipathRouteBatches -le 0 -or
        [int]$data.MultipathDeliveryParts -le 0 -or
        [int]$data.PhysicalEdgeCount -le $expected.Tiles) {
        throw "Bad Goldberg physical workload counters in $file"
    }
    if ([int]$data.JointPasses -lt 1 -or
        [int]$data.JointPasses -gt 3 -or
        [int]$data.JointAcceptedProposals -lt 0) {
        throw "Invalid joint pass or accepted count in $file"
    }
    if ($data.JointOutcome -eq 'BUDGET_EXHAUSTED' -and
        ([int]$data.JointPasses -ne 3 -or
         [int]$data.JointAcceptedProposals -lt 1)) {
        throw "Budget exhaustion did not follow accepted improvements in $file"
    }
    $trace = @($data.JointPotentialHistoryScaled)
    if ($trace.Count -ne [int]$data.JointAcceptedProposals + 1) {
        throw "Potential trace length mismatch in $file"
    }
    for ($i = 1; $i -lt $trace.Count; $i++) {
        $previous = [System.Numerics.BigInteger]::Parse([string]$trace[$i - 1])
        $following = [System.Numerics.BigInteger]::Parse([string]$trace[$i])
        if ($following -le $previous) {
            throw "Non-improving accepted joint state in $file"
        }
    }

    $stages = @($data.Stages)
    if ($stages.Count -ne $stageNames.Count) {
        throw "Missing one or more profiling stages in $file"
    }
    $durations = @()
    $allocatedKib = @()
    for ($i = 0; $i -lt $stageNames.Count; $i++) {
        $stage = $stages[$i]
        if ($stage.Name -ne $stageNames[$i] -or
            [long]$stage.ElapsedMilliseconds -lt 0 -or
            [long]$stage.ManagedAllocatedBytesCurrentThread -lt 0) {
            throw "Invalid stage or counter in $file"
        }
        $durations += [long]$stage.ElapsedMilliseconds
        $allocatedKib += [math]::Round(
            [double]$stage.ManagedAllocatedBytesCurrentThread / 1024.0, 1)
    }

    $lines += "| $name | $($data.SellerMarketPairsUpperBound) | $($durations[0]) | $($durations[1]) | $($durations[2]) | $($durations[3]) | $($data.JointOutcome) |"
    $allocations += "| $name | $($allocatedKib[0]) | $($allocatedKib[1]) | $($allocatedKib[2]) | $($allocatedKib[3]) |"
    Write-Host "GA-NEXT JOINT PROFILING $name :: outcome=$($data.JointOutcome) pairs=$($data.SellerMarketPairsUpperBound) globalMs=$($durations[0]) routeMs=$($durations[1]) auditMs=$($durations[2]) jointMs=$($durations[3])"
}
$lines += $allocations
$lines += @(
    '',
    'Measurements are per-run observations from CI runners. They are not deterministic budgets, peak process memory, benchmark distributions or a claim that time scales linearly.',
    'The complete joint search re-runs global allocation and routing internally, so its total time and allocations cannot be summed with the three independently executed stage measurements.',
    'BLOCK_STATIONARY certifies only a complete pass without a better proposal under the declared discretization; BUDGET_EXHAUSTED publishes no candidate market or economic settlement.',
    'No closed economic cycle, prices or tariffs are published by this profiling gate.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
