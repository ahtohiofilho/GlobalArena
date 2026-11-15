"""ADR058 negative contracts: changed real loads cannot share frozen route cache."""
from __future__ import annotations
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr058 as A


class SampleCache:
    def __init__(self, network, loads, exponent):
        if exponent != 4:
            raise AssertionError("quartic exponent changed")
        self.loads = dict(loads)
    def paths(self, origin, seller):
        return {origin: 0.0}, {origin: ()}, {origin: 1}
    def weights(self, seller):
        return {eid: 1.0 + value for eid, value in self.loads.items()}


class InvalidStaleCache(SampleCache):
    def weights(self, seller):
        return {eid: 1.0 for eid in self.loads}


class UnstablePathsCache(SampleCache):
    def __init__(self, network, loads, exponent):
        super().__init__(network, loads, exponent)
        self.counter = 0
    def paths(self, origin, seller):
        self.counter += 1
        return {origin: float(self.counter)}, {origin: ()}, {origin: 1}


class ADR058Contracts(unittest.TestCase):
    def test_same_workers_and_geometry_across_192_and_384(self):
        a, b = A.FIXTURES["192"], A.FIXTURES["384"]
        self.assertEqual((a["expected_workers"], b["expected_workers"]),
                         (4096, 4096))
        self.assertEqual((a["config"].m, b["config"].m), (24, 24))
        self.assertEqual((a["markets"], b["markets"]), (384, 768))

    def test_original_manifest_original_budget_and_F072_tolerance(self):
        self.assertEqual(
            A.R57.R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(), 16)
        self.assertEqual(A.R57.R56.R55.TOL, 1e-4)
        self.assertIs(A.FIXTURES["192"]["budget"],
                      A.R57.R56.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"],
                      A.R57.R56.R55.LIMITS_384)

    def test_new_experiment_has_no_new_numeric_budget(self):
        self.assertFalse(hasattr(A, "LIMITS_058"))
        self.assertEqual(A.TRANSFER_FRACTION_OF_ORIGINAL_FUNDED_COLUMN, 0.25)
        self.assertEqual(A.MAX_SIGNED_SAMPLE_TARGETS, 8)

    def test_only_registered_two_physical_worker_fixtures(self):
        for key in ("", "48", "96", "2048"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                A.report_fixture(key)

    def test_producer_flow_redistribution_preserves_ledger(self):
        conf = A.FIXTURES["192"]["config"]
        net, initial, meta = A.R57.R56.R55.R54.R53.R52.build_population(conf)
        oldhash = A._flow_checksum(initial)
        changed, loads_a, loads_b, summary = A.physical_pair(net, initial)
        self.assertEqual(A._flow_checksum(initial), oldhash)
        self.assertNotEqual(A._flow_checksum(changed), oldhash)
        self.assertEqual(set(changed), set(initial))
        self.assertEqual(summary["physical_group_shifts"], 96)
        self.assertEqual(summary["market_count_positive_both"], 384)
        self.assertGreater(summary["changed_edge_loads"], 0)
        self.assertNotEqual(loads_a, loads_b)
        self.assertEqual(summary["perturbed_allocation_F072_original_certificate"],
                         "NOT_ATTEMPTED")
        self.assertTrue(meta["travel_time_does_not_duplicate_demand"])

    def test_funded_384_group_flow_conservation(self):
        conf = A.FIXTURES["384"]["config"]
        net, flows, _ = A.R57.R56.R55.R54.R53.R52.build_population(conf)
        updated, loads_a, loads_b, summary = A.physical_pair(net, flows)
        self.assertEqual(summary["physical_group_shifts"], 192)
        self.assertEqual(summary["market_count_positive_both"], 768)
        self.assertEqual(len(flows), len(updated))
        self.assertGreater(summary["changed_edge_loads"], 0)

    def test_small_perturbation_preserves_seller_specific_weight_difference(self):
        with self.assertRaisesRegex(AssertionError, "unchanged seller weights"):
            A.weight_drift(InvalidStaleCache(None, {1: 1.}, 4),
                           InvalidStaleCache(None, {1: 2.}, 4), (0,))
        row = A.weight_drift(SampleCache(None, {1: 1.}, 4),
                             SampleCache(None, {1: 2.}, 4), (0,))
        self.assertEqual(row["total_changed_edge_weights_across_sellers"], 1)
        self.assertTrue(row["frozen_old_cache_must_NOT_be_reused_on_new_loads"])

    def test_cache_weights_finite_and_same_edge_id_required(self):
        with self.assertRaisesRegex(AssertionError, "edge sets"):
            A.weight_drift(SampleCache(None, {1: 1.}, 4),
                           SampleCache(None, {2: 2.}, 4), (0,))

    def test_cold_and_warm_signature_invariance(self):
        cache = SampleCache(None, {1: 1.0}, 4)
        queries = ((5, 1), (5, 1), (6, 2))
        a = A.observe_pass(cache, queries)
        b = A.observe_pass(cache, queries)
        A.check_frozen(a, b, "frozen")
        self.assertEqual(a["unique_origin_seller_pairs"], 2)
        self.assertEqual(a["group_queries"], 3)
        self.assertTrue(a["unprofiled_wall_time_NOT_CSharp_SLA"])

    def test_frozen_cache_path_mutation_rejected(self):
        cache = UnstablePathsCache(None, {1: 2.0}, 4)
        one = A.observe_pass(cache, ((5, 1),))
        two = A.observe_pass(cache, ((5, 1),))
        with self.assertRaisesRegex(AssertionError, "Frozen paths drifted"):
            A.check_frozen(one, two, "mock")

    def test_missing_origin_in_cache_rejected(self):
        class Missing:
            def paths(self, origin, seller):
                return ({999: 0.}, {999: ()}, {999: 1})
        with self.assertRaisesRegex(AssertionError, "missing source"):
            A.observe_pass(Missing(), ((1, 0),))

    def test_lifecycle_probe_192_uses_distinct_cache_on_each_snapshot(self):
        constructed = []
        class RecordingCache(SampleCache):
            def __init__(self, net, loads, exp):
                constructed.append(dict(loads))
                super().__init__(net, loads, exp)
        with (patch.object(A, "FrozenRouteCache", RecordingCache),
              patch.object(A.R57.R56.R55.R54.R53.R52.ADR51,
                           "solve_chunked",
                           side_effect=AssertionError("Never run F072 in probe"))):
            result = A.report_fixture("192")
        self.assertEqual(len(constructed), 2)
        self.assertNotEqual(constructed[0], constructed[1])
        self.assertEqual(result["status"],
                         "PASS_ADR058_TWO_FROZEN_SNAPSHOTS_LIFECYCLE_OBSERVED")
        self.assertFalse(result["commercial_F072_attempted_in_this_cache_probe"])
        self.assertTrue(result["original_F072_mandatory_regression_is_separate_CI_job"])
        self.assertFalse(result["production_approved"])
        self.assertTrue(result["cache_is_original_frozen_route_cache_no_mutation"])

    def test_lifecycle_probe_384_does_not_duplicate_worker_demand(self):
        with patch.object(A, "FrozenRouteCache", SampleCache):
            result = A.report_fixture("384")
        self.assertEqual(result["physical_workers"], 4096)
        self.assertEqual(result["demand_nodes"], 384)
        self.assertEqual(result["market_pairs"], 768)
        self.assertTrue(result["same_physical_workers_not_multiple_demand_sources"])
        self.assertTrue(result["rss_is_process_lifetime_highwater_not_current_memory"])

    def test_stale_cache_does_not_claim_validity_across_loads(self):
        with patch.object(A, "FrozenRouteCache", InvalidStaleCache):
            with self.assertRaisesRegex(AssertionError, "unchanged seller weights"):
                A.report_fixture("192")

    def test_memory_is_traced_python_only_and_release_is_observed(self):
        with patch.object(A, "FrozenRouteCache", SampleCache):
            result = A.report_fixture("192")
        self.assertTrue(result["tracemalloc_started_after_network_construction"])
        self.assertTrue(result["tracemalloc_does_not_measure_full_solver_or_native_heap"])
        self.assertGreaterEqual(result["observed_python_heap_drop_after_release_bytes"], 0)
        self.assertTrue(result["no_F059_ratification"])
        self.assertTrue(result["CSharp_Godot_untouched"])


if __name__ == "__main__":
    unittest.main()
