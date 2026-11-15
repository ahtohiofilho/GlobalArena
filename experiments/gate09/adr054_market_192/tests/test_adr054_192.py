"""ADR054 mandatory 48/96 regressions and honest 192 F072 exploratory gate."""
from __future__ import annotations
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr054 as A


def rows(cert_48=True, cert_96=True, cert_192=False):
    def row(name, workers, producer, demander, groups, markets, tried, certified):
        return {
            "name": name,
            "physical_workers": workers,
            "production_nodes": producer,
            "demand_nodes": demander,
            "aggregated_production_groups": groups,
            "worker_sourced_markets_node_item": markets,
            "F072_attempted": tried,
            "F072_independent_certified": certified,
            "production_nodes_subset_demand_nodes": True,
            "worker_demand_conserved_without_extra_travel_demand": True,
            "original_congestion_exponent_unchanged": True,
            "original_F072_global_FINAL_if_pass": 1e-12 if certified else None,
            "original_F072_worst_group_FINAL_if_pass": 1e-11 if certified else None,
            "F072_certificate_original_only":
                "PASS_STATIC_F072_FIRST_ORDER_ONLY" if certified else None,
        }
    return [
        row(A.REFERENCE[0], 2048, 12, 48, 24, 96, True, cert_48),
        row(A.REFERENCE[1], 2048, 24, 96, 48, 192, True, cert_96),
        row(A.CANDIDATE, 4096, 48, 192, 96, 384, True, cert_192),
        row(A.SPATIAL_ONLY, 8192, 512, 2048, 1024, 4096, False, False),
    ]


