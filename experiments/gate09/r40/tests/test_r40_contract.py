from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_r40 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from oracle_cache_r14 import evaluate_r14


class R40FinalNumericalAndArchitectureGate(unittest.TestCase):
    def setUp(self):
        self.cfg=R.R21.CASES["small"]

    def fixture(self,regime="original",factor=1.0):
        net,flows,meta=R.R34.capacity_fixture(self.cfg,regime,factor)
        settings=ClearingSettings(exponent=self.cfg.exponent)
        obs=evaluate_r14(net,flows,settings,())
        return net,flows,settings,obs

    def test_original_r20_sha256_manifest_and_frozen_economics(self):
        self.assertEqual(R.R21.validate_manifest(),16)
        net,flows,settings,obs=self.fixture()
        self.assertTrue(R.R33.verify_price_law(net,obs))
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"decay_sqrt",6))
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"backtracking",6))

    def test_exact_three_families_and_final_architecture_commitment(self):
        self.assertEqual(R.FAMILY_NUMBER,3)
        self.assertEqual(R.MAX_FAMILIES,3)
        self.assertEqual(R.CANDIDATE,"full_support_entropic_proportional")
        self.assertNotEqual(R.CANDIDATE,R.R39.CANDIDATE)
        self.assertNotEqual(R.CANDIDATE,R.R38.CANDIDATE)
        self.assertEqual(set(R.METHODS)-{R.CANDIDATE},
                         {"decay_sqrt","backtracking","progressive_global"})
        self.assertEqual(R.FINAL_NO_GO,
                         "NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION")

    def test_entropic_update_conserves_every_producer_supply(self):
        net,flows,settings,obs=self.fixture()
        digest=_flow_checksum(flows)
        for eta in R.ETAS:
            with self.subTest(eta=eta):
                updated,work=R.full_support_proportional_update(
                    net,flows,obs,settings.exponent,eta)
                net.snapshots(updated)
                for gid,group in net.groups.items():
                    self.assertAlmostEqual(
                        sum(mass for col,mass in updated.items()
                            if col.group_id==gid),
                        group.supply,delta=1e-10)
                self.assertTrue(work["no_new_supply_or_demand"])
                self.assertTrue(work["all_markets_price_positive"])
                self.assertGreater(work["new_canonical_columns"],0)
                self.assertGreater(work["independent_shortest_path_scans"],0)
                self.assertEqual(_flow_checksum(flows),digest)

    def test_update_supports_every_reachable_market_at_positive_price(self):
        net,flows,settings,obs=self.fixture(
            "heterogeneous_D_and_supply",.2)
        updated,work=R.full_support_proportional_update(
            net,flows,obs,settings.exponent,R.ETAS[-1])
        quantities=net.snapshots(updated)[1]
        prices=net.snapshots(updated)[2]
        for m in net.markets:
            self.assertGreater(quantities[m],0)
            self.assertTrue(math.isfinite(prices[m]) and prices[m]>0)
        after=evaluate_r14(net,updated,settings,())
        self.assertTrue(R.R33.verify_price_law(net,after))
        self.assertGreater(work["min_trial_column_quantity"],0)

    def test_entropic_update_uses_original_tariffs_and_canonical_routes(self):
        net,flows,settings,obs=self.fixture()
        old_edges=dict(net.edges)
        updated,work=R.full_support_proportional_update(
            net,flows,obs,settings.exponent,8.)
        for col,amount in updated.items():
            net.validate_column(col)
            self.assertGreater(amount,0)
        self.assertEqual(old_edges,net.edges)
        before=R.R21.settlement_check(net,flows)
        after=R.R21.settlement_check(net,updated)
        self.assertEqual(before["gross_micro"],before["credits_micro"])
        self.assertEqual(after["gross_micro"],after["credits_micro"])

    def test_new_algorithm_does_not_apply_unpriced_fictitious_mass(self):
        net,flows,settings,obs=self.fixture()
        for eta,prior in ((0,.04),(-1,.04),(2,0),(2,1),(2,-.01)):
            with self.subTest(eta=eta,prior=prior):
                with self.assertRaises(ValueError):
                    R.full_support_proportional_update(
                        net,flows,obs,settings.exponent,eta,prior=prior)
        moved,stats=R.full_support_proportional_update(
            net,flows,obs,settings.exponent,2.)
        for g in net.groups.values():
            old_supply=sum(v for c,v in flows.items() if c.group_id==g.id)
            new_supply=sum(v for c,v in moved.items() if c.group_id==g.id)
            self.assertAlmostEqual(old_supply,new_supply,delta=1e-10)

    def test_single_trial_is_one_full_R14_and_not_free(self):
        net,flows,settings,obs=self.fixture()
        original_hash=_flow_checksum(flows)
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as counted:
            changed,priced,delta,decision=R.entropic_trial(
                net,flows,obs,settings,remaining=1)
            self.assertEqual(counted.call_count,1)
        self.assertEqual(decision["oracle_calls"],1)
        self.assertEqual(decision["candidate_R14_calls"],1)
        self.assertGreater(decision["independent_shortest_path_scans"],0)
        self.assertTrue(decision["all_candidates_full_R14_repricing"])
        self.assertEqual(_flow_checksum(flows),original_hash)
        if decision["selected"]:
            self.assertTrue(R.R33.verify_price_law(net,priced))

    def test_all_candidate_oracles_and_trial_work_are_accounted(self):
        net,flows,settings,obs=self.fixture()
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as counted:
            moved,priced,delta,result=R.entropic_trial(
                net,flows,obs,settings,remaining=4)
            self.assertEqual(counted.call_count,result["oracle_calls"])
        self.assertEqual(len(result["trials"]),result["oracle_calls"])
        self.assertLessEqual(result["oracle_calls"],len(R.ETAS))
        self.assertEqual(
            result["independent_shortest_path_scans"],
            sum(t["candidate_independent_shortest_path_scans"]
                for t in result["trials"]))
        self.assertTrue(result["all_candidates_full_R14_repricing"])

    def test_fallback_is_explicit_and_does_not_falsify_stability(self):
        net,flows,settings,obs=self.fixture()
        with patch.object(R.R38,"numerical_merit",return_value=123.):
            proposal,_,_,outcome=R.entropic_trial(
                net,flows,obs,settings,remaining=4)
        self.assertTrue(outcome["fallback"])
        self.assertFalse(outcome["accepted_early"])
        self.assertEqual(outcome["oracle_calls"],4)
        self.assertIn(outcome["status"],(
            "BEST_REPRICED_ENTROPIC_FALLBACK",
            "STALLED_WITH_NONZERO_COMMERCIAL_GAP"))

    def test_run_preserves_hard_oracle_limit_and_final_finance(self):
        for budget in (16,17,25,33):
            with self.subTest(budget=budget):
                report=R.run_candidate(
                    self.cfg,"original",1.,budget,audit_every=2)
                self.assertLessEqual(report["actual_oracle_calls"],budget)
                self.assertEqual(report["actual_oracle_calls"],
                    1+sum(d["R14_calls"] for d in report["decisions"]))
                self.assertEqual(
                    report["financial_audits"][-1]["step"],
                    report["accepted_updates"])
                self.assertEqual(
                    report["financial_audits"][-1]["gross_micro"],
                    report["financial_audits"][-1]["credits_micro"])
                self.assertFalse(report["production_approved"])

    def test_original_controllers_are_exact_R38_replays(self):
        for method in R.METHODS:
            if method==R.CANDIDATE:continue
            with self.subTest(method=method):
                result=R.R38.run_method(
                    self.cfg,"original",.20,method,25,audit_every=2)
                self.assertLessEqual(result["actual_oracle_calls"],25)
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["accepted_updates"])

    def test_candidate_financial_integrity_with_heterogeneous_stress(self):
        for regime,capacity in (
            ("original",.20),
            ("heterogeneous_D_and_supply",.20),
            ("heterogeneous_D_and_supply",1.),
        ):
            with self.subTest(regime=regime,capacity=capacity):
                row=R.run_candidate(
                    self.cfg,regime,capacity,17,audit_every=2)
                self.assertFalse(row["global_equilibrium_proven"])
                self.assertFalse(row["F059_financial_policy_ratified"])
                self.assertEqual(row["financial_audits"][-1]["gross_micro"],
                                 row["financial_audits"][-1]["credits_micro"])

    def test_binding_architecture_decision_has_no_r41_candidate(self):
        cases=[R.run_candidate(self.cfg,"original",1.,17)]
        cases.extend(R.R38.run_method(
            self.cfg,"original",1.,method,17)
            for method in R.METHODS if method!=R.CANDIDATE)
        conclusion=R.architecture_decision(cases)
        self.assertIn(conclusion["final_architecture_decision"],(
            R.FINAL_NO_GO,
            "SELECT_R40_FOR_SEPARATE_VALIDATION_NOT_PRODUCTION"))
        self.assertEqual(conclusion["final_solver_family_count"],3)
        self.assertTrue(conclusion["no_fourth_numerical_candidate_authorized"])
        self.assertFalse(conclusion["change_authoritative_CSharp_or_Godot"])
        self.assertFalse(conclusion["financial_F059_ratified"])
        self.assertFalse(conclusion["production_approved"])

    def test_unmatched_or_duplicate_comparisons_must_fail(self):
        cases=[R.run_candidate(self.cfg,"original",1.,17)]
        with self.assertRaises(AssertionError):
            R.architecture_decision(cases)
        with self.assertRaises(AssertionError):
            R.architecture_decision(cases+cases)

    def test_equal_fixture_and_budget_is_obligatory_across_controls(self):
        cases=[R.run_candidate(self.cfg,"original",1.,25)]
        cases.extend(R.R38.run_method(
            self.cfg,"original",1.,method,25)
            for method in R.METHODS if method!=R.CANDIDATE)
        self.assertEqual(len({x["first_flow_sha256"] for x in cases}),1)
        self.assertEqual(len({x["fixture"]["actual_fixture_fingerprint"]
                              for x in cases}),1)
        decision=R.architecture_decision(cases)
        self.assertEqual(decision["fixture_count"],1)
        self.assertEqual(decision["fixtures"][0]["r14_call_ceiling_each"],25)

    def test_no_hidden_fourth_family_and_no_relaxed_f072_thresholds(self):
        self.assertEqual(R.MAX_FAMILIES,3)
        self.assertEqual(R.R24.TARGET,1e-4)
        self.assertFalse(R.R24.numerical_pass(
            {"relative_gap":.001,"max_group_gap":.001},None))
        with self.assertRaises(ValueError):
            R.run_candidate(self.cfg,"original",1.,15)
        with self.assertRaises(ValueError):
            R.run_candidate(self.cfg,"original",.333,17)
        with self.assertRaises(ValueError):
            R.entropic_trial(*self.fixture()[:3],
                             self.fixture()[3],remaining=0)


if __name__=="__main__":
    unittest.main()
