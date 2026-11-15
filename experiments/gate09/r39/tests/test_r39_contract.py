from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_r39 as R
from clearing_r5 import ClearingSettings,_flow_checksum,_mix,_change_metrics
from oracle_cache_r14 import evaluate_r14


class R39TwoStageNumericalSolutionContracts(unittest.TestCase):
    def setUp(self):
        self.cfg=R.R21.CASES["small"]

    def initial(self,regime="original",factor=1.0):
        net,flows,meta=R.R34.capacity_fixture(self.cfg,regime,factor)
        cfg=ClearingSettings(exponent=4)
        observed=evaluate_r14(net,flows,cfg,())
        return net,flows,cfg,observed

    def test_r20_immutable_manifest_and_phase_transition(self):
        self.assertEqual(R.R21.validate_manifest(),16)
        self.assertEqual(R.R37.DECISION,
                         "CLOSE_DIAGNOSTIC_TRACK_START_R38_SOLVER_EXPERIMENTS")
        self.assertEqual(R.FAMILY_NUMBER,2)
        self.assertEqual(R.MAX_FAMILIES,3)

    def test_new_candidate_differs_from_coordinate_and_controls(self):
        self.assertEqual(R.CANDIDATE,"predictor_corrector_best_response")
        self.assertNotIn(R.R38.CANDIDATE,R.METHODS)
        self.assertEqual(set(R.METHODS)-{R.CANDIDATE},
                         {"decay_sqrt","backtracking","progressive_global"})
        self.assertEqual(R.ALPHAS,(.25,.125,.0625,.03125))
        self.assertEqual(R.WORK_PER_TRIAL,2)

    def test_predictor_and_corrector_are_repriced_with_original_R14(self):
        net,flows,settings,obs=self.initial()
        original_sha=_flow_checksum(flows)
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as oracle:
            changed,priced,delta,result=R.predictor_corrector_trial(
                net,flows,obs,settings,remaining=2)
            self.assertEqual(oracle.call_count,2)
        first=result["trials"][0]
        alpha=first["alpha"]
        predictor=_mix(flows,net,obs["target"],alpha)
        forecast=evaluate_r14(net,predictor,settings,())
        corrected=_mix(flows,net,forecast["target"],alpha)
        corrected_obs=evaluate_r14(net,corrected,settings,())
        self.assertEqual(first["predictor_flow_sha256"],
                         _flow_checksum(predictor))
        self.assertEqual(first["correction_flow_sha256"],
                         _flow_checksum(corrected))
        self.assertEqual(first["corrected_gap"],
                         corrected_obs["relative_gap"])
        self.assertEqual(result["oracle_calls"],2)
        self.assertEqual(result["predictor_oracle_calls"],1)
        self.assertEqual(result["corrector_oracle_calls"],1)
        self.assertEqual(_flow_checksum(flows),original_sha)
        self.assertTrue(first["prediction_only_never_committed"])

    def test_both_stages_conserve_each_group_and_price_law(self):
        net,flows,settings,obs=self.initial(
            "heterogeneous_D_and_supply",.20)
        y=_mix(flows,net,obs["target"],.25)
        priced=evaluate_r14(net,y,settings,())
        z=_mix(flows,net,priced["target"],.25)
        updated=evaluate_r14(net,z,settings,())
        for allocation,state in ((y,priced),(z,updated)):
            self.assertTrue(R.R33.verify_price_law(net,state))
            for gid,group in net.groups.items():
                self.assertAlmostEqual(
                    sum(v for col,v in allocation.items() if col.group_id==gid),
                    group.supply,delta=1e-10)
            fiscal=R.R21.settlement_check(net,allocation)
            self.assertEqual(fiscal["gross_micro"],fiscal["credits_micro"])

    def test_correction_origin_is_old_x_not_predictor_y(self):
        net,flows,settings,obs=self.initial()
        y=_mix(flows,net,obs["target"],.25)
        y_obs=evaluate_r14(net,y,settings,())
        expected=_mix(flows,net,y_obs["target"],.25)
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as oracle:
            _,_,_,decision=R.predictor_corrector_trial(
                net,flows,obs,settings,2)
        self.assertEqual(decision["trials"][0]["correction_flow_sha256"],
                         _flow_checksum(expected))
        self.assertEqual(oracle.call_count,2)

    def test_two_call_atomic_limit_for_extragradient_update(self):
        net,flows,settings,obs=self.initial()
        for n in (-1,0,1,2.5):
            with self.subTest(remaining=n):
                with self.assertRaises(ValueError):
                    R.predictor_corrector_trial(
                        net,flows,obs,settings,n)
        result=R.predictor_corrector_trial(
            net,flows,obs,settings,3)[3]
        self.assertEqual(result["oracle_calls"],2)
        self.assertEqual(result["candidates_tested"],1)
        self.assertTrue(result["fallback"] or result["accepted_early"])

    def test_all_candidates_are_billed_including_rejected_forecasts(self):
        net,flows,settings,obs=self.initial()
        with patch.object(R,"evaluate_r14",wraps=R.evaluate_r14) as oracle:
            changed,priced,delta,decision=R.predictor_corrector_trial(
                net,flows,obs,settings,8)
        self.assertEqual(oracle.call_count,decision["oracle_calls"])
        self.assertEqual(decision["oracle_calls"],
                         2*decision["candidates_tested"])
        self.assertLessEqual(decision["candidates_tested"],4)
        self.assertEqual(len(decision["trials"]),
                         decision["candidates_tested"])
        self.assertTrue(decision["all_candidate_stages_are_fully_repriced"])
        self.assertTrue(decision["no_vi_monotonicity_theorem_assumed"])

    def test_forced_fallback_explicitly_allows_uphill_not_fake_stability(self):
        net,flows,settings,obs=self.initial()
        with patch.object(R.R38,"numerical_merit",return_value=123.):
            changed,_,_,result=R.predictor_corrector_trial(
                net,flows,obs,settings,8)
        self.assertFalse(result["accepted_early"])
        self.assertTrue(result["fallback"])
        self.assertEqual(result["status"],
                         "BEST_EVALUATED_FALLBACK_UPHILL_ALLOWED")
        self.assertEqual(result["oracle_calls"],8)
        self.assertEqual(result["candidates_tested"],4)

    def test_invalid_method_and_fixture_and_budget_rejected(self):
        with self.assertRaises(ValueError):
            R.run_candidate(self.cfg,"original",1.,15)
        with self.assertRaises(ValueError):
            R.run_candidate(self.cfg,"invalid",1.,33)
        with self.assertRaises(ValueError):
            R.run_candidate(self.cfg,"original",.333,33)

    def test_candidate_never_spends_above_hard_oracle_budget(self):
        for budget in (16,17,18,25,33):
            with self.subTest(budget=budget):
                result=R.run_candidate(
                    self.cfg,"original",1.,budget,audit_every=2)
                self.assertLessEqual(result["actual_oracle_calls"],budget)
                self.assertEqual(
                    result["actual_oracle_calls"],
                    1+sum(x["charged_R14_calls"] for x in result["decisions"]))
                self.assertEqual(
                    result["predictor_R14_calls"],
                    result["corrector_R14_calls"])
                self.assertEqual(
                    result["predictor_R14_calls"]+
                    result["corrector_R14_calls"]+1,
                    result["actual_oracle_calls"])
                self.assertEqual(
                    result["financial_audits"][-1]["step"],
                    result["accepted_updates"])

    def test_solution_stability_not_confused_with_small_alpha_or_stalled(self):
        trial=R.run_candidate(self.cfg,"original",.20,33,audit_every=2)
        self.assertFalse(trial["global_convergence_proven"])
        self.assertFalse(trial["CSharp_parity_proven"])
        self.assertFalse(trial["production_approved"])
        self.assertFalse(trial["F059_policy_ratified"])
        for row in trial["rows"]:
            if row["five_original_stability_metrics_met"]:
                self.assertLessEqual(row["relative_gap"],R.TARGET)
                self.assertLessEqual(row["max_group_gap"],R.TARGET)
        self.assertEqual(trial["original_five_thresholds_observed"],
                         trial["first_three_joint_stable_step"] is not None)

    def test_original_R21_and_R26_controls_exactly_reused(self):
        net,flows,settings,obs=self.initial()
        for method in ("decay_sqrt","backtracking","progressive_global"):
            with self.subTest(method=method):
                trial=R.R38.run_method(
                    self.cfg,"original",1.,method,25,audit_every=2)
                self.assertLessEqual(trial["actual_oracle_calls"],25)
                self.assertEqual(trial["first_flow_sha256"],
                                 _flow_checksum(flows))
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"decay_sqrt",6))
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg,"backtracking",6))

    def test_all_comparison_methods_start_identical_physical_economy(self):
        data=[R.run_candidate(self.cfg,"original",1.,25)]
        data += [R.R38.run_method(
            self.cfg,"original",1.,method,25)
                 for method in R.METHODS if method!=R.CANDIDATE]
        self.assertEqual(len({r["first_flow_sha256"] for r in data}),1)
        self.assertEqual(len({r["fixture"]["actual_fixture_fingerprint"]
                              for r in data}),1)
        verdict=R.comparative_decision(data)
        self.assertEqual(verdict["fixture_count"],1)
        self.assertTrue(verdict["comparison_uses_same_oracle_ceiling_not_identical_actual_calls"])

    def test_comparison_has_binding_go_no_go(self):
        data=[R.run_candidate(self.cfg,"original",.20,25)]
        data += [R.R38.run_method(
            self.cfg,"original",.20,method,25)
                 for method in R.METHODS if method!=R.CANDIDATE]
        verdict=R.comparative_decision(data)
        self.assertIn(verdict["candidate_decision"],(
            "PROVISIONAL_GO_TO_MORE_VALIDATION",
            "NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET"))
        self.assertEqual(verdict["solver_families_evaluated"],2)
        self.assertEqual(
            verdict["next_if_no_go"],
            "R40_LAST_DISTINCT_SOLVER_FAMILY_THEN_ARCHITECTURE_DECISION")
        self.assertFalse(verdict["global_existence_convergence_uniqueness_proven"])

    def test_no_missing_or_extra_frozen_controls_allowed(self):
        data=[R.run_candidate(self.cfg,"original",1.,16)]
        with self.assertRaises(AssertionError):
            R.comparative_decision(data)
        data.append(data[0])
        with self.assertRaises(AssertionError):
            R.comparative_decision(data)

    def test_stressed_fixture_and_normal_have_same_controlled_laws(self):
        for regime,factor in (
            ("original",.20),("original",1.),
            ("heterogeneous_D_and_supply",.20),
        ):
            with self.subTest(regime=regime,factor=factor):
                result=R.run_candidate(
                    self.cfg,regime,factor,17,audit_every=2)
                self.assertFalse(result["production_approved"])
                self.assertEqual(
                    result["financial_audits"][-1]["gross_micro"],
                    result["financial_audits"][-1]["credits_micro"])


if __name__=="__main__":
    unittest.main()
