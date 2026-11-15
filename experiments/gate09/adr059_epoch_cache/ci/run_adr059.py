#!/usr/bin/env python3
"""ADR059 LAB: reversible cache-epoch ownership, no economic changes.

Compare "overlap" (A kept while B warms) versus "release_first" (A
released before constructing B) for two worker-sourced physical load
snapshots. All routing and seller-specific weights use the ORIGINAL
FrozenRouteCache and quartic exponent 4. A/B are NOT F072 equilibria.

The owner is an ISOLATED experimental wrapper, not a patch to the
production game, solver or original route oracle. Epoch tokens are scoped
to one immutable Network instance; never reuse them across networks.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
from hashlib import sha256
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter, process_time
import tracemalloc
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr058_cache_lifecycle" / "ci"))
import run_adr058 as R58
from clearing_r5 import _flow_checksum
from oracle_cache_r14 import FrozenRouteCache

SCHEMA = "GA_GATE09_ADR059_REVERSIBLE_FROZEN_CACHE_EPOCH_POLICY_LAB_V1"
POLICIES = ("overlap", "release_first")
FIXTURES = R58.FIXTURES
EXPONENT = 4


def load_epoch_token(loads):
    """Content fingerprint of sparse physical edge loads WITHIN one network."""
    digest = sha256()
    for eid in sorted(loads):
        value = loads[eid]
        if not isfinite(value) or value < 0:
            raise ValueError("Invalid physical edge load")
        digest.update(repr((eid, value)).encode("utf-8"))
        digest.update(b"\n")
    return digest.hexdigest()


class StaleSnapshotError(ValueError):
    """An epoch token does not name the active snapshot."""


class EpochCacheOwner:
    """Experimental owner with no exposed original cache handles.

    Query methods require the current epoch token; attempts using a
    retired token fail closed even while the old cache is retained for
    overlap measurement. The wrapper does not change routing internals.
    """

    def __init__(self, network, policy, factory=None):
        if policy not in POLICIES:
            raise ValueError("Unregistered cache lifecycle policy")
        self.network = network
        self.policy = policy
        self._factory = factory if factory is not None else FrozenRouteCache
        self._active = None
        self._active_epoch = None
        self._retired = None
        self.release_before_build_bytes = None
        self.release_before_build_performed = False
        self.replacement_wall_seconds = None

    def _require(self, epoch):
        if self._active is None or epoch != self._active_epoch:
            raise StaleSnapshotError("Old or missing load snapshot epoch")
        return self._active

    def open(self, loads):
        if self._active is not None or self._retired is not None:
            raise ValueError("Cache owner already has an active snapshot")
        epoch = load_epoch_token(loads)
        self._active = self._factory(self.network, loads, EXPONENT)
        self._active_epoch = epoch
        return epoch

    def change(self, loads):
        if self._active is None:
            raise ValueError("Cannot change without existing active snapshot")
        if self._retired is not None:
            raise ValueError("Retired cache must be released before next change")
        epoch = load_epoch_token(loads)
        if epoch == self._active_epoch:
            raise ValueError("No physical load change; do not create new epoch")
        start = perf_counter()
        # Invalidate the old token BEFORE creating B. Under "overlap"
        # the old object remains only in a private, inaccessible field.
        if self.policy == "overlap":
            self._retired = self._active
        self._active = None
        self._active_epoch = None
        if self.policy == "release_first":
            gc.collect()
            self.release_before_build_performed = True
            if tracemalloc.is_tracing():
                self.release_before_build_bytes = tracemalloc.get_traced_memory()[0]
        # Fail closed if construction fails: old epoch is never restored.
        self._active = self._factory(self.network, loads, EXPONENT)
        self._active_epoch = epoch
        self.replacement_wall_seconds = round(perf_counter() - start, 6)
        return epoch

    def paths(self, epoch, origin, seller):
        return self._require(epoch).paths(origin, seller)

    def weights(self, epoch, seller):
        return self._require(epoch).weights(seller)

    @property
    def retired_cache_still_owned(self):
        return self._retired is not None

    def release_retired(self):
        self._retired = None
        gc.collect()

    def close(self):
        self._active = None
        self._active_epoch = None
        self._retired = None
        gc.collect()


def observe(owner, epoch, queries):
    begin_wall, begin_cpu = perf_counter(), process_time()
    hashes = []
    for origin, seller in queries:
        distances, paths, _ = owner.paths(epoch, origin, seller)
        hashes.append(R58.compact_signature(distances, paths, origin, seller))
    return {
        "group_queries": len(queries),
        "distinct_origin_seller_keys": len(set(queries)),
        "route_response_digest_sampled": sha256(
            "\n".join(hashes).encode("utf-8")).hexdigest(),
        "observed_wall_s": round(perf_counter() - begin_wall, 6),
        "observed_process_cpu_s": round(process_time() - begin_cpu, 6),
        "sample_digest_is_NOT_full_route_or_F072_certificate": True,
    }


def assert_same_routes(a, b):
    if (a["route_response_digest_sampled"] != b["route_response_digest_sampled"]
        or a["group_queries"] != b["group_queries"]):
        raise AssertionError("Frozen route results changed within identical epoch")


def weight_fingerprints(owner, epoch, sellers):
    """Hash ALL original seller-specific edge weights, without retaining maps.

    This supports cross-policy comparison in CI while avoiding a
    persistent Python shadow copy of every cached path and edge weight.
    """
    output = []
    for seller in sellers:
        weights = owner.weights(epoch, seller)
        digest = sha256()
        for eid in sorted(weights):
            value = weights[eid]
            if not isfinite(value):
                raise AssertionError("Nonfinite original edge weight")
            digest.update(repr((eid, value)).encode("utf-8"))
            digest.update(b"\n")
        output.append({"seller": seller, "edges": len(weights),
                       "original_weights_sha256": digest.hexdigest()})
    return output


def run_policy(fixture, policy):
    if fixture not in FIXTURES or policy not in POLICIES:
        raise ValueError("Only registered worker fixtures and policies")
    if R58.R57.R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Frozen original source manifest changed")
    config = FIXTURES[fixture]
    net, original, meta = R58.R57.R56.R55.R54.R53.R52.build_population(config["config"])
    workers = meta.pop("source_workers")
    if (len(workers) != 4096 or meta["workers"] != 4096
        or meta["demand_nodes_count"] != config["demand_nodes"]
        or meta["production_nodes_count"] != config["production_nodes"]
        or len(net.groups) != config["groups"]
        or len(net.markets) != config["markets"]
        or meta["initial_real_flow_sha256"] != _flow_checksum(original)
        or not meta["physical_production_nodes"].issubset(
            meta["physical_demand_nodes"])
        or not meta["travel_time_does_not_duplicate_demand"]):
        raise AssertionError("Original physical workers/supply/demand drift")
    modified, loads_a, loads_b, validity = R58.physical_pair(net, original)
    queries = tuple((net.groups[gid].origin, net.groups[gid].seller)
                    for gid in sorted(net.groups))
    sellers = tuple(sorted({seller for _, seller in queries}))
    del workers
    gc.collect()
    rss_before = R58.R57.rss()
    traced = {}
    owner = EpochCacheOwner(net, policy)
    tracemalloc.start(1)
    try:
        token_a = owner.open(loads_a)
        a_cold = observe(owner, token_a, queries)
        a_warm = observe(owner, token_a, queries)
        assert_same_routes(a_cold, a_warm)
        a_weights = weight_fingerprints(owner, token_a, sellers)
        traced["after_A_warm"], traced["peak_after_A"] = tracemalloc.get_traced_memory()

        token_b = owner.change(loads_b)
        try:
            owner.paths(token_a, queries[0][0], queries[0][1])
        except StaleSnapshotError:
            stale_token_rejected = True
        else:
            raise AssertionError("Old load epoch was served after changing loads")

        traced["after_B_construct_before_warm"], _ = tracemalloc.get_traced_memory()
        b_cold = observe(owner, token_b, queries)
        b_warm = observe(owner, token_b, queries)
        assert_same_routes(b_cold, b_warm)
        b_weights = weight_fingerprints(owner, token_b, sellers)
        if [x["seller"] for x in a_weights] != [x["seller"] for x in b_weights]:
            raise AssertionError("Seller identity changed with worker flows")
        changed_sellers = sum(
            x["original_weights_sha256"] != y["original_weights_sha256"]
            for x, y in zip(a_weights, b_weights))
        if changed_sellers <= 0:
            raise AssertionError("Load change failed to invalidate original edge weights")
        traced["after_B_warm_before_retire"], _ = tracemalloc.get_traced_memory()
        had_retired_during_B = owner.retired_cache_still_owned
        if had_retired_during_B != (policy == "overlap"):
            raise AssertionError("Unexpected lifecycle policy during B construction")

        owner.release_retired()
        traced["after_release_A"], _ = tracemalloc.get_traced_memory()
        b_after_retire = observe(owner, token_b, queries)
        assert_same_routes(b_cold, b_after_retire)
        traced["after_B_reuse"], traced["peak_total"] = tracemalloc.get_traced_memory()
        prebuild_release = owner.release_before_build_bytes
        release_before_build_performed = owner.release_before_build_performed
        replacement_wall_seconds = owner.replacement_wall_seconds
    finally:
        owner.close()
        tracemalloc.stop()

    if (_flow_checksum(original) != validity["original_flow_sha256"]
        or _flow_checksum(modified) != validity["perturbed_flow_sha256"]):
        raise AssertionError("Probe mutated original physical worker flows")
    if (policy == "release_first" and (
            not release_before_build_performed or prebuild_release is None)):
        raise AssertionError("Release-first did not release A before building B")
    if (policy == "overlap" and release_before_build_performed):
        raise AssertionError("Overlap baseline unexpectedly released A before B")
    return {
        "schema": SCHEMA,
        "status": "PASS_ADR059_ORIGINAL_FROZEN_EPOCH_CACHE_POLICY_LAB",
        "fixture": fixture,
        "policy": policy,
        "goldberg_geometry_nodes": meta["geometry_nodes"],
        "workers": meta["workers"],
        "demand_nodes": meta["demand_nodes_count"],
        "production_nodes": meta["production_nodes_count"],
        "groups": len(net.groups),
        "markets_node_item": len(net.markets),
        "worker_ledger_sha256": meta["worker_ledger_sha256"],
        "original_flow_sha256": _flow_checksum(original),
        "changed_flow_sha256": _flow_checksum(modified),
        "physical_redistribution": validity,
        "snapshot_A_epoch_sha256": token_a,
        "snapshot_B_epoch_sha256": token_b,
        "different_epoch_tokens": token_a != token_b,
        "old_epoch_token_rejected_after_switch": stale_token_rejected,
        "original_quartic_exponent": EXPONENT,
        "original_seller_weights_A": a_weights,
        "original_seller_weights_B": b_weights,
        "sellers_with_changed_original_weights": changed_sellers,
        "first_A_cold": a_cold,
        "repeat_A_warm": a_warm,
        "first_B_cold": b_cold,
        "repeat_B_warm": b_warm,
        "B_after_A_retire": b_after_retire,
        "old_cache_still_owned_during_B": had_retired_during_B,
        "release_A_before_B_build_executed": release_before_build_performed,
        "traced_python_bytes_after_release_A_before_B": prebuild_release,
        "replacement_wall_seconds_including_optional_gc": replacement_wall_seconds,
        "python_tracemalloc_bytes": traced,
        "tracemalloc_starts_after_network_construction": True,
        "tracemalloc_excludes_full_F072_solver_and_native_memory": True,
        "process_rss_peak_before": rss_before,
        "process_rss_peak_after": R58.R57.rss(),
        "process_RSS_is_lifetime_highwater_not_current_memory": True,
        "cache_owner_is_experimental_not_original_solver_patch": True,
        "A_and_B_not_original_F072_certificates": True,
        "original_independent_F072_separate_CI_regression": True,
        "CSharp_Godot_unchanged": True,
        "F059_financial_policy_pending": True,
        "production_approved": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--fixture", choices=tuple(FIXTURES), required=True)
    p.add_argument("--policy", choices=POLICIES, required=True)
    p.add_argument("--output", type=Path, default=Path.cwd())
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": SCHEMA, "fixture": args.fixture, "policy": args.policy,
        "platform": platform.platform(), "python": platform.python_version(),
        "utc": datetime.now(timezone.utc).isoformat(),
        "status": "FAILED_ADR059_EPOCH_CACHE_POLICY_CONTRACT",
        "production_approved": False,
    }
    try:
        report.update(run_policy(args.fixture, args.policy))
    except Exception as exc:
        report["status"] = "FAILED_ADR059_EPOCH_CACHE_POLICY_CONTRACT"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR059] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output = args.output / (
        "GA-Gate09-ADR059-" + args.fixture + "-" + args.policy + "-" + stamp + ".json")
    output.write_text(json.dumps(report, indent=2, sort_keys=True,
                                 ensure_ascii=False) + "\n", encoding="utf-8")
    archive = args.output / (
        "GA-Gate09-ADR059-Evidencias-" + args.fixture + "-" + args.policy
        + "-" + stamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(output, output.name)
    if report["status"].startswith("PASS_"):
        m = report["python_tracemalloc_bytes"]
        print("[ADR059:{}:{}] PASS orig_weights_changed_sellers={} "
              "epoch_old_rejected={} A_cold_s={} B_cold_s={} "
              "A_heap_B={} B_heap_B={} peak_python_B={} "
              "released_before_B_B={} RSS_peak_B={} original_F072_NOT_RUN".format(
                args.fixture, args.policy, report["sellers_with_changed_original_weights"],
                report["old_epoch_token_rejected_after_switch"],
                report["first_A_cold"]["observed_wall_s"],
                report["first_B_cold"]["observed_wall_s"],
                m["after_A_warm"], m["after_B_warm_before_retire"],
                m["peak_total"],
                report["traced_python_bytes_after_release_A_before_B"],
                report["process_rss_peak_after"]["process_peak_rss_bytes"]),
              flush=True)
    print("[ADR059] STATUS:", report["status"], flush=True)
    print("[ADR059] EVIDENCE:", archive, flush=True)
    return 0 if report["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
