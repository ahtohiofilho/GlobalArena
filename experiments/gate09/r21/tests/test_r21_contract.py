import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r21 as R


class TestR21Contracts(unittest.TestCase):
    def test_r20_upstream_is_immutable(self):
        self.assertEqual(R.validate_manifest(), 16)

    def test_finance_and_original_oracle_replay(self):
        result = R.run_trajectory(R.CASES["small"], "fixed_1_8",
                                  steps=3, audit_every=1)
        self.assertEqual(len(result["rows"]), 4)
        self.assertEqual(len(result["audits"]), 4)
        self.assertTrue(all(a["sold_micro"] > 0 and
                            a["gross_micro"] == a["credits_micro"]
                            for a in result["audits"]))
        self.assertTrue(all(math.isfinite(row["relative_gap"])
                            for row in result["rows"]))
        self.assertFalse(result["equilibrium_convergence_proven"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])

    def test_longer_iteration_budget_preserves_prefix(self):
        self.assertTrue(R.prefix_check(R.CASES["small"], "fixed_1_8"))
        self.assertTrue(R.prefix_check(R.CASES["small"], "backtracking"))

    def test_backtracking_is_observational_not_convergence_claim(self):
        result = R.run_trajectory(R.CASES["small"], "backtracking",
                                  steps=5, audit_every=5)
        self.assertGreaterEqual(result["oracle_evaluations_for_proposals"], 5)
        self.assertLessEqual(result["oracle_evaluations_for_proposals"], 20)
        self.assertLessEqual(result["backtracking_forced_uphill_steps"], 5)
        self.assertFalse(result["production_approved"])


if __name__ == "__main__":
    unittest.main()
