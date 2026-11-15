"""ADR059 cross-policy equivalence/no-false-memory-savings negative gates."""
from __future__ import annotations

import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
import zipfile

CI = Path(__file__).resolve().parents[1] / "ci"
sys.path.insert(0, str(CI))
import compare_adr059 as C
import run_adr059 as A


def synthetic(policy, peak=200000, fixture="192"):
    ff = A.FIXTURES[fixture]
    phase = {"route_response_digest_sampled": "same",
             "group_queries": ff["groups"]}
    return {
        "schema": A.SCHEMA,
        "status": "PASS_ADR059_ORIGINAL_FROZEN_EPOCH_CACHE_POLICY_LAB",
        "policy": policy,
        "fixture": fixture,
        "goldberg_geometry_nodes": 5762,
        "workers": 4096,
        "demand_nodes": ff["demand_nodes"],
        "production_nodes": ff["production_nodes"],
        "groups": ff["groups"],
        "markets_node_item": ff["markets"],
        "worker_ledger_sha256": "workers",
        "original_flow_sha256": "flowA",
        "changed_flow_sha256": "flowB",
        "snapshot_A_epoch_sha256": "epochA",
        "snapshot_B_epoch_sha256": "epochB",
        "different_epoch_tokens": True,
        "old_epoch_token_rejected_after_switch": True,
        "original_quartic_exponent": 4,
        "original_seller_weights_A": [{"seller": 0, "original_weights_sha256": "one"}],
        "original_seller_weights_B": [{"seller": 0, "original_weights_sha256": "two"}],
        "sellers_with_changed_original_weights": 1,
        "first_A_cold": dict(phase),
        "repeat_A_warm": dict(phase),
        "first_B_cold": dict(phase),
        "repeat_B_warm": dict(phase),
        "B_after_A_retire": dict(phase),
        "old_cache_still_owned_during_B": policy == "overlap",
        "release_A_before_B_build_executed": policy == "release_first",
        "traced_python_bytes_after_release_A_before_B": (
            10000 if policy == "release_first" else None),
        "python_tracemalloc_bytes": {"peak_total": peak},
        "A_and_B_not_original_F072_certificates": True,
        "original_independent_F072_separate_CI_regression": True,
        "production_approved": False,
    }


class ADR059ComparisonContracts(unittest.TestCase):
    def test_cross_policy_equivalent_and_reduced_peak(self):
        result = C.compare({
            "overlap": synthetic("overlap", 250000),
            "release_first": synthetic("release_first", 125000)}, "192")
        self.assertEqual(result["status"],
                         "PASS_ADR059_POLICY_EQUIVALENCE_AND_PYTHON_HEAP_REDUCTION")
        self.assertEqual(result["python_traced_peak_difference_bytes"], 125000)
        self.assertEqual(result["python_traced_peak_reduction_fraction"], 0.5)

    def test_measured_no_memory_saving_is_explicit_NO_GO_not_fake_pass(self):
        result = C.compare({
            "overlap": synthetic("overlap", 100000),
            "release_first": synthetic("release_first", 150000)}, "192")
        self.assertEqual(result["status"],
                         "NO_GO_ADR059_PYTHON_HEAP_REDUCTION_NOT_OBSERVED")
        self.assertLess(result["python_traced_peak_difference_bytes"], 0)

    def test_stale_epoch_access_false_claim_rejected(self):
        values = {"overlap": synthetic("overlap"),
                  "release_first": synthetic("release_first")}
        values["release_first"]["old_epoch_token_rejected_after_switch"] = False
        with self.assertRaisesRegex(AssertionError, "physical gate"):
            C.compare(values, "192")

    def test_weight_mismatch_rejected_even_when_sampled_paths_same(self):
        values = {"overlap": synthetic("overlap"),
                  "release_first": synthetic("release_first")}
        values["release_first"]["original_seller_weights_B"][0][
            "original_weights_sha256"] = "stale"
        with self.assertRaisesRegex(AssertionError, "weight mismatch"):
            C.compare(values, "192")

    def test_sampled_route_mismatch_rejected_even_when_weights_match(self):
        values = {"overlap": synthetic("overlap"),
                  "release_first": synthetic("release_first")}
        values["release_first"]["first_B_cold"][
            "route_response_digest_sampled"] = "divergent"
        with self.assertRaisesRegex(AssertionError, "route digest"):
            C.compare(values, "192")

    def test_unreleased_old_cache_rejected(self):
        values = {"overlap": synthetic("overlap"),
                  "release_first": synthetic("release_first")}
        values["release_first"]["old_cache_still_owned_during_B"] = True
        with self.assertRaisesRegex(AssertionError, "still kept old cache"):
            C.compare(values, "192")

    def test_missing_policy_rejected(self):
        with self.assertRaises(ValueError):
            C.compare({"overlap": synthetic("overlap")}, "192")
        with self.assertRaises(ValueError):
            C.compare({"overlap": synthetic("overlap"),
                       "release_first": synthetic("release_first")}, "2048")

    def test_load_two_actual_zip_containers_and_reject_duplicates(self):
        with TemporaryDirectory() as tmp:
            root = Path(tmp)
            for policy in A.POLICIES:
                outer = root / ("GA-Gate09-ADR059-Evidencias-192-" + policy + ".zip")
                with zipfile.ZipFile(outer, "w") as zipped:
                    zipped.writestr("evidence.json", json.dumps(synthetic(policy)))
            loaded = C.load_policy_archives(root)
            self.assertEqual(set(loaded), set(A.POLICIES))
            self.assertEqual(C.compare(loaded, "192")["status"],
                             "NO_GO_ADR059_PYTHON_HEAP_REDUCTION_NOT_OBSERVED")
            with zipfile.ZipFile(root / "GA-Gate09-ADR059-Evidencias-duplicate.zip", "w") as z:
                z.writestr("evidence.json", json.dumps(synthetic("overlap")))
            with self.assertRaisesRegex(ValueError, "Duplicate policy"):
                C.load_policy_archives(root)


if __name__ == "__main__":
    unittest.main()
