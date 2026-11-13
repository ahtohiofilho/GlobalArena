import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))

import run_r23 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class TestR23ControlledDamping(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def test_frozen_r20_provenance(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_uniform_group_mix_exactly_reproduces_r20(self):
        self.assertTrue(R.verify_uniform_mix(self.cfg))

    def test_priority_uses_absolute_weighted_regret(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        observed = evaluate_r14(net, flows, cfg, ())
        k = (len(net.groups) + 3) // 4
        chosen = R.priority_set(net, observed)
        expected = sorted(
            net.groups,
            key=lambda gid: (
                -net.groups[gid].supply *
                observed["groups"][gid]["score"] *
                observed["groups"][gid]["gap"], gid
            ),
        )[:k]
        self.assertEqual(list(chosen), expected)

    def test_group_updates_preserve_all_supply_and_all_prices(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        observed = evaluate_r14(net, flows, cfg, ())
        for policy in ("targeted_quartile", "inverse_quartile", "uniform_matched"):
            with self.subTest(policy=policy):
                next_flows, next_obs, avg_alpha = R.propose(
                    net, flows, observed, cfg, policy, 1)
                self.assertTrue(0 < avg_alpha <= 1)
                self.assertTrue(all(x >= 0 for x in next_flows.values()))
                _, quantities, prices = net.snapshots(next_flows)
                self.assertTrue(all(math.isfinite(p) and p > 0 for p in prices.values()))
                self.assertEqual(set(quantities), set(net.markets))
                self.assertGreaterEqual(next_obs["relative_gap"], 0)
                money = R.R21.settlement_check(net, next_flows)
                self.assertEqual(money["gross_micro"], money["credits_micro"])

    def test_same_state_counterfactual_does_not_change_source(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        observation = evaluate_r14(net, flows, cfg, ())
        before = _flow_checksum(flows)
        probe = R.same_state_probe(net, flows, observation, cfg, 0)
        self.assertEqual(before, _flow_checksum(flows))
        self.assertEqual(probe["starting_flow_sha256"], before)
        self.assertEqual(len(probe["outcomes"]), 4)
        self.assertAlmostEqual(
            probe["outcomes"]["targeted_quartile"]["supply_weighted_alpha"],
            probe["outcomes"]["uniform_matched"]["supply_weighted_alpha"],
            delta=1e-15,
        )

    def test_original_r21_reference_unchanged(self):
        self.assertTrue(R.verify_reference(self.cfg, "fixed_1_8", 6))
        self.assertTrue(R.verify_reference(self.cfg, "decay_sqrt", 6))

    def test_prefix_determinism_for_targeting(self):
        early = R.run_case(self.cfg, "targeted_quartile", 4, audit_every=2)
        later = R.run_case(self.cfg, "targeted_quartile", 7, audit_every=2)
        self.assertEqual(early["rows"], later["rows"][:5])
        self.assertFalse(early["numerical_equilibrium_proven"])
        self.assertFalse(early["production_approved"])

    def test_joint_stability_and_f059_are_independent(self):
        result = R.run_case(self.cfg, "uniform_matched", 4, audit_every=1)
        self.assertEqual(len(result["financial_audits"]), 5)
        self.assertEqual(result["main_oracle_evaluations"], 5)
        self.assertEqual(result["counterfactual_oracle_evaluations"], 0)
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])
        self.assertIsNone(result["rows"][0]["price_change"])
        self.assertFalse(result["rows"][0]["numerical_five_thresholds_met"])


if __name__ == "__main__":
    unittest.main()
