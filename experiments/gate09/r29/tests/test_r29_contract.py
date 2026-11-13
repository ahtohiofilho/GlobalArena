from pathlib import Path
import math
import sys
from types import SimpleNamespace
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r29 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class StructuralDiagnosticContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flow = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flow, settings, ())
        return net, flow, settings, obs

    def test_frozen_r20_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_market_route_partition_exhaustive(self):
        def col(destination, edges):
            return SimpleNamespace(destination=destination, edges=tuple(edges))
        a = col(1, (1, 2))
        self.assertEqual(R.market_route_parts(a, a), "unchanged")
        self.assertEqual(R.market_route_parts(a, col(2, (1, 2))), "market_only")
        self.assertEqual(R.market_route_parts(a, col(1, (2, 3))), "route_only")
        self.assertEqual(R.market_route_parts(a, col(2, (2, 3))), "both")

    def test_two_step_aba_is_only_target_reversal(self):
        older = {1: "A", 2: "A", 3: "A"}
        previous = {1: "B", 2: "A", 3: "B"}
        current = {1: "A", 2: "B", 3: "C"}
        self.assertEqual(R.aba_reversals(older, previous, current), (1,))
        self.assertEqual(R.aba_reversals(None, previous, current), ())
        with self.assertRaises(AssertionError):
            R.aba_reversals({1: "A"}, previous, current)

    def test_target_signature_is_deterministic(self):
        net, flows, settings, obs = self.frozen()
        targets = obs["target"]
        self.assertEqual(R.target_signature(targets),
                         R.target_signature(dict(reversed(list(targets.items())))))
        self.assertEqual(len(R.target_signature(targets)), 64)
        self.assertEqual(_flow_checksum(flows), _flow_checksum(flows))

    def test_real_transition_and_financial_parity(self):
        net, flows, settings, obs = self.frozen()
        changed, successor, alpha, queries, forced = R.R21.choose_step(
            net, flows, obs, settings, "decay_sqrt", 1)
        info = R.transition(net, obs, successor)
        self.assertEqual(sum(info["switch_partition"].values()),
                         len(net.groups))
        self.assertEqual(
            info["switches"],
            len(net.groups) - info["switch_partition"]["unchanged"])
        self.assertLessEqual(
            info["exposed_to_top5_preexisting_stress_edges"],
            info["switches"])
        self.assertEqual(info["two_step_aba_reversals"], 0)
        self.assertGreaterEqual(info["largest_price_shock"]["relative_change"], 0)
        R.R21.frozen_check(net, changed, successor)
        money = R.R21.settlement_check(net, changed)
        self.assertEqual(money["gross_micro"], money["credits_micro"])

    def test_counterfactual_probe_is_paid_and_nonmutating(self):
        net, flows, settings, obs = self.frozen()
        digest = _flow_checksum(flows)
        with patch.object(R, "evaluate_r14", wraps=R.evaluate_r14) as oracle:
            report = R.local_probe(net, flows, obs, settings)
            self.assertEqual(oracle.call_count, 2)
        self.assertEqual(report["candidate_oracle_evaluations"], 2)
        self.assertEqual(report["source_flow_sha256"], digest)
        self.assertEqual(_flow_checksum(flows), digest)
        self.assertEqual(
            [a["alpha"] for a in report["proposals"]],
            list(R.PROBE_ALPHAS))

    def test_pearson_association_not_causality(self):
        self.assertIsNone(R.pearson([1, 1, 1], [1, 2, 3]))
        self.assertIsNone(R.pearson([1], [2]))
        self.assertAlmostEqual(R.pearson([1, 2, 3], [2, 4, 6]), 1)
        self.assertAlmostEqual(R.pearson([1, 2, 3], [6, 4, 2]), -1)

    def test_case_replays_immutable_r21_and_costs_probes(self):
        result = R.run_case(self.cfg, "decay_sqrt", 6,
                            audit_every=2, probe_every=2)
        self.assertEqual(result["steps"], 6)
        self.assertEqual(result["proposal_oracles_used"], 7)
        self.assertEqual(result["counterfactual_probe_oracles_used"], 8)
        self.assertEqual(result["combined_oracles_used"], 15)
        self.assertEqual(result["financial_audits"][-1]["step"], 6)
        self.assertTrue(result["diagnostics"]["descriptive_only"])
        self.assertFalse(result["diagnostics"]["proof_that_switches_cause_price_shocks"])
        self.assertFalse(result["economic_equilibrium_proven"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])

    def test_prefix_matches_independent_horizon(self):
        small = R.run_case(self.cfg, "fixed_1_8", 4,
                           audit_every=2, probe_every=2)
        long = R.run_case(self.cfg, "fixed_1_8", 7,
                          audit_every=2, probe_every=2)
        self.assertEqual(small["rows"], long["rows"][:5])

    def test_backtracking_reference_and_probe_accounting(self):
        result = R.run_case(self.cfg, "backtracking", 4,
                            audit_every=2, probe_every=2)
        self.assertGreaterEqual(result["proposal_oracles_used"], 5)
        self.assertEqual(result["counterfactual_probe_oracles_used"], 6)
        self.assertTrue(all(row["target_hash"] for row in result["rows"]))
        self.assertIsNone(result["rows"][0]["transition"])

    def test_progressive_reference_is_immutable(self):
        result = R.run_case(self.cfg, "progressive_global", 4,
                            audit_every=2, probe_every=4)
        old = R.R26.run_case(self.cfg, "progressive_global", 4, 81,
                             audit_every=2)
        self.assertEqual(result["final_flow_sha256"], old["final_flow_sha256"])
        self.assertEqual(result["final_gap"], old["final_gap"])
        self.assertEqual(
            result["proposal_oracles_used"], old["oracles_used"])


if __name__ == "__main__":
    unittest.main()
