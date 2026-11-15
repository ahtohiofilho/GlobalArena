"""ADR056 source-frozen profiler, mandatory original certificates and heap honesty."""
from __future__ import annotations

import cProfile
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr056 as A


def valid_row(key="384", certified=True):
    f = A.FIXTURES[key]
    return {
        "physical_workers": f["expected_workers"],
        "production_nodes": f["production_nodes"],
        "demand_nodes": f["demand_nodes"],
        "aggregated_production_groups": f["groups"],
        "worker_sourced_markets_node_item": f["markets"],
        "worker_ledger_sha256": "worker-ledger-hash",
        "initial_physical_flow_sha256": "funded-flow-hash",
        "F072_attempted": True,
        "F072_independent_certified": certified,
        "original_F072_global_FINAL_if_pass": 1e-12 if certified else None,
        "original_F072_worst_group_FINAL_if_pass": 2e-12 if certified else None,
        "F072_certificate_original_only":
            "PASS_STATIC_F072_FIRST_ORDER_ONLY" if certified else None,
        "solver_status": ("PASS_BLOCK_MCP_FULL_ORIGINAL_F072_CERTIFIED"
                          if certified else "NO_GO_BOUNDED"),
        "solver_work": {
            "H_evaluations_including_rejected_trials": 32,
            "matrixfree_Jv_and_Jtv_products": 389,
            "separate_shortest_path_scans": 240,
        },
        "production_nodes_subset_demand_nodes": True,
        "worker_demand_conserved_without_extra_travel_demand": True,
        "original_congestion_exponent_unchanged": True,
    }


def probe():
    return {
        "original_worker_ledger_sha256": "worker-ledger-hash",
        "original_initial_flow_sha256": "funded-flow-hash",
        "python_traced_peak_bytes_during_construction": 99,
    }


