"""ADR057 route-cache observability contracts; no economic solver shortcut."""
from __future__ import annotations
import cProfile
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr057 as A


class FakeCache:
    def paths(self, origin, seller):
        return ({origin: 0.0}, {origin: ()}, {origin: 1})


class UnstableCache:
    def __init__(self, *args):
        self.calls = 0
    def paths(self, origin, seller):
        self.calls += 1
        return ({origin: self.calls}, {origin: ()}, {origin: 1})


class ADR057CacheContracts(unittest.TestCase):
    def test_world_and_workers_remain_same(self):
        one, two = A.FIXTURES["192"], A.FIXTURES["384"]
        self.assertEqual(one["config"].m, 24)
        self.assertEqual(two["config"].m, 24)
        self.assertEqual(one["expected_workers"], 4096)
        self.assertEqual(two["expected_workers"], 4096)
        self.assertEqual((one["demand_nodes"], two["demand_nodes"]), (192, 384))
        self.assertEqual((one["markets"], two["markets"]), (384, 768))

    def test_original_f072_tolerance_and_budgets_remain_frozen(self):
        self.assertEqual(A.R56.R55.TOL, 1e-4)
        self.assertIs(A.FIXTURES["192"]["budget"], A.R56.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"], A.R56.R55.LIMITS_384)
        self.assertEqual(
            A.R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(), 16)

    def test_empty_or_unregistered_fixture_rejected(self):
        for key in ("", "48", "96", "2048"):
            with self.subTest(key=key):
                with self.assertRaises(ValueError):
                    A.diagnostic(key)

    def test_profile_is_observational_not_hit_rate(self):
        p = cProfile.Profile()
        p.enable()
        sum(range(10))
        p.disable()
        observed = A.profile_counts(p)
        self.assertIsInstance(observed, dict)
        self.assertTrue(A.EXPERIMENT_ONLY)
        self.assertEqual(A.SCHEMA.endswith("_V1"), True)

    def test_each_group_queried_even_if_keys_repeat(self):
        item = A.phase(FakeCache(), ((1, 0), (1, 0), (2, 0)))
        self.assertEqual(item["calls_requested"], 3)
        self.assertEqual(item["distinct_origin_seller_keys"], 2)
        self.assertEqual(len(item["route_result_signatures"]), 3)
        self.assertTrue(item["cprofile_alters_measured_wall_time"])

    def test_fake_missing_origin_rejected(self):
        class Missing:
            def paths(self, origin, seller):
                return ({9: 0.0}, {9: ()}, {9: 1})
        with self.assertRaises(AssertionError):
            A.phase(Missing(), ((1, 0),))

    def test_cache_top_level_fields_only_not_total_retained_size(self):
        cache = FakeCache()
        cache.data = {1: "test", 2: "test"}
        info = A.cache_shallow_fields(cache)
        self.assertTrue(info["fields_introspectable"])
        item = next(x for x in info["fields"] if x["field"] == "data")
        self.assertEqual(item["entry_count"], 2)
        self.assertTrue(item["shallow_is_NOT_reachable_object_graph_size"])

    def test_profile_whole_192_worker_ledger_but_never_run_f072(self):
        with (patch.object(A, "FrozenRouteCache", FakeCache),
              patch.object(A.R56.R55.R54.R53.R52.ADR51, "solve_chunked",
                           side_effect=AssertionError("No F072 in cache probe"))):
            row = A.diagnostic("192")
        self.assertEqual(row["status"], "PASS_ADR057_UNCHANGED_ORIGINAL_CACHE_DIAGNOSTIC")
        self.assertEqual(row["workers"], 4096)
        self.assertEqual(row["demand_nodes"], 192)
        self.assertEqual(row["producer_groups"], 96)
        self.assertEqual(row["markets_node_product"], 384)
        self.assertFalse(row["production_approved"])
        self.assertTrue(row["python_traced_heap_excludes_full_original_F072_solver"])
        self.assertEqual(row["original_F072_certificates_here"],
                         "NOT_ATTEMPTED_CACHE_OBSERVATION_ONLY")

    def test_384_frozen_network_funded_by_worker_demand(self):
        with patch.object(A, "FrozenRouteCache", FakeCache):
            row = A.diagnostic("384")
        self.assertEqual((row["workers"], row["production_nodes"],
                          row["demand_nodes"], row["producer_groups"],
                          row["markets_node_product"]), (4096, 96, 384, 192, 768))
        self.assertTrue(row["production_is_subset_of_demand"])
        self.assertTrue(row["traveler_demand_not_duplicated"])

    def test_changed_frozen_paths_fail_not_averaged(self):
        with patch.object(A, "FrozenRouteCache", UnstableCache):
            with self.assertRaisesRegex(AssertionError, "Repeated frozen"):
                A.diagnostic("192")

    def test_shallow_rss_is_not_labelled_as_solver_memory(self):
        with patch.object(A, "FrozenRouteCache", FakeCache):
            row = A.diagnostic("192")
        self.assertTrue(row["process_peak_rss_is_lifetime_highwater_not_delta"])
        self.assertTrue(row["python_traced_heap_excludes_graph_built_before_tracing"])
        self.assertTrue(row["dijkstra_call_reduction_proxy_only_not_measured_cache_hit_rate"])
        self.assertTrue(row["independent_F072_regression_in_separate_CI_job"])

    def test_cache_probe_does_not_authorize_f059_or_game_engine(self):
        self.assertFalse(getattr(A, "F059_ratified", False))
        self.assertTrue(A.EXPERIMENT_ONLY)


if __name__ == "__main__":
    unittest.main()
