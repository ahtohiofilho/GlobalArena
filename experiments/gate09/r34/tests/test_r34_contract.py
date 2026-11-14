from dataclasses import replace
import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r34 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R34CapacityFrontierContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def test_immutable_r20_sha256_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_registered_capacity_factors_strict_increasing_and_positive(self):
        self.assertEqual(tuple(sorted(set(R.CAPACITY_FACTORS))),
                         R.CAPACITY_FACTORS)
        self.assertIn(0.20, R.CAPACITY_FACTORS)
        self.assertIn(0.25, R.CAPACITY_FACTORS)
        self.assertIn(1.0, R.CAPACITY_FACTORS)
        self.assertTrue(all(x > 0 for x in R.CAPACITY_FACTORS))
        for bad in (-1, 0, 0.222, float("nan"), float("inf")):
            with self.assertRaises(ValueError):
                R.capacity_fixture(self.cfg, "original", bad)

    def test_original_at_factor_one_reproduces_r33_baseline(self):
        net, flows, info = R.capacity_fixture(self.cfg, "original", 1.0)
        old, prior, _ = R.R33.fixture_variant(self.cfg, "baseline")
        self.assertEqual(R.R33.fixture_fingerprint(net),
                         R.R33.fixture_fingerprint(old))
        self.assertEqual(_flow_checksum(flows), _flow_checksum(prior))
        self.assertTrue(info["tariffs_and_owners_unchanged"])

    def test_only_capacity_is_changed_when_original_regime(self):
        baseline, first = construct_scenario(self.cfg)
        for factor in R.CAPACITY_FACTORS:
            with self.subTest(factor=factor):
                changed, flow, info = R.capacity_fixture(
                    self.cfg, "original", factor)
                self.assertEqual(set(changed.edges), set(baseline.edges))
                self.assertEqual(changed.groups, baseline.groups)
                self.assertEqual(changed.markets, baseline.markets)
                self.assertEqual(_flow_checksum(flow),
                                 _flow_checksum(first))
                for eid, edge in changed.edges.items():
                    reference = baseline.edges[eid]
                    self.assertEqual(replace(edge, capacity=reference.capacity),
                                     reference)
                    self.assertEqual(edge.capacity, reference.capacity * factor)
                self.assertTrue(R.R33.verify_price_law(
                    changed, evaluate_r14(
                        changed, flow, ClearingSettings(exponent=4), ())))

    def test_heterogeneous_regime_fixed_across_capacity_values(self):
        a, flows_a, info_a = R.capacity_fixture(
            self.cfg, "heterogeneous_D_and_supply", 0.2)
        b, flows_b, info_b = R.capacity_fixture(
            self.cfg, "heterogeneous_D_and_supply", 4.0)
        self.assertEqual(a.markets, b.markets)
        self.assertEqual(a.groups, b.groups)
        self.assertEqual(flows_a, flows_b)
        self.assertNotEqual(info_a["actual_fixture_fingerprint"],
                            info_b["actual_fixture_fingerprint"])
        self.assertEqual(info_a["source_fixture_fingerprint"],
                         info_b["source_fixture_fingerprint"])
        ref, old_flows, _ = R.R33.fixture_variant(
            self.cfg, "combined_stress")
        self.assertEqual(a.markets, ref.markets)
        self.assertEqual(a.groups, ref.groups)
        self.assertEqual(_flow_checksum(flows_a),
                         _flow_checksum(old_flows))

    def test_peak_load_capacity_ratio_scales_at_initial_state(self):
        a, flows, _ = R.capacity_fixture(self.cfg, "original", 1.0)
        b, other, _ = R.capacity_fixture(self.cfg, "original", 0.25)
        self.assertEqual(flows, other)
        loads, _, _ = a.snapshots(flows)
        under = R.R22.edge_stress(a, {"loads": loads})
        tight = R.R22.edge_stress(b, {"loads": loads})
        self.assertAlmostEqual(tight["maximum_load_capacity_ratio"],
                               4*under["maximum_load_capacity_ratio"],
                               delta=1e-12)

    def test_all_frozen_market_scores_and_f084f_unchanged(self):
        net, flows, _ = R.capacity_fixture(
            self.cfg, "heterogeneous_D_and_supply", 0.15)
        settings = ClearingSettings(exponent=4)
        before = evaluate_r14(net, flows, settings, ())
        newer, after, delta, calls, alpha = R.R29.step_forward(
            net, flows, before, settings, "decay_sqrt", 1)
        R.R21.frozen_check(net, newer, after)
        money = R.R21.settlement_check(net, newer)
        self.assertEqual(money["gross_micro"], money["credits_micro"])
        self.assertTrue(R.R33.verify_price_law(net, after))
        with patch.object(R.R32, "evaluate_r14",
                          side_effect=AssertionError("Hybrid performed R14")):
            hybrid = R.R32.frozen_hybrid_rankings(net, before, after, 4)
        self.assertEqual(hybrid["extra_r14_oracle_calls"], 0)

    def test_capacity_sweep_pure_repeatable_and_financially_audited(self):
        one = R.run_case(self.cfg, "original", 0.25,
                         "decay_sqrt", 5, audit_every=2)
        two = R.run_case(self.cfg, "original", 0.25,
                         "decay_sqrt", 5, audit_every=2)
        self.assertEqual(one["rows"], two["rows"])
        self.assertEqual(one["financial_audits"], two["financial_audits"])
        self.assertEqual(one["financial_audits"][-1]["step"], 5)
        self.assertEqual(one["hybrid_extra_r14_calls"], 0)
        self.assertEqual(one["policy_r14_calls"], 6)
        self.assertGreater(one["independent_shortest_path_scans"], 0)
        self.assertFalse(one["global_equilibrium_proven"])
        self.assertFalse(one["financial_F059_ratified"])
        self.assertFalse(one["production_approved"])

    def test_r33_frozen_baseline_reference_replayed_exactly(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                new = R.run_case(self.cfg, "original", 1.0,
                                 policy, 5, audit_every=2)
                ref = R.R33.run_case(self.cfg, "baseline",
                                     policy, 5, audit_every=2)
                self.assertEqual(new["final_flow_sha256"],
                                 ref["final_flow_sha256"])
                self.assertEqual(new["final_global_gap"],
                                 ref["final_gap"])
                self.assertEqual(new["market_switches"],
                                 ref["summary"]["market_switches"])
                self.assertEqual(new["policy_r14_calls"],
                                 ref["proposal_oracle_calls"])

    def test_prefix_independent_of_future_horizon(self):
        short = R.run_case(self.cfg, "heterogeneous_D_and_supply",
                           0.2, "backtracking", 4, audit_every=2)
        long = R.run_case(self.cfg, "heterogeneous_D_and_supply",
                          0.2, "backtracking", 7, audit_every=2)
        self.assertEqual(short["rows"], long["rows"][:5])

    def test_failure_witnesses_are_only_actual_non_reproductions(self):
        test = R.run_case(self.cfg, "heterogeneous_D_and_supply",
                          0.15, "decay_sqrt", 6)
        for row in test["rows"][1:]:
            self.assertLessEqual(
                len(row["price_failure_witnesses"]),
                R.MAX_FAILURE_WITNESSES_PER_STEP)
            for witness in row["price_failure_witnesses"]:
                self.assertNotEqual(witness["price_only_market"],
                                    witness["new_market"])
                self.assertEqual(witness["new_market"],
                                 witness["full_r14_actual_best"])
                self.assertTrue(witness["an_observational_nonclearing_hybrid"])

    def test_boundary_detection_detects_all_nonmonotonic_crossings(self):
        rows = [
            {"capacity_factor": .2, "price_only_winner_fraction": .91,
             "max_peak_load_over_capacity": 4.},
            {"capacity_factor": .25, "price_only_winner_fraction": .97,
             "max_peak_load_over_capacity": 3.},
            {"capacity_factor": .5, "price_only_winner_fraction": .94,
             "max_peak_load_over_capacity": 2.},
            {"capacity_factor": 1., "price_only_winner_fraction": 1.,
             "max_peak_load_over_capacity": 1.},
        ]
        result = R.observed_brackets(rows)
        self.assertEqual(len(result["0.95"]), 3)
        self.assertEqual(len(result["0.99"]), 1)
        self.assertEqual(result["0.95"][0]["capacity_factor_below"], .2)
        self.assertTrue(result["0.95"][0][
            "crossed_cutoff_not_a_mathematical_phase_transition"])

    def test_no_switch_fraction_undefined_not_imputed(self):
        row = {
            "regime": "original", "capacity_factor": 1.,
            "market_switches": 0, "price_only_full_winner_matches": 0,
            "transport_only_full_winner_matches": 0,
            "max_load_over_capacity_across_steps": .1,
            "price_only_full_winner_fraction": None,
        }
        report = R.aggregate_capacity("original", 1., [row])
        self.assertIsNone(report["price_only_winner_fraction"])
        self.assertTrue(report["no_switch_share_is_null"])
        self.assertEqual(report["runs_with_no_switches"], 1)

    def test_original_r21_reference_unchanged(self):
        for policy in ("decay_sqrt", "backtracking"):
            self.assertTrue(R.R24.check_r21_reference(
                self.cfg, policy, 6))


if __name__=="__main__":
    unittest.main()
