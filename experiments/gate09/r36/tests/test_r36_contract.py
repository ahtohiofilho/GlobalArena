from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_r36 as R
from clearing_r5 import ClearingSettings, _flow_checksum, _mix
from oracle_cache_r14 import evaluate_r14


class R36ObservedMarketCrossingContracts(unittest.TestCase):
    def setUp(self):
        self.cfg=R.R21.CASES["small"]

    def frozen(self,regime="original",factor=1.0):
        net,flows,meta=R.R34.capacity_fixture(self.cfg,regime,factor)
        settings=ClearingSettings(exponent=4)
        obs=evaluate_r14(net,flows,settings,())
        return net,flows,settings,obs

    def first_transition_with_switch(self,regime="original",factor=1.0,
                                      policy="decay_sqrt"):
        net,flows,settings,obs=self.frozen(regime,factor)
        for step in range(1,17):
            new,after,delta,calls,alpha=R.R29.step_forward(
                net,flows,obs,settings,policy,step)
            if any(obs["target"][gid].destination!=after["target"][gid].destination
                   for gid in net.groups):
                transition=R.R31.transition_crossings(
                    net,obs,after,settings.exponent)
                self.assertGreater(transition["market_switches"],0)
                return net,flows,settings,obs,new,after,alpha,transition
            flows,obs=new,after
        self.fail("R36 fixture had no destination switch in the test horizon")

    def test_manifest_frozen_sha256(self):
        self.assertEqual(R.R21.validate_manifest(),16)

    def test_registered_dimensions_target_real_market_switches(self):
        self.assertEqual(R.CAPACITY_FACTORS,(0.20,0.40,1.0))
        self.assertEqual(R.POLICIES,("decay_sqrt","backtracking"))
        self.assertEqual(R.REGIMES,R.R34.REGIMES)
        self.assertEqual(R.BISECTION_ITERATIONS,24)
        self.assertEqual(R.GROUPS_PER_TRANSITION,2)

    def test_market_score_pair_matches_original_r14_frozen_best(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        gid=transition["pairs"][0]["group"]
        a,b=transition["pairs"][0]["old_destination"],transition[
            "pairs"][0]["new_destination"]
        old=R.market_pair_snapshot(net,obs,settings,gid,a,b)
        new=R.market_pair_snapshot(net,after,settings,gid,a,b)
        self.assertLessEqual(
            old["pair"]["log_advantage_new_over_old"],R.ENDPOINT_SIGN_EPS)
        self.assertGreaterEqual(
            new["pair"]["log_advantage_new_over_old"],-R.ENDPOINT_SIGN_EPS)
        self.assertEqual(old["original_r14_best_market"],a)
        self.assertEqual(new["original_r14_best_market"],b)
        self.assertGreater(old["extra_shortest_path_scans"],0)

    def test_replay_accepted_step_is_exact_original_f072_mix(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        reproduced=_mix(flows,net,obs["target"],alpha)
        self.assertEqual(_flow_checksum(next_flows),_flow_checksum(reproduced))
        self.assertEqual(evaluate_r14(net,reproduced,settings,())["target"],
                         after["target"])

    def test_bisection_uses_only_actual_switched_market_pairs(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        result=R.inspect_accepted_transition(
            net,flows,obs,next_flows,after,settings,alpha,1)
        self.assertEqual(result["market_switches_in_full_transition"],
                         transition["market_switches"])
        self.assertLessEqual(
            result["selected_group_count"],R.GROUPS_PER_TRANSITION)
        self.assertEqual(
            result["extra_r14_oracle_calls"],
            (R.BISECTION_ITERATIONS+1)*result["selected_group_count"])
        for row in result["selected_witnesses"]:
            self.assertNotEqual(row["old_market"],row["new_market"])
            self.assertEqual(row["old_market"],obs["target"][row["group"]].destination)
            self.assertEqual(row["new_market"],after["target"][row["group"]].destination)
            self.assertLessEqual(row["interval_width"],2**(-24)+1e-15)

    def test_exact_probe_oracle_billing_includes_final_replay(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        pair=transition["pairs"][0]
        original=_flow_checksum(flows)
        accepted=_flow_checksum(next_flows)
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as oracle:
            checked=R.crossing_bracket(
                net,flows,obs,next_flows,after,settings,alpha,pair,
                iterations=7)
            self.assertEqual(oracle.call_count,8)
        self.assertEqual(checked["extra_r14_oracle_calls"],8)
        self.assertEqual(len(checked["trace"]),7)
        self.assertEqual(checked["root_f072_r13_parity_checks"],1)
        self.assertEqual(checked["root_f084f_financial_audits"],1)
        self.assertEqual(_flow_checksum(flows),original)
        self.assertEqual(_flow_checksum(next_flows),accepted)
        self.assertTrue(checked["same_original_f072_policy_update_not_new_turns"])

    def test_intermediate_market_quote_law_and_financial_conservation(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch("heterogeneous_D_and_supply",.20))
        pair=transition["pairs"][0]
        result=R.crossing_bracket(
            net,flows,obs,next_flows,after,settings,alpha,pair,
            iterations=8)
        self.assertEqual(result["root_financial_witness"]["gross_micro"],
                         result["root_financial_witness"]["credits_micro"])
        self.assertTrue(result["interval_guarantees_one_score_sign_crossing_not_uniqueness"])
        self.assertTrue(result["root_not_claimed_exact"])

    def test_pair_tie_is_distinct_from_global_market_tie(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        pair=transition["pairs"][0]
        gid=pair["group"]
        a,b=pair["old_destination"],pair["new_destination"]
        # The contract does NOT equate an A/B tie with F-059.
        outcome=R.market_pair_snapshot(net,obs,settings,gid,a,b)
        self.assertIn(outcome["pair_near_tie_using_exact_r14_score_threshold"],
                      (True,False))
        self.assertIn(outcome["both_pair_markets_competitive_for_global_best"],
                      (True,False))
        self.assertIn(outcome["original_r14_group_has_tie_flag"],(True,False))
        self.assertFalse(outcome["pair_tied_but_global_best_is_third"] and
                         outcome["both_pair_markets_competitive_for_global_best"])

    def test_inverted_endpoint_is_rejected(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        pair=dict(transition["pairs"][0])
        pair["old_destination"],pair["new_destination"]=(
            pair["new_destination"],pair["old_destination"])
        with self.assertRaises(AssertionError):
            R.crossing_bracket(
                net,flows,obs,next_flows,after,settings,alpha,pair,
                iterations=4)

    def test_frozen_policy_trajectory_matches_r34_baseline(self):
        result=R.run_case(self.cfg,"original",1.0,"decay_sqrt",5,audit_every=2)
        prior=R.R34.run_case(
            self.cfg,"original",1.0,"decay_sqrt",5,audit_every=2)
        self.assertEqual(result["final_flow_sha256"],prior["final_flow_sha256"])
        self.assertEqual(result["final_global_gap"],prior["final_global_gap"])
        self.assertEqual(result["original_policy_r14_calls"],
                         prior["policy_r14_calls"])
        self.assertEqual(result["financial_audits"][-1]["step"],5)
        self.assertFalse(result["global_equilibrium_proven"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])

    def test_source_trajectory_unaffected_by_all_probe_calls(self):
        first=R.run_case(
            self.cfg,"original",.20,"decay_sqrt",4,audit_every=2)
        longer=R.run_case(
            self.cfg,"original",.20,"decay_sqrt",7,audit_every=2)
        self.assertEqual(first["rows"],longer["rows"][:5])
        self.assertEqual(
            first["total_r14_calls"],
            first["original_policy_r14_calls"]+
            first["extra_r14_probe_calls"])

    def test_congestion_fixture_preserves_original_prices(self):
        net,flows,settings,obs=self.frozen(
            "heterogeneous_D_and_supply",.20)
        self.assertTrue(R.R33.verify_price_law(net,obs))
        self.assertEqual(R.R21.settlement_check(net,flows)["gross_micro"],
                         R.R21.settlement_check(net,flows)["credits_micro"])

    def test_no_switches_produce_no_fabricated_brackets(self):
        net,flows,settings,obs=self.frozen()
        with self.assertRaises(ValueError):
            R.inspect_accepted_transition(
                net,flows,obs,flows,obs,settings,.25,0)

    def test_bound_rejects_bad_work_budget(self):
        net,flows,settings,obs,next_flows,after,alpha,transition=(
            self.first_transition_with_switch())
        pair=transition["pairs"][0]
        with self.assertRaises(ValueError):
            R.crossing_bracket(
                net,flows,obs,next_flows,after,settings,alpha,pair,
                iterations=0)
        with self.assertRaises(ValueError):
            R.crossing_bracket(
                net,flows,obs,next_flows,after,settings,alpha,pair,
                iterations=41)

    def test_aggregate_counts_are_separate_from_convergence(self):
        result=R.run_case(
            self.cfg,"original",1.0,"decay_sqrt",6,audit_every=2)
        aggregated=R.summarize([result])
        self.assertEqual(aggregated["runs"],1)
        self.assertEqual(
            aggregated["selected_market_crossing_witnesses"],
            result["selected_producer_crossings"])
        self.assertEqual(
            aggregated["additional_r14_bisection_oracles"],
            result["extra_r14_probe_calls"])
        self.assertTrue(aggregated["not_iid_samples_not_general_equilibrium"])

    def test_original_r21_replay_untouched(self):
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"decay_sqrt",6))
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"backtracking",6))


if __name__=="__main__":
    unittest.main()
