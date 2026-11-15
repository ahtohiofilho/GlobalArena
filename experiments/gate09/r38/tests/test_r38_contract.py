from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_r38 as R
from clearing_r5 import ClearingSettings,_flow_checksum,_change_metrics
from oracle_cache_r14 import evaluate_r14


class R38ActualCoordinateSolverTrial(unittest.TestCase):
    def setUp(self):
        self.cfg=R.R21.CASES["small"]

    def initial(self,regime="original",factor=1.):
        net,flows,meta=R.R34.capacity_fixture(self.cfg,regime,factor)
        settings=ClearingSettings(exponent=4)
        obs=evaluate_r14(net,flows,settings,())
        return net,flows,settings,obs

    def test_r20_manifest_preserved_and_r37_track_closed(self):
        self.assertEqual(R.R21.validate_manifest(),16)
        self.assertTrue(R.R37.DECISION.startswith("CLOSE_DIAGNOSTIC_TRACK"))
        self.assertEqual(R.CANDIDATE,"coordinate_exchange")

    def test_new_algorithm_does_not_modify_price_or_tariff(self):
        net,flows,settings,obs=self.initial(
            "heterogeneous_D_and_supply",.20)
        self.assertTrue(R.R33.verify_price_law(net,obs))
        before={eid:edge for eid,edge in net.edges.items()}
        trial=R.run_method(
            self.cfg,"heterogeneous_D_and_supply",.20,
            R.CANDIDATE,oracle_budget=33,audit_every=2)
        self.assertFalse(trial["production_approved"])
        self.assertFalse(trial["F059_policy_ratified"])
        self.assertFalse(trial["convergence_proven"])
        self.assertEqual(net.edges,before)

    def test_weighted_regret_direction_has_supply_and_distinct_columns(self):
        net,flows,settings,obs=self.initial()
        ranked=R.donor_directions(net,flows,obs)
        self.assertTrue(ranked)
        self.assertLessEqual(len(ranked),R.DIRECTIONS_PER_STEP)
        self.assertEqual(
            [x["weighted_regret"] for x in ranked],
            sorted([x["weighted_regret"] for x in ranked],reverse=True))
        for point in ranked:
            self.assertGreater(point["weighted_regret"],0)
            self.assertGreater(point["source_amount"],0)
            self.assertNotEqual(point["source"],point["sink"])
            self.assertEqual(point["source"].group_id,point["sink"].group_id)
            net.validate_column(point["sink"])

    def test_one_trade_candidate_preserves_full_sale_and_price(self):
        net,flows,settings,obs=self.initial()
        row=R.donor_directions(net,flows,obs)[0]
        moved=R.R35.transfer(
            net,flows,row["source"],row["sink"],
            row["source_amount"]*.125)
        next_obs=evaluate_r14(net,moved,settings,())
        self.assertTrue(R.R33.verify_price_law(net,next_obs))
        for gid,group in net.groups.items():
            actual=sum(q for col,q in moved.items() if col.group_id==gid)
            self.assertAlmostEqual(actual,group.supply,delta=1e-10)
        financial=R.R21.settlement_check(net,moved)
        self.assertEqual(financial["gross_micro"],financial["credits_micro"])

    def test_true_oracle_billing_of_each_proposed_trade(self):
        net,flows,settings,obs=self.initial()
        previous_hash=_flow_checksum(flows)
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as oracle:
            moved,priced,delta,decision=R.coordinate_trial(
                net,flows,obs,settings,remaining=5)
            self.assertEqual(oracle.call_count,decision["oracle_calls"])
        self.assertEqual(len(decision["trials"]),5)
        self.assertTrue(decision["truncated_by_budget"])
        self.assertEqual(_flow_checksum(flows),previous_hash)
        if moved is None:
            self.assertFalse(decision["selected"])
            self.assertFalse(decision["strict_merit_improved"])
        else:
            self.assertTrue(decision["selected"])
            self.assertLess(R.numerical_merit(priced),
                            R.numerical_merit(obs))
            self.assertNotEqual(_flow_checksum(moved),previous_hash)

    def test_no_freeze_claims_false_stability_when_all_candidates_rejected(self):
        net,flows,settings,obs=self.initial()
        with patch.object(R,"numerical_merit",
                          side_effect=lambda snapshot:100.):
            changed,next_obs,delta,decision=R.coordinate_trial(
                net,flows,obs,settings,remaining=1)
        self.assertIsNone(changed)
        self.assertFalse(decision["selected"])
        self.assertEqual(decision["oracle_calls"],1)
        self.assertEqual(decision["status"],
                         "STALLED_ALL_CANDIDATES_NONIMPROVING")

    def test_unregistered_candidate_and_illegal_work_budget_rejected(self):
        net,flows,settings,obs=self.initial()
        with self.assertRaises(ValueError):
            R.coordinate_trial(net,flows,obs,settings,0)
        with self.assertRaises(ValueError):
            R.donor_directions(net,flows,obs,limit=0)
        with self.assertRaises(ValueError):
            R.run_method(self.cfg,"original",1.,"unknown_method",16)
        with self.assertRaises(ValueError):
            R.run_method(self.cfg,"original",1.,R.CANDIDATE,15)

    def test_true_merit_has_positive_weight_but_is_not_price_rule(self):
        net,flows,settings,obs=self.initial()
        self.assertAlmostEqual(
            R.numerical_merit(obs),
            obs["relative_gap"]+.30*obs["max_group_gap"],
            delta=1e-12)
        self.assertEqual(R.MERIT_WORST_WEIGHT,.30)
        self.assertTrue(R.R33.verify_price_law(net,obs))

    def test_decay_sqrt_incumbent_replays_original_r21_exactly(self):
        net,flows,settings,obs=self.initial()
        trial=R.incumbent_step(
            net,flows,obs,settings,"decay_sqrt",1,remaining=5)
        expected=R.R21.choose_step(
            net,flows,obs,settings,"decay_sqrt",1)
        self.assertEqual(_flow_checksum(trial[0]),_flow_checksum(expected[0]))
        self.assertEqual(trial[1]["relative_gap"],expected[1]["relative_gap"])
        self.assertEqual(trial[3]["oracle_calls"],expected[3])

    def test_backtracking_matches_reference_and_never_overspends(self):
        net,flows,settings,obs=self.initial()
        proposal=R.incumbent_step(
            net,flows,obs,settings,"backtracking",1,remaining=4)
        expected=R.R21.choose_step(net,flows,obs,settings,"backtracking",1)
        self.assertEqual(_flow_checksum(proposal[0]),
                         _flow_checksum(expected[0]))
        self.assertEqual(proposal[3]["oracle_calls"],expected[3])
        self.assertIsNone(R.incumbent_step(
            net,flows,obs,settings,"backtracking",1,remaining=3))

    def test_progressive_incumbent_is_actual_original_r26_policy(self):
        net,flows,settings,obs=self.initial()
        flow,priced,delta,record=R.incumbent_step(
            net,flows,obs,settings,"progressive_global",1,remaining=5)
        original=R.R26.choose_progressive(
            net,flows,obs,settings,"progressive_global",5)
        self.assertEqual(_flow_checksum(flow),_flow_checksum(original[0]))
        self.assertEqual(priced["relative_gap"],original[1]["relative_gap"])
        self.assertEqual(record["oracle_calls"],original[3]["candidate_count"])
        self.assertGreaterEqual(record["oracle_calls"],1)

    def test_run_respects_hard_R14_oracle_ceiling(self):
        for method in R.METHODS:
            with self.subTest(method=method):
                result=R.run_method(
                    self.cfg,"original",1.0,method,oracle_budget=25,
                    audit_every=2)
                self.assertLessEqual(result["actual_oracle_calls"],25)
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["accepted_updates"])
                self.assertEqual(result["original_r14_trials_including_discarded"],
                                 result["actual_oracle_calls"])
                self.assertEqual(
                    result["actual_oracle_calls"],
                    1+sum(d["oracle_calls"] for d in result["decisions"]))

    def test_coordinate_steps_only_strictly_improve_numerical_merit(self):
        result=R.run_method(
            self.cfg,"original",.20,R.CANDIDATE,
            oracle_budget=49,audit_every=2)
        merits=[row["numerical_merit"] for row in result["rows"]]
        self.assertTrue(all(merits[i]<=merits[i-1]+1e-12
                            for i in range(1,len(merits))))
        for d in result["decisions"]:
            if d["decision"]["selected"]:
                self.assertTrue(d["decision"]["strict_merit_improved"])
        self.assertFalse(result["convergence_proven"])
        self.assertFalse(result["CSharp_parity_proven"])

    def test_replay_same_initial_fixture_across_competitors(self):
        reports=[
            R.run_method(self.cfg,"original",1.,method,oracle_budget=25)
            for method in R.METHODS]
        self.assertEqual(len({r["first_flow_sha256"] for r in reports}),1)
        self.assertEqual(len({r["fixture"]["actual_fixture_fingerprint"]
                              for r in reports}),1)
        self.assertEqual(len({r["maximum_oracle_budget"] for r in reports}),1)
        judgement=R.comparative_decision(reports)
        self.assertEqual(len(judgement["fixtures"]),1)
        self.assertIn(judgement["candidate_decision"],(
            "PROVISIONAL_GO_TO_MORE_VALIDATION",
            "NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET"))
        self.assertTrue(judgement["max_three_candidate_families_including_R38"])

    def test_financial_conservation_all_stressed_methods(self):
        for method in R.METHODS:
            with self.subTest(method=method):
                result=R.run_method(
                    self.cfg,"heterogeneous_D_and_supply",.20,
                    method,oracle_budget=25,audit_every=2)
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["accepted_updates"])
                for check in result["financial_audits"]:
                    self.assertEqual(check["gross_micro"],check["credits_micro"])
                self.assertFalse(result["F059_policy_ratified"])

    def test_solution_decision_is_separate_from_economic_certification(self):
        fixtures=[]
        for method in R.METHODS:
            result=R.run_method(
                self.cfg,"original",1.,method,oracle_budget=17)
            fixtures.append(result)
        outcome=R.comparative_decision(fixtures)
        self.assertFalse(outcome["economic_global_convergence_NOT_proven"] is False)
        self.assertTrue(outcome["no_manipulation_of_price_demand_or_tariff"])
        self.assertTrue(all(
            not record["production_approved"] for record in fixtures))


if __name__=="__main__":
    unittest.main()
