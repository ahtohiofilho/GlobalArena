# Global Arena — Script Reliability Protocol

**Protocol ID:** GA-SRP
**Version:** 1.9
**Status:** Active
**Effective date:** 2026-10-07
**Scope:** PowerShell automation used to inspect, modify, validate or formally close work in the Global Arena repository.

## 1. Purpose

This protocol exists to reduce repeated operational failures in repository automation.

The governing rule is:

> A failure class discovered once must become a permanent regression guard whenever it can be checked mechanically.

The protocol is intentionally small and evidence-driven. It grows from observed failures, not from speculative rules.

This document is the source of truth. Conversation memory may mirror these rules for convenience, but repository documentation wins if there is any disagreement.

## 2. Operating baseline

Global Arena repository automation targets:

- Windows PowerShell 5.1;
- branch `main`;
- fail-fast execution;
- explicit preflight;
- deterministic repository mutation;
- auditable evidence;
- one short user command;
- no interactive patching of an earlier script revision.

If a script revision is defective, create a new revision. Do not instruct the operator to edit the defective file manually.

## 3. Mandatory execution gate

Every new `.ps1` supplied for Global Arena repository work must be checked by:

`scripts/Test-GAScriptReliability.ps1`

before substantive execution.

The preferred operator path is:

`scripts/Invoke-GASafeScript.ps1`

which runs the reliability gate first and executes the target only after a PASS.

The reliability gate must run under Windows PowerShell 5.1. Its parser check is therefore a native PS5.1 grammar check rather than a textual approximation.

A script that does not pass the selected reliability profile is not eligible for execution.

## 4. Reliability profiles

### General

Mandatory:

- native PowerShell parser PASS;
- `#requires -Version 5.1`;
- no known PS5.1-hostile line-leading operators;
- no arithmetic binary continuation operators beginning a code line;
- no inconsistent variable spelling that differs only by case;
- no whitespace-fragile exact C# member-chain semantic assertions;
- no generated xUnit `Assert.Single(collection.Where(...))` anti-pattern;
- no PowerShell-expression use of `checked(...)`;
- no trailing whitespace in script source;
- no fragile single-quoted multiline anchor escapes.

### ReadOnly

Adds to General:

- repository mutation Git commands are forbidden;
- commit and push are forbidden;
- repository source files must not be modified;
- evidence/temp files may be created outside the repository worktree.

### QaMutation

Adds to General:

- commit and push are forbidden;
- rollback/restore behavior is mandatory;
- exact staged-file control is mandatory;
- both `git diff --check` and `git diff --cached --check` are mandatory;
- build/test validation is mandatory when compiled code can be affected;
- PASS and FAIL evidence must each collapse to exactly one ZIP.

### FormalClose

Adds to General:

- the accepted QA/audit state must be verified before mutation;
- exact staged-file control is mandatory;
- final diff checks are mandatory;
- build/test validation is mandatory when relevant;
- commit and push are expected;
- local/remote synchronization and clean worktree must be verified after push;
- a failure after commit must never silently reset committed work;
- PASS and FAIL evidence must each collapse to exactly one ZIP.

### Bootstrap

Used only to establish or upgrade this reliability infrastructure.

It behaves like QaMutation, but may create or replace the reliability tools themselves.

## 5. Known failure classes and permanent guards

### GA-SR-001 — Native PS5.1 syntax validation

Observed failure:
a script reached the operator with a parser error caused by a comparison operator beginning a continuation line.

Guard:
the native Windows PowerShell parser must report zero parse errors before execution.

### GA-SR-002 — No line-leading PowerShell operators

Observed failure:
`-ne`, `-or` and `-and` at the beginning of continuation lines produced PS5.1 parser failures.

Guard:
comparison, logical, bitwise and containment operators may not begin a code line.

### GA-SR-003 — No PowerShell 7 syntax in PS5.1 automation

Guard:
do not use PS7-only syntax such as null-coalescing or null-conditional operators.

Native PS5.1 parsing is authoritative.

### GA-SR-004 — Multiline anchors are literal and LF-normalized

