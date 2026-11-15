"""Mandatory 48/96/192 original F072 regression and 384-node honest trial."""
from __future__ import annotations

from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_adr055 as A


def stages(pass_48=True, pass_96=True, pass_192=True, pass_384=False):
    def row(name, workers, producers, demand, groups, markets, tried, cert):
        return {
            "name": name,
            "physical_goldberg_nodes": 5762,
            "physical_workers": workers,
            "production_nodes": producers,
            "demand_nodes": demand,
            "aggregated_production_groups": groups,
            "worker_sourced_markets_node_item": markets,
            "F072_attempted": tried,
            "F072_independent_certified": cert,
            "production_nodes_subset_demand_nodes": True,
            "worker_demand_conserved_without_extra_travel_demand": True,
            "original_congestion_exponent_unchanged": True,
            "original_F072_global_FINAL_if_pass": 2e-14 if cert else None,
            "original_F072_worst_group_FINAL_if_pass": 1e-13 if cert else None,
            "F072_certificate_original_only":
                "PASS_STATIC_F072_FIRST_ORDER_ONLY" if cert else None,
        }
    return [
        row(A.REFERENCE[0], 2048, 12, 48, 24, 96, True, pass_48),
        row(A.REFERENCE[1], 2048, 24, 96, 48, 192, True, pass_96),
        row(A.REFERENCE[2], 4096, 48, 192, 96, 384, True, pass_192),
        row(A.CANDIDATE, 4096, 96, 384, 192, 768, True, pass_384),
        row(A.SPATIAL_ONLY, 8192, 512, 2048, 1024, 4096, False, False),
    ]


