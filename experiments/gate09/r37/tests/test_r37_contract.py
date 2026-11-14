from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_r37 as R
from clearing_r5 import ClearingSettings,_flow_checksum,_mix
from oracle_cache_r14 import evaluate_r14


class R37FinalEnvelopeAndSolutionGate(unittest.TestCase):
    def setUp(self):
        self.cfg=R.R21.CASES["small"]

    def frozen(self,regime="original",factor=1.0):
        net,flows,meta=R.R34.capacity_fixture(self.cfg,regime,factor)
        settings=ClearingSettings(exponent=4)
        obs=evaluate_r14(net,flows,settings,())
        return net,flows,settings,obs

    def first_switch(self,regime="original",factor=1.0,policy="decay_sqrt"):
        net,flows,settings,obs=self.frozen(regime,factor)
        for step in range(1,17):
            newer,after,delta,calls,alpha=R.R29.step_forward(
                net,flows,obs,settings,policy,step)
            crossed=R.R31.transition_crossings(net,obs,after,settings.exponent)
            if crossed["market_switches"]:
                pairs=sorted(crossed["pairs"],key=lambda r:(
                    min(abs(r["before_log_advantage_new_over_old"]),
                        abs(r["after_log_advantage_new_over_old"])),r["group"]))
                return net,flows,settings,obs,newer,after,alpha,pairs[0]
            flows,obs=newer,after
        self.fail("Expected at least one true market switch")

    def root_and_envelope(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair=self.first_switch()
        bracket=R.R36.crossing_bracket(
            net,flows,old,new_flow,new_obs,settings,alpha,pair,
            iterations=R.BISECTION_STEPS)
        return net,flows,settings,old,new_flow,new_obs,alpha,pair,bracket

    def test_original_r20_sha256_manifest_immutable(self):
        self.assertEqual(R.R21.validate_manifest(),16)

    def test_explicit_end_diagnostics_decision_is_binding(self):
        self.assertEqual(
            R.DECISION,"CLOSE_DIAGNOSTIC_TRACK_START_R38_SOLVER_EXPERIMENTS")
        self.assertEqual(R.BISECTION_STEPS,R.R36.BISECTION_ITERATIONS)
        self.assertEqual(R.CAPACITY_FACTORS,R.R36.CAPACITY_FACTORS)
        self.assertEqual(R.REGIMES,R.R36.REGIMES)
        self.assertEqual(R.POLICIES,R.R36.POLICIES)

    def test_full_rank_replays_r14_exact_winner_score_and_ties(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair=self.first_switch()
        gid,A,B=pair["group"],pair["old_destination"],pair["new_destination"]
        before=R.full_envelope_rank(net,old,settings,gid,A,B)
        after=R.full_envelope_rank(net,new_obs,settings,gid,A,B)
        self.assertEqual(before["global_winner_market"],A)
        self.assertEqual(after["global_winner_market"],B)
        self.assertEqual(before["global_winner_score"],
                         old["groups"][gid]["score"])
        self.assertEqual(after["global_winner_score"],
                         new_obs["groups"][gid]["score"])
        self.assertEqual(before["r14_group_tie_flag"],
                         gid in old["tie_groups"])
        self.assertEqual(after["r14_group_tie_flag"],
                         gid in new_obs["tie_groups"])
        self.assertGreater(before["independent_route_scans"],0)
        self.assertGreaterEqual(len(before["all_compatible_markets"]),2)

    def test_full_rank_stress_fixture_also_replays_f072(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair=self.first_switch(
            "heterogeneous_D_and_supply",.2)
        gid,A,B=pair["group"],pair["old_destination"],pair["new_destination"]
        for snapshot in (old,new_obs):
            ranked=R.full_envelope_rank(net,snapshot,settings,gid,A,B)
            self.assertEqual(ranked["global_winner_market"],
                             snapshot["target"][gid].destination)
            self.assertEqual(ranked["global_winner_score"],
                             snapshot["groups"][gid]["score"])

    def test_topology_direct_A_to_B_class_is_not_global_continuity_proof(self):
        sampled=[
            {"t":0.,"ranking":{"global_winner_market":5,
                              "pair_near_tie_but_shadowed_by_third":False,
                              "old_new_pair_compete_for_global_top":False,
                              "r14_group_tie_flag":False,
                              "top_runner_up_within_original_tie_threshold":False,
                              "top_runner_up_relative_margin":.04,
                              "old_market_route":[1],"new_market_route":[2]}},
            {"t":1.,"ranking":{"global_winner_market":6,
                              "pair_near_tie_but_shadowed_by_third":False,
                              "old_new_pair_compete_for_global_top":False,
                              "r14_group_tie_flag":False,
                              "top_runner_up_within_original_tie_threshold":False,
                              "top_runner_up_relative_margin":.03,
                              "old_market_route":[1],"new_market_route":[2]}}
        ]
        result=R.classify_envelope(sampled,5,6)
        self.assertEqual(result["sampled_topology_class"],
                         "A_to_B_direct_in_sample")
        self.assertEqual(result["sampled_winner_sequence"],[5,6])
        self.assertTrue(result["sampling_not_exhaustive_no_absence_certification"])

    def test_topology_explicitly_detects_third_and_reentry(self):
        def point(t,w):
            return {"t":t,"ranking":{
                "global_winner_market":w,
                "pair_near_tie_but_shadowed_by_third":False,
                "old_new_pair_compete_for_global_top":False,
                "r14_group_tie_flag":False,
                "top_runner_up_within_original_tie_threshold":False,
                "top_runner_up_relative_margin":.02,
                "old_market_route":[1],"new_market_route":[2]}}
        third=R.classify_envelope(
            [point(0.,1),point(.25,3),point(.5,3),point(1.,2)],1,2)
        self.assertEqual(third["sampled_topology_class"],
                         "A_via_third_to_B_in_sample")
        self.assertEqual(third["sampled_distinct_third_winner_markets"],[3])
        reverse=R.classify_envelope(
            [point(0.,1),point(.25,2),point(.5,1),point(1.,2)],1,2)
        self.assertEqual(reverse["sampled_topology_class"],
                         "A_B_reentry_in_sample")
        self.assertEqual(reverse["sampled_winner_sequence"],[1,2,1,2])

    def test_third_market_tie_and_R14_flag_are_distinct_classifiers(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair=self.first_switch()
        observed=R.full_envelope_rank(
            net,old,settings,pair["group"],
            pair["old_destination"],pair["new_destination"])
        if observed["old_new_pair_near_tie"]:
            self.assertFalse(observed["old_new_pair_compete_for_global_top"]
                             and observed["pair_near_tie_but_shadowed_by_third"])
        self.assertEqual(
            observed["r14_group_tie_flag"],
            pair["group"] in old["tie_groups"])

    def test_grid_preserves_real_original_accepted_step(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair,bracket=(
            self.root_and_envelope())
        before_hash=_flow_checksum(flows)
        after_hash=_flow_checksum(new_flow)
        envelope=R.envelope_samples(
            net,flows,old,new_flow,new_obs,settings,alpha,bracket,pair)
        self.assertEqual(envelope["samples"][0]["t"],0.)
        self.assertEqual(envelope["samples"][-1]["t"],1.)
        self.assertEqual(envelope["samples"][0]["flow_sha256"],before_hash)
        self.assertEqual(envelope["samples"][-1]["flow_sha256"],after_hash)
        self.assertEqual(_flow_checksum(flows),before_hash)
        self.assertEqual(_flow_checksum(new_flow),after_hash)
        self.assertEqual(
            envelope["sampled_points"],len(envelope["samples"]))
        self.assertEqual(
            envelope["extra_r14_oracles"],envelope["sampled_points"]-2)
        self.assertGreater(envelope["independent_route_scans"],0)

    def test_each_additional_complete_ranking_reprices_R14(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair,bracket=(
            self.root_and_envelope())
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as mocked:
            result=R.envelope_samples(
                net,flows,old,new_flow,new_obs,settings,alpha,bracket,pair)
        self.assertEqual(mocked.call_count,result["extra_r14_oracles"])
        self.assertTrue(result["hybrid_counterfactual_is_never_committed"])

    def test_no_out_of_range_root_interval_or_fake_continuity(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair,bracket=(
            self.root_and_envelope())
        broken=dict(bracket,interval_start_fraction=-.01)
        with self.assertRaises(AssertionError):
            R.envelope_samples(
                net,flows,old,new_flow,new_obs,settings,alpha,broken,pair)
        broken=dict(bracket,interval_end_fraction=1.01)
        with self.assertRaises(AssertionError):
            R.envelope_samples(
                net,flows,old,new_flow,new_obs,settings,alpha,broken,pair)

    def test_selected_envelope_price_and_finance_are_unchanged(self):
        net,flows,settings,old,new_flow,new_obs,alpha,pair,bracket=(
            self.root_and_envelope())
        result=R.envelope_samples(
            net,flows,old,new_flow,new_obs,settings,alpha,bracket,pair)
        self.assertTrue(R.R33.verify_price_law(net,new_obs))
        for flow in (flows,new_flow):
            settle=R.R21.settlement_check(net,flow)
            self.assertEqual(settle["gross_micro"],settle["credits_micro"])
        self.assertFalse(result["result"].get("global_equilibrium_proven",False))

    def test_run_matches_frozen_R36_all_original_results(self):
        result=R.run_case(
            self.cfg,"original",1.,"decay_sqrt",4,audit_every=2)
        prior=R.R36.run_case(
            self.cfg,"original",1.,"decay_sqrt",4,audit_every=2)
        self.assertEqual(result["final_flow_sha256"],
                         prior["final_flow_sha256"])
        self.assertEqual(result["final_gap"],
                         prior["final_global_gap"])
        self.assertEqual(result["original_policy_R14_calls"],
                         prior["original_policy_r14_calls"])
        self.assertEqual(result["total_market_switches"],
                         prior["all_market_switches"])
        self.assertEqual(result["sampled_pairs"],
                         prior["selected_producer_crossings"])
        self.assertEqual(result["financial_audits"][-1]["step"],4)
        self.assertTrue(result["diagnostic_track_closed"])
        self.assertFalse(result["F059_financial_ties_ratified"])
        self.assertFalse(result["global_equilibrium_proven"])
        self.assertFalse(result["production_approved"])

    def test_oracle_budget_separately_charges_brackets_and_envelopes(self):
        report=R.run_case(
            self.cfg,"original",.20,"decay_sqrt",3,audit_every=2)
        self.assertEqual(report["R36_reused_bracket_extra_R14_calls"],
                         25*report["sampled_pairs"])
        self.assertEqual(report["total_R14_calls"],
                         report["original_policy_R14_calls"]+
                         report["R36_reused_bracket_extra_R14_calls"]+
                         report["R37_full_envelope_extra_R14_calls"])
        self.assertEqual(report["independent_root_R13_parity_checks"],
                         report["sampled_pairs"])
        self.assertEqual(report["root_F084F_financial_audits"],
                         report["sampled_pairs"])

    def test_repeated_run_is_bitwise_deterministic_on_observational_data(self):
        first=R.run_case(
            self.cfg,"original",.4,"decay_sqrt",3,audit_every=2)
        second=R.run_case(
            self.cfg,"original",.4,"decay_sqrt",3,audit_every=2)
        self.assertEqual(first["rows"],second["rows"])
        self.assertEqual(first["events"],second["events"])

    def test_no_switches_do_not_get_fake_third_market_count(self):
        self.assertEqual(R.aggregate([])["sampled_via_third"],0)
        self.assertEqual(R.aggregate([])["runs"],0)
        self.assertEqual(R.aggregate([])["research_decision"],R.DECISION)

    def test_unmodified_R21_and_original_safety_gate(self):
        for policy in ("decay_sqrt","backtracking"):
            self.assertTrue(R.R24.check_r21_reference(
                self.cfg,policy,6))
        self.assertFalse(R.R24.numerical_pass(
            {"relative_gap":1.0,"max_group_gap":1.0},None))


if __name__=="__main__":
    unittest.main()
