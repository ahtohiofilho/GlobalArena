param(
    [Parameter(Mandatory = $true)]
    [string] $ReportDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$file = Join-Path $ReportDirectory 'identical-market-104.json'
if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
    throw "Missing 104-market exact early-discard report: $file"
}
$data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
if ($data.Schema -ne 'ga-next.identical-market-route-elision-ab.v1' -or
    [int]$data.TileCount -ne 112 -or
    [int]$data.SellerCount -ne 8 -or
    [int]$data.MarketCount -ne 104 -or
    [uint64]$data.TotalAvailableGoodsMicro -ne 8000000 -or
    [uint64]$data.TotalAllocatedGoodsMicro -ne 8000000 -or
    $data.StateHashUnchanged -ne $true -or
    $data.NoClosedCycle -ne $true -or
    $data.EconomicallySettled -ne $false -or
    $data.ExactA_B -ne $true -or
    $data.SameFinalPotential -ne $true) {
    throw "Identity, conservation, StateHash or publication boundary not certified"
}
$refMs = [double]$data.ReferenceElapsedMilliseconds
$newMs = [double]$data.OptimizedElapsedMilliseconds
$refBytes = [long]$data.ReferenceAllocatedBytesCurrentThread
$newBytes = [long]$data.OptimizedAllocatedBytesCurrentThread
if ([double]::IsNaN($refMs) -or [double]::IsInfinity($refMs) -or
    [double]::IsNaN($newMs) -or [double]::IsInfinity($newMs) -or
    $refMs -lt 0 -or $newMs -lt 0 -or
    $refBytes -lt 0 -or $newBytes -lt 0) {
    throw 'Invalid measured wall-clock duration or current-thread allocation'
}
$hit = [bool]$data.WasMarketRouteElided
$fmt = [System.Globalization.CultureInfo]::InvariantCulture
$refTime = $refMs.ToString('F3', $fmt)
$newTime = $newMs.ToString('F3', $fmt)
$refAlloc = ($refBytes / 1024.0).ToString('F1', $fmt)
$newAlloc = ($newBytes / 1024.0).ToString('F1', $fmt)
$lines = @(
    '### GA-Next Gate 01B.2D-17 — A/B for exact identical global market input',
    '',
    '| Scenario | Original rerouting | Identical-input guard |',
    '|---|---:|---:|',
    "| 112 Goldberg tiles, 8 physical producers, 104 real markets: wall time (ms) | $refTime | $newTime |",
    "| Current-thread managed allocation (KiB) | $refAlloc | $newAlloc |",
    "| Guard applied in this exact 104-market sample | reference path | $hit |",
    '',
    'Exact economic and physical equivalence, 8,000,000 conserved micro-units, StateHash unchanged and NoClosedCycle are mandatory and PASSED. If guard=false, this case exercised the safe fallback and **does not demonstrate a runtime saving**.',
    'A separate *single real local market* A/B test guarantees the guard is exercised and produces the exact reference market potential/history without rerouting.',
    'Benchmarks are one wall-clock measurement per CI runner; time is not CPU-exclusive and allocations are cumulative managed bytes, not peak RAM. No statistical SLA or extrapolation to thousands of nodes.',
    'Different source routes, market identities, buyers, quantities or baseline paths must retain the original audited multipath process. No economic settlement was published.'
)
$summary = $lines -join [Environment]::NewLine
Write-Host $summary
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
}
