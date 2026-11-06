#!/usr/bin/env python3
"""GA-VS107: fail-closed checkpoint guard audit and deterministic PR scope.

No external Python dependency. The job's outputs are hints for fast CI only:
the explicit ga-ci-full checkpoint still runs original historical tests.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import subprocess
import sys

GUARD = "contains(github.event.pull_request.labels.*.name, 'ga-ci-full')"
ROOT = Path(__file__).resolve().parents[2]
BASELINE = ("GaVs17", "GaVs23", "GaVs29")
MINIMUM_HISTORICAL = 109
CHECKPOINT = "contains(github.event.pull_request.labels.*.name, 'ga-ci-checkpoint')"
CHECKPOINT_WORKFLOWS = (
    "cross-platform-kernel-regression.yml",
    "cross-platform-state-hash.yml",
    "turn-locked-freight-market-benchmark.yml",
)
TEST_CLASS = re.compile(
    r"(?m)^\s*(?:(?:public|internal|private|protected|sealed|partial|static|abstract)\s+)*"
    r"class\s+([A-Za-z_][A-Za-z_0-9]*)\b"
)
TEST_MARKER = re.compile(r"(?m)^\s*\[(?:Fact|Theory|SkippableFact|SkippableTheory)(?:\(|\])")


def changed_test_classes(source: str) -> set[str]:
    """Selected tests come from actual C# test types, never a GaVs filename prefix."""
    if not TEST_MARKER.search(source):
        return set()
    types = set(TEST_CLASS.findall(source))
    if not types:
        raise RuntimeError("Changed C# test methods lack a discoverable class")
    return types


def historical_files() -> list[Path]:
    folder = ROOT / ".github" / "workflows"
    return sorted(
        p for p in folder.glob("*.yml")
        if p.name.startswith("ga-vs")
        or p.name in ("ga-ui-qa-compact-sidebar.yml", "ga-qa-ui-visual-002.yml")
    )


def audit() -> None:
    files = historical_files()
    if len(files) < MINIMUM_HISTORICAL:
        raise RuntimeError(
            f"Historical checkpoint workflows disappeared: {len(files)}"
        )
    jobs = 0
    for path in files:
        text = path.read_text(encoding="utf-8")
        lines = text.splitlines()
        if not re.search(r"(?m)^  pull_request:\s*$", text):
            raise RuntimeError(f"PR trigger missing: {path}")
        if not re.search(
            r"(?m)^    types: \[opened, synchronize, reopened, labeled\]$", text
        ):
            raise RuntimeError(f"Explicit checkpoint label trigger missing: {path}")
        if "  workflow_dispatch:" not in text:
            raise RuntimeError(f"Historical manual dispatch removed: {path}")
        try:
            index = lines.index("jobs:")
        except ValueError as exc:
            raise RuntimeError(f"No historical jobs: {path}") from exc
        heads = [
            i for i in range(index + 1, len(lines))
            if re.match(r"^  [\w-]+:\s*(?:#.*)?$", lines[i])
        ]
        if not heads:
            raise RuntimeError(f"No historical jobs: {path}")
        for pos, start in enumerate(heads):
            stop = heads[pos + 1] if pos + 1 < len(heads) else len(lines)
            predicates = [
                line for line in lines[start + 1:stop]
                if line.startswith("    if:")
            ]
            if len(predicates) != 1 or GUARD not in predicates[0]:
                raise RuntimeError(
                    f"Unsafe checkpoint job guard: {path}:{start + 1}"
                )
            jobs += 1
    # The real UI end-to-end test is never gated: it is useful on normal UI PRs.
    e2e = ROOT / ".github" / "workflows" / "ga-qa-ui-e2e-001.yml"
    if not e2e.is_file() or GUARD in e2e.read_text(encoding="utf-8"):
        raise RuntimeError("Real Godot UI E2E must remain automatically enabled")
    folder = ROOT / ".github" / "workflows"
    # The full platform suites are triggered by labels, dispatch or main,
    # never by an ordinary PR. A skipped run is NOT full checkpoint evidence.
    for name in CHECKPOINT_WORKFLOWS:
        checkpoint = folder / name
        if not checkpoint.is_file():
            raise RuntimeError(f"Missing checkpoint workflow: {name}")
        content = checkpoint.read_text(encoding="utf-8")
        if ("  workflow_dispatch:" not in content or
                "    types: [opened, synchronize, reopened, labeled]" not in content or
                CHECKPOINT not in content or GUARD not in content or
                "github.event_name != 'pull_request'" not in content):
            raise RuntimeError(f"Unsafe checkpoint policy: {name}")
    print(f"GA_CI_VS107_GATES_AUDITED workflows={len(files)} guarded_jobs={jobs} checkpoints={len(CHECKPOINT_WORKFLOWS)}")


def scope(base: str, head: str) -> None:
    result = subprocess.run(
        ["git", "diff", "--name-only", "--diff-filter=ACMRT", base, head],
        cwd=ROOT, check=True, capture_output=True, text=True
    )
    changed = sorted(set(filter(None, result.stdout.splitlines())))
    if not changed:
        raise RuntimeError("Empty PR diff: cannot infer fast validation scope")
    prefixes = (
        "GlobalArena.Kernel/", "GlobalArena.World/", "GlobalArena.Runtime/",
        "GlobalArena.Simulation/", "GlobalArena.Economy/", "GlobalArena.Tests/",
        "GlobalArena.Console/", "GlobalArena.Benchmarks/",
        "GlobalArena.Godot/", "tools/GlobalArena.Godot/"
    )
    root_files = {"Directory.Build.props", "GlobalArena.slnx"}
    ci_paths = {"tools/ci/ga_ci_lanes.py", "tools/ci/test_ga_ci_lanes.py",
                ".github/workflows/ga-ci-fast.yml"}
    code = any(p.startswith(prefixes) or p in root_files or p in ci_paths
               for p in changed)
    ui = any(
        p.startswith(("GlobalArena.Godot/", "tools/GlobalArena.Godot/"))
        or p in root_files
        for p in changed
    )
    selected = set(BASELINE)
    for path in changed:
        if path.startswith("GlobalArena.Tests/") and path.endswith(".cs"):
            changed_file = ROOT / path
            if not changed_file.is_file():
                raise RuntimeError(f"Changed test source is missing: {path}")
            selected.update(changed_test_classes(
                changed_file.read_text(encoding="utf-8")))
    test_filter = "|".join(
        "FullyQualifiedName~" + name for name in sorted(selected)
    )
    output = {
        "needs_build": str(code).lower(),
        "needs_ui": str(ui).lower(),
        "test_filter": test_filter,
        "changed_count": str(len(changed))
    }
    destination = os.environ.get("GITHUB_OUTPUT")
    if destination:
        with open(destination, "a", encoding="utf-8") as out:
            for key, value in output.items():
                out.write(f"{key}={value}\n")
    print("GA_CI_VS107_SCOPE", output)


def main() -> None:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("audit")
    cmd = commands.add_parser("scope")
    cmd.add_argument("--base", required=True)
    cmd.add_argument("--head", required=True)
    args = parser.parse_args()
    if args.command == "audit":
        audit()
    else:
        scope(args.base, args.head)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f"GA_CI_VS107_FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
