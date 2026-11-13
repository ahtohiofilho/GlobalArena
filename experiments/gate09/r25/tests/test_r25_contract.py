import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r25 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class TestR25ElasticTrust(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def test_immutable_r20_sources(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_reference_r21_replay_still_passes(self):
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "decay_sqrt", 6))
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "backtracking", 6))

    def test_trial_bank_is_frozen_and_exactly_five(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        observed = evaluate_r14(net, flows, cfg, ())
        digest = _flow_checksum(flows)
        trials = R.candidate_bank(net, flows, observed, cfg)
        self.assertEqual(len(trials), 5)
        self.assertEqual([t["alpha"] for t in trials], list(R.ALPHAS))
        self.assertEqual(digest, _flow_checksum(flows))
        for trial in trials:
            self.assertGreaterEqual(trial["merit"], 0)
            self.assertTrue(math.isfinite(trial["volatility"]))
            financial = R.R21.settlement_check(net, trial["flow"])
            self.assertEqual(financial["gross_micro"], financial["credits_micro"])

    def test_feasibility_expansion_never_fabricates_convergence(self):
        bank = [
            {"alpha": 0.25, "volatility": 0.16, "merit": 0.6,
             "global_ratio": 0.6, "group_ratio": 0.6,
             "observation": {"relative_gap": 0.6, "max_group_gap": 0.6}},
            {"alpha": 0.125, "volatility": 0.12, "merit": 0.8,
             "global_ratio": 0.8, "group_ratio": 0.8,
             "observation": {"relative_gap": 0.8, "max_group_gap": 0.8}},
            {"alpha": 0.0625, "volatility": 0.08, "merit": 0.9,
             "global_ratio": 0.9, "group_ratio": 0.9,
             "observation": {"relative_gap": 0.9, "max_group_gap": 0.9}},
        ]
        selected, decision = R.pick_candidate(bank, 0.05, "elastic_feasible")
        self.assertTrue(decision["expanded_for_feasibility"])
        self.assertFalse(decision["expanded_for_opportunity"])
        self.assertEqual(selected["alpha"], 0.0625)
        self.assertGreaterEqual(decision["radius_after"], 0.08)

    def test_opportunity_expansion_is_recorded_and_costed(self):
        bank = [
            {"alpha": 0.25, "volatility": 0.12, "merit": 0.7,
             "global_ratio": 0.7, "group_ratio": 0.7,
             "observation": {"relative_gap": 0.7, "max_group_gap": 0.7}},
            {"alpha": 0.125, "volatility": 0.03, "merit": 1.05,
             "global_ratio": 1.05, "group_ratio": 1.05,
             "observation": {"relative_gap": 1.05, "max_group_gap": 1.05}},
            {"alpha": 0.0625, "volatility": 0.01, "merit": 1.10,
             "global_ratio": 1.10, "group_ratio": 1.10,
             "observation": {"relative_gap": 1.10, "max_group_gap": 1.10}},
        ]
        selected, decision = R.pick_candidate(bank, 0.05, "elastic_opportunity")
        self.assertTrue(decision["expanded_for_opportunity"])
        self.assertEqual(selected["alpha"], 0.25)
        self.assertEqual(decision["candidate_evaluations"], 3)
        self.assertGreater(decision["radius_after"], 0.05)

    def test_budget_prefix_and_conservation(self):
        self.assertTrue(R.validate_budget_and_prefix(self.cfg))

    def test_five_numerical_checks_and_f059_remain_separate(self):
        result = R.run_case(self.cfg, "elastic_opportunity", 5, 26, audit_every=2)
        self.assertEqual(result["oracles_used"], 1 + 5 * result["steps"])
        self.assertEqual(result["financial_audits"][-1]["step"], result["steps"])
        self.assertFalse(result["rows"][0]["numerical_five_thresholds_met"])
        self.assertFalse(result["global_equilibrium_proven"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])
        for r in result["rows"]:
            self.assertTrue(math.isfinite(r["relative_gap"]))
            self.assertTrue(math.isfinite(r["max_group_gap"]))


if __name__ == "__main__":
    unittest.main()
