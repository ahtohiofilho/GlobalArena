#!/usr/bin/env bash
# G3-J3o: actual SIGKILL of a separate xUnit TESTHOST process on Linux.
# The supervisor is not the process under test, and inspection is always
# executed in a freshly launched dotnet test invocation.
set -euo pipefail

project="GlobalArena.Tests/GlobalArena.Tests.csproj"
dotnet restore "$project"
dotnet build "$project" -c Release --no-restore

dotnet test "$project" -c Release --no-build --no-restore \
  --filter "FullyQualifiedName~J3oInspectorNeverInterpretsUnmarkedSaveAsAuthority"

for phase in ArchiveVerified InventoryExtracted ReplayVerified BeforePublish AfterPublish BeforeAdmission; do
  root="$(mktemp -d)"
  worker=""
  cleanup() {
    if [[ -n "$worker" ]]; then
      kill -KILL -- "-$worker" 2>/dev/null || true
      wait "$worker" 2>/dev/null || true
    fi
    rm -rf -- "$root"
  }
  trap cleanup EXIT
  export GA_J3O_ROOT="$root"
  export GA_J3O_PHASE="$phase"

  dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3oPrepareRealCrashFixture" \
    --logger "console;verbosity=minimal"

  # setsid gives the whole testhost/runner subtree its own process group.
  # Never send the signal to the runner's own group.
  setsid env GA_J3O_ROOT="$root" GA_J3O_PHASE="$phase" \
    dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3oCrashWorker" \
    --logger "console;verbosity=minimal" \
    >"$root/worker.log" 2>&1 &
  worker="$!"
  ready="false"
  for attempt in $(seq 1 250); do
    if [[ -f "$root/worker-ready" ]]; then
      ready="true"
      break
    fi
    if ! kill -0 "$worker" 2>/dev/null; then
      break
    fi
    sleep 0.2
  done
  if [[ "$ready" != "true" ]]; then
    echo "G3-J3o worker did not reach $phase"
    cat "$root/worker.log"
    exit 1
  fi

  echo "G3-J3o SIGKILL at phase $phase; process group $worker"
  kill -KILL -- "-$worker"
  set +e
  wait "$worker" 2>/dev/null
  exit_code="$?"
  set -e
  worker=""
  if [[ "$exit_code" -ne 137 ]]; then
    echo "Expected SIGKILL exit=137, got $exit_code for $phase"
    cat "$root/worker.log"
    exit 1
  fi

  # New host/test process must refuse all public restore paths for
  # marked published directories; orphaned ZIP quarantines are inventoried.
  dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3oNewProcessInspectsCrashWithoutAdmittingWriter" \
    --logger "console;verbosity=minimal"

  cleanup
  trap - EXIT
done

echo "G3-J3o: all six actual SIGKILL/restart checkpoints passed"