class ADR056DiagnosticContracts(unittest.TestCase):
    def test_population_constant_from_192_to_384(self):
        one, two = A.FIXTURES["192"], A.FIXTURES["384"]
        self.assertEqual(one["expected_workers"], two["expected_workers"])
        self.assertEqual(one["expected_workers"], 4096)
        self.assertEqual((one["demand_nodes"], two["demand_nodes"]), (192, 384))
        self.assertEqual((one["markets"], two["markets"]), (384, 768))
        self.assertEqual((one["groups"], two["groups"]), (96, 192))
        self.assertEqual((one["config"].m, two["config"].m), (24, 24))

    def test_exact_original_budgets_reused_by_identity(self):
        self.assertIs(A.FIXTURES["192"]["budget"], A.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"], A.R55.LIMITS_384)
        self.assertEqual(A.FIXTURES["384"]["budget"].max_krylov_products, 60000)
        self.assertEqual(A.FIXTURES["384"]["budget"].max_block_coefficients, 250000)

    def test_original_manifest_and_f072_tolerance_untouched(self):
        self.assertEqual(
            A.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(), 16)
        self.assertEqual(A.R55.TOL, 1e-4)
        self.assertEqual(A.R55.R54.R53.TOLERANCE, 1e-4)

    def test_unregistered_fixture_or_2048_not_accepted(self):
        with self.assertRaises(ValueError):
            A.run("2048")
        with self.assertRaises(ValueError):
            A.solve_profiled("48", probe())

    def test_384_original_pass_profile_is_diagnostic_only(self):
        with patch.object(A.R55, "solve_candidate", return_value=valid_row("384")):
            result = A.solve_profiled("384", probe())
        self.assertTrue(result["original_F072_independent_certified"])
        self.assertTrue(result["tracemalloc_disabled_during_solver"])
        self.assertFalse(result["production_approved"])
        self.assertIn("top_25_self_solver_source", result["profile_calls_and_hotspots"])

    def test_192_reference_is_the_original_certified_wrapper(self):
        with patch.object(A.R55.R54, "solve_candidate_192", return_value=valid_row("192")):
            result = A.solve_profiled("192", probe())
        self.assertEqual(result["original_F072_global_gap"], 1e-12)
        self.assertEqual(result["original_F072_worst_group_gap"], 2e-12)

    def test_original_certificate_absent_is_hard_error(self):
        with patch.object(A.R55, "solve_candidate", return_value=valid_row("384", False)):
            with self.assertRaisesRegex(AssertionError, "Mandatory original F072"):
                A.solve_profiled("384", probe())

    def test_seed_or_nonoriginal_claim_rejected(self):
        r = valid_row("384")
        r["F072_certificate_original_only"] = "POSITIVE_SEED_ONLY"
        with patch.object(A.R55, "solve_candidate", return_value=r):
            with self.assertRaisesRegex(AssertionError, "not original"):
                A.solve_profiled("384", probe())

    def test_invalid_original_gap_rejected(self):
        r = valid_row("384")
        r["original_F072_worst_group_FINAL_if_pass"] = 1e-2
        with patch.object(A.R55, "solve_candidate", return_value=r):
            with self.assertRaisesRegex(AssertionError, "Mandatory original F072"):
                A.solve_profiled("384", probe())

    def test_producer_no_longer_demanding_is_rejected(self):
        r = valid_row("384")
        r["production_nodes_subset_demand_nodes"] = False
        with patch.object(A.R55, "solve_candidate", return_value=r):
            with self.assertRaisesRegex(AssertionError, "physical or quartic"):
                A.solve_profiled("384", probe())

    def test_workers_or_snapshot_hash_drift_rejected(self):
        r = valid_row("384")
        r["worker_ledger_sha256"] = "different workers"
        with patch.object(A.R55, "solve_candidate", return_value=r):
            with self.assertRaisesRegex(AssertionError, "physical or quartic"):
                A.solve_profiled("384", probe())

    def test_profiled_output_is_nested_nonadditive(self):
        def work():
            return sum(i * i for i in range(50))
        p = cProfile.Profile()
        p.enable()
        work()
        p.disable()
        data = A.hotspots(p)
        self.assertTrue(data["cumulative_time_nested_nonadditive"])
        self.assertTrue(data["cprofile_is_instrumented_wall_timing_not_CSharp_CPU"])
        self.assertTrue(data["top_20_cumulative_functions_all"])
        self.assertTrue(data["top_20_self_functions_all"])

    def test_rss_unavailable_is_null_not_synthetic(self):
        with patch.object(A.R55, "peak_process_rss_bytes",
                          side_effect=OSError("cannot read")):
            r = A.rss_observation()
        self.assertIsNone(r["process_peak_rss_bytes"])
        self.assertIn("UNAVAILABLE", r["rss_source"])

    def test_python_heap_probe_stops_tracing(self):
        self.assertFalse(A.tracemalloc.is_tracing())
        snapshot = A.build_probe("192")
        self.assertFalse(A.tracemalloc.is_tracing())
        self.assertEqual(snapshot["physical_contract"]["workers"], 4096)
        self.assertEqual(snapshot["physical_contract"]["demand_nodes"], 192)
        self.assertEqual(snapshot["physical_contract"]["markets"], 384)
        self.assertTrue(snapshot["tracemalloc_stopped_before_solver"])
        self.assertGreater(snapshot["python_traced_peak_bytes_during_construction"], 0)
        self.assertTrue(snapshot["traced_python_heap_is_NOT_total_RSS_or_solver_allocation"])

    def test_384_physical_probe_has_no_synthetic_demand(self):
        snapshot = A.build_probe("384")
        self.assertEqual(snapshot["physical_contract"]["workers"], 4096)
        self.assertEqual(snapshot["physical_contract"]["demand_nodes"], 384)
        self.assertEqual(snapshot["physical_contract"]["production_nodes"], 96)
        self.assertTrue(snapshot["physical_contract"]["producers_subset_demand"])

    def test_profiles_do_not_commit_production_or_financial_policy(self):
        self.assertFalse(getattr(A, "F059_ratified", False))
        self.assertFalse(getattr(A, "production_approved", False))


if __name__ == "__main__":
    unittest.main()