Observed failure:
a replacement anchor used `` `n `` inside a single-quoted PowerShell string. The escape was treated literally and the anchor could never match.

Guard:

- multiline anchors use here-strings;
- source, anchor and replacement are normalized to LF before exact matching;
- expected anchor cardinality is asserted, normally exactly one;
- a failed anchor never falls back to an approximate replacement.

### GA-SR-005 — PowerShell variable names are case-insensitive

Observed failure:
two intended variables differed only by case and collided.

Guard:
a script must not contain multiple spellings of the same variable name that differ only by case.

### GA-SR-006 — `checked(...)` is C#, not a PowerShell expression

Observed failure:
C#-style `checked(...)` was emitted into PowerShell expression code.

Guard:
`checked(...)` may appear inside literal C# payloads, but not as executable PowerShell syntax.

### GA-SR-007 — Controlled text encoding and line endings

Guard:

- controlled repository text is UTF-8 without BOM where practical;
- controlled content is normalized to LF;
- repository Git operations use `git -c core.safecrlf=false` where warning behavior could interact with `$ErrorActionPreference = 'Stop'`;
- `.gitattributes` remains authoritative for repository line-ending policy.

### GA-SR-008 — Rollback is armed before the first repository write

Observed failure:
an early write occurred before rollback protection was active and left a dirty worktree.

Guard:
backups/state required for rollback are established before the first repository mutation.

### GA-SR-009 — Failure handling accepts empty diagnostic content

Observed failure:
clean `git status` output became an empty string and evidence writing failed because a mandatory string parameter rejected empty content.

Guard:
diagnostic text helpers used on failure paths accept empty strings.

### GA-SR-010 — Exactly one evidence ZIP per execution

Guard:

- evidence may be assembled in a temporary directory;
- PASS publishes only `EVIDENCE_ZIP=...`;
- FAIL publishes only `FAILURE_EVIDENCE_ZIP=...`;
- the ZIP is verified readable and non-empty before temporary evidence is removed;
- no permanent loose evidence directory accompanies the ZIP;
- packaging has a fallback implementation;
- failure diagnostics are captured before rollback when rollback would erase them.

### GA-SR-011 — Do not reconstruct Unicode-sensitive files from captured `git show`

Observed failure:
Windows PowerShell console decoding corrupted text used as a restoration oracle.

Guard:
prefer exact file backups, raw file bytes, known-state Git restore, hashes and status/diff shape.

### GA-SR-012 — Exact staging and diff hygiene

Guard:

- stage only the expected file set;
- verify staged names exactly;
- reject unexpected unstaged tracked changes;
- run `git diff --check`;
- run `git diff --cached --check`;
- generated files are checked for trailing whitespace before staging when practical.

### GA-SR-013 — External probes are strict and isolated

Observed failures:
probe compilation warnings blocked strict builds; a missing comparer caused an avoidable runtime failure.

Guard:

- probes run outside the repository worktree;
- `TreatWarningsAsErrors` stays enabled when used by the gate;
- probe variable names obey GA-SR-005;
- custom identities do not assume `IComparable`; provide explicit deterministic comparers when ordering is required;
- probe failure output includes a useful tail in the failure evidence.

### GA-SR-014 — QA and formal close remain separate

Guard:

- ReadOnly and QaMutation profiles never commit or push;
- formal close happens only after accepted QA/audit evidence;
- formal close uses a separate script;
- a formal-close failure after commit never pretends that no commit exists.

### GA-SR-015 — Failed revisions are immutable historical evidence

Guard:
when a script has a bug, generate a new revision. Do not patch the old revision in place for the operator.

### GA-SR-016 — Recovery is bounded by a proven controlled state

Observed failures:
a previous failed script left a partial state that a later script had to recover; M4.5-C later exposed an exact residual staged payload that needed safe automated normalization without risking unrelated work.

Guard:

- automatic recovery is allowed only for an explicitly recognized state;
- verify expected HEAD/branch when relevant;
- verify the exact path set and exact Git status shape;
- verify there are no unexpected unstaged or untracked paths;
- verify controlled file content, hashes or accepted evidence identity before destructive normalization;
- restore/remove only the proven controlled paths;
- if any identity check differs, fail without modifying the residual state;
- after recovery, prove the intended baseline or protected state was restored exactly;
- capture the recovery decision and evidence in the execution ZIP.

### GA-SR-018 — Nested PowerShell payload transport is delimiter-independent

Observed failures:
the GA-SRP bootstrap embedded complete PowerShell scripts inside a raw single-quoted here-string, and a later M2.5.4 design-freeze script embedded validator self-test snippets containing their own here-string delimiters. In both cases an inner terminator prematurely closed the outer payload and made the carrier script unparsable.

Guard:

- any transported PowerShell payload that can contain the carrier's here-string delimiter must not be embedded as a raw same-delimiter here-string;
- this applies to complete scripts, validator fixtures, probe snippets and other executable PowerShell fragments;
- use Base64/UTF-8 transport, an external file, or another delimiter-independent representation;
- bootstrap and protocol-upgrade payload transport must be delimiter-independent;
- the native Windows PowerShell 5.1 parser gate remains the final syntax authority and must block execution on any recurrence.

### GA-SR-019 — Avoid `New-Object List[object]` with array subexpression

Observed failure:
the validator created `System.Collections.Generic.List[object]` with `New-Object` and later materialized it through `@(...)`. On Windows PowerShell 5.1 this can throw `System.ArgumentException: Argument types do not match` even when parsing succeeds.

Guard:

- do not construct `System.Collections.Generic.List[object]` with `New-Object` in GA automation;
- prefer the .NET constructor form;
- materialize with `.ToArray()` when an object array is required;
- the validator rejects the known hazardous constructor form.

### GA-SR-020 — Normalize zero/one/many command output before `.Count`

Observed failure:
rollback used `(Get-ChildItem ...).Count` under StrictMode. When the directory was empty, there was no scalar object exposing `Count`, causing a secondary rollback/evidence failure.

Guard:

- command output that may contain zero, one or many items must be normalized with `@(...)` before `.Count`;
- empty diagnostic or directory state is valid and must not break failure handling;
- the validator rejects the known direct `Get-ChildItem(...).Count` form.

### GA-SR-021 — Mechanical detectors require bad and safe fixtures

Observed failures:

- the GA-SR-020 detector rejected the correct normalized form `@(Get-ChildItem ...).Count` because its regex matched the unsafe substring inside the safe expression;
- the first GA-SR-029 implementation scanned the complete carrier text and matched its own diagnostic/documentation mention of `Assert.Single(collection.Where(...))`, causing the hardened validator to reject the hardening script itself.

Guard:

- every new textual detector must have at least one bad fixture that must fail;
- every new textual detector must also have at least one safe neighboring fixture that must pass;
- a detector is not accepted merely because it catches the known bad example;
- detectors must be scoped to the syntactic or semantic context that actually makes the pattern unsafe;
- documentation, diagnostic messages and detector definitions that merely describe a prohibited pattern must not be treated as occurrences of that unsafe construct;
- self-validation must prove that the validator does not reject its own prescribed safe form or its own rule documentation.

### GA-SR-022 — Gate output is persisted before a gate can fail

Observed failure:
the bootstrap collected validator output in memory but wrote the validation log only after all targets passed. When the first target failed, the evidence ZIP did not contain the validator's specific rejection.

Guard:

- output from parser, validator, build, test, probe or audit gates must be written to evidence before the script evaluates the gate result;
- failure evidence should contain the immediate tool output that caused the stop whenever practical.

### GA-SR-023 — Variable-case detection is scope-aware

Observed failure:
GA-SR-005 compared variable spellings across the whole file and rejected legitimate names in different PowerShell scopes, such as a function parameter `$ScriptText` and a top-level `$scriptText`.

Guard:

- case-only variable collisions are checked inside the same syntactic `ScriptBlockAst` scope;
- the detector must still reject two case variants in one scope;
- the detector must allow equivalent spellings in separate function/scriptblock scopes;
- GA-SR-021 applies: both unsafe and safe neighboring fixtures are required.

### GA-SR-024 — Native stderr is durably separated before exit evaluation

Observed failures:

- a read-only performance diagnostic invoked `dotnet run ... 2>&1` under Windows PowerShell 5.1 with `$ErrorActionPreference = 'Stop'`; native stderr became a terminating `RemoteException` before the real compiler/runtime output could be persisted;
- the M2.5.4-C formal-close script invoked `git push ... 2>&1`; Git wrote its normal remote-status line to stderr, PowerShell converted it into a terminating error, and the script reported FAIL even though the commit had already been pushed successfully.

Guard:

- native commands known to emit informational or diagnostic stderr must not rely on direct PowerShell stream merging through `2>&1` when `$ErrorActionPreference = 'Stop'`;
- stdout and stderr must be redirected to durable files, preferably with `Start-Process -RedirectStandardOutput` and `-RedirectStandardError`, when output participates in an auditable gate;
- evaluate the native process exit code only after durable stdout/stderr capture is available;
- post-action verification remains authoritative for externally visible state such as a Git push;
- GA-SR-022 still applies: the immediate native output must survive a failed gate;
- the validator mechanically rejects the observed `dotnet run ... 2>&1` and `git push ... 2>&1` forms;
- additional native commands are added to the detector when evidence demonstrates the same failure mode;
- GA-SR-021 applies: each detector extension requires failing and safe neighboring fixtures.
### GA-SR-025 — Durable native capture waits for the exact process, not the Windows process tree

Observed failure:
the M2.5.4-D R2 QA run launched `dotnet build` through `Start-Process -Wait -PassThru` with durable stdout/stderr redirection. The build log reported a successful build in `7.11 s` and its evidence timestamp was `06:25:10`, but the next test gate did not begin until `06:40:22`. The approximately 15-minute gap is consistent with Windows `Start-Process -Wait` waiting on descendant processes such as persistent .NET build servers after the direct `dotnet` process had already completed.

Guard:

- GA automation must not use `Start-Process -Wait` for durable native-process capture;
- launch with `Start-Process -PassThru` without `-Wait`;
- wait on the returned `System.Diagnostics.Process` object with `WaitForExit(timeoutMilliseconds)` so the gate is bounded to the exact launched process;
- every exact-process wait must have a finite timeout appropriate to the command;
- after a successful timed `WaitForExit(timeoutMilliseconds)`, call parameterless `WaitForExit()` and `Refresh()` on the same direct `Process` before reading `ExitCode`; this finalizes the direct process state without reintroducing `Start-Process -Wait` process-tree waiting;
- timeout failure evidence must preserve the direct process ID and durable stdout/stderr paths;
- durable logs should be read with sharing that tolerates descendant processes still holding inherited file handles;
- GA-SR-022 and GA-SR-024 remain applicable;
- the validator rejects `Start-Process` commands containing the `-Wait` parameter;
- GA-SR-021 applies: the detector has both failing and safe neighboring fixtures.

### GA-SR-026 — Captured `git diff --check` diagnostics are authoritative

Observed failures:

- the M2.5.4-E R1 formal-close correctly stopped on `git diff --cached --check` because `PROGRESS_LEDGER.md` and `RISK_REGISTER.md` contained new blank lines at EOF;
- the M2.5.4-E R2 recovery re-ran the same check through durable native-process capture. The captured stdout contained exactly the two expected `new blank line at EOF` diagnostics, but the returned `Process.ExitCode` was observed as `0`, so an exit-code-only gate incorrectly rejected the known residue signature instead of using the diagnostic content.

Guard:

- when `git diff --check` or `git diff --cached --check` is executed through durable captured-process infrastructure, success requires both a successful process state and zero non-empty diagnostic lines across captured stdout/stderr;
- captured diagnostics must be persisted before the gate can fail, per GA-SR-022;
- a captured diff-check result must not be judged only by `.ExitCode`;
- direct synchronous Git invocation with immediate `$LASTEXITCODE` is not changed by this rule;
- the validator rejects the observed `Invoke-BoundedProcess` captured Git diff-check pattern when the result variable's stdout/stderr is never inspected;
- GA-SR-021 applies: the detector has failing and safe neighboring fixtures.

### GA-SR-027 — Arithmetic binary operators stay on the preceding expression line

Observed failure:
M4.5-B QA used a multiline arithmetic assignment in which `- $Passed` and `- $Failed` began continuation lines. Windows PowerShell 5.1 accepted the script shape but evaluated the intended calculation incorrectly, producing a false test-accounting failure.

Guard:

- `+`, `-`, `*`, `/` and `%` used as binary continuation operators must remain on the preceding expression line;
- unary negative literals such as `-1` are not prohibited by this rule;
- the validator mechanically rejects the observed line-leading binary-operator shape;
- GA-SR-021 applies with both unsafe and safe fixtures.

### GA-SR-028 — Semantic audits must tolerate source formatting

Observed failures:
M4.5-C entry/audit scripts used exact textual assertions for semantic facts. Equivalent documentation wording or a C# member access split across lines caused false audit failures even though the underlying design/code was correct.

Guard:

- semantic code audits must prefer AST/behavioral checks, atomic tokens or whitespace-tolerant regex over long exact source fragments;
- exact prose assertions should target stable contract identifiers/headings/fields, not incidental sentence wording, unless the exact sentence itself is the contract;
- exact `Assert-ContainsLiteral` checks for variable/member chains such as `worldState.WorldBinding` are prohibited because formatting may split the chain;
- the validator mechanically rejects the observed lower-camel/member exact-literal shape;
- reviewed audits remain responsible for broader prose brittleness that cannot be identified safely by static analysis;
- GA-SR-021 applies with unsafe and safe neighboring fixtures.

### GA-SR-029 — Generated xUnit assertions must satisfy analyzer-safe idioms

Observed failure:
M4.5-C R1 generated `Assert.Single(collection.Where(predicate))`, and the repository analyzer emitted `xUnit2031`, blocking an otherwise valid build.

Guard:

- generated xUnit source must use `Assert.Single(collection, predicate)` rather than filtering with `.Where(...)` before `Assert.Single`;
- known analyzer failures that are deterministic and statically recognizable are promoted to generated-source regression guards;
- the validator rejects the observed `Assert.Single(...Where(...))` pattern only inside payloads that are structurally recognizable as generated xUnit/C# test source;
- plain documentation or diagnostic text that merely mentions the prohibited expression is safe and must not trigger the detector;
- GA-SR-021 applies with unsafe, safe-code and safe-documentation fixtures.

### GA-SR-030 — Accepted Git state identity uses object IDs, not rendered diff text

Observed failure:
the GA-SRP 1.5 independent audit compared the accepted hardening `git diff --cached` text with a newly captured diff. The hardening evidence used durable redirected Git output while the audit recaptured native Git text through Windows PowerShell 5.1. Unicode-sensitive diff text can be decoded or serialized differently even when the staged Git objects are identical, producing a false audit failure.

Guard:

- accepted staged state identity is proved by expected HEAD, exact path/status shape and staged Git object IDs;
- use full object IDs from `git rev-parse :path`, `git ls-files --stage`, or equivalent Git object identity rather than rendered diff equality;
- for modified tracked files, baseline HEAD plus staged blob identity proves the accepted content state;
- for added files, staged blob identity plus exact path/status shape proves the accepted content state;
- rendered `git diff` remains valuable human-readable evidence, but it is not an identity oracle;
- when Unicode-sensitive native text must be consumed semantically, capture bytes durably and decode explicitly rather than comparing output captured through different transport paths;
- direct Windows PowerShell native-text capture must not be compared against durable UTF-8 redirected output as a repository-state identity check;
- a broad static detector is intentionally not added because context-free matching would risk the GA-SR-021 false-positive class; this rule is enforced by reviewed audit/formal-close design.

### GA-SR-031 — Git capability detection recognizes safe process wrappers

Observed failure:
the GA-SRP 1.5 FormalClose script correctly executed Git through the bounded native-process helper, using `Invoke-BoundedProcess -FilePath 'git' -ArgumentList @('commit', ...)` and the equivalent wrapped `push`. GA-SR-014 looked only for direct textual `git commit` / `git push` forms, so it rejected a valid FormalClose before execution.

Guard:

- profile capability checks must recognize Git operations executed through approved process wrappers as well as direct native invocation;
- inspect the PowerShell AST for `Invoke-BoundedProcess` and `Start-Process` commands targeting `git` or `git.exe`;
- derive the Git verb from argument string nodes instead of requiring the verb to be adjacent to the executable name in rendered source text;
- `ReadOnly` must still reject wrapped `add`, `reset`, `restore`, `commit` and `push`;
- `QaMutation` and `Bootstrap` must still reject wrapped `commit` and `push`;
- `FormalClose` must accept either direct or safely wrapped `commit` and `push`, and must still reject a close missing either capability;
- GA-SR-021 applies: include a safe wrapped commit+push fixture and an unsafe wrapped commit-without-push fixture.

### GA-SR-032 — PS5.1 numeric literals and type accelerators stay PS5.1-compatible

Observed failures:

- the M4.5-D entry/design audit R1 used executable PowerShell comparisons containing `0UL` and `1UL`; Windows PowerShell 5.1 rejected that newer unsigned-literal suffix syntax at parse time;
- after the first hardening, M4.5-D entry/design audit R3 used the prescribed `[ulong]0` / `[ulong]1` casts; Windows PowerShell 5.1 parsed the script but failed at runtime because the `[ulong]` accelerator was added only in PowerShell 6.2.

Guard:

- repository automation remains native Windows PowerShell 5.1 syntax and runtime semantics;
- do not use newer unsigned numeric suffixes such as `0UL`, `1UL`, `0U` or `1U` in executable PowerShell;
- do not use the PowerShell 6.2+ `[ulong]` accelerator in PS5.1 automation;
- use the PS5.1-compatible `[uint64]` accelerator (or the fully qualified `[System.UInt64]`) when an unsigned 64-bit cast is required;
- the native PS5.1 parser remains authoritative for grammar failures, while the validator also rejects the observed parser-valid/runtime-invalid `[ulong]` type constraint/expression;
- the regression suite permanently preserves the `-ne 0UL` parser trap, the `[ulong]0` runtime-compatibility trap and a neighboring `[uint64]` safe fixture;
- GA-SR-021 applies to future extensions of numeric-literal and type-accelerator portability checks.

### GA-SR-033 — Profile Git-diff capability detection recognizes safe process wrappers

Observed failure:
the first GA-SRP 1.6 hardening script contained both required Git hygiene gates, but executed them through a bounded helper using `-FilePath 'git'` and `-ArgumentList` values for `diff`, `--check` and `--cached`. The installed Bootstrap profile searched only for a direct rendered Git command and rejected the target before execution.

Guard:

- QaMutation and Bootstrap profile capability checks recognize required Git diff hygiene whether Git is invoked directly or through an approved process wrapper;
- wrapper-aware detection inspects `Invoke-BoundedProcess`, `Start-Process` and the GA local bounded `Run` helper when `-FilePath` targets `git` or `git.exe`;
- a wrapped argument set containing `diff` and `--check` satisfies the ordinary diff-hygiene capability;
- the cached gate additionally requires `--cached`;
- a target that supplies only ordinary `diff --check` and omits the cached check remains invalid;
- direct synchronous Git forms remain valid;
- this extends the wrapper capability model introduced by GA-SR-031 without weakening mutation restrictions;
- GA-SR-021 applies with complete and incomplete wrapped fixtures.

### GA-SR-034 — Native-command wrappers must not shadow the executable they invoke

Observed failure:
the GA-SRP 1.6 hardening R2 declared a PowerShell function named `Git` and invoked `& git -c ...` from inside it. PowerShell command resolution is case-insensitive, so the invocation could resolve to the wrapper function itself rather than `git.exe`; PowerShell then tried to bind native option `-c` as a function parameter and failed before any repository mutation.

Guard:

- helper functions that wrap a native executable must use a distinct PowerShell command name such as `Invoke-GitLines`, `Invoke-NativeGit` or `Invoke-BoundedProcess`;
- do not name a wrapper `Git`, `Dotnet`, `Pwsh`, `PowerShell` or another native executable name when its body invokes the same command name;
- when ambiguity matters, resolve the application explicitly, for example `git.exe`, while still keeping the wrapper name distinct;
- the current mechanical detector covers the observed Git-shadowing form and can be extended evidence-first to other native executables;
- the self-test suite permanently includes an unsafe `function Git { & git ... }` fixture and a safe `Invoke-GitLines { & git.exe ... }` neighboring fixture;
- GA-SR-021 applies to detector extensions.

### GA-SR-035 — Revision-specific artifact names use one source of truth

Observed failure:
the GA-SRP 1.6 hardening R4 expected `ga-srp-1.6-hardening-r4-full.trx` through `$FullTrxPath`, but its `dotnet test --logger` argument still generated the copied-forward R3 filename. The tests themselves passed `1044/1044`, yet evidence validation failed because the script looked for an artifact name that the test command never produced.

Guard:

- revision-specific evidence filenames must not be independently hard-coded in multiple places;
- prefer one shared filename variable reused by the producer command and the consumer/evidence path;
- when literal `*TrxPath` assignments and literal `trx;LogFileName=...` values coexist, the validator compares their filename sets and rejects disagreement;
- a copied revision must not retain an older revision token in a producer/consumer artifact-name pair;
- the mechanical guard targets the observed TRX producer/consumer class and can be extended evidence-first to other revision-specific artifacts;
- GA-SR-021 applies: mismatched and matching TRX-name fixtures are permanent neighboring regressions.

### GA-SR-036 — Function parameters must not shadow PowerShell automatic variables

Observed failure:
the M4.5-D entry/design audit R2 declared a helper as `Run([string]$Exe,[string[]]$Args,...)`. PowerShell variable names are case-insensitive and `$args` is an automatic variable containing undeclared function arguments. The collision caused the helper's intended argument-list parameter to be empty at runtime, so `Start-Process -ArgumentList` failed before the first subprocess could start.

Guard:

- do not declare a PowerShell function parameter named `$Args` / `$args`;
- use a distinct descriptive name such as `$ArgumentList`, `$NativeArguments` or `$ProcessArguments`;
- the validator inspects real `ParameterAst` nodes inside function definitions, so documentation and string payloads that merely mention `$args` are not violations;
- the current mechanical detector is intentionally limited to the observed `$args` automatic-variable collision and may be extended evidence-first if another automatic-variable parameter collision is observed;
- the self-test suite permanently includes an unsafe function-parameter `$Args` fixture and a safe `$ArgumentList` neighboring fixture;
- GA-SR-021 applies to detector extensions.

### GA-SR-037 — Command invocation line breaks require explicit continuation in PS5.1

Observed failure:
the M4.5-D World MVP pivot independent audit R1 split command invocations between a named parameter and its argument inside grouping parentheses, for example `Test-Path -LiteralPath` on one line and the path expression on the next. Windows PowerShell 5.1 does not treat the surrounding parentheses as implicit command-line continuation, so the target produced a parser-error cascade before execution.

Guard:

- do not split a PowerShell command invocation between a named parameter token and its argument unless the command line is explicitly continued;
- surrounding `(...)` does not make command invocation newlines safe in Windows PowerShell 5.1;
- prefer either a single logical command line or explicit backtick continuation before moving parameter/argument pairs to following lines;
- when readability would require fragile nested command formatting, assign the command result to a temporary variable before using it inside `if`, member access or another expression;
- the native Windows PowerShell 5.1 parser remains the mechanical authority for this grammar failure;
- the self-test suite permanently includes the observed unsafe parameter/argument line-break fixture and an explicit-continuation safe neighboring fixture;
- GA-SR-021 applies to future detector extensions if a parser-valid neighboring form later requires static enforcement.

### GA-SR-017 — Protocol regression promotion

When a new failure occurs:

1. determine whether it is a one-off defect or a reusable failure class;
2. if reusable, assign a new `GA-SR-xxx` rule;
3. add a mechanical check when feasible;
4. add or extend validator self-tests;
5. bump the protocol version;
6. only then continue normal automation.

## 6. Automated versus reviewed rules

Passing the validator is necessary, not sufficient.

Script-specific review still owns questions such as:

- whether rollback is armed before the actual first write;
- whether the exact staged set is semantically correct;
- whether an anchor is the right semantic location;
- whether a probe tests the intended architecture;
- whether a formal-close recovery policy is safe after a commit.

## 7. Reliability gate evidence

A successful reliability check prints:

- selected profile;
- engine version;
- parser error count;
- rule violation count;
- `RESULT=PASS_GA_SCRIPT_RELIABILITY`.

A rejected script prints each violation and:

`RESULT=FAIL_GA_SCRIPT_RELIABILITY`

The safe runner must not execute a rejected target.

## 8. Stable operator workflow

Preferred one-command pattern:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Invoke-GASafeScript.ps1" -ScriptPath "$HOME\Downloads\<script>.ps1" -Profile QaMutation
```

Use the profile appropriate to the script purpose.

## 9. Progress accounting

GA-SRP is engineering infrastructure.

Adopting or improving it:

- does not create a gameplay milestone;
- does not promote GPP by itself;
- does not alter Global Arena V1 scope;
- does reduce repeated operational risk and human execution friction.

## 10. Authority

From formal adoption onward:

- this document is normative;
- `scripts/Test-GAScriptReliability.ps1` is the executable gate;
- `scripts/Invoke-GASafeScript.ps1` is the preferred one-command entry point;
- conversation memory is advisory and may not override the repository protocol.
