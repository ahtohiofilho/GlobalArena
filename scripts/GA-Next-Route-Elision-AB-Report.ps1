param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$file = Join-Path $ReportDirectory 'route-elision-104.json'
if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
    throw "Missing exact Goldberg route-elision A/B report: $file"
}

$data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
if ($data.Schema -ne 'ga-next.route-elision-ab.v1' -or
    [int]$data.TileCount -ne 112 -or
    [int]$data.SellerCount -ne 8 -or
    [int]$data.MarketCount -ne 104 -or
    [uint64]$data.TotalAvailableGoodsMicro -ne 8000000 -or
    [uint64]$data.TotalAllocatedGoodsMicro -ne 8000000 -or
    $data.StateHashUnchanged -ne $true -or
    $data.EconomicallySettled -ne $false -or
    $data.NoClosedCycle -ne $true -or
    $data.ExactlyEquivalent -ne $true -or
    $data.CandidateObjectReused -ne $true -or
    $data.RouteProposalRejected -ne $true) {
    throw "A/B route result not exactly equivalent, physically conserved or unpublished"
}

$referenceMs = [double]$data.ReferenceElapsedMilliseconds
$optimizedMs = [double]$data.OptimizedElapsedMilliseconds
$referenceBytes = [long]$data.ReferenceAllocatedBytesCurrentThread
$optimizedBytes = [long]$data.OptimizedAllocatedBytesCurrentThread
if ([double]::IsNaN($referenceMs) -or
    [double]::IsInfinity($referenceMs) -or
    [double]::IsNaN($optimizedMs) -or
    [double]::IsInfinity($optimizedMs) -or
    $referenceMs -lt 0 -or $optimizedMs -lt 0 -or
    $referenceBytes -lt 0 -or $optimizedBytes -lt 0 -or
    [string]::IsNullOrWhiteSpace([string]$data.PotentialScaled)) {
    throw "Invalid performance observation or potential in A/B report"
}

$fmt = [System.Globalization.CultureInfo]::InvariantCulture
$msRef = $referenceMs.ToString('F3', $fmt)
$msOpt = $optimizedMs.ToString('F3', $fmt)
$kiBRef = ($referenceBytes / 1024.0).ToString('F1', $fmt)
$kiBOpt = ($optimizedBytes / 1024.0).ToString('F1', $fmt)
$timeChange = if ($referenceMs -eq 0) { 'not-defined' } else {
    ((($referenceMs - $optimizedMs) / $referenceMs) * 100.0).ToString('F2', $fmt) + '%'
}
$allocationChange = if ($referenceBytes -eq 0) { 'not-defined' } else {
    ((($referenceBytes - $optimizedBytes) / [double]$referenceBytes) * 100.0).ToString('F2', $fmt) + '%'
}

$lines = @(
    '### GA-Next Gate 01B.2D-16 — exact duplicate route proposal A/B',
    '',
    '| Real Goldberg 112 tiles / 8 sellers / 104 markets | Original recalculation | Elided duplicate proposal | Observed reduction |',
    '|---|---:|---:|---:|',
    "| Wall clock (ms) | $msRef | $msOpt | $timeChange |",
    "| Current-thread managed allocations (KiB) | $kiBRef | $kiBOpt | $allocationChange |",
    '',
    'Strict identity gate PASSED: same economic potential, buyer quantities, delivery routes, freight, full accepted potential history, route proposal counters, completed passes, state hash and no closed economic cycle. The newly skipped duplicate is **counted** as a rejected proposal and uses the immutable, previously audited candidate.',
    'These are single CI wall-clock and cumulative current-thread allocation observations, NOT CPU-time SLAs, peak RAM, or statistical guarantees. A timing or allocation regression does not fail CI. Only explicit logical equivalence and physical conservation do.',
    'The market proposal, changed worker census, reprice/road rebase, route budgets, tariffs and prices are not skipped or weakened. This A/B gate does not certify thousand-node throughput.',
    '',
    'Optimization affects only the opt-in progressive coordinator, not the full/reference economic solver or settlement publication.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
