"""High-ROI R13 shortest-route arithmetic equivalence, frozen-economy guards."""
from __future__ import annotations

from pathlib import Path
import sys
import unittest
from unittest.mock import patch

CI = Path(__file__).resolve().parents[1] / "ci"
sys.path.insert(0, str(CI))
import run_adr061 as A


class R13RouteWeightReuse(unittest.TestCase):
    def test_registered_cases_and_original_budget_identity(self):
        self.assertEqual(tuple(A.FIXTURES), ("192", "384"))
        self.assertEqual(A.POLICIES, ("baseline", "precompute"))
        self.assertEqual(A.TOL, 1e-4)
        self.assertIs(A.FIXTURES["192"]["budget"], A.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"], A.R55.LIMITS_384)
        self.assertIs(A.R13.path_audit, A.ORIGINAL_PATH_AUDIT)
        self.assertEqual(A.R51.R49.ADR48.ADR42.R21.validate_manifest(), 16)

    def test_only_valid_cases_and_policies_can_solve(self):
        for case, policy in (("2048", "precompute"), ("48", "baseline"),
                             ("384", "old-cache")):
            with self.subTest(case=case, policy=policy), self.assertRaises(ValueError):
                A.exercise(case, policy)

    def test_original_small_route_equivalence(self):
        net, flows = A.ADR42.one_group_example(0.5)
        loads, _, _ = net.snapshots(flows)
        group = net.groups[1]
        optimized = A.R13WeightsOncePerSnapshot()
        for snapshot in ({}, loads):
            original = A.ORIGINAL_PATH_AUDIT(
                net, group.origin, group.seller, snapshot, 4)
            trial = optimized(net, group.origin, group.seller, snapshot, 4)
            self.assertEqual(original, trial)
        self.assertGreater(optimized.counters()["weight_table_creation_misses"], 0)

    def test_zero_load_route_table_reused_across_distinct_empty_dictionaries(self):
        net, _ = A.ADR42.one_group_example(0.5)
        accelerated = A.R13WeightsOncePerSnapshot()
        original = A.ORIGINAL_PATH_AUDIT(net, 0, 0, {}, 4)
        for _ in range(5):
            self.assertEqual(accelerated(net, 0, 0, {}, 4), original)
        self.assertEqual(accelerated.counters()["weight_table_creation_misses"], 1)
        self.assertEqual(accelerated.counters()["weight_table_reuse_hits"], 4)

    def test_new_load_object_discards_previous_seller_specific_weights(self):
        net, flows = A.ADR42.one_group_example(0.5)
        first, _, _ = net.snapshots(flows)
        second = {key: value + 0.5 for key, value in first.items()}
        alg = A.R13WeightsOncePerSnapshot()
        a = alg(net, 0, 0, first, 4)
        b = alg(net, 0, 0, second, 4)
        self.assertEqual(a, A.ORIGINAL_PATH_AUDIT(net, 0, 0, first, 4))
        self.assertEqual(b, A.ORIGINAL_PATH_AUDIT(net, 0, 0, second, 4))
        self.assertEqual(alg.counters()["weight_table_creation_misses"], 2)
        self.assertNotEqual(a[0], b[0])

    def test_original_quartic_frozen(self):
        net, _ = A.ADR42.one_group_example(0.5)
        with self.assertRaisesRegex(ValueError, "quartic"):
            A.R13WeightsOncePerSnapshot()(net, 0, 0, {}, 3)

    def test_192_physical_world_paths_exact_on_sampled_groups(self):
        conf = A.FIXTURES["192"]["config"]
        net, flows, ledger = A.R55.R54.R53.R52.build_population(conf)
        loads, _, _ = net.snapshots(flows)
        accelerated = A.R13WeightsOncePerSnapshot()
        for group in list(sorted(net.groups.values(), key=lambda g: g.id))[:8]:
            for snap in ({}, loads):
                expected = A.ORIGINAL_PATH_AUDIT(net, group.origin, group.seller, snap, 4)
                observed = accelerated(net, group.origin, group.seller, snap, 4)
                self.assertEqual(expected, observed)
        self.assertEqual(ledger["geometry_nodes"], 5762)
        self.assertGreater(accelerated.counters()["weight_table_reuse_hits"], 0)

    def test_input_flows_and_original_checker_intact(self):
        net, flows = A.ADR42.one_group_example(0.5)
        before = A.R13._flow_checksum(flows)
        original = A.ADR42.static_certificate(net, flows)
        engine = A.R13WeightsOncePerSnapshot()
        with patch.object(A.R13, "path_audit", engine):
            # For simple toy equality should be exact even under candidate.
            patched = A.ADR42.static_certificate(net, flows)
        self.assertIs(A.R13.path_audit, A.ORIGINAL_PATH_AUDIT)
        after = A.ADR42.static_certificate(net, flows)
        self.assertEqual(before, A.R13._flow_checksum(flows))
        self.assertEqual(original["status"], patched["status"])
        self.assertEqual(original["gap_global_complementarity"],
                         patched["gap_global_complementarity"])
        self.assertEqual(original["gap_global_complementarity"],
                         after["gap_global_complementarity"])

    def test_post_certificate_rejects_unrestored_R13_patch(self):
        net, _ = A.ADR42.one_group_example(0.5)
        with patch.object(A.R13, "path_audit", A.R13WeightsOncePerSnapshot()):
            with self.assertRaisesRegex(AssertionError, "not restored"):
                A.original_post_certification(net, {"final_allocations": []})

    def test_original_post_certificate_rejects_false_final_hash(self):
        net, flows = A.ADR42.one_group_example(0.5)
        candidate = {
            "final_flow_sha256": "incorrect",
            "final_allocations": [
                [col.group_id, col.destination, list(col.edges), mass]
                for col, mass in flows.items() if mass > 0],
        }
        with self.assertRaisesRegex(AssertionError, "hash"):
            A.original_post_certification(net, candidate)

    def test_reject_duplicate_final_column(self):
        net, flows = A.ADR42.one_group_example(0.5)
        col = next(iter(flows))
        item = [col.group_id, col.destination, list(col.edges), 0.5]
        with self.assertRaisesRegex(AssertionError, "Duplicate"):
            A.original_post_certification(
                net, {"final_allocations": [item, item], "final_flow_sha256": "wrong"})

    def test_preserves_independent_original_F072_only(self):
        self.assertEqual(A.ADR42.static_certificate.__module__, "verify_complementarity")
        self.assertTrue(A.ORIGINAL_PATH_AUDIT is A.R2.path_audit)
        self.assertFalse(getattr(A, "production_approved", False))


if __name__ == "__main__":
    unittest.main()
