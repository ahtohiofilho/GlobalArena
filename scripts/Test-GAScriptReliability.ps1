#requires -Version 5.1
[CmdletBinding(DefaultParameterSetName = 'Path')]
param(
    [Parameter(
        Mandatory,
        ParameterSetName = 'Path',
        Position = 0)]
    [string]$Path,

    [Parameter(ParameterSetName = 'Path')]
    [ValidateSet(
        'General',
        'ReadOnly',
        'QaMutation',
        'FormalClose',
        'Bootstrap')]
    [string]$Profile = 'General',

    [Parameter(Mandatory, ParameterSetName = 'SelfTest')]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-Violation {
    param(
        [Parameter(Mandatory)][string]$Rule,
        [Parameter(Mandatory)][string]$Message,
        [int]$Line = 0
    )

    [pscustomobject]@{
        Rule = $Rule
        Line = $Line
        Message = $Message
    }
}

function Get-CodeLineRecords {
    param([Parameter(Mandatory)][string]$Text)

    $lines =
        $Text.Replace("`r`n", "`n").
            Replace("`r", "`n").
            Split("`n")

    $insideSingleHereString = $false
    $insideDoubleHereString = $false
    $records = @()

    for ($index = 0;
         $index -lt $lines.Length;
         $index++)
    {
        $line = $lines[$index]
        $trimmed = $line.Trim()
        $lineNumber = $index + 1

        if ($insideSingleHereString)
        {
            if ($trimmed -eq "'@")
            {
                $insideSingleHereString = $false
            }

            continue
        }

        if ($insideDoubleHereString)
        {
            if ($trimmed -eq '"@')
            {
                $insideDoubleHereString = $false
            }

            continue
        }

        if ($line -match "@'`s*$")
        {
            $insideSingleHereString = $true
            $records +=
                [pscustomobject]@{
                    Number = $lineNumber
                    Text = $line
                }

            continue
        }

        if ($line -match '@"`s*$')
        {
            $insideDoubleHereString = $true
            $records +=
                [pscustomobject]@{
                    Number = $lineNumber
                    Text = $line
                }

            continue
        }

        $records +=
            [pscustomobject]@{
                Number = $lineNumber
                Text = $line
            }
    }

    return @($records)
}

function Test-GAScriptText {
    param(
        [Parameter(Mandatory)][string]$ScriptText,
        [Parameter(Mandatory)][string]$DisplayPath,
        [Parameter(Mandatory)][string]$SelectedProfile
    )

    $violations =
        [System.Collections.Generic.List[object]]::new()

    if ($PSVersionTable.PSVersion.Major -ne 5)
    {
        $violations.Add(
            (New-Violation `
                -Rule 'GA-SR-001' `
                -Message "Reliability validation must run under Windows PowerShell 5.1. Current engine: $($PSVersionTable.PSVersion)."))
    }

    [System.Management.Automation.Language.Token[]]$tokens = $null
    [System.Management.Automation.Language.ParseError[]]$parseErrors = $null

    $ast =
        [System.Management.Automation.Language.Parser]::ParseInput(
            $ScriptText,
            [ref]$tokens,
            [ref]$parseErrors)

    foreach ($parseError in @($parseErrors))
    {
        $violations.Add(
            (New-Violation `
                -Rule 'GA-SR-001' `
                -Line $parseError.Extent.StartLineNumber `
                -Message $parseError.Message))
    }

    if ($ScriptText -notmatch '(?im)^\s*#requires\s+-Version\s+5\.1\s*$')
    {
        $violations.Add(
            (New-Violation `
                -Rule 'GA-SR-001' `
                -Message 'Missing exact #requires -Version 5.1 declaration.'))
    }

    $codeLines =
        @(Get-CodeLineRecords -Text $ScriptText)

    $operatorPattern =
        '^\s*-(eq|ne|gt|ge|lt|le|like|notlike|match|notmatch|contains|notcontains|in|notin|is|isnot|and|or|xor|not|band|bor|bxor|shl|shr)\b'

    foreach ($record in $codeLines)
    {
        if ($record.Text -match $operatorPattern)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-002' `
                    -Line $record.Number `
                    -Message 'PowerShell operator begins a code line. Keep the operator on the preceding expression line.'))
        }

        if ($record.Text -match '-(Old|New)\s+''[^'']*`n[^'']*''')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-004' `
                    -Line $record.Number `
                    -Message 'Single-quoted replacement anchor contains a literal `n escape. Use a here-string and LF normalization.'))
        }

        if ($record.Text -match 'New-Object\s+System\.Collections\.Generic\.List\[object\]')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-019' `
                    -Line $record.Number `
                    -Message 'Do not construct List[object] with New-Object; use constructor form and ToArray() for materialization.'))
        }

        if ($record.Text -match '(?<!@)\(Get-ChildItem[^\)]*\)\.Count')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-020' `
                    -Line $record.Number `
                    -Message 'Wrap zero/one/many Get-ChildItem output in @(...) before using .Count.'))
        }

        if (($record.Text -match '\s+$') -and
            ($record.Text.Length -gt 0))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-012' `
                    -Line $record.Number `
                    -Message 'Trailing whitespace detected.'))
        }
    }

    for ($recordIndex = 0;
         $recordIndex -lt $codeLines.Count;
         $recordIndex++)
    {
        $recordText =
            $codeLines[$recordIndex].Text

        $isObservedNativeStderrRisk =
            ($recordText -match
                '&\s*dotnet(?:\.exe)?\s+run\b') -or
            ($recordText -match
                '&\s*git(?:\.exe)?\s+push\b')

        if (-not $isObservedNativeStderrRisk)
        {
            continue
        }

        $windowEnd =
            [Math]::Min(
                $recordIndex + 12,
                $codeLines.Count - 1)

        $windowLines =
            @(
                for ($windowIndex = $recordIndex;
                     $windowIndex -le $windowEnd;
                     $windowIndex++)
                {
                    $codeLines[$windowIndex].Text
                }
            )

        $windowText =
            $windowLines -join "`n"

        if ($windowText -match '2>&1')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-024' `
                    -Line $codeLines[$recordIndex].Number `
                    -Message 'Observed native stderr-risk command must redirect stdout/stderr durably instead of merging through 2>&1.'))

            break
        }
    }
    $startProcessCommands =
        @(
            $ast.FindAll(
                {
                    param($node)

                    if ($node -isnot
                        [System.Management.Automation.Language.CommandAst])
                    {
                        return $false
                    }

                    return $node.GetCommandName() -eq
                        'Start-Process'
                },
                $true)
        )

    foreach ($startProcessCommand in $startProcessCommands)
    {
        $hasWaitParameter =
            @(
                $startProcessCommand.CommandElements |
                    Where-Object {
                        $_ -is
                            [System.Management.Automation.Language.CommandParameterAst] -and
                        $_.ParameterName -eq
                            'Wait'
                    }
            ).Count -gt 0

        if ($hasWaitParameter)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-025' `
                    -Line $startProcessCommand.Extent.StartLineNumber `
                    -Message 'Do not use Start-Process -Wait; launch with -PassThru and wait on the exact returned Process with a finite WaitForExit(timeout).'))
        }
    }

    $capturedDiffCheckAssignments =
        @(
            $ast.FindAll(
                {
                    param($node)

                    if ($node -isnot
                        [System.Management.Automation.Language.AssignmentStatementAst])
                    {
                        return $false
                    }

                    if ($node.Left -isnot
                        [System.Management.Automation.Language.VariableExpressionAst])
                    {
                        return $false
                    }

                    $rightText =
                        $node.Right.Extent.Text

                    $isBoundedProcess =
                        $rightText -match
                            '(?i)\bInvoke-BoundedProcess\b'

                    $isGit =
                        $rightText -match
                            '(?is)-FilePath\s+[''"]git[''"]'

                    $isDiffCheck =
                        ($rightText -match
                            '(?is)[''"]diff[''"]') -and
                        ($rightText -match
                            '(?is)[''"]--check[''"]')

                    return $isBoundedProcess -and
                        $isGit -and
                        $isDiffCheck
                },
                $true)
        )

    foreach ($capturedDiffCheckAssignment in
        $capturedDiffCheckAssignments)
    {
        $resultVariableName =
            $capturedDiffCheckAssignment.Left.VariablePath.UserPath

        $escapedResultVariable =
            [System.Text.RegularExpressions.Regex]::Escape(
                '$' + $resultVariableName)

        $inspectsCapturedDiagnostics =
            ($ScriptText -match
                ($escapedResultVariable + '\.Stdout\b')) -or
            ($ScriptText -match
                ($escapedResultVariable + '\.Stderr\b'))

        if (-not $inspectsCapturedDiagnostics)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-026' `
                    -Line $capturedDiffCheckAssignment.Extent.StartLineNumber `
                    -Message 'Captured git diff --check result is evaluated without inspecting captured stdout/stderr diagnostics; diagnostics are authoritative in addition to process state.'))
        }
    }

    $variableSpellingsByScope = @{}

    $variableExpressions =
        @(
            $ast.FindAll(
                {
                    param($node)

                    $node -is
                        [System.Management.Automation.Language.VariableExpressionAst]
                },
                $true)
        )

    foreach ($variableExpression in $variableExpressions)
    {
        $scopeAst =
            $variableExpression

        while ($null -ne $scopeAst -and
            $scopeAst -isnot
                [System.Management.Automation.Language.ScriptBlockAst])
        {
            $scopeAst =
                $scopeAst.Parent
        }

        if ($null -eq $scopeAst)
        {
            continue
        }

        $name =
            $variableExpression.VariablePath.UserPath

        $scopeKey =
            "$($scopeAst.Extent.StartOffset):$($scopeAst.Extent.EndOffset)"

        $key =
            $scopeKey +
            '|' +
            $name.ToLowerInvariant()

        if (-not $variableSpellingsByScope.ContainsKey(
            $key))
        {
            $variableSpellingsByScope[$key] =
                [System.Collections.Generic.HashSet[string]]::new()
        }

        [void]$variableSpellingsByScope[$key].Add(
            $name)
    }

    foreach ($entry in $variableSpellingsByScope.GetEnumerator())
    {
        if ($entry.Value.Count -gt 1)
        {
            $spellings =
                @($entry.Value | Sort-Object)

            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-005' `
                    -Message ("Variable differs only by case inside the same PowerShell scope: " + ($spellings -join ', '))))
        }
    }

    $tokenArray =
        @($tokens)

    for ($index = 0;
         $index -lt $tokenArray.Count - 1;
         $index++)
    {
        $current = $tokenArray[$index]
        $next = $tokenArray[$index + 1]

        if (($current.Text -eq 'checked') -and
            ($next.Text -eq '('))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-006' `
                    -Line $current.Extent.StartLineNumber `
                    -Message 'C# checked(...) syntax appears in executable PowerShell.'))
        }
    }

    $normalized =
        $ScriptText.Replace("`r`n", "`n").
            Replace("`r", "`n")

    $gitCommandPrefix =
        '(?im)(?:^|[@(])\s*&?\s*git(?:\.exe)?\b[^\r\n]*\b'

    $hasCommit =
        $normalized -match
            ($gitCommandPrefix + 'commit\b')

    $hasPush =
        $normalized -match
            ($gitCommandPrefix + 'push\b')

    $hasGitAdd =
        $normalized -match
            ($gitCommandPrefix + 'add\b')

    $hasGitReset =
        $normalized -match
            ($gitCommandPrefix + 'reset\b')

    $hasGitRestore =
        $normalized -match
            ($gitCommandPrefix + 'restore\b')

    if ($SelectedProfile -eq 'ReadOnly')
    {
        if ($hasCommit -or
            $hasPush -or
            $hasGitAdd -or
            $hasGitReset -or
            $hasGitRestore)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-014' `
                    -Message 'ReadOnly profile contains a Git mutation command.'))
        }
    }

    if (($SelectedProfile -eq 'QaMutation') -or
        ($SelectedProfile -eq 'Bootstrap'))
    {
        if ($hasCommit -or
            $hasPush)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-014' `
                    -Message "$SelectedProfile profile must not commit or push."))
        }

        if ($normalized -notmatch 'git[^\r\n]*diff[^\r\n]*--check')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-012' `
                    -Message "$SelectedProfile profile must include git diff --check."))
        }

        if (($normalized -notmatch 'git[^\r\n]*diff[^\r\n]*--cached[^\r\n]*--check') -and
            ($normalized -notmatch 'git[^\r\n]*diff[^\r\n]*--check[^\r\n]*--cached'))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-012' `
                    -Message "$SelectedProfile profile must include git diff --cached --check."))
        }

        if ($normalized -notmatch '(?i)(rollback|restore-baseline|restore-approved|restore-)')
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-008' `
                    -Message "$SelectedProfile profile must expose rollback/restore behavior."))
        }

        if (($normalized -notmatch 'EVIDENCE_ZIP') -or
            ($normalized -notmatch 'FAILURE_EVIDENCE_ZIP'))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-010' `
                    -Message "$SelectedProfile profile must expose PASS and FAIL one-ZIP evidence markers."))
        }
    }

    if ($SelectedProfile -eq 'FormalClose')
    {
        if ((-not $hasCommit) -or
            (-not $hasPush))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-014' `
                    -Message 'FormalClose profile must contain commit and push operations.'))
        }

        if (($normalized -notmatch 'EVIDENCE_ZIP') -or
            ($normalized -notmatch 'FAILURE_EVIDENCE_ZIP'))
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-010' `
                    -Message 'FormalClose profile must expose PASS and FAIL one-ZIP evidence markers.'))
        }
    }

    return [pscustomobject]@{
        DisplayPath = $DisplayPath
        Profile = $SelectedProfile
        ParseErrorCount = @($parseErrors).Count
        Violations = $violations.ToArray()
    }
}

function Invoke-SelfTest {
    $script:testsPassed = 0
    $script:testsFailed = 0

    function Assert-Case {
        param(
            [Parameter(Mandatory)][string]$Name,
            [Parameter(Mandatory)][string]$Text,
            [Parameter(Mandatory)][string]$SelectedProfile,
            [Parameter(Mandatory)][bool]$ShouldPass,
            [string]$ExpectedRule = ''
        )

        $result =
            Test-GAScriptText `
                -ScriptText $Text `
                -DisplayPath $Name `
                -SelectedProfile $SelectedProfile

        if ($ShouldPass)
        {
            $passed =
                $result.Violations.Count -eq 0
        }
        else
        {
            $passed =
                $result.Violations.Count -gt 0 -and
                ([string]::IsNullOrEmpty(
                    $ExpectedRule) -or
                    $result.Violations.Rule -contains $ExpectedRule)
        }

        if ($passed)
        {
            $script:testsPassed++
            Write-Host "SELFTEST_PASS=$Name"
        }
        else
        {
            $script:testsFailed++
            Write-Host "SELFTEST_FAIL=$Name"

            foreach ($violation in $result.Violations)
            {
                Write-Host "  $($violation.Rule) line=$($violation.Line) $($violation.Message)"
            }
        }
    }

    $good = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$value = 1
if ($value -ne 2) {
    Write-Host 'ok'
}
'@

    Assert-Case `
        -Name 'general-good' `
        -Text $good `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $leadingOperator = @'
#requires -Version 5.1
$value = 1
if (($value -eq 1)
    -or ($value -eq 2)) {
    Write-Host 'bad'
}
'@

    Assert-Case `
        -Name 'leading-operator' `
        -Text $leadingOperator `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-002'

    $caseCollision = @'
#requires -Version 5.1
$ProbeProgram = 'a'
$probeProgram = 'b'
'@

    Assert-Case `
        -Name 'case-collision' `
        -Text $caseCollision `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-005'

    $caseAcrossScopesSafe = @'
#requires -Version 5.1
function Show-InnerName {
    param([string]$Name)

    Write-Host $Name
}

$name = 'outer'
Show-InnerName -Name $name
'@

    Assert-Case `
        -Name 'case-difference-across-scopes-safe' `
        -Text $caseAcrossScopesSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $checkedExpression = @'
#requires -Version 5.1
$value = checked(1 + 1)
'@

    Assert-Case `
        -Name 'checked-expression' `
        -Text $checkedExpression `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-006'

    $badAnchor = @'
#requires -Version 5.1
$value = Replace-Unique -Old 'Header:`n`nValue' -New 'Replacement'
'@

    Assert-Case `
        -Name 'single-quoted-newline-anchor' `
        -Text $badAnchor `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-004'

    $readOnlyMutation = @'
#requires -Version 5.1
& git -c core.safecrlf=false add -- docs/file.md
'@

    Assert-Case `
        -Name 'readonly-git-mutation' `
        -Text $readOnlyMutation `
        -SelectedProfile 'ReadOnly' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-014'

    $listObjectTrap = @'
#requires -Version 5.1
$items = New-Object System.Collections.Generic.List[object]
'@

    Assert-Case `
        -Name 'new-object-list-object' `
        -Text $listObjectTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-019'

    $directCountTrap = @'
#requires -Version 5.1
if ((Get-ChildItem -LiteralPath '.').Count -eq 0) {
    Write-Host 'empty'
}
'@

    Assert-Case `
        -Name 'direct-get-childitem-count' `
        -Text $directCountTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-020'

    $normalizedCountSafe = @'
#requires -Version 5.1
if (@(Get-ChildItem -LiteralPath '.').Count -eq 0) {
    Write-Host 'empty'
}
'@

    Assert-Case `
        -Name 'normalized-get-childitem-count-safe' `
        -Text $normalizedCountSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $nativeStderrTrap = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$output = @(
    & dotnet run `
        --project 'probe.csproj' `
        -c Release 2>&1
)
'@

    Assert-Case `
        -Name 'native-dotnet-run-stderr-merge' `
        -Text $nativeStderrTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-024'

    $nativeStderrSafe = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$process = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', 'probe.csproj', '-c', 'Release') `
    -PassThru `
    -RedirectStandardOutput 'stdout.log' `
    -RedirectStandardError 'stderr.log'

$completed = $process.WaitForExit(600000)

if (-not $completed) {
    throw 'probe timeout'
}

$process.WaitForExit()
$process.Refresh()

if ($process.ExitCode -ne 0) {
    throw 'probe failed'
}
'@

    Assert-Case `
        -Name 'native-dotnet-run-durable-redirection-safe' `
        -Text $nativeStderrSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $nativeGitPushTrap = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$output = @(
    & git push origin main 2>&1
)
'@

    Assert-Case `
        -Name 'native-git-push-stderr-merge' `
        -Text $nativeGitPushTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-024'

    $nativeGitPushSafe = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$process = Start-Process `
    -FilePath 'git' `
    -ArgumentList @('push', 'origin', 'main') `
    -PassThru `
    -RedirectStandardOutput 'stdout.log' `
    -RedirectStandardError 'stderr.log'

$completed = $process.WaitForExit(600000)

if (-not $completed) {
    throw 'push timeout'
}

$process.WaitForExit()
$process.Refresh()

if ($process.ExitCode -ne 0) {
    throw 'push failed'
}
'@

    Assert-Case `
        -Name 'native-git-push-durable-redirection-safe' `
        -Text $nativeGitPushSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $formalCloseInlineGitSafe = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$commitOutput = @(& git commit -m 'fixture')
$pushOutput = @(& git push origin main)
Write-Host 'EVIDENCE_ZIP=fixture.zip'
Write-Host 'FAILURE_EVIDENCE_ZIP=fixture-fail.zip'
'@

    Assert-Case `
        -Name 'formalclose-inline-git-detection-safe' `
        -Text $formalCloseInlineGitSafe `
        -SelectedProfile 'FormalClose' `
        -ShouldPass $true

    $startProcessWaitTrap = @'
#requires -Version 5.1
$process = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('build', 'GlobalArena.slnx', '-c', 'Release') `
    -Wait `
    -PassThru `
    -RedirectStandardOutput 'stdout.log' `
    -RedirectStandardError 'stderr.log'
'@

    Assert-Case `
        -Name 'start-process-process-tree-wait' `
        -Text $startProcessWaitTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-025'

    $exactProcessWaitSafe = @'
#requires -Version 5.1
$process = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('build', 'GlobalArena.slnx', '-c', 'Release') `
    -PassThru `
    -RedirectStandardOutput 'stdout.log' `
    -RedirectStandardError 'stderr.log'

$completed = $process.WaitForExit(600000)

if (-not $completed) {
    throw 'native process timeout'
}

$process.WaitForExit()
$process.Refresh()

if ($process.ExitCode -ne 0) {
    throw 'native process failed'
}
'@

    Assert-Case `
        -Name 'exact-process-bounded-wait-safe' `
        -Text $exactProcessWaitSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $capturedDiffCheckExitOnlyTrap = @'
#requires -Version 5.1
$diffCheck = Invoke-BoundedProcess `
    -FilePath 'git' `
    -ArgumentList @(
        '-c',
        'core.safecrlf=false',
        'diff',
        '--cached',
        '--check'
    ) `
    -Name 'git-cached-diff-check'

if ($diffCheck.ExitCode -ne 0) {
    throw 'git diff --cached --check failed'
}
'@

    Assert-Case `
        -Name 'captured-diff-check-exitcode-only' `
        -Text $capturedDiffCheckExitOnlyTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-026'

    $capturedDiffCheckDiagnosticsSafe = @'
#requires -Version 5.1
$diffCheck = Invoke-BoundedProcess `
    -FilePath 'git' `
    -ArgumentList @(
        '-c',
        'core.safecrlf=false',
        'diff',
        '--cached',
        '--check'
    ) `
    -Name 'git-cached-diff-check'

$diffCheckText =
    $diffCheck.Stdout +
    "`n" +
    $diffCheck.Stderr

$diagnostics =
    @(
        $diffCheckText -split "`n" |
            Where-Object {
                [string]::IsNullOrWhiteSpace("$_") -eq $false
            }
    )

if (($diffCheck.ExitCode -ne 0) -or
    ($diagnostics.Count -ne 0)) {
    throw 'git diff --cached --check failed'
}
'@

    Assert-Case `
        -Name 'captured-diff-check-diagnostics-safe' `
        -Text $capturedDiffCheckDiagnosticsSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    if ($script:testsFailed -ne 0)
    {
        Write-Host "SELFTESTS_PASSED=$($script:testsPassed)"
        Write-Host "SELFTESTS_FAILED=$($script:testsFailed)"
        Write-Host 'RESULT=FAIL_GA_SCRIPT_RELIABILITY_SELFTEST'
        exit 1
    }

    Write-Host "SELFTESTS_PASSED=$($script:testsPassed)"
    Write-Host 'SELFTESTS_FAILED=0'
    Write-Host 'RESULT=PASS_GA_SCRIPT_RELIABILITY_SELFTEST'
    exit 0
}

if ($SelfTest)
{
    Invoke-SelfTest
}

if ($PSVersionTable.PSVersion.Major -ne 5)
{
    Write-Host "ENGINE_VERSION=$($PSVersionTable.PSVersion)"
    Write-Host 'RESULT=FAIL_GA_SCRIPT_RELIABILITY'
    Write-Host 'GA-SR-001: validator must run under Windows PowerShell 5.1.'
    exit 1
}

$resolvedPath =
    (Resolve-Path -LiteralPath $Path).Path

$scriptText =
    [System.IO.File]::ReadAllText(
        $resolvedPath,
        [System.Text.UTF8Encoding]::new($false))

$result =
    Test-GAScriptText `
        -ScriptText $scriptText `
        -DisplayPath $resolvedPath `
        -SelectedProfile $Profile

Write-Host "SCRIPT=$resolvedPath"
Write-Host "PROFILE=$Profile"
Write-Host "ENGINE_VERSION=$($PSVersionTable.PSVersion)"
Write-Host "PARSER_ERRORS=$($result.ParseErrorCount)"
Write-Host "RULE_VIOLATIONS=$($result.Violations.Count)"

foreach ($violation in $result.Violations)
{
    if ($violation.Line -gt 0)
    {
        $lineText =
            " line=$($violation.Line)"
    }
    else
    {
        $lineText = ''
    }

    Write-Host "$($violation.Rule)$lineText $($violation.Message)"
}

if ($result.Violations.Count -ne 0)
{
    Write-Host 'RESULT=FAIL_GA_SCRIPT_RELIABILITY'
    exit 1
}

Write-Host 'RESULT=PASS_GA_SCRIPT_RELIABILITY'
exit 0
