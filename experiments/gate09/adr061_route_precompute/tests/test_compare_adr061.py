"""ADR061 strict original F072 equivalence and explicit one-shot CPU ROI test."""
from __future__ import annotations

import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
import zipfile

CI = Path(__file__).resolve().parents[1] / "ci"
sys.path.insert(0, str(CI))
import compare_adr061 as C
import run_adr061 as A


def sample(policy, fixture="384", solver_cpu=100.0):
    f=A.FIXTURES[fixture]
    return {
        "schema":A.SCHEMA,
        "policy":policy,
        "fixture":fixture,
        "status":"PASS_ADR061_ORIGINAL_F072_WITH_UNPATCHED_POST_CERTIFICATION",
        "workers":f["workers"],
        "production_nodes":f["production_nodes"],
        "demand_nodes":f["demand_nodes"],
        "groups":f["groups"],
        "markets":f["markets"],
        "worker_ledger_sha256":"ledger",
        "initial_flow_sha256":"initial",
        "original_final_F072_flow_sha256":"final",
        "original_static_F072_certificate":"PASS_STATIC_F072_FIRST_ORDER_ONLY",
        "original_global_gap":1e-13,
        "original_worst_group_gap":2e-13,
        "original_work_counters":{"Krylov":2643,"H":80},
        "original_generations":[{"g":1}],
        "original_budget_limits":vars(f["budget"]),
        "original_quartic_exponent":4,
        "original_independent_ADR042_FULL_post_certificate":True,
        "R13_path_audit_restored_before_original_post_certificate":True,
        "experimental_route_counters":(
            {"R13_path_audit_calls":256,
             "weight_table_reuse_hits":200,
             "weight_table_creation_misses":56} if policy=="precompute" else None),
        "observed_solver_cpu_s":solver_cpu,
        "native_process_rss_after":[1000000,"test"],
        "production_approved":False,
    }


class OriginalEquivalence(unittest.TestCase):
    def setUp(self):
        self.records = {"baseline":sample("baseline"),
                        "precompute":sample("precompute",solver_cpu=75)}

    def test_candidate_cpu_gain_above_20_percent_and_exact_F072(self):
        r=C.compare(self.records,"384")
        self.assertEqual(r["status"],
                         "PASS_ADR061_ORIGINAL_F072_PARITY_AND_INDICATIVE_CPU_ROI")
        self.assertEqual(r["observed_cpu_gain_fraction_one_run"],.25)
        self.assertTrue(r["F072_original_post_certificate_identical"])

    def test_under_20_percent_is_explicit_NO_GO(self):
        self.records["precompute"]["observed_solver_cpu_s"]=90
        r=C.compare(self.records,"384")
        self.assertEqual(r["status"],"NO_GO_ADR061_CPU_ROI_NOT_DEMONSTRATED")

    def test_memory_regression_is_no_go_even_if_cpu_faster(self):
        self.records["precompute"]["native_process_rss_after"][0]=1200000
        r=C.compare(self.records,"384")
        self.assertEqual(r["status"],"NO_GO_ADR061_CPU_ROI_NOT_DEMONSTRATED")

    def test_original_final_flow_hash_mismatch_is_hard_fail(self):
        self.records["precompute"]["original_final_F072_flow_sha256"]="not_same"
        with self.assertRaisesRegex(AssertionError, "original_final_F072_flow_sha256"):
            C.compare(self.records,"384")

    def test_original_GAP_drift_hard_fail(self):
        self.records["precompute"]["original_worst_group_gap"]=3e-13
        with self.assertRaisesRegex(AssertionError,"original_worst_group_gap"):
            C.compare(self.records,"384")

    def test_original_work_counts_must_match(self):
        self.records["precompute"]["original_work_counters"]["Krylov"]=2644
        with self.assertRaisesRegex(AssertionError,"original_work_counters"):
            C.compare(self.records,"384")

    def test_noncertified_original_F072_rejected(self):
        self.records["precompute"]["original_static_F072_certificate"]="NOT_CERTIFIED"
        with self.assertRaisesRegex(AssertionError, "ORIGINAL"):
            C.compare(self.records,"384")

    def test_unpatched_original_post_certificate_required(self):
        self.records["precompute"][
            "R13_path_audit_restored_before_original_post_certificate"]=False
        with self.assertRaises(AssertionError):
            C.compare(self.records,"384")

    def test_no_cache_reuse_measured_fail_closed(self):
        self.records["precompute"]["experimental_route_counters"][
            "weight_table_reuse_hits"]=0
        with self.assertRaisesRegex(AssertionError,"No real R13 weight"):
            C.compare(self.records,"384")

    def test_missing_policy_or_2048_rejected(self):
        with self.assertRaises(ValueError):
            C.compare({"baseline":self.records["baseline"]},"384")
        with self.assertRaises(ValueError):
            C.compare(self.records,"2048")

    def test_ZIP_artifacts_require_both_one_per_policy(self):
        with TemporaryDirectory() as tmp:
            directory=Path(tmp)
            for policy in A.POLICIES:
                with zipfile.ZipFile(
                        directory/("GA-Gate09-ADR061-Evidencias-"+policy+".zip"),"w") as z:
                    z.writestr("row.json",json.dumps(self.records[policy]))
            found=C.read_evidence(directory)
            self.assertEqual(set(found),set(A.POLICIES))
            self.assertEqual(C.compare(found,"384")["status"],
                             "PASS_ADR061_ORIGINAL_F072_PARITY_AND_INDICATIVE_CPU_ROI")
            with zipfile.ZipFile(
                    directory/"GA-Gate09-ADR061-Evidencias-duplicate.zip","w") as z:
                z.writestr("row.json",json.dumps(self.records["baseline"]))
            with self.assertRaisesRegex(ValueError,"Duplicate"):
                C.read_evidence(directory)


if __name__=="__main__":
    unittest.main()
