#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$ScriptPath,

    [Parameter(Position = 1)]
    [ValidateSet(
        'General',
        'ReadOnly',
        'QaMutation',
        'FormalClose',
        'Bootstrap')]
    [string]$Profile = 'General',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ScriptArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -ne 5)
{
    throw "GA safe runner requires Windows PowerShell 5.1. Current engine: $($PSVersionTable.PSVersion)"
}

$validator =
    Join-Path `
        $PSScriptRoot `
        'Test-GAScriptReliability.ps1'

if (-not (Test-Path -LiteralPath $validator -PathType Leaf))
{
    throw "GA reliability validator not found: $validator"
}

$resolvedTarget =
    (Resolve-Path -LiteralPath $ScriptPath).Path

Write-Host '=== GA SCRIPT RELIABILITY GATE ==='

& powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File $validator `
    -Path $resolvedTarget `
    -Profile $Profile

if ($LASTEXITCODE -ne 0)
{
    Write-Host 'TARGET_EXECUTED=False'
    Write-Host 'RESULT=FAIL_GA_SAFE_RUNNER'
    exit $LASTEXITCODE
}

Write-Host '=== GA TARGET EXECUTION ==='

& powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File $resolvedTarget `
    @ScriptArguments

$targetExitCode =
    $LASTEXITCODE

Write-Host 'TARGET_EXECUTED=True'
Write-Host "TARGET_EXIT_CODE=$targetExitCode"

if ($targetExitCode -ne 0)
{
    Write-Host 'RESULT=FAIL_GA_SAFE_RUNNER_TARGET'
    exit $targetExitCode
}

Write-Host 'RESULT=PASS_GA_SAFE_RUNNER'
exit 0
