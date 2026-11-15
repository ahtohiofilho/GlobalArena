"""ADR060 actual-original-solver cross-policy F072 and cache epoch equivalence."""
from __future__ import annotations

import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
import zipfile

CI=Path(__file__).resolve().parents[1]/"ci"
sys.path.insert(0,str(CI))
import compare_adr060 as C
import run_adr060 as A


def row(policy,fixture="192"):
    f=A.FIXTURES[fixture]
    event={
        "load_epoch_sha256_within_same_network":"source",
        "live_original_caches_before_creation":0,
        "live_original_caches_immediately_after_creation":1,
    }
    return {
        "schema":A.SCHEMA,
        "status":"PASS_ADR060_ORIGINAL_SOLVER_F072_AND_CACHE_LIVENESS_OBSERVED",
        "policy":policy,
        "fixture":fixture,
        "workers":4096,
        "production_nodes":f["production_nodes"],
        "demand_nodes":f["demand_nodes"],
        "aggregated_production_groups":f["groups"],
        "markets_node_item":f["markets"],
        "worker_ledger_sha256":"source_workers",
        "initial_flow_sha256":"initial_flow",
        "final_original_F072_flow_sha256":"independent_certified_final",
        "original_independent_F072_status":"PASS_STATIC_F072_FIRST_ORDER_ONLY",
        "original_independent_F072_global_gap":1e-12,
        "original_independent_F072_worst_group_gap":2e-12,
        "original_solver_status":"PASS_BLOCK_MCP_FULL_ORIGINAL_F072_CERTIFIED",
        "original_work_counters":{"Krylov":389,"H":32},
        "original_generation_summary":[{"generation":1,"status":"certified"}],
        "original_quartic_congestion_exponent":4,
        "original_budget_limits":vars(f["budget"]),
        "observed_solver_native_process_rss_highwater_after_bytes":1000000,
        "observed_solver_process_cpu_seconds":30.0,
        "cache_liveness_observations":{
            "original_cache_type_returned_unchanged":True,
            "R45_only_cache_factory_instrumented":True,
            "original_ADR042_independent_auditor_NOT_monkeypatched":True,
            "constructed_R45_route_cache_instances":2,
            "distinct_physical_edge_load_epochs":1,
            "maximum_simultaneously_live_R45_caches":1,
            "maximum_old_R45_caches_live_before_next_creation":0,
            "original_R45_caches_still_live_after_solve":0,
            "explicit_gc_before_each_cache_construction":
                2 if policy=="gc_before_cache" else 0,
            "observed_original_cache_construction_events":[event,event],
        },
        "gc_before_cache_is_not_ADR059_owned_release_first":True,
        "production_approved":False,
    }


class CompareContracts(unittest.TestCase):
    def setUp(self):
        self.records={"baseline":row("baseline"),
                      "gc_before_cache":row("gc_before_cache")}

    def test_identical_original_F072_and_cache_events_pass(self):
        result=C.compare(self.records,"192")
        self.assertEqual(result["status"],
                         "PASS_ADR060_ORIGINAL_F072_EQUIVALENCE_WITH_OBSERVED_CACHE_LIFETIMES")
        self.assertTrue(result["natural_original_R45_cache_release_before_next_create_observed"])
        self.assertTrue(result["F072_original_independent_and_final_allocations_identical"])

    def test_different_original_FINAL_F072_allocation_hash_is_hard_failure(self):
        self.records["gc_before_cache"]["final_original_F072_flow_sha256"]="changed"
        with self.assertRaisesRegex(AssertionError,"final_original_F072_flow_sha256"):
            C.compare(self.records,"192")

    def test_different_original_F072_gap_is_hard_failure(self):
        self.records["gc_before_cache"]["original_independent_F072_global_gap"]=2e-12
        with self.assertRaisesRegex(AssertionError,"original_independent_F072_global_gap"):
            C.compare(self.records,"192")

    def test_numerical_work_counter_drift_is_hard_failure(self):
        self.records["gc_before_cache"]["original_work_counters"]["Krylov"]=400
        with self.assertRaisesRegex(AssertionError,"original_work_counters"):
            C.compare(self.records,"192")

    def test_missing_original_independent_cert_is_rejected(self):
        self.records["baseline"]["original_independent_F072_status"]="NOT_CERTIFIED"
        with self.assertRaisesRegex(AssertionError,"economic contract"):
            C.compare(self.records,"192")

    def test_missing_original_cache_constructions_fail_closed(self):
        self.records["baseline"]["cache_liveness_observations"][
            "constructed_R45_route_cache_instances"]=0
        with self.assertRaisesRegex(AssertionError,"construction counts"):
            C.compare(self.records,"192")

    def test_different_snapshot_sequences_rejected(self):
        self.records["gc_before_cache"]["cache_liveness_observations"][
            "observed_original_cache_construction_events"][0]={
                "load_epoch_sha256_within_same_network":"other"}
        with self.assertRaisesRegex(AssertionError,"epochs differ"):
            C.compare(self.records,"192")

    def test_garbage_collector_policy_must_really_execute(self):
        self.records["gc_before_cache"]["cache_liveness_observations"][
            "explicit_gc_before_each_cache_construction"]=0
        with self.assertRaisesRegex(AssertionError,"not enforced"):
            C.compare(self.records,"192")

    def test_fixture_worker_and_market_integrity_enforced(self):
        self.records["baseline"]["workers"]=8192
        with self.assertRaisesRegex(AssertionError,"economic contract"):
            C.compare(self.records,"192")

    def test_cross_job_memory_delta_is_not_claimed_as_causal(self):
        self.records["gc_before_cache"][
            "observed_solver_native_process_rss_highwater_after_bytes"]=1200000
        outcome=C.compare(self.records,"192")
        self.assertEqual(outcome["observed_RSS_highwater_delta_B_baseline_minus_extra_GC"],
                         -200000)
        self.assertTrue(outcome["cross_job_RSS_CPU_differences_not_causal_proof"])

    def test_missing_policy_rejected(self):
        with self.assertRaises(ValueError):
            C.compare({"baseline":row("baseline")},"192")
        with self.assertRaises(ValueError):
            C.compare(self.records,"2048")

    def test_zip_parser_rejects_duplicates_and_accepts_both(self):
        with TemporaryDirectory() as tmp:
            d=Path(tmp)
            for policy in A.POLICIES:
                with zipfile.ZipFile(
                    d/("GA-Gate09-ADR060-Evidencias-192-"+policy+".zip"),"w"
                ) as z:
                    z.writestr("row.json",json.dumps(row(policy)))
            loaded=C.load_archives(d)
            self.assertEqual(set(loaded),set(A.POLICIES))
            self.assertTrue(C.compare(loaded,"192")[
                "F072_original_independent_and_final_allocations_identical"])
            with zipfile.ZipFile(d/"GA-Gate09-ADR060-Evidencias-duplicate.zip","w") as z:
                z.writestr("row.json",json.dumps(row("baseline")))
            with self.assertRaisesRegex(ValueError,"Duplicate"):
                C.load_archives(d)


if __name__=="__main__":
    unittest.main()