class ADR054ScaleContracts(unittest.TestCase):
    def test_unmodified_adr053_stage_configs(self):
        self.assertEqual(A.R53.CONFIGS[A.CANDIDATE].workers, 4096)
        self.assertEqual(A.R53.CONFIGS[A.CANDIDATE].hubs, 48)
        self.assertEqual(A.R53.CONFIGS[A.REFERENCE[1]].workers, 2048)
        self.assertEqual(A.R53.CONFIGS[A.REFERENCE[1]].hubs, 24)
        self.assertEqual({cfg.m for cfg in A.R53.CONFIGS.values()}, {24})

    def test_original_certificate_function_reused_not_reimplemented(self):
        self.assertIs(A.R53.approve_original_F072, A.R53.approve_original_F072)
        self.assertEqual(A.R53.TOLERANCE, 1e-4)
        self.assertEqual(
            A.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(), 16)

    def test_192_budget_predetermined_and_not_game_sla(self):
        b = A.registered_budget(A.CANDIDATE)
        self.assertEqual(b.max_h_evaluations, 1100)
        self.assertEqual(b.max_route_scans, 3100)
        self.assertEqual(b.max_columns, 1500)
        self.assertEqual(b.max_krylov_products, 60000)
        self.assertEqual(b.max_block_coefficients, 250000)
        self.assertIs(A.registered_budget(A.REFERENCE[0]),
                      A.R53.registered_budget(A.REFERENCE[0]))
        self.assertIs(A.registered_budget(A.REFERENCE[1]),
                      A.R53.registered_budget(A.REFERENCE[1]))
        with self.assertRaises(ValueError):
            A.registered_budget(A.SPATIAL_ONLY)

    def test_48_and_96_remain_mandatory_even_if_192_passes(self):
        r = rows(False, True, True)
        self.assertEqual(A.classify("ci", r),
                         ("NO_GO_MANDATORY_48_F072_REGRESSION", False))
        r = rows(True, False, True)
        self.assertEqual(A.classify("ci", r),
                         ("NO_GO_MANDATORY_96_F072_REGRESSION", False))

    def test_no_go_192_is_mandatory_and_blocks_ci(self):
        self.assertEqual(
            A.classify("ci", rows()),
            ("NO_GO_MANDATORY_ADR054_192_F072_REGRESSION", False))

    def test_original_independent_192_certificate_required(self):
        self.assertEqual(
            A.classify("ci", rows(cert_192=True)),
            ("PASS_ADR054_192_ORIGINAL_F072_CERTIFIED_MANDATORY", True))

    def test_self_asserted_pass_without_valid_global_gap_rejected(self):
        r = rows(cert_192=True)
        r[2]["original_F072_global_FINAL_if_pass"] = 1e-2
        self.assertEqual(A.classify("ci", r)[1], False)

    def test_self_asserted_pass_without_valid_worst_group_rejected(self):
        r = rows(cert_192=True)
        r[2]["original_F072_worst_group_FINAL_if_pass"] = None
        self.assertEqual(A.classify("ci", r)[1], False)

    def test_self_asserted_pass_without_original_cert_rejected(self):
        r = rows(cert_192=True)
        r[2]["F072_certificate_original_only"] = "SEED_ONLY"
        self.assertEqual(A.classify("ci", r)[1], False)

    def test_no_uncertified_final_gap_disclosure(self):
        r = rows()
        r[2]["original_F072_global_FINAL_if_pass"] = 5e-5
        self.assertEqual(
            A.classify("ci", r),
            ("NO_GO_UNCERTIFIED_192_FINAL_RESULT_LEAK", False))

    def test_no_false_192_if_original_quartic_changed(self):
        r = rows(cert_192=True)
        r[2]["original_congestion_exponent_unchanged"] = False
        self.assertEqual(
            A.classify("ci", r),
            ("NO_GO_MANDATORY_PHYSICAL_OR_QUARTIC_CONTRACT", False))

    def test_no_false_192_when_demands_are_not_worker_conserved(self):
        r = rows(cert_192=True)
        r[2]["worker_demand_conserved_without_extra_travel_demand"] = False
        self.assertFalse(A.classify("ci", r)[1])

    def test_2048_spatial_ledger_never_runs_f072(self):
        r = rows(cert_192=True)
        r[3]["F072_attempted"] = True
        self.assertFalse(A.classify("ci", r)[1])

    def test_missing_or_out_of_order_evidence_rejected(self):
        with self.assertRaises(ValueError):
            A.classify("ci", [])
        with self.assertRaises(ValueError):
            A.classify("ci", list(reversed(rows())))
        with self.assertRaises(ValueError):
            A.classify("invalid", [])

    def test_both_original_references_required_in_smoke(self):
        self.assertEqual(
            A.classify("smoke", rows()[:2]),
            ("PASS_ADR054_48_AND_96_F072_REGRESSIONS", True))
        self.assertFalse(A.classify("smoke", rows(True, False)[:2])[1])

    def test_192_spatial_seed_still_not_equilibrium(self):
        # This is a ledger test, NOT an attempted commercial certificate.
        with patch.object(A.R53.R52.ADR51, "solve_chunked",
                          side_effect=AssertionError("Unexpected F072")):
            r = A.R53.run_stage(A.CANDIDATE, False)
        self.assertEqual(r["physical_workers"], 4096)
        self.assertEqual(r["production_nodes"], 48)
        self.assertEqual(r["demand_nodes"], 192)
        self.assertEqual(r["aggregated_production_groups"], 96)
        self.assertEqual(r["worker_sourced_markets_node_item"], 384)
        self.assertFalse(r["F072_independent_certified"])
        self.assertIsNone(r["original_F072_global_FINAL_if_pass"])
        self.assertTrue(r["production_nodes_subset_demand_nodes"])

    def test_missing_192_certificate_boolean_is_rejected(self):
        r = rows()
        r[2]["F072_independent_certified"] = None
        with self.assertRaises(ValueError):
            A.classify("ci", r)

    def test_unattempted_192_cannot_pass(self):
        r = rows(cert_192=True)
        r[2]["F072_attempted"] = False
        self.assertFalse(A.classify("ci", r)[1])

    def test_192_regression_does_not_change_required_48_96_order(self):
        self.assertEqual(tuple(n for n, _ in A.STAGES["ci"])[:2],
                         A.REFERENCE)
        self.assertEqual(A.STAGES["ci"][2], (A.CANDIDATE, True))
        self.assertEqual(A.STAGES["ci"][3], (A.SPATIAL_ONLY, False))

    def test_original_quartic_and_f059_not_ratified(self):
        self.assertFalse(getattr(A, "F059_ratified", False))
        self.assertEqual(A.R53.TOLERANCE, 1e-4)
        self.assertFalse(A.exercise.__doc__ and "production approved" in
                         A.exercise.__doc__.lower())


if __name__ == "__main__":
    unittest.main()
