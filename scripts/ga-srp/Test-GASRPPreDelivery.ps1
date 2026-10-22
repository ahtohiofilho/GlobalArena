#requires -Version 5.1
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -ne 5) { throw 'GA_SRP_REQUIRES_WINDOWS_POWERSHELL_5_1' }
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$validator = Join-Path $repo 'scripts\Test-GAScriptReliability.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw 'OFFICIAL_GA_SRP_VALIDATOR_MISSING' }
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('GA-SRP2-REGRESSION-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$passed = 0
try {
    $good = @'
#requires -Version 5.1
$unstaged = @(& git.exe diff --check)
$staged = @(& git.exe diff --cached --check)
function Rollback-OwnFiles { return }
Write-Host 'EVIDENCE_ZIP=example'
Write-Host 'FAILURE_EVIDENCE_ZIP=example'
'@
    $fixtures = @(
        [pscustomobject]@{ Name='qa-contract-accept'; Text=$good; Profile='QaMutation'; Expected='PASS' },
        [pscustomobject]@{ Name='ga-sr-002-reject'; Text=($good + "`n" + '    -not $true' + "`n"); Profile='QaMutation'; Expected='GA-SR-002' },
        [pscustomobject]@{ Name='ga-sr-025-reject'; Text=($good + "`n" + ('Start-Process -FilePath cmd.exe ' + '-Wait') + "`n"); Profile='QaMutation'; Expected='GA-SR-025' },
        [pscustomobject]@{ Name='ga-sr-012-reject'; Text=$good.Replace('git.exe diff --cached --check','git.exe status'); Profile='QaMutation'; Expected='GA-SR-012' },
        [pscustomobject]@{ Name='ga-sr-008-reject'; Text=$good.Replace('Rollback-OwnFiles','Undo-OwnFiles'); Profile='QaMutation'; Expected='GA-SR-008' },
        [pscustomobject]@{ Name='ga-sr-010-reject'; Text=$good.Replace('FAILURE_EVIDENCE_ZIP','FAILURE_ZIP'); Profile='QaMutation'; Expected='GA-SR-010' },
        [pscustomobject]@{ Name='ga-sr-014-reject'; Text=($good + "`n" + "& git.exe commit -m 'invalid'" + "`n"); Profile='QaMutation'; Expected='GA-SR-014' },
        [pscustomobject]@{ Name='readonly-contract-accept'; Text="#requires -Version 5.1`n`$x = 1`n"; Profile='ReadOnly'; Expected='PASS' }
    )
    foreach ($fixture in $fixtures) {
        $file = Join-Path $work ($fixture.Name + '.ps1')
        [System.IO.File]::WriteAllText($file, [string]$fixture.Text, [System.Text.UTF8Encoding]::new($false))
        $lines = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator -Path $file -Profile $fixture.Profile)
        $code = [int]$LASTEXITCODE
        $detail = $lines -join "`n"
        if ($fixture.Expected -eq 'PASS') {
            if ($code -ne 0 -or $detail -notmatch 'RESULT=PASS') {
                throw ('REGRESSION_POSITIVE_FAILED_' + $fixture.Name + "`n" + $detail)
            }
        } else {
            if ($code -eq 0 -or (-not $detail.Contains([string]$fixture.Expected))) {
                throw ('REGRESSION_NEGATIVE_FAILED_' + $fixture.Name + "`n" + $detail)
            }
        }
        $passed++
        Write-Host ('PASS_REGRESSION=' + $fixture.Name)
    }
    # Reproduce the real R21/R22 incident when both are available locally.
    $downloads = Join-Path $HOME 'Downloads'
    $historical = @(
        [pscustomobject]@{ Name='R21'; Path=(Join-Path $downloads 'GA-UI-TACTICAL-PREFETCH-DATA-PLANNER-QA-R21.ps1'); Expected='GA-SR-002' },
        [pscustomobject]@{ Name='R22'; Path=(Join-Path $downloads 'GA-UI-TACTICAL-PREFETCH-DATA-PLANNER-QA-R22.ps1'); Expected='PASS' }
    )
    foreach ($item in $historical) {
        if (-not (Test-Path -LiteralPath $item.Path -PathType Leaf)) {
            Write-Host ('OPTIONAL_HISTORICAL_NOT_FOUND=' + $item.Name)
            continue
        }
        $lines = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator -Path $item.Path -Profile QaMutation)
        $code = [int]$LASTEXITCODE
        $detail = $lines -join "`n"
        if ($item.Expected -eq 'PASS') {
            if ($code -ne 0 -or $detail -notmatch 'RESULT=PASS') {
                throw ('HISTORICAL_GOOD_FAILED_' + $item.Name + "`n" + $detail)
            }
        } else {
            if ($code -eq 0 -or (-not $detail.Contains([string]$item.Expected))) {
                throw ('HISTORICAL_BAD_WAS_ACCEPTED_' + $item.Name + "`n" + $detail)
            }
        }
        $passed++
        Write-Host ('PASS_HISTORICAL=' + $item.Name)
    }
    Write-Host ('REGRESSIONS_PASSED=' + $passed)
    Write-Host 'RESULT=PASS_GA_SRP_PREDELIVERY_REGRESSIONS'
} catch {
    Write-Host ('REGRESSION_ERROR=' + $_.Exception.Message)
    Write-Host 'RESULT=FAIL_GA_SRP_PREDELIVERY_REGRESSIONS'
    exit 1
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
