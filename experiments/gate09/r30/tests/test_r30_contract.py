import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r30 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R30FrozenMarginContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flows, settings, ())
        return net, flows, settings, obs

    def test_frozen_r20_16_file_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_r14_market_tie_threshold_is_exactly_reused(self):
        self.assertTrue(R.near_baseline_score(1., 1. - 0.5e-10))
        self.assertFalse(R.near_baseline_score(1., 1. - 2e-10))
        self.assertTrue(R.near_baseline_score(.001, .001 - 0.5e-10))
        self.assertFalse(R.near_baseline_score(.001, .001 - 2e-10))

    def test_market_ranking_reconstructs_every_winner_and_f059_flag(self):
        net, flows, settings, obs = self.frozen()
        info = R.market_alternatives(net, obs, settings.exponent)
        self.assertEqual(len(info["groups"]), len(net.groups))
        self.assertEqual(info["exact_r14_tie_groups"],
                         len(obs["tie_groups"]))
        self.assertFalse(not info["not_a_second_best_route_search"])
        for gid, row in info["groups"].items():
            self.assertEqual(row["best_destination"], obs["target"][gid].destination)
            self.assertEqual(row["best_route_edges"],
                             list(obs["target"][gid].edges))
            self.assertEqual(row["best_score"],
                             obs["groups"][gid]["score"])
            self.assertEqual(row["baseline_f059_tie_flag"],
                             gid in obs["tie_groups"])
            self.assertGreaterEqual(row["market_margin_relative"], -1e-14)
            self.assertIsNotNone(row["second_market_destination"])
            self.assertNotEqual(row["second_market_destination"],
                                row["best_destination"])
            self.assertTrue(row["second_best_route_not_enumerated"])
        self.assertGreater(info["extra_shortest_path_scans"], 0)

    def test_previous_route_is_scored_on_new_state_without_mutation(self):
        net, flows, settings, obs = self.frozen()
        sha = _flow_checksum(flows)
        first = R.market_alternatives(
            net, obs, settings.exponent, old_targets=obs["target"])
        self.assertEqual(sha, _flow_checksum(flows))
        for row in first["groups"].values():
            self.assertAlmostEqual(row["previous_target_score_loss_relative"], 0.,
                                   delta=1e-11)
            self.assertAlmostEqual(
                row["incumbent_same_destination_route_loss_relative"], 0.,
                delta=1e-11)
        next_flow, next_obs, _, calls, _ = R.R29.step_forward(
            net, flows, obs, settings, "decay_sqrt", 1)
        self.assertEqual(calls, 1)
        margin = R.market_alternatives(
            net, next_obs, settings.exponent, old_targets=obs["target"])
        attrib = R.transition_attribution(net, obs, next_obs, first, margin)
        self.assertEqual(
            attrib["counts"]["changed_market"],
            attrib["partition"]["switch_partition"]["market_only"] +
            attrib["partition"]["switch_partition"]["both"])
        self.assertEqual(
            attrib["counts"]["changed_route_only"],
            attrib["partition"]["switch_partition"]["route_only"])
        self.assertEqual(sha, _flow_checksum(flows))
        R.R21.frozen_check(net, next_flow, next_obs)
        fiscal = R.R21.settlement_check(net, next_flow)
        self.assertEqual(fiscal["gross_micro"], fiscal["credits_micro"])

    def test_group_aba_is_not_interpreted_as_full_economy_cycle(self):
        net, flows, settings, obs = self.frozen()
        prior = None
        older = None
        margin_prior = None
        for step in range(1, 5):
            new_flow, future, _, _, _ = R.R29.step_forward(
                net, flows, obs, settings, "backtracking", step)
            before_margin = R.market_alternatives(
                net, obs, settings.exponent,
                old_targets=None if prior is None else prior["target"])
            after_margin = R.market_alternatives(
                net, future, settings.exponent, old_targets=obs["target"])
            witness = R.transition_attribution(
                net, obs, future, before_margin, after_margin, older=prior)
            self.assertLessEqual(witness["counts"]["aba_groups"],
                                 witness["partition"]["switches"])
            self.assertLessEqual(witness["counts"]["aba_market_near_margin"],
                                 witness["counts"]["aba_groups"])
            self.assertFalse(
                any(row.get("causal_proven", False) for row in witness["changed_groups"]))
            prior, flows, obs, margin_prior = obs, new_flow, future, after_margin

    def test_counterfactuals_consume_two_new_oracles_and_remain_pure(self):
        net, flows, settings, obs = self.frozen()
        digest = _flow_checksum(flows)
        with patch.object(R, "evaluate_r14", wraps=R.evaluate_r14) as mocked:
            output = R.counterfactual_market_margin(net, flows, obs, settings, 0)
            self.assertEqual(mocked.call_count, 2)
        self.assertEqual(output["extra_r14_probe_evaluations"], 2)
        self.assertEqual([p["alpha"] for p in output["probes"]],
                         list(R.PROBE_ALPHAS))
        self.assertGreater(output["extra_shortest_path_scans"], 0)
        self.assertEqual(digest, output["source_flow_sha256"])
        self.assertEqual(digest, _flow_checksum(flows))
        self.assertEqual(obs["target"], evaluate_r14(
            net, flows, settings, ())["target"])

    def test_snapshot_loss_is_not_a_second_shortest_path_certificate(self):
        net, flows, settings, obs = self.frozen()
        info = R.market_alternatives(net, obs, settings.exponent,
                                     old_targets=obs["target"])
        self.assertTrue(info["not_a_second_best_route_search"])
        self.assertTrue(all(
            row["second_best_route_not_enumerated"]
            for row in info["groups"].values()))
        # The R14 route multiplicity count is capped at 2, so it cannot
        # certify the exact number of shortest routes.
        self.assertTrue(all(
            row["best_route_multiplicity_capped_at_two"] in (1, 2)
            for row in info["groups"].values()))

    def test_r29_and_r30_preserve_frozen_trajectories(self):
        out = R.run_case(self.cfg, "decay_sqrt", 5,
                         audit_every=2, probe_every=2)
        old = R.R29.run_case(self.cfg, "decay_sqrt", 5,
                             audit_every=2, probe_every=2)
        self.assertEqual(out["final_flow_sha256"], old["final_flow_sha256"])
        self.assertEqual(out["final_gap"], old["final_gap"])
        self.assertEqual(out["first_three_joint_stable_step"],
                         old["first_three_joint_stable_step"])
        self.assertEqual(out["proposal_oracle_evaluations"],
                         old["proposal_oracles_used"])
        self.assertEqual(out["counterfactual_probe_oracle_evaluations"],
                         old["counterfactual_probe_oracles_used"])
        self.assertEqual(out["financial_audits"][-1]["step"], 5)
        self.assertFalse(out["economic_equilibrium_proven"])
        self.assertFalse(out["financial_F059_ratified"])
        self.assertFalse(out["production_approved"])

    def test_prefix_is_exact_over_different_horizons(self):
        short = R.run_case(self.cfg, "fixed_1_8", 4,
                           audit_every=2, probe_every=2)
        long = R.run_case(self.cfg, "fixed_1_8", 7,
                          audit_every=2, probe_every=2)
        self.assertEqual(short["rows"], long["rows"][:5])

    def test_progressive_reference_matches_r26(self):
        result = R.run_case(self.cfg, "progressive_global", 4,
                            audit_every=2, probe_every=4)
        reference = R.R26.run_case(
            self.cfg, "progressive_global", 4, 41, audit_every=2)
        self.assertEqual(result["final_flow_sha256"],
                         reference["final_flow_sha256"])
        self.assertEqual(result["final_gap"], reference["final_gap"])
        self.assertEqual(result["proposal_oracle_evaluations"],
                         reference["oracles_used"])

    def test_aggregate_count_partition_and_nearby_fractions(self):
        result = R.run_case(self.cfg, "backtracking", 6,
                            audit_every=2, probe_every=2)
        stats = result["summary"]
        self.assertEqual(
            stats["market_switches"],
            stats["market_switch_close_1e4"] +
            stats["market_switch_not_close_1e4"])
        self.assertEqual(
            stats["route_only_switches"],
            stats["route_only_known_incumbent_near_1e4"] +
            stats["route_only_known_incumbent_not_near"])
        self.assertIsNone(result["first_three_joint_stable_step"])
        self.assertGreater(
            result["extra_shortest_path_scans_for_market_margin"], 0)


if __name__ == "__main__":
    unittest.main()
