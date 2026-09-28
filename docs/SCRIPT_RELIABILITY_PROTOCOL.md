# Global Arena — Script Reliability Protocol

**Protocol ID:** GA-SRP
**Version:** 1.0
**Status:** Active
**Effective date:** 2026-09-28
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
- no inconsistent variable spelling that differs only by case;
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

### GA-SR-016 — Recovery is bounded by a known state

Observed failure:
a previous failed script left a partial state that a later script had to recover.

Guard:
automatic recovery may occur only when the dirty-state shape and relevant artifacts match a specifically recognized state.

### GA-SR-018 — Nested script payload transport is delimiter-independent

Observed failure:
the GA-SRP bootstrap embedded complete PowerShell scripts inside a raw single-quoted here-string. The embedded scripts contained their own here-string terminators, which prematurely closed the outer payload and made the bootstrap itself unparsable.

Guard:

- a PowerShell script that transports another complete PowerShell script must not embed that payload as a raw here-string;
- use Base64/UTF-8 transport or an external file;
- bootstrap payload transport must be delimiter-independent;
- once GA-SRP is installed, the native Windows PowerShell 5.1 parser gate remains the final syntax authority.

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

Observed failure:
the GA-SR-020 detector rejected the correct normalized form `@(Get-ChildItem ...).Count` because its regex matched the unsafe substring inside the safe expression.

Guard:

- every new textual detector must have at least one bad fixture that must fail;
- every new textual detector must also have at least one safe neighboring fixture that must pass;
- a detector is not accepted merely because it catches the known bad example;
- self-validation must prove that the validator does not reject its own prescribed safe form.

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
