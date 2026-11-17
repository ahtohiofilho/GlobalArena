param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$lines = @(
    '### GA-Next — residual shortest-path workspace A/B equivalence',
    '',
    '| Segments / market | Residual augmentations | Reference time (ms) | Reused time (ms) | Reference allocated (KiB) | Reused allocated (KiB) | Allocation delta |',
    '|---:|---:|---:|---:|---:|---:|---:|'
)
foreach ($segments in @(2, 4, 8)) {
    $file = Join-Path $ReportDirectory ("segments-$segments.json")
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing exact equivalence report: $file"
    }
    $item = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($item.Schema -ne 'ga-next.residual-workspace.v1' -or
        [int]$item.SegmentsPerAverageMarket -ne $segments -or
        [int]$item.SellerCount -ne 8 -or
        [int]$item.MarketCount -ne 104 -or
        [uint64]$item.TotalAvailableGoodsMicro -ne 8000000 -or
        [uint64]$item.TotalAllocatedGoodsMicro -ne 8000000 -or
        [int]$item.Augmentations -lt 1 -or
        $item.ExactlyEquivalent -ne $true -or
        $item.CandidateSettledEconomically -ne $false) {
        throw "Market/worker conservation or strict equivalence not certified in $file"
    }
    $refMs = [long]$item.ReferenceElapsedMilliseconds
    $newMs = [long]$item.ReusedElapsedMilliseconds
    $refAlloc = [long]$item.ReferenceAllocatedBytesCurrentThread
    $newAlloc = [long]$item.ReusedAllocatedBytesCurrentThread
    if ($refMs -lt 0 -or $newMs -lt 0 -or
        $refAlloc -le 0 -or $newAlloc -le 0) {
        throw "Invalid timing or managed allocation observations in $file"
    }
    $refKiB = [math]::Round($refAlloc / 1024.0, 1)
    $newKiB = [math]::Round($newAlloc / 1024.0, 1)
    $deltaPercent = [math]::Round(
        (($refAlloc - $newAlloc) / [double]$refAlloc) * 100.0, 2)
    $lines += "| $segments | $($item.Augmentations) | $refMs | $newMs | $refKiB | $newKiB | $deltaPercent% |"
    Write-Host "GA-NEXT RESIDUAL A/B segments=$segments refAllocatedBytes=$refAlloc reusedAllocatedBytes=$newAlloc allocationSavingPercent=$deltaPercent refMs=$refMs reusedMs=$newMs EXACT"
}
$lines += @(
    '',
    'Every row passed a complete A/B identity check of all seller allocations, buyer totals, ordered physical delivery routes, integrated/segmented potential, audited freight and unchanged StateHash/NoClosedCycle.',
    'Allocation delta is (reference − reused)/reference. Positive means fewer allocated bytes; negative means more. Measurements are single CI observations, not statistical performance guarantees.',
    'GC.GetAllocatedBytesForCurrentThread measures cumulative managed allocations, NOT process peak RAM or retained heap. Elapsed times can vary due to runtime warmup and CI scheduling.',
    'No change to demand, price, tariff, physical routes, multipath or settled economic trade.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
