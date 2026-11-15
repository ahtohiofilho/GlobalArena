from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr044 as R
from clearing_r5 import ClearingSettings,_flow_checksum
from gate09 import Column,Edge,Group,Market,Network


class ADR044MCPNCPContractTests(unittest.TestCase):
    def test_frozen_manifest_sha16(self):
        self.assertEqual(R.ADR42.R21.validate_manifest(),16)

    def test_fischer_burmeister_exact_complementarity(self):
        self.assertEqual(R._fb(0.,0.),0.)
        self.assertEqual(R._fb(1.,0.),0.)
        self.assertEqual(R._fb(0.,3.),0.)
        self.assertLess(R._fb(.5,.5),0.)
        self.assertGreater(R._fb(-1.,1.),0.)

    def test_one_group_analytical_equilibrium_passes_NCP_and_F072(self):
        net,flows=R.ADR42.one_group_example(.5)
        r=R.complementarity_contract(net,flows)
        self.assertEqual(r["status"],"INTERIOR_NCP_WITNESS")
        self.assertAlmostEqual(r["max_absolute_Fischer_Burmeister_residual"],0.,delta=1e-12)
        self.assertAlmostEqual(r["raw_VI_regret"],0.,delta=1e-12)
        self.assertTrue(r["static_commercial_pass"])
        self.assertEqual(r["economic_F072_certification"]["status"],
                         "PASS_STATIC_F072_FIRST_ORDER_ONLY")

    def test_asymmetric_snapshot_fails_both_contracts(self):
        net,flows=R.ADR42.one_group_example(.6)
        r=R.complementarity_contract(net,flows)
        self.assertGreater(r["max_absolute_Fischer_Burmeister_residual"],0)
        self.assertGreater(r["raw_VI_regret"],0)
        self.assertFalse(r["static_commercial_pass"])
        self.assertEqual(r["economic_F072_certification"]["status"],
                         "FAIL_STATIC_F072_COMMERCIAL_REGRET")

    def test_empty_reachable_market_is_NCP_vi_boundary_but_no_R14_cert(self):
        net,flows=R.ADR42.one_group_example(1.)
        with patch.object(R.ADR42,"static_certificate",
                          wraps=R.ADR42.static_certificate) as cert:
            r=R.complementarity_contract(net,flows)
        self.assertEqual(cert.call_count,0)
        self.assertEqual(r["status"],"BOUNDARY_Q_ZERO_NOT_F072_CERTIFIED")
        self.assertFalse(r["static_commercial_pass"])
        self.assertEqual(r["unpriced_reachable_markets"],[[2,0]])
        self.assertGreater(r["raw_VI_regret"],0.)
        self.assertIsNone(r["economic_F072_certification"])

    def test_nonuniqueness_family_all_statically_certified(self):
        hashes=set()
        for a in (0.,.25,.5,.75,1.):
            with self.subTest(a=a):
                net,flows=R.ADR42.two_group_example(a)
                r=R.complementarity_contract(net,flows)
                self.assertTrue(r["static_commercial_pass"])
                self.assertAlmostEqual(
                    r["max_absolute_Fischer_Burmeister_residual"],0.,delta=1e-12)
                hashes.add(r["flow_sha256"])
        self.assertEqual(len(hashes),5)

    def test_commercial_ties_do_not_ratify_financial_F059(self):
        net,flows=R.ADR42.two_group_example(.25)
        r=R.complementarity_contract(net,flows)
        self.assertTrue(r["static_commercial_pass"])
        self.assertFalse(r["financial_F059_ratified"])
        self.assertEqual(r["economic_F072_certification"]["financial_tie_groups"],[1,2])

    def test_missing_structurally_unreachable_market_does_not_block(self):
        net=Network([Edge(1,0,1),Edge(2,0,2)],[Group(1,0,0,0,1.)],
                    [Market(1,0,1.),Market(2,0,1.),Market(3,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        r=R.complementarity_contract(net,flows)
        self.assertEqual(r["reachable_market_count"],2)
        self.assertEqual(r["unpriced_reachable_markets"],[])
        self.assertTrue(r["static_commercial_pass"])

    def test_funded_noncanonical_path_does_not_disappear(self):
        net=Network(
            [Edge(1,0,1,c_free=3.,capacity=100.),
             Edge(2,0,2,c_free=1.,capacity=100.),
             Edge(3,0,3,c_free=.5,capacity=100.),
             Edge(4,3,1,c_free=.5,capacity=100.)],
            [Group(1,0,0,0,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        r=R.complementarity_contract(net,flows)
        self.assertFalse(r["static_commercial_pass"])
        self.assertGreater(r["groups"][0]["restricted_plus_global_columns_checked"],2)
        self.assertGreater(r["raw_VI_regret"],0)

    def test_tariff_differentiation_uses_frozen_original_weights(self):
        net=Network(
            [Edge(1,0,1,c_free=1.,capacity=10.,tariff_micro=250000,
                  owners=((0,1_000_000),)),
             Edge(2,0,2,c_free=1.,capacity=10.)],
            [Group(1,0,0,0,1.),Group(2,0,0,1,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5,
               Column(2,1,(1,)):.5,Column(2,2,(2,)):.5}
        r=R.complementarity_contract(net,flows)
        self.assertEqual(r["group_count"],2)
        self.assertGreater(r["raw_VI_regret"],0)
        self.assertFalse(r["CSharp_integrated"])

    def test_exact_budgeted_toy_root_recovers_from_point_six(self):
        net,flows=R.ADR42.one_group_example(.6)
        source=_flow_checksum(flows)
        r=R.bracketed_two_market_reference(net,flows)
        self.assertEqual(r["status"],"PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE")
        self.assertAlmostEqual(r["candidate_left_mass"],.5,delta=1e-11)
        self.assertAlmostEqual(r["candidate_right_mass"],.5,delta=1e-11)
        self.assertLessEqual(r["H_evaluations_used_including_endpoints"],64)
        self.assertEqual(r["F072_independent_certificate"]["status"],
                         "PASS_STATIC_F072_FIRST_ORDER_ONLY")
        self.assertEqual(_flow_checksum(flows),source)

    def test_bracket_recovery_from_unpriced_Q_zero_without_floor(self):
        net,flows=R.ADR42.one_group_example(1.)
        r=R.bracketed_two_market_reference(net,flows)
        self.assertTrue(r["input_initial_has_Q_zero"])
        self.assertEqual(r["status"],"PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE")
        self.assertTrue(r["no_numeric_price_floor"])
        self.assertEqual(r["F072_independent_certificate"]["R13_checks"],1)
        self.assertEqual(r["F072_independent_certificate"]["R14_calls"],1)
        self.assertEqual(r["F072_independent_certificate"]["F084F_checks"],1)

    def test_extremely_small_root_budget_yields_explicit_no_go_not_false_pass(self):
        # Choose unequal market demands: the root need not fall at .5.
        net,flows=R.ADR42.one_group_example(.6)
        net=Network(list(net.edges.values()),list(net.groups.values()),
                    [Market(1,0,1.),Market(2,0,2.)])
        r=R.bracketed_two_market_reference(
            net,flows,max_evaluations=4,root_cost_tolerance=1e-14)
        self.assertEqual(r["status"],"NO_GO_TOY_BUDGET_OR_PRECISION")
        self.assertLessEqual(r["H_evaluations_used_including_endpoints"],4)
        self.assertIsNone(r["F072_independent_certificate"])

    def test_bracket_rejects_invalid_shapes(self):
        net,flows=R.ADR42.two_group_example(.5)
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,flows)
        net,flows=R.ADR42.one_group_example(.6)
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,flows,max_evaluations=3)
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,flows,root_cost_tolerance=0.)
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,flows,root_cost_tolerance=1.)

    def test_bracket_rejects_network_that_has_an_extra_noncanonical_path(self):
        net,flows=R.ADR42.one_group_example(.6)
        net=Network(list(net.edges.values())+[Edge(3,1,2)],
                    list(net.groups.values()),list(net.markets.values()))
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,flows)

    def test_bracket_rejects_supply_violation_before_running(self):
        net,flows=R.ADR42.one_group_example(.6)
        corrupt=dict(flows)
        corrupt[Column(1,1,(1,))]=.7
        with self.assertRaises(ValueError):
            R.bracketed_two_market_reference(net,corrupt)

    def test_R34_original_snapshot_contract_without_solving(self):
        net,flows,meta=R.ADR42.R34.capacity_fixture(
            R.ADR42.R21.CASES["small"],"original",1.)
        r=R.complementarity_contract(net,flows)
        self.assertEqual(r["group_count"],len(net.groups))
        self.assertEqual(r["unpriced_reachable_markets"],[])
        self.assertEqual(r["economic_F072_certification"]["R13_checks"],1)
        self.assertFalse(r["no_flow_update"] is False)

    def test_R34_stress_snapshot_contract_without_solving(self):
        net,flows,meta=R.ADR42.R34.capacity_fixture(
            R.ADR42.R21.CASES["small"],"heterogeneous_D_and_supply",.2)
        r=R.complementarity_contract(net,flows)
        self.assertEqual(r["unpriced_reachable_markets"],[])
        self.assertTrue(r["groups"])
        self.assertGreater(r["independent_shortest_path_scans"],0)
        self.assertEqual(r["economic_F072_certification"]["F084F_financial_checks"],1)

    def test_original_R21_remains_unchanged_and_no_fourth_solver(self):
        self.assertTrue(R.ADR42.R34.R24.check_r21_reference(
            R.ADR42.R21.CASES["small"],"decay_sqrt",6))
        net,flows=R.ADR42.one_group_example(.5)
        witness=R.complementarity_contract(net,flows)
        self.assertFalse(witness["global_convergence_proven"])
        self.assertFalse(witness["production_approved"])

    def test_runner_evidence_is_finite_and_bounded(self):
        r=R.exercise()
        self.assertEqual(len(r["static_complementarity_cases"]),5)
        self.assertTrue(r["first_ncp_contract_not_general_solver"])
        self.assertEqual(r["next_gate"],"BOUNDED_GENERAL_MCP_VI_COLUMN_GENERATION_IMPL")
        self.assertEqual(
            r["toy_bracketed_root_from_unpriced_boundary"]["status"],
            "PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE")


if __name__=="__main__":
    unittest.main()