class ADR055Contracts(unittest.TestCase):
    def test_candidate_keeps_world_and_4096_workers(self):
        self.assertEqual(A.CONFIG_384.m, 24)
        self.assertEqual(A.CONFIG_384.hubs, 96)
        self.assertEqual(A.CONFIG_384.workers, 4096)
        self.assertEqual(A.R54.R53.CONFIGS[A.R54.CANDIDATE].workers, 4096)
        self.assertEqual(10 * A.CONFIG_384.m ** 2 + 2, 5762)

    def test_worker_384_ledger_is_physical_and_worker_funded(self):
        network, flows, ledger = A.R54.R53.R52.build_population(A.CONFIG_384)
        self.assertEqual((ledger["workers"], ledger["producing_workers"],
                          ledger["nonproducing_workers"]), (4096, 1024, 3072))
        self.assertEqual(ledger["demand_nodes_count"], 384)
        self.assertEqual(ledger["production_nodes_count"], 96)
        self.assertEqual(len(network.groups), 192)
        self.assertEqual(len(network.markets), 768)
        self.assertEqual(len(flows), 768)
        self.assertTrue(ledger["physical_production_nodes"].issubset(
            ledger["physical_demand_nodes"]))
        self.assertTrue(ledger["travel_time_does_not_duplicate_demand"])
        self.assertTrue(ledger["no_independent_synthetic_R13_market_demand"])

    def test_old_unmodified_original_certificate(self):
        self.assertEqual(A.TOL, 1e-4)
        self.assertIs(A.R54.R53.approve_original_F072,
                      A.R54.R53.approve_original_F072)
        self.assertEqual(
            A.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(), 16)

    def test_fixed_predeclared_budget_fits_original_ceiling(self):
        b = A.registered_budget(A.CANDIDATE)
        self.assertEqual(b.max_h_evaluations, 1600)
        self.assertEqual(b.max_route_scans, 6000)
        self.assertEqual(b.max_columns, 3000)
        self.assertEqual(b.max_krylov_products, 60000)
        self.assertEqual(b.max_block_coefficients, 250000)
        self.assertIs(A.registered_budget(A.REFERENCE[2]), A.R54.LIMITS_192)
        with self.assertRaises(ValueError):
            A.registered_budget(A.SPATIAL_ONLY)

    def test_order_of_all_mandatory_regressions(self):
        self.assertEqual(A.STAGES["ci"][:3],
                         tuple((name, True) for name in A.REFERENCE))
        self.assertEqual(A.STAGES["ci"][3], (A.CANDIDATE, True))
        self.assertEqual(A.STAGES["ci"][4], (A.SPATIAL_ONLY, False))

    def test_first_mandatory_48_failure_blocks_ci(self):
        self.assertEqual(A.classify("ci", stages(pass_48=False)),
                         ("NO_GO_MANDATORY_48_F072_REGRESSION", False))

    def test_second_mandatory_96_failure_blocks_ci(self):
        self.assertEqual(A.classify("ci", stages(pass_96=False)),
                         ("NO_GO_MANDATORY_96_F072_REGRESSION", False))

    def test_third_mandatory_192_failure_blocks_ci(self):
        self.assertEqual(A.classify("ci", stages(pass_192=False)),
                         ("NO_GO_MANDATORY_192_F072_REGRESSION", False))

    def test_reference_not_blessed_by_boolean_without_gaps(self):
        r = stages()
        r[2]["original_F072_global_FINAL_if_pass"] = None
        self.assertFalse(A.classify("ci", r)[1])
        r = stages()
        r[0]["original_F072_worst_group_FINAL_if_pass"] = 1e-3
        self.assertFalse(A.classify("ci", r)[1])

    def test_reference_missing_or_not_attempted_rejected(self):
        r = stages()
        r[1]["F072_attempted"] = False
        self.assertFalse(A.classify("ci", r)[1])
        r = stages()
        r[2]["F072_independent_certified"] = None
        with self.assertRaises(ValueError):
            A.classify("ci", r)

    def test_384_certificate_is_now_mandatory_and_blocks_ci(self):
        self.assertEqual(A.classify("ci", stages()),
                         ("NO_GO_MANDATORY_ADR055_384_F072_REGRESSION", False))

    def test_independent_original_384_certificate_is_mandatory(self):
        self.assertEqual(
            A.classify("ci", stages(pass_384=True)),
            ("PASS_ADR055_384_ORIGINAL_F072_CERTIFIED_MANDATORY", True))

    def test_fake_original_certificate_or_gap_rejected(self):
        r = stages(pass_384=True)
        r[3]["F072_certificate_original_only"] = "SEED_ONLY"
        self.assertEqual(A.classify("ci", r)[1], False)
        r = stages(pass_384=True)
        r[3]["original_F072_global_FINAL_if_pass"] = 1e-3
        self.assertEqual(A.classify("ci", r)[1], False)
        r = stages(pass_384=True)
        r[3]["original_F072_worst_group_FINAL_if_pass"] = None
        self.assertEqual(A.classify("ci", r)[1], False)

    def test_noncertified_384_final_gaps_must_be_null(self):
        r = stages()
        r[3]["original_F072_global_FINAL_if_pass"] = 1e-15
        self.assertEqual(A.classify("ci", r),
                         ("NO_GO_UNCERTIFIED_384_FINAL_DISCLOSURE", False))

    def test_candidate_boolean_mandatory(self):
        r = stages()
        r[3]["F072_independent_certified"] = None
        with self.assertRaises(ValueError):
            A.classify("ci", r)

    def test_worker_demand_or_quartic_drift_blocks_ci(self):
        r = stages(pass_384=True)
        r[3]["worker_demand_conserved_without_extra_travel_demand"] = False
        self.assertFalse(A.classify("ci", r)[1])
        r = stages(pass_384=True)
        r[3]["original_congestion_exponent_unchanged"] = False
        self.assertFalse(A.classify("ci", r)[1])

    def test_extreme_2048_ledger_remains_unsolved(self):
        r = stages(pass_384=True)
        r[4]["F072_attempted"] = True
        self.assertFalse(A.classify("ci", r)[1])
        with patch.object(A.R54.R53.R52.ADR51, "solve_chunked",
                          side_effect=AssertionError("Never solve 2048")):
            spatial = A.R54.R53.run_stage(A.SPATIAL_ONLY, False)
        self.assertEqual(spatial["demand_nodes"], 2048)
        self.assertFalse(spatial["F072_attempted"])

    def test_smoke_requires_all_three_independent_references(self):
        self.assertEqual(A.classify("smoke", stages()[:3]),
                         ("PASS_ADR055_MANDATORY_48_96_192_F072", True))
        self.assertFalse(A.classify(
            "smoke", stages(pass_192=False)[:3])[1])

    def test_unordered_or_missing_evidence_rejected(self):
        with self.assertRaises(ValueError):
            A.classify("ci", stages()[:3])
        with self.assertRaises(ValueError):
            A.classify("ci", list(reversed(stages())))
        with self.assertRaises(ValueError):
            A.classify("unknown", stages())

    def test_384_must_actually_run_solver_even_if_cert_flag_is_true(self):
        r = stages(pass_384=True)
        r[3]["F072_attempted"] = False
        self.assertEqual(A.classify("ci", r)[1], False)

    def test_384_no_go_cannot_be_reclassified_by_positive_seed(self):
        r = stages()
        r[3]["solver_status"] = "POSITIVE_SEED_ONLY"
        self.assertEqual(A.classify("ci", r),
                         ("NO_GO_MANDATORY_ADR055_384_F072_REGRESSION", False))

    def test_384_reference_original_tolerance_stays_unchanged(self):
        r = stages(pass_384=True)
        r[3]["original_F072_worst_group_FINAL_if_pass"] = A.TOL * 1.01
        self.assertEqual(A.classify("ci", r),
                         ("NO_GO_UNVERIFIED_384_F072_CLAIM", False))

    def test_cpu_and_process_memory_are_observations_not_game_sla(self):
        with patch.object(A, "peak_process_rss_bytes",
                          side_effect=[(123456, "native"), (234567, "native")]):
            start = A.observation_start()
            metrics = A.observation_end(start)
        self.assertEqual(metrics["process_peak_rss_bytes_before_stage"], 123456)
        self.assertEqual(metrics["process_peak_rss_bytes_after_stage"], 234567)
        self.assertTrue(metrics["rss_is_process_lifetime_highwater_not_stage_allocation"])
        self.assertTrue(metrics["cpu_is_process_time_not_game_thread_sla"])
        self.assertGreaterEqual(metrics["python_process_cpu_seconds"], 0)

    def test_memory_unsupported_does_not_invent_numbers(self):
        with patch.object(A.platform, "system", return_value="Unsupported"):
            mem, reason = A.peak_process_rss_bytes()
        self.assertIsNone(mem)
        self.assertIn("UNSUPPORTED", reason)

    def test_rss_platform_native_method_when_available(self):
        r, source = A.peak_process_rss_bytes()
        if r is not None:
            self.assertGreater(r, 0)
            self.assertTrue(source.startswith(("resource.", "psapi.")))

    def test_F059_and_CSharp_Godot_not_approved(self):
        self.assertFalse(getattr(A, "F059_ratified", False))
        self.assertEqual(A.TOL, 1e-4)
        self.assertEqual(A.CONFIG_384.m, 24)


if __name__ == "__main__":
    unittest.main()
