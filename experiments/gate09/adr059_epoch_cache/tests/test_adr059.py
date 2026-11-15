"""ADR059 negative gates for epoch invalidation and reversible ownership policy."""
from __future__ import annotations

from pathlib import Path
import sys
import unittest
import weakref
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr059 as A


class FakeNet:
    pass


class FakeCache:
    def __init__(self, network, loads, exponent):
        if exponent != 4:
            raise AssertionError("Original quartic is mandatory")
        self.loads = dict(loads)
    def paths(self, origin, seller):
        return {origin: 0.}, {origin: ()}, {origin: 1}
    def weights(self, seller):
        return {key: 1.0 + value for key, value in self.loads.items()}


class BadStaleCache(FakeCache):
    def weights(self, seller):
        return {key: 1.0 for key in self.loads}


class ADR059EpochContracts(unittest.TestCase):
    def test_only_two_original_physical_worker_fixtures(self):
        self.assertEqual(tuple(A.FIXTURES), ("192", "384"))
        for bad in ("", "48", "96", "2048"):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                A.run_policy(bad, "release_first")

    def test_only_registered_reversible_policies(self):
        with self.assertRaises(ValueError):
            A.EpochCacheOwner(FakeNet(), "unbounded", FakeCache)
        with self.assertRaises(ValueError):
            A.run_policy("192", "bad")

    def test_original_prices_quartic_F072_manifest_and_budgets_not_changed(self):
        self.assertEqual(A.EXPONENT, 4)
        self.assertEqual(A.R58.R57.R56.R55.TOL, 1e-4)
        self.assertEqual(
            A.R58.R57.R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(),
            16)
        self.assertIs(A.FIXTURES["192"]["budget"],
                      A.R58.R57.R56.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"],
                      A.R58.R57.R56.R55.LIMITS_384)

    def test_tokens_deterministic_for_unchanged_sparse_loads(self):
        a = {3: 2., 1: 0.2}
        b = {1: 0.2, 3: 2.}
        self.assertEqual(A.load_epoch_token(a), A.load_epoch_token(b))
        self.assertNotEqual(A.load_epoch_token(a),
                            A.load_epoch_token({1: 0.2, 3: 3.}))

    def test_nonfinite_or_negative_loads_fail_closed(self):
        for value in (-1.0, float("inf"), float("nan")):
            with self.subTest(value=value), self.assertRaises(ValueError):
                A.load_epoch_token({2: value})

    def test_no_rebuild_for_same_snapshot_and_original_stays_available(self):
        owner = A.EpochCacheOwner(FakeNet(), "release_first", FakeCache)
        key = owner.open({1: 0.5})
        with self.assertRaisesRegex(ValueError, "No physical load change"):
            owner.change({1: 0.5})
        self.assertIsNotNone(owner.paths(key, 12, 1))
        owner.close()

    def test_release_first_drops_old_cache_before_new_factory_called(self):
        constructed = []
        def factory(net, loads, exponent):
            if constructed:
                # Only weakref retained; any live A here indicates wrong policy.
                self.assertIsNone(constructed[0]())
            obj = FakeCache(net, loads, exponent)
            constructed.append(weakref.ref(obj))
            return obj
        owner = A.EpochCacheOwner(FakeNet(), "release_first", factory)
        old = owner.open({1: 1.})
        new = owner.change({1: 2.})
        self.assertEqual(len(constructed), 2)
        self.assertIsNone(constructed[0]())
        self.assertFalse(owner.retired_cache_still_owned)
        self.assertTrue(owner.release_before_build_performed)
        self.assertNotEqual(old, new)
        with self.assertRaises(A.StaleSnapshotError):
            owner.paths(old, 1, 0)
        owner.close()

    def test_overlap_keeps_A_until_explicit_retirement_but_cannot_serve_old_epoch(self):
        refs = []
        def factory(net, loads, exponent):
            if refs:
                self.assertIsNotNone(refs[0]())
            obj = FakeCache(net, loads, exponent)
            refs.append(weakref.ref(obj))
            return obj
        owner = A.EpochCacheOwner(FakeNet(), "overlap", factory)
        old = owner.open({1: 1.})
        new = owner.change({1: 2.})
        self.assertTrue(owner.retired_cache_still_owned)
        self.assertFalse(owner.release_before_build_performed)
        self.assertIsNotNone(refs[0]())
        with self.assertRaises(A.StaleSnapshotError):
            owner.weights(old, 0)
        self.assertIsNotNone(owner.weights(new, 0))
        owner.release_retired()
        self.assertIsNone(refs[0]())
        owner.close()

    def test_factory_error_does_not_reactivate_old_epoch(self):
        calls = []
        def factory(net, loads, exponent):
            calls.append(dict(loads))
            if len(calls) == 2:
                raise RuntimeError("B failed")
            return FakeCache(net, loads, exponent)
        owner = A.EpochCacheOwner(FakeNet(), "release_first", factory)
        old = owner.open({1: 1.})
        with self.assertRaises(RuntimeError):
            owner.change({1: 2.})
        with self.assertRaises(A.StaleSnapshotError):
            owner.paths(old, 1, 0)
        self.assertFalse(owner.retired_cache_still_owned)
        owner.close()

    def test_can_reuse_only_current_epoch(self):
        owner = A.EpochCacheOwner(FakeNet(), "overlap", FakeCache)
        a = owner.open({1: 1.})
        b = owner.change({1: 2.})
        self.assertEqual(owner.paths(b, 2, 1)[0][2], 0.)
        with self.assertRaises(A.StaleSnapshotError):
            owner.paths(a, 2, 1)
        owner.close()
        with self.assertRaises(A.StaleSnapshotError):
            owner.paths(b, 2, 1)

    def test_cannot_change_twice_with_unreleased_retired_overlap(self):
        owner = A.EpochCacheOwner(FakeNet(), "overlap", FakeCache)
        owner.open({1: 1.})
        owner.change({1: 2.})
        with self.assertRaisesRegex(ValueError, "Retired cache"):
            owner.change({1: 3.})
        owner.release_retired()
        owner.change({1: 3.})
        owner.close()

    def test_all_original_edge_weights_sampled_without_shadow_map(self):
        owner = A.EpochCacheOwner(FakeNet(), "release_first", FakeCache)
        a = owner.open({1: 2.})
        weights_a = A.weight_fingerprints(owner, a, (0, 1))
        b = owner.change({1: 3.})
        weights_b = A.weight_fingerprints(owner, b, (0, 1))
        self.assertEqual(len(weights_a), 2)
        self.assertNotEqual(weights_a, weights_b)
        owner.close()

    def test_original_route_signatures_stable_within_one_epoch(self):
        owner = A.EpochCacheOwner(FakeNet(), "release_first", FakeCache)
        key = owner.open({1: 2.})
        a = A.observe(owner, key, ((1, 0), (1, 0), (2, 1)))
        b = A.observe(owner, key, ((1, 0), (1, 0), (2, 1)))
        A.assert_same_routes(a, b)
        self.assertEqual(a["distinct_origin_seller_keys"], 2)
        owner.close()

    def test_false_route_result_rejected(self):
        a = {"route_response_digest_sampled": "A", "group_queries": 4}
        b = {"route_response_digest_sampled": "B", "group_queries": 4}
        with self.assertRaises(AssertionError):
            A.assert_same_routes(a, b)

    def test_actual_worker_192_policy_with_fake_oracle_never_runs_solver(self):
        with (patch.object(A, "FrozenRouteCache", FakeCache),
              patch.object(A.R58.R57.R56.R55.R54.R53.R52.ADR51,
                           "solve_chunked",
                           side_effect=AssertionError("F072 forbidden in probe"))):
            row = A.run_policy("192", "release_first")
        self.assertEqual((row["workers"], row["demand_nodes"],
                          row["markets_node_item"]), (4096, 192, 384))
        self.assertEqual(row["physical_redistribution"]["changed_edge_loads"], 89)
        self.assertTrue(row["old_epoch_token_rejected_after_switch"])
        self.assertTrue(row["release_A_before_B_build_executed"])
        self.assertFalse(row["old_cache_still_owned_during_B"])
        self.assertFalse(row["production_approved"])

    def test_actual_worker_384_overlap_baseline_with_fake_oracle(self):
        with patch.object(A, "FrozenRouteCache", FakeCache):
            row = A.run_policy("384", "overlap")
        self.assertEqual((row["workers"], row["demand_nodes"],
                          row["markets_node_item"]), (4096, 384, 768))
        self.assertEqual(row["physical_redistribution"]["changed_edge_loads"], 184)
        self.assertTrue(row["old_cache_still_owned_during_B"])
        self.assertFalse(row["release_A_before_B_build_executed"])

    def test_fake_stale_weights_cannot_pass_original_weight_drift_gate(self):
        with patch.object(A, "FrozenRouteCache", BadStaleCache):
            with self.assertRaisesRegex(AssertionError, "invalidate original edge weights"):
                A.run_policy("192", "release_first")

    def test_memory_source_and_governance_disclaimers(self):
        with patch.object(A, "FrozenRouteCache", FakeCache):
            row = A.run_policy("192", "release_first")
        self.assertTrue(row["tracemalloc_excludes_full_F072_solver_and_native_memory"])
        self.assertTrue(row["process_RSS_is_lifetime_highwater_not_current_memory"])
        self.assertTrue(row["cache_owner_is_experimental_not_original_solver_patch"])
        self.assertTrue(row["A_and_B_not_original_F072_certificates"])
        self.assertTrue(row["F059_financial_policy_pending"])
        self.assertFalse(row["production_approved"])


if __name__ == "__main__":
    unittest.main()
