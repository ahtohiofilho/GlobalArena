#!/usr/bin/env bash
# G3-J3p: real supervisor SIGKILL + NEW testhost reconciliation for
# signed archive-only custody. No production save is ever promoted.
set -euo pipefail
project="GlobalArena.Tests/GlobalArena.Tests.csproj"

dotnet restore "$project"
dotnet build "$project" -c Release --no-restore

dotnet test "$project" -c Release --no-build --no-restore \
  --filter "FullyQualifiedName~J3pSignedOperatorArchiveAndSynchronousAuditAndIdempotentReconciliation|FullyQualifiedName~J3pRejectsForgedExpiredMutatedAndUnfencedApproval|FullyQualifiedName~J3pAuditFailureAndInterruptedRenameRequireExplicitReconciliation|FullyQualifiedName~J3pFencingRevocationBetweenIntentAndRenameFailsClosed|FullyQualifiedName~J3pPostIntentInterruptionPreservesSourceAndRetry|FullyQualifiedName~J3pRefusesDuplicateSourceAndVaultOrUnsafeSourceAlias|FullyQualifiedName~J3pSymlinkInsideQuarantineIsRejectedBeforeApproval"

for phase in IntentPersisted DirectoryMoved; do
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
  export GA_J3P_ROOT="$root"
  export GA_J3P_PHASE="$phase"

  dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3pPrepareCustodySigkillFixture" \
    --logger "console;verbosity=minimal"

  setsid env GA_J3P_ROOT="$root" GA_J3P_PHASE="$phase" \
    dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3pCustodySigkillWorker" \
    --logger "console;verbosity=minimal" >"$root/worker.log" 2>&1 &
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
    echo "G3-J3p worker did not reach $phase"
    cat "$root/worker.log"
    exit 1
  fi

  echo "G3-J3p SIGKILL at $phase; group=$worker"
  kill -KILL -- "-$worker"
  set +e
  wait "$worker" 2>/dev/null
  result="$?"
  set -e
  worker=""
  if [[ "$result" -ne 137 ]]; then
    echo "Expected SIGKILL exit=137; got $result for $phase"
    cat "$root/worker.log"
    exit 1
  fi

  # A separately started testhost checks custody and reconciles with the
  # SAME independently signed, still-valid operator approval.
  dotnet test "$project" -c Release --no-build --no-restore \
    --filter "FullyQualifiedName~J3pRestartInspectsAndReconcilesCustodyAfterSigkill" \
    --logger "console;verbosity=minimal"

  cleanup
  trap - EXIT
done
echo "G3-J3p: signed custody persisted and reconciled after both real SIGKILL cutpoints"
