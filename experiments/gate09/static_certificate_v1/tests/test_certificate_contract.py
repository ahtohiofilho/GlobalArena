from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import verify_complementarity as V

from clearing_r5 import ClearingSettings,_evaluate,_flow_checksum
from gate09 import Edge,Group,Market,Column,Network
from oracle_cache_r14 import evaluate_r14


class StaticF072CertificateContracts(unittest.TestCase):
    def test_immutable_r20_manifest(self):
        self.assertEqual(V.R21.validate_manifest(),16)

    def test_exact_analytical_one_group_symmetric_equilibrium(self):
        net,flows=V.one_group_example(.5)
        c=V.static_certificate(net,flows)
        self.assertEqual(c["status"],"PASS_STATIC_F072_FIRST_ORDER_ONLY")
        self.assertEqual(c["gap_global_complementarity"],0.)
        self.assertEqual(c["gap_worst_group_complementarity"],0.)
        self.assertAlmostEqual(
            c["group_evidence"][0]["lambda_max_f072_score"],
            16*math.sqrt(2)/33,places=12)
        self.assertFalse(c["production_approved"])

    def test_asymmetric_one_group_is_not_falsely_passed(self):
        net,flows=V.one_group_example(.6)
        result=V.static_certificate(net,flows)
        self.assertEqual(result["status"],"FAIL_STATIC_F072_COMMERCIAL_REGRET")
        self.assertGreater(result["gap_global_complementarity"],.01)
        self.assertGreater(result["gap_worst_group_complementarity"],.01)
        self.assertFalse(result["static_complementarity_approximate"])

    def test_nonunique_equilibria_both_boundary_and_interiors(self):
        states=[]
        for a in (0.,.125,.25,.5,.75,.875,1.):
            with self.subTest(a=a):
                net,flows=V.two_group_example(a)
                r=V.static_certificate(net,flows)
                self.assertEqual(r["status"],"PASS_STATIC_F072_FIRST_ORDER_ONLY")
                self.assertAlmostEqual(r["gap_global_complementarity"],0.,delta=1e-14)
                self.assertAlmostEqual(r["gap_worst_group_complementarity"],0.,delta=1e-14)
                self.assertAlmostEqual(
                    r["group_evidence"][0]["lambda_max_f072_score"],
                    1/3,delta=1e-14)
                self.assertEqual(r["financial_unresolved_tie_group_count"],2)
                states.append(r["flow_sha256"])
        self.assertEqual(len(set(states)),7)

    def test_commercial_ties_do_not_ratify_financial_f059(self):
        net,flows=V.two_group_example(.25)
        r=V.static_certificate(net,flows)
        self.assertTrue(r["static_complementarity_approximate"])
        self.assertEqual(r["financial_F059_tie_groups"],[1,2])
        self.assertFalse(r["financial_F059_ratified"])
        self.assertEqual(
            r["F084F_conservation_witness"]["gross_micro"],
            r["F084F_conservation_witness"]["credits_micro"])

    def test_unpriced_reachable_market_blocks_with_no_fictitious_floor(self):
        net,flows=V.one_group_example(1.)
        with patch.object(V,"evaluate_r14",wraps=V.evaluate_r14) as oracle:
            r=V.static_certificate(net,flows)
            self.assertEqual(oracle.call_count,0)
        self.assertEqual(
            r["status"],"BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE")
        self.assertEqual(r["unpriced_reachable_markets"],[[2,0]])
        self.assertEqual(r["original_R14_oracle_calls"],0)
        self.assertEqual(r["F084F_financial_checks"],0)
        self.assertFalse(r["static_complementarity_approximate"])

    def test_structurally_unreachable_market_is_declared_not_artificially_quoted(self):
        net=Network(
            [Edge(1,0,1),Edge(2,0,2)],
            [Group(1,0,0,0,1.)],
            [Market(1,0,1.),Market(2,0,1.),Market(3,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        r=V.static_certificate(net,flows)
        self.assertEqual(r["structurally_unreachable_markets"],[[3,0]])
        self.assertEqual(r["unpriced_reachable_markets"],[])
        self.assertEqual(r["reachable_market_count"],2)
        self.assertEqual(r["status"],"PASS_STATIC_F072_FIRST_ORDER_ONLY")

    def test_original_F072_price_law_and_congestion_are_exact(self):
        net,flows=V.one_group_example(.5)
        loads,q,prices=net.snapshots(flows)
        self.assertEqual(q[(1,0)],.5)
        self.assertEqual(loads[1],.5)
        self.assertAlmostEqual(prices[(1,0)],math.sqrt(2),delta=1e-14)
        self.assertAlmostEqual(
            net.edges[1].congestion_cost(loads[1]),17/16,delta=1e-14)
        self.assertAlmostEqual(
            V.static_certificate(net,flows)["group_evidence"][0][
                "lambda_max_f072_score"],16*math.sqrt(2)/33,delta=1e-14)

    def test_funded_noncanonical_route_creates_real_commercial_regret(self):
        net=Network(
            [Edge(1,0,1,c_free=3.,capacity=100.),
             Edge(2,0,2,c_free=1.,capacity=100.),
             Edge(3,0,3,c_free=.5,capacity=100.),
             Edge(4,3,1,c_free=.5,capacity=100.)],
            [Group(1,0,0,0,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        result=V.static_certificate(net,flows)
        self.assertEqual(result["status"],"FAIL_STATIC_F072_COMMERCIAL_REGRET")
        self.assertGreater(result["gap_global_complementarity"],0)
        self.assertGreater(result["independent_shortest_path_scans"],0)
        self.assertTrue(result["all_compatible_markets_globally_separated"])

    def test_live_R13_vs_R14_independence_is_charged(self):
        net,flows=V.one_group_example(.5)
        with patch.object(V,"evaluate_r14",wraps=V.evaluate_r14) as oracle:
            with patch.object(V,"_evaluate",wraps=V._evaluate) as original:
                result=V.static_certificate(net,flows)
                self.assertEqual(oracle.call_count,1)
                self.assertEqual(original.call_count,1)
        self.assertEqual(result["independent_R13_checks"],1)
        self.assertEqual(result["original_R14_oracle_calls"],1)
        self.assertEqual(result["F084F_financial_checks"],1)

    def test_certificate_immutable_original_flow(self):
        net,flows=V.two_group_example(.25)
        digest=_flow_checksum(flows)
        before=dict(flows)
        V.static_certificate(net,flows)
        self.assertEqual(_flow_checksum(flows),digest)
        self.assertEqual(flows,before)

    def test_original_global_and_worst_gap_match_complementarity(self):
        for fraction in (.5,.6,.7):
            net,flows=V.one_group_example(fraction)
            r=V.static_certificate(net,flows)
            obs=evaluate_r14(net,flows,ClearingSettings(exponent=4),())
            self.assertAlmostEqual(
                r["gap_global_complementarity"],obs["relative_gap"],
                delta=1e-12)
            self.assertAlmostEqual(
                r["gap_worst_group_complementarity"],obs["max_group_gap"],
                delta=1e-12)

    def test_does_not_demand_three_artificial_updated_snapshots(self):
        net,flows=V.one_group_example(.5)
        result=V.static_certificate(net,flows)
        self.assertEqual(result["status"],"PASS_STATIC_F072_FIRST_ORDER_ONLY")
        self.assertTrue(result["no_fake_R24_streak_required"])
        self.assertFalse(result["original_R24_three_step_gate_changed"])
        self.assertFalse(result["dynamic_convergence_proven"])
        self.assertFalse(result["solver_implemented"])

    def test_invalid_tolerance_rejected(self):
        net,flows=V.one_group_example(.5)
        for tol in (-.1,1.,math.inf,math.nan):
            with self.subTest(tolerance=tol):
                with self.assertRaises(ValueError):
                    V.static_certificate(net,flows,tolerance=tol)

    def test_infeasible_supply_and_negative_flow_rejected(self):
        net,flows=V.one_group_example(.5)
        illegal=dict(flows)
        illegal[Column(1,1,(1,))]=.4
        with self.assertRaises(ValueError):
            V.static_certificate(net,illegal)
        illegal=dict(flows)
        illegal[Column(1,1,(1,))]=-.5
        with self.assertRaises(ValueError):
            V.static_certificate(net,illegal)

    def test_invalid_path_rejected_by_original_graph_not_ignored(self):
        net,_=V.one_group_example(.5)
        bad={Column(1,1,(2,)):.5,Column(1,2,(2,)):.5}
        with self.assertRaises(ValueError):
            V.static_certificate(net,bad)

    def test_R34_original_synthetic_fixture_audited_without_solving(self):
        net,flows,meta=V.R34.capacity_fixture(
            V.R21.CASES["small"],"original",1.)
        original=_flow_checksum(flows)
        observed=V.static_certificate(net,flows)
        self.assertEqual(observed["flow_sha256"],original)
        self.assertEqual(_flow_checksum(flows),original)
        self.assertGreaterEqual(observed["gap_global_complementarity"],0)
        self.assertEqual(observed["independent_R13_checks"],1)
        self.assertEqual(observed["F084F_financial_checks"],1)
        self.assertFalse(observed["production_approved"])

    def test_R34_heterogeneous_congested_fixture_without_model_changes(self):
        net,flows,meta=V.R34.capacity_fixture(
            V.R21.CASES["small"],"heterogeneous_D_and_supply",.2)
        result=V.static_certificate(net,flows)
        self.assertEqual(result["status"] in (
            "PASS_STATIC_F072_FIRST_ORDER_ONLY",
            "FAIL_STATIC_F072_COMMERCIAL_REGRET"),True)
        self.assertEqual(result["original_R14_oracle_calls"],1)
        self.assertTrue(result["all_compatible_markets_globally_separated"])
        self.assertEqual(
            result["F084F_conservation_witness"]["gross_micro"],
            result["F084F_conservation_witness"]["credits_micro"])

    def test_adr042_runner_examples_preserve_witness_decision(self):
        result=V.exercise()
        self.assertEqual(result["decision"],V.APPROVAL)
        self.assertEqual(len(result["analytical_cases"]),8)
        self.assertEqual(
            result["analytical_cases"][-1]["certificate"]["status"],
            "BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE")
        self.assertTrue(result["static_certificate_not_numerical_solver"])
        self.assertFalse(result["F059_ratified"])

    def test_original_R21_numerical_gate_kept_unmodified(self):
        self.assertTrue(V.R34.R24.check_r21_reference(
            V.R21.CASES["small"],"decay_sqrt",6))
        self.assertFalse(V.R34.R24.numerical_pass(
            {"relative_gap":1.,"max_group_gap":1.},None))


if __name__=="__main__":
    unittest.main()
