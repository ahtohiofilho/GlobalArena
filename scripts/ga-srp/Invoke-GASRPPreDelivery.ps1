#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$CandidatesDirectory,
    [string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -ne 5) { throw 'GA_SRP_REQUIRES_WINDOWS_POWERSHELL_5_1' }
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$validator = Join-Path $repo 'scripts\Test-GAScriptReliability.ps1'
$regression = Join-Path $PSScriptRoot 'Test-GASRPPreDelivery.ps1'
if ([string]::IsNullOrWhiteSpace($CandidatesDirectory)) {
    $CandidatesDirectory = Join-Path $repo 'scripts\candidates'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'ga-srp-pre-delivery'
}
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw 'OFFICIAL_VALIDATOR_MISSING' }
if (-not (Test-Path -LiteralPath $regression -PathType Leaf)) { throw 'REGRESSION_SUITE_MISSING' }
if (-not (Test-Path -LiteralPath $CandidatesDirectory -PathType Container)) { throw 'CANDIDATE_FOLDER_MISSING' }
$resolvedCandidates = (Resolve-Path -LiteralPath $CandidatesDirectory).Path
$allowedCandidates = (Resolve-Path -LiteralPath (Join-Path $repo 'scripts\candidates')).Path
if (-not [string]::Equals($resolvedCandidates, $allowedCandidates, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'CANDIDATE_DIRECTORY_NOT_TRUSTED'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OUTPUT_DIRECTORY_MUST_BE_NEW' }

Write-Host 'GA_SRP_PREDELIVERY_REGRESSIONS_START'
$regressionOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $regression)
$regressionExit = [int]$LASTEXITCODE
foreach ($line in $regressionOutput) { Write-Host $line }
if ($regressionExit -ne 0) { throw 'PREDELIVERY_REGRESSIONS_FAILED' }

$candidates = @(Get-ChildItem -LiteralPath $resolvedCandidates -Filter '*.ps1' -File | Sort-Object Name)
if ($candidates.Count -eq 0) {
    Write-Host 'CANDIDATES=0'
    Write-Host 'RESULT=PASS_INFRASTRUCTURE_NO_CANDIDATES'
    exit 0
}

$reports = Join-Path $OutputDirectory 'reports'
$approved = Join-Path $OutputDirectory 'approved'
[void](New-Item -ItemType Directory -Path $reports -Force)
$approvedManifest = [System.Collections.Generic.List[string]]::new()
$validated = [System.Collections.Generic.List[object]]::new()
$officialHash = (Get-FileHash -LiteralPath $validator -Algorithm SHA256).Hash.ToUpperInvariant()
try {
    foreach ($candidate in $candidates) {
        $profileFile = $candidate.FullName + '.profile'
        if (-not (Test-Path -LiteralPath $profileFile -PathType Leaf)) {
            throw ('MISSING_PROFILE=' + $candidate.Name)
        }
        $profile = ([System.IO.File]::ReadAllText($profileFile)).Trim()
        if (@('ReadOnly','QaMutation','FormalClose','Bootstrap') -cnotcontains $profile) {
            throw ('INVALID_PROFILE=' + $candidate.Name)
        }
        $shaBefore = (Get-FileHash -LiteralPath $candidate.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        $output = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator -Path $candidate.FullName -Profile $profile)
        $exitCode = [int]$LASTEXITCODE
        $text = $output -join "`n"
        [System.IO.File]::WriteAllText((Join-Path $reports ($candidate.Name + '.validator.txt')), $text, [System.Text.UTF8Encoding]::new($false))
        $shaAfter = (Get-FileHash -LiteralPath $candidate.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($shaBefore -cne $shaAfter) { throw ('CANDIDATE_CHANGED_DURING_VALIDATION=' + $candidate.Name) }
        if ($exitCode -ne 0 -or $text -notmatch 'RESULT=PASS') {
            throw ('OFFICIAL_VALIDATOR_REJECTED=' + $candidate.Name + "`n" + $text)
        }
        $validated.Add([pscustomobject]@{ File=$candidate.FullName; Name=$candidate.Name; Profile=$profile; SHA256=$shaBefore })
        $approvedManifest.Add(($candidate.Name + ' | ' + $profile + ' | ' + $shaBefore))
        Write-Host ('PASS_CANDIDATE=' + $candidate.Name)
    }
    [void](New-Item -ItemType Directory -Path $approved -Force)
    foreach ($entry in $validated) {
        $shaFinal = (Get-FileHash -LiteralPath $entry.File -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($entry.SHA256 -cne $shaFinal) { throw ('CANDIDATE_CHANGED_AFTER_VALIDATION=' + $entry.Name) }
        Copy-Item -LiteralPath $entry.File -Destination (Join-Path $approved $entry.Name)
        $copyHash = (Get-FileHash -LiteralPath (Join-Path $approved $entry.Name) -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($copyHash -cne $entry.SHA256) { throw ('APPROVED_COPY_HASH_MISMATCH=' + $entry.Name) }
    }
    $manifest = @(
        'RESULT=PASS_GA_SRP_PREDELIVERY',
        'ENGINE=Windows PowerShell 5.1',
        ('VALIDATOR_SHA256=' + $officialHash),
        ('APPROVED_COUNT=' + $validated.Count)
    ) + $approvedManifest.ToArray()
    [System.IO.File]::WriteAllLines((Join-Path $approved 'MANIFEST.txt'), $manifest, [System.Text.UTF8Encoding]::new($false))
    Write-Host ('APPROVED_COUNT=' + $validated.Count)
    Write-Host 'RESULT=PASS_GA_SRP_PREDELIVERY'
} catch {
    if (Test-Path -LiteralPath $approved) { Remove-Item -LiteralPath $approved -Recurse -Force }
    Write-Host ('REJECTED=' + $_.Exception.Message)
    Write-Host 'RESULT=FAIL_GA_SRP_PREDELIVERY'
    exit 1
}
