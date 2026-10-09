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

    $arithmeticContinuationOperatorPattern =
        '^\s*[+\-*/%]\s+'

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

        if ($record.Text -match $arithmeticContinuationOperatorPattern)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-027' `
                    -Line $record.Number `
                    -Message 'Arithmetic binary operator begins a continuation line. Keep +, -, *, / and % on the preceding expression line in PS5.1 automation.'))
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

    $containsLiteralCommands =
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
                        'Assert-ContainsLiteral'
                },
                $true)
        )

    foreach ($containsLiteralCommand in
        $containsLiteralCommands)
    {
        $elements =
            @(
                $containsLiteralCommand.CommandElements
            )

        for ($elementIndex = 0;
             $elementIndex -lt $elements.Count - 1;
             $elementIndex++)
        {
            $element =
                $elements[$elementIndex]

            if ($element -isnot
                [System.Management.Automation.Language.CommandParameterAst])
            {
                continue
            }

            if ($element.ParameterName -ne
                'Literal')
            {
                continue
            }

            $valueAst =
                $elements[$elementIndex + 1]

            $literalValue = $null

            if ($valueAst -is
                [System.Management.Automation.Language.StringConstantExpressionAst])
            {
                $literalValue =
                    $valueAst.Value
            }
            elseif ($valueAst -is
                [System.Management.Automation.Language.ExpandableStringExpressionAst])
            {
                $literalValue =
                    $valueAst.Value
            }

            if ($null -eq $literalValue)
            {
                continue
            }

            if ($literalValue -match
                '\b[a-z_][A-Za-z0-9_]*\.[A-Z_][A-Za-z0-9_]*\b')
            {
                $violations.Add(
                    (New-Violation `
                        -Rule 'GA-SR-028' `
                        -Line $containsLiteralCommand.Extent.StartLineNumber `
                        -Message 'Exact semantic audit literal contains a C#-style member chain that may be split by formatting. Assert atomic tokens or use a whitespace-tolerant semantic pattern.'))

                break
            }
        }
    }

    $containsAllCommands =
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
                        'Assert-ContainsAll'
                },
                $true)
        )

    foreach ($containsAllCommand in $containsAllCommands)
    {
        $elements =
            @(
                $containsAllCommand.CommandElements
            )

        for ($elementIndex = 0;
             $elementIndex -lt $elements.Count - 1;
             $elementIndex++)
        {
            $element =
                $elements[$elementIndex]

            if ($element -isnot
                [System.Management.Automation.Language.CommandParameterAst])
            {
                continue
            }

            if ($element.ParameterName -ne
                'Tokens')
            {
                continue
            }

            $tokenValueAst =
                $elements[$elementIndex + 1]

            $tokenStringNodes =
                @(
                    $tokenValueAst.FindAll(
                        {
                            param($node)

                            return ($node -is
                                [System.Management.Automation.Language.StringConstantExpressionAst]) -or
                                ($node -is
                                    [System.Management.Automation.Language.ExpandableStringExpressionAst])
                        },
                        $true)
                )

            foreach ($tokenStringNode in $tokenStringNodes)
            {
                $tokenValue =
                    $tokenStringNode.Value.Trim()

                if ([string]::IsNullOrWhiteSpace(
                    $tokenValue))
                {
                    continue
                }

                $isStableHeading =
                    $tokenValue -match
                        '^#{1,6}\s+\S'

                $isStructuredField =
                    $tokenValue -match
                        '^[A-Z][A-Z0-9_.-]*=[^\s].*$'

                $isStableStatusField =
                    $tokenValue -match
                        '^\*\*Status:\*\*\s+\S'

                $wordLikeCount =
                    [System.Text.RegularExpressions.Regex]::Matches(
                        $tokenValue,
                        '[A-Za-z][A-Za-z0-9/_-]*').
                        Count

                $isIncidentalProse =
                    ($wordLikeCount -ge 4) -and
                    (-not $isStableHeading) -and
                    (-not $isStructuredField) -and
                    (-not $isStableStatusField)

                if ($isIncidentalProse)
                {
                    $violations.Add(
                        (New-Violation `
                            -Rule 'GA-SR-039' `
                            -Line $containsAllCommand.Extent.StartLineNumber `
                            -Message 'Assert-ContainsAll -Tokens contains a prose-like exact literal. Use atomic stable contract tokens, an exact stable heading/field, or case/whitespace-tolerant semantic matching.'))

                    break
                }
            }
        }
    }

    $xunitSingleWherePattern =
        '(?is)Assert\.Single\s*\(\s*.{0,800}?\.Where\s*\('

    $stringPayloadNodes =
        @(
            $ast.FindAll(
                {
                    param($node)

                    return ($node -is
                        [System.Management.Automation.Language.StringConstantExpressionAst]) -or
                        ($node -is
                            [System.Management.Automation.Language.ExpandableStringExpressionAst])
                },
                $true)
        )

    foreach ($stringPayloadNode in $stringPayloadNodes)
    {
        $payloadText =
            $stringPayloadNode.Value

        $isLikelyXunitSource =
            ($payloadText -match
                '(?m)^\s*\[(Fact|Theory)\]\s*$') -and
            (($payloadText -match
                '(?m)^\s*(public\s+)?(sealed\s+)?class\b') -or
             ($payloadText -match
                '(?m)^\s*namespace\s+'))

        if (-not $isLikelyXunitSource)
        {
            continue
        }

        if ($payloadText -match $xunitSingleWherePattern)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-029' `
                    -Line $stringPayloadNode.Extent.StartLineNumber `
                    -Message 'Generated xUnit source uses the analyzer-hostile Assert.Single plus Where pattern; use the Assert.Single collection-and-predicate overload to avoid xUnit2031.'))

            break
        }
    }

    $ps51UnsupportedUnsignedTypeAsts =
        @(
            $ast.FindAll(
                {
                    param($node)

                    $isTypeNode =
                        ($node -is
                            [System.Management.Automation.Language.TypeConstraintAst]) -or
                        ($node -is
                            [System.Management.Automation.Language.TypeExpressionAst])

                    if (-not $isTypeNode)
                    {
                        return $false
                    }

                    return [string]::Equals(
                        $node.TypeName.FullName,
                        'ulong',
                        [System.StringComparison]::OrdinalIgnoreCase)
                },
                $true)
        )

    foreach ($unsupportedUnsignedTypeAst in
        $ps51UnsupportedUnsignedTypeAsts)
    {
        $violations.Add(
            (New-Violation `
                -Rule 'GA-SR-032' `
                -Line $unsupportedUnsignedTypeAst.Extent.StartLineNumber `
                -Message 'The [ulong] accelerator is not available in Windows PowerShell 5.1. Use [uint64] or [System.UInt64].'))
    }

    $functionDefinitions =
        @(
            $ast.FindAll(
                {
                    param($node)

                    $node -is
                        [System.Management.Automation.Language.FunctionDefinitionAst]
                },
                $true)
        )

    foreach ($functionDefinition in $functionDefinitions)
    {
        if (-not [string]::Equals(
            $functionDefinition.Name,
            'git',
            [System.StringComparison]::OrdinalIgnoreCase))
        {
            continue
        }

        $nestedGitCommands =
            @(
                $functionDefinition.Body.FindAll(
                    {
                        param($node)

                        if ($node -isnot
                            [System.Management.Automation.Language.CommandAst])
                        {
                            return $false
                        }

                        $nestedCommandName =
                            $node.GetCommandName()

                        return (-not [string]::IsNullOrWhiteSpace(
                            $nestedCommandName)) -and
                            [string]::Equals(
                                $nestedCommandName,
                                'git',
                                [System.StringComparison]::OrdinalIgnoreCase)
                    },
                    $true)
            )

        if ($nestedGitCommands.Count -gt 0)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-034' `
                    -Line $functionDefinition.Extent.StartLineNumber `
                    -Message 'PowerShell function Git shadows the native git executable it invokes. Use a distinct wrapper name such as Invoke-GitLines and resolve git.exe explicitly when needed.'))
        }
    }

    $trxPathFileNames =
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

                    return $node.Left.VariablePath.UserPath -match
                        '(?i)TrxPath$'
                },
                $true) |
                ForEach-Object {
                    $assignment = $_

                    $strings =
                        @(
                            $assignment.Right.FindAll(
                                {
                                    param($node)

                                    return ($node -is
                                        [System.Management.Automation.Language.StringConstantExpressionAst]) -or
                                        ($node -is
                                            [System.Management.Automation.Language.ExpandableStringExpressionAst])
                                },
                                $true)
                        )

                    foreach ($stringNode in $strings)
                    {
                        if ($stringNode.Value -match
                            '(?i)([^\\/]+\.trx)$')
                        {
                            $matches[1]
                        }
                    }
                } |
                Sort-Object -Unique
        )

    $trxLoggerFileNames =
        @(
            $stringPayloadNodes |
                ForEach-Object {
                    if ($_.Value -match
                        '(?i)^trx;LogFileName=([^\\/]+\.trx)$')
                    {
                        $matches[1]
                    }
                } |
                Sort-Object -Unique
        )

    if (($trxPathFileNames.Count -gt 0) -and
        ($trxLoggerFileNames.Count -gt 0))
    {
        $trxNamesDiffer =
            ($trxPathFileNames.Count -ne
                $trxLoggerFileNames.Count)

        if (-not $trxNamesDiffer)
        {
            for ($trxIndex = 0;
                 $trxIndex -lt $trxPathFileNames.Count;
                 $trxIndex++)
            {
                if (-not [string]::Equals(
                    $trxPathFileNames[$trxIndex],
                    $trxLoggerFileNames[$trxIndex],
                    [System.StringComparison]::OrdinalIgnoreCase))
                {
                    $trxNamesDiffer = $true
                    break
                }
            }
        }

        if ($trxNamesDiffer)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-035' `
                    -Message 'TRX evidence path names disagree with trx;LogFileName producer names. Derive revision-specific artifact names from one shared source of truth.'))
        }
    }

    foreach ($functionDefinition in $functionDefinitions)
    {
        $functionParameters = @()

        if ($null -ne
            $functionDefinition.Parameters)
        {
            $functionParameters +=
                @(
                    $functionDefinition.Parameters
                )
        }

        if ($null -ne
            $functionDefinition.Body.ParamBlock)
        {
            $functionParameters +=
                @(
                    $functionDefinition.Body.ParamBlock.Parameters
                )
        }

        foreach ($functionParameter in $functionParameters)
        {
            $parameterName =
                $functionParameter.Name.VariablePath.UserPath

            if ([string]::Equals(
                $parameterName,
                'args',
                [System.StringComparison]::OrdinalIgnoreCase))
            {
                $violations.Add(
                    (New-Violation `
                        -Rule 'GA-SR-036' `
                        -Line $functionParameter.Extent.StartLineNumber `
                        -Message 'Function parameter $Args shadows the PowerShell automatic $args variable. Use a distinct name such as $ArgumentList.'))

                break
            }
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

    # GA-SR-014 WRAPPER-CONTRACT COVERAGE
    # Fail closed unless the one actual wrapper definition has an approved body.
    $gitLinesDefinitions = @(
        $ast.FindAll(
            {
                param($node)
                ($node -is [System.Management.Automation.Language.FunctionDefinitionAst]) -and
                ($node.Name -eq 'Invoke-GitLines')
            },
            $true)
    )
    $approvedGitLinesWrapper = $false
    if ($gitLinesDefinitions.Count -eq 1)
    {
        $wrapperBody = $gitLinesDefinitions[0].Body.Extent.Text
        $wrapperBody = $wrapperBody.Replace("`r`n", "`n").Replace("`r", "`n")
        # Both the fixture and the reviewed FormalClose transport are allowed.
        # No additional statements, nested functions, or overriding body allowed.
        $wrapperPattern = '(?s)^\{\s*\$out\s*=\s*&\s*git\.exe\s+@argv\s+2>&1\s*(?:;|\n)\s*(?:if\s*\(\s*\$LASTEXITCODE\s+-ne\s+0\s*\)\s*\{\s*throw\s*\(\s*''git ''\s*\+\s*\(\s*\$argv\s+-join\s+'' ''\s*\)\s*\+\s*'' failed: ''\s*\+\s*\(\s*\$out\s+-join\s+"`n"\s*\)\s*\)\s*\}\s*(?:;|\n)\s*)?return\s+,@\(\$out\)\s*\}$'
        $approvedGitLinesWrapper = $wrapperBody -match $wrapperPattern
    }
    if ($approvedGitLinesWrapper)
    {
        $gitLinesInvocations = @(
            $ast.FindAll(
                {
                    param($node)
                    ($node -is [System.Management.Automation.Language.CommandAst]) -and
                    ($node.GetCommandName() -eq 'Invoke-GitLines')
                },
                $true)
        )
        foreach ($gitLinesInvocation in $gitLinesInvocations)
        {
            # GA-SR-014-AUD-002: Calls inside a function definition do not
            # establish that the FormalClose flow invokes commit or push.
            # Conservative scope: only direct script flow is accepted.
            $callAncestor = $gitLinesInvocation.Parent
            $insideFunctionDefinition = $false
            while ($null -ne $callAncestor)
            {
                if ($callAncestor -is [System.Management.Automation.Language.FunctionDefinitionAst])
                {
                    $insideFunctionDefinition = $true
                    break
                }
                $callAncestor = $callAncestor.Parent
            }
            if ($insideFunctionDefinition) { continue }
            $invocationText = $gitLinesInvocation.Extent.Text
            if ($invocationText -match '(?s)^Invoke-GitLines\s+@\(\s*[\x27\x22]commit[\x27\x22]\s*,')
            {
                $hasCommit = $true
            }
            if ($invocationText -match '(?s)^Invoke-GitLines\s+@\(\s*[\x27\x22]push[\x27\x22]\s*,')
            {
                $hasPush = $true
            }
        }
    }
    $hasGitDiffCheck =
        $normalized -match
            'git[^\r\n]*diff[^\r\n]*--check'

    $hasGitCachedDiffCheck =
        (($normalized -match
            'git[^\r\n]*diff[^\r\n]*--cached[^\r\n]*--check') -or
         ($normalized -match
            'git[^\r\n]*diff[^\r\n]*--check[^\r\n]*--cached'))

    $wrappedGitCommands =
        @(
            $ast.FindAll(
                {
                    param($node)

                    if ($node -isnot
                        [System.Management.Automation.Language.CommandAst])
                    {
                        return $false
                    }

                    $commandName =
                        $node.GetCommandName()

                    return ($commandName -eq
                        'Invoke-BoundedProcess') -or
                        ($commandName -eq
                            'Start-Process') -or
                        ($commandName -eq
                            'Run')
                },
                $true)
        )

    foreach ($wrappedGitCommand in
        $wrappedGitCommands)
    {
        $wrappedText =
            $wrappedGitCommand.Extent.Text

        $targetsGit =
            $wrappedText -match
                '(?is)-FilePath\s+[''"]git(?:\.exe)?[''"]'

        if (-not $targetsGit)
        {
            continue
        }

        $argumentStrings =
            @(
                $wrappedGitCommand.FindAll(
                    {
                        param($node)

                        return ($node -is
                            [System.Management.Automation.Language.StringConstantExpressionAst]) -or
                            ($node -is
                                [System.Management.Automation.Language.ExpandableStringExpressionAst])
                    },
                    $true) |
                    ForEach-Object {
                        $_.Value
                    }
            )

        if ($argumentStrings -contains 'commit')
        {
            $hasCommit = $true
        }

        if ($argumentStrings -contains 'push')
        {
            $hasPush = $true
        }

        if ($argumentStrings -contains 'add')
        {
            $hasGitAdd = $true
        }

        if ($argumentStrings -contains 'reset')
        {
            $hasGitReset = $true
        }

        if ($argumentStrings -contains 'restore')
        {
            $hasGitRestore = $true
        }

        if (($argumentStrings -contains 'diff') -and
            ($argumentStrings -contains '--check'))
        {
            $hasGitDiffCheck = $true

            if ($argumentStrings -contains '--cached')
            {
                $hasGitCachedDiffCheck = $true
            }
        }
    }

    foreach ($wrappedGitCommand in $wrappedGitCommands)
    {
        $wrappedText =
            $wrappedGitCommand.Extent.Text

        $targetsGit =
            $wrappedText -match
                '(?is)-FilePath\s+[''"]git(?:\.exe)?[''"]'

        if (-not $targetsGit)
        {
            continue
        }

        $argumentStrings =
            @(
                $wrappedGitCommand.FindAll(
                    {
                        param($node)

                        return ($node -is
                            [System.Management.Automation.Language.StringConstantExpressionAst]) -or
                            ($node -is
                                [System.Management.Automation.Language.ExpandableStringExpressionAst])
                    },
                    $true) |
                    ForEach-Object {
                        $_.Value
                    }
            )

        $isGitCommit =
            $argumentStrings -contains
                'commit'

        $hasMessageSwitch =
            $argumentStrings -contains
                '-m'

        if (-not ($isGitCommit -and
            $hasMessageSwitch))
        {
            continue
        }

        $unsafeVariableMessageTransport =
            $wrappedText -match
                '(?is)[''"]-m[''"]\s*,\s*\$[A-Za-z_][A-Za-z0-9_:]*'

        if ($unsafeVariableMessageTransport)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-038' `
                    -Line $wrappedGitCommand.Extent.StartLineNumber `
                    -Message 'Wrapped git commit passes -m followed by a variable through Start-Process-style ArgumentList transport. Whitespace-bearing values can split into multiple native arguments. Use an explicitly native-quoted literal or another reviewed one-argument-preserving transport.'))
        }
    }

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

        if (-not $hasGitDiffCheck)
        {
            $violations.Add(
                (New-Violation `
                    -Rule 'GA-SR-012' `
                    -Message "$SelectedProfile profile must include git diff --check."))
        }

        if (-not $hasGitCachedDiffCheck)
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

    $arithmeticContinuationTrap = @'
#requires -Version 5.1
$ExpectedTotal = 20
$Passed = 18
$Failed = 1
$Skipped =
    $ExpectedTotal
    - $Passed
    - $Failed
'@

    Assert-Case `
        -Name 'arithmetic-continuation-operator' `
        -Text $arithmeticContinuationTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-027'

    $unaryNegativeSafe = @'
#requires -Version 5.1
$value =
    -1
Write-Host $value
'@

    Assert-Case `
        -Name 'unary-negative-continuation-safe' `
        -Text $unaryNegativeSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $fragileMemberLiteralTrap = @'
#requires -Version 5.1
$source = 'fixture'
Assert-ContainsLiteral `
    -Text $source `
    -Literal 'militaryMove.TraversedStrategicEdgeId' `
    -Label 'fixture'
'@

    Assert-Case `
        -Name 'fragile-member-literal-audit' `
        -Text $fragileMemberLiteralTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-028'

    $atomicSemanticAuditSafe = @'
#requires -Version 5.1
$source = 'fixture'
Assert-ContainsLiteral `
    -Text $source `
    -Literal 'militaryMove' `
    -Label 'fixture'
Assert-ContainsLiteral `
    -Text $source `
    -Literal 'TraversedStrategicEdgeId' `
    -Label 'fixture'
'@

    Assert-Case `
        -Name 'atomic-semantic-audit-safe' `
        -Text $atomicSemanticAuditSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $proseTokenSetTrap = @'
#requires -Version 5.1
$source = 'fixture'
Assert-ContainsAll `
    -Text $source `
    -Label 'fixture' `
    -Tokens @(
        'renderer, camera, HUD, colors, strategic/tactical viewport layout'
    )
'@

    Assert-Case `
        -Name 'prose-token-set-exactness-trap' `
        -Text $proseTokenSetTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-039'

    $stableTokenSetSafe = @'
#requires -Version 5.1
$source = 'fixture'
Assert-ContainsAll `
    -Text $source `
    -Label 'fixture' `
    -Tokens @(
        'ADR-052',
        'RENDERER_CHOICE=OPEN',
        '# Stable Contract Heading'
    )
'@

    Assert-Case `
        -Name 'stable-token-set-semantic-audit-safe' `
        -Text $stableTokenSetSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $xunitSingleWhereTrap = @'
#requires -Version 5.1
$payload = @"
using Xunit;

public sealed class FixtureTests
{
    [Fact]
    public void One()
    {
        Assert.Single(items.Where(predicate));
    }
}
"@
'@

    Assert-Case `
        -Name 'xunit-single-where-generated-source' `
        -Text $xunitSingleWhereTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-029'

    $xunitSinglePredicateSafe = @'
#requires -Version 5.1
$payload = @"
using Xunit;

public sealed class FixtureTests
{
    [Fact]
    public void One()
    {
        Assert.Single(items, predicate);
    }
}
"@
'@

    Assert-Case `
        -Name 'xunit-single-predicate-safe' `
        -Text $xunitSinglePredicateSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $xunitDocumentationMentionSafe = @'
#requires -Version 5.1
$note =
    'Documentation may mention Assert.Single(collection.Where(...)) without containing generated xUnit source.'
Write-Host $note
'@

    Assert-Case `
        -Name 'xunit-documentation-mention-safe' `
        -Text $xunitDocumentationMentionSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $formalCloseWrappedGitSafe = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$commit =
    Invoke-BoundedProcess `
        -FilePath 'git' `
        -ArgumentList @(
            'commit',
            '--only',
            '-m',
            'fixture',
            '--',
            'docs/file.md'
        ) `
        -Name 'git-commit'

$push =
    Invoke-BoundedProcess `
        -FilePath 'git' `
        -ArgumentList @(
            'push',
            'origin',
            'main'
        ) `
        -Name 'git-push'

Write-Host 'EVIDENCE_ZIP=fixture.zip'
Write-Host 'FAILURE_EVIDENCE_ZIP=fixture-fail.zip'
'@

    Assert-Case `
        -Name 'formalclose-wrapped-git-detection-safe' `
        -Text $formalCloseWrappedGitSafe `
        -SelectedProfile 'FormalClose' `
        -ShouldPass $true

    $formalCloseWrappedMissingPush = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$commit =
    Invoke-BoundedProcess `
        -FilePath 'git' `
        -ArgumentList @(
            'commit',
            '--only',
            '-m',
            'fixture',
            '--',
            'docs/file.md'
        ) `
        -Name 'git-commit'

Write-Host 'EVIDENCE_ZIP=fixture.zip'
Write-Host 'FAILURE_EVIDENCE_ZIP=fixture-fail.zip'
'@

    Assert-Case `
        -Name 'formalclose-wrapped-git-missing-push' `
        -Text $formalCloseWrappedMissingPush `
        -SelectedProfile 'FormalClose' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-014'

    $ps51UnsignedSuffixTrap = @'
#requires -Version 5.1
$value = [uint64]0
if ($value -ne 0UL) {
    Write-Host 'bad'
}
'@

    Assert-Case `
        -Name 'ps51-unsigned-literal-suffix-trap' `
        -Text $ps51UnsignedSuffixTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-001'

    $ps51UlongAcceleratorTrap = @'
#requires -Version 5.1
$value = [ulong]0
Write-Host $value
'@

    Assert-Case `
        -Name 'ps51-ulong-accelerator-trap' `
        -Text $ps51UlongAcceleratorTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-032'

    $ps51UnsignedCastSafe = @'
#requires -Version 5.1
$value = [uint64]0
if ($value -ne [uint64]1) {
    Write-Host 'ok'
}
'@

    Assert-Case `
        -Name 'ps51-unsigned-cast-safe' `
        -Text $ps51UnsignedCastSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $wrappedDiffPairSafe = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$diff =
    Run `
        -FilePath 'git' `
        -ArgumentList @(
            'diff',
            '--check'
        ) `
        -Name 'git-diff-check'

$cached =
    Run `
        -FilePath 'git' `
        -ArgumentList @(
            'diff',
            '--cached',
            '--check'
        ) `
        -Name 'git-diff-cached-check'

Write-Host 'rollback-ready'
Write-Host 'EVIDENCE_ZIP=fixture.zip'
Write-Host 'FAILURE_EVIDENCE_ZIP=fixture-fail.zip'
'@

    Assert-Case `
        -Name 'wrapped-git-diff-pair-safe' `
        -Text $wrappedDiffPairSafe `
        -SelectedProfile 'Bootstrap' `
        -ShouldPass $true

    $wrappedDiffMissingCached = @'
#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$diff =
    Run `
        -FilePath 'git' `
        -ArgumentList @(
            'diff',
            '--check'
        ) `
        -Name 'git-diff-check'

Write-Host 'rollback-ready'
Write-Host 'EVIDENCE_ZIP=fixture.zip'
Write-Host 'FAILURE_EVIDENCE_ZIP=fixture-fail.zip'
'@

    Assert-Case `
        -Name 'wrapped-git-diff-missing-cached' `
        -Text $wrappedDiffMissingCached `
        -SelectedProfile 'Bootstrap' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-012'

    $gitWrapperShadowTrap = @'
#requires -Version 5.1
function Git {
    @(
        & git -c core.safecrlf=false status
    )
}

Git
'@

    Assert-Case `
        -Name 'native-git-wrapper-shadow-trap' `
        -Text $gitWrapperShadowTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-034'

    $gitWrapperDistinctSafe = @'
#requires -Version 5.1
function Invoke-GitLines {
    @(
        & git.exe -c core.safecrlf=false status
    )
}

Invoke-GitLines
'@

    Assert-Case `
        -Name 'native-git-wrapper-distinct-safe' `
        -Text $gitWrapperDistinctSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $trxRevisionMismatchTrap = @'
#requires -Version 5.1
$root = '.'
$FullTrxPath =
    Join-Path `
        $root `
        'cycle-r5-full.trx'

$arguments = @(
    '--logger',
    'trx;LogFileName=cycle-r4-full.trx'
)

Write-Host $FullTrxPath
Write-Host ($arguments -join ' ')
'@

    Assert-Case `
        -Name 'trx-revision-name-mismatch-trap' `
        -Text $trxRevisionMismatchTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-035'

    $trxRevisionMatchSafe = @'
#requires -Version 5.1
$root = '.'
$FullTrxPath =
    Join-Path `
        $root `
        'cycle-r5-full.trx'

$arguments = @(
    '--logger',
    'trx;LogFileName=cycle-r5-full.trx'
)

Write-Host $FullTrxPath
Write-Host ($arguments -join ' ')
'@

    Assert-Case `
        -Name 'trx-revision-name-match-safe' `
        -Text $trxRevisionMatchSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $automaticArgsParameterTrap = @'
#requires -Version 5.1
function Invoke-Native(
    [string]$FilePath,
    [string[]]$Args
) {
    Write-Host $FilePath
    Write-Host $Args.Count
}
'@

    Assert-Case `
        -Name 'automatic-args-function-parameter-trap' `
        -Text $automaticArgsParameterTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-036'

    $automaticArgsParameterSafe = @'
#requires -Version 5.1
function Invoke-Native(
    [string]$FilePath,
    [string[]]$ArgumentList
) {
    Write-Host $FilePath
    Write-Host $ArgumentList.Count
}
'@

    Assert-Case `
        -Name 'automatic-args-function-parameter-safe' `
        -Text $automaticArgsParameterSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $commandArgumentLineBreakTrap = @'
#requires -Version 5.1
$path = 'fixture.txt'

if (-not (Test-Path -LiteralPath
    $path)) {
    Write-Host 'missing'
}
'@

    Assert-Case `
        -Name 'ps51-command-argument-linebreak-trap' `
        -Text $commandArgumentLineBreakTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-001'

    $commandArgumentExplicitContinuationSafe = @'
#requires -Version 5.1
$path = 'fixture.txt'

$exists =
    Test-Path `
        -LiteralPath $path

if (-not $exists) {
    Write-Host 'missing'
}
'@

    Assert-Case `
        -Name 'ps51-command-argument-explicit-continuation-safe' `
        -Text $commandArgumentExplicitContinuationSafe `
        -SelectedProfile 'General' `
        -ShouldPass $true

    $nativeCommitVariableMessageTrap = @'
#requires -Version 5.1
$CommitMessage = 'docs: fixture message'

$commit =
    Invoke-BoundedProcess `
        -FilePath 'git.exe' `
        -ArgumentList @(
            'commit',
            '-m',
            $CommitMessage,
            '--',
            'docs/file.md'
        ) `
        -Name 'git-commit'
'@

    Assert-Case `
        -Name 'native-commit-variable-message-boundary-trap' `
        -Text $nativeCommitVariableMessageTrap `
        -SelectedProfile 'General' `
        -ShouldPass $false `
        -ExpectedRule 'GA-SR-038'

    $nativeCommitQuotedLiteralSafe = @'
#requires -Version 5.1

$commit =
    Invoke-BoundedProcess `
        -FilePath 'git.exe' `
        -ArgumentList @(
            'commit',
            '-m',
            '"docs: fixture message"',
            '--',
            'docs/file.md'
        ) `
        -Name 'git-commit'
'@

    Assert-Case `
        -Name 'native-commit-quoted-literal-boundary-safe' `
        -Text $nativeCommitQuotedLiteralSafe `
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
