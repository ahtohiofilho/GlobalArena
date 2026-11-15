from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import verify_inverse_vi as V
from clearing_r5 import ClearingSettings,_flow_checksum
from gate09 import Edge,Group,Market,Column,Network


class InverseVIFormulationReadOnlyContracts(unittest.TestCase):
    def test_R20_manifest_SHA16_unchanged(self):
        self.assertEqual(V.ADR42.R21.validate_manifest(),16)

    def test_exact_reciprocal_identity_symmetry(self):
        net,flows=V.ADR42.one_group_example(.5)
        record=V.inverse_vi_witness(net,flows)
        self.assertAlmostEqual(
            record["group_evidence"][0]["min_inverse_score_cost"],
            33/(16*math.sqrt(2)),delta=1e-12)
        self.assertEqual(
            record["exact_identity_checks_price_times_inverse_cost"],1)
        self.assertEqual(record["VI_raw_residual_sum"],0.)
        self.assertTrue(record["VI_zero_residual_approximate"])

    def test_strictly_positive_canonical_score_at_funded_market(self):
        net,flows=V.ADR42.one_group_example(.5)
        q=net.snapshots(flows)[1]
        price=net.snapshots(flows)[2][(1,0)]
        cost=net.edges[1].congestion_cost(.5)
        s=price/(1+cost)
        h=(1+cost)*math.sqrt(q[(1,0)]/1)
        self.assertAlmostEqual(h*s,1.,delta=1e-14)

    def test_boundary_has_finite_inverse_vi_cost_but_original_price_is_infinite(self):
        net,flows=V.ADR42.one_group_example(1.)
        self.assertEqual(net.snapshots(flows)[2][(2,0)],math.inf)
        result=V.inverse_vi_witness(net,flows)
        self.assertEqual(result["zero_quantity_reachable_markets"],[[2,0]])
        self.assertEqual(result["group_evidence"][0]["min_inverse_score_cost"],0.)
        self.assertTrue(result["original_prices_undefined_at_zero_Q"])
        self.assertTrue(result["boundary_is_not_eligible_for_F072_certification"])
        self.assertFalse(result["original_R14_called"])
        self.assertEqual(
            result["original_static_certificate_status"],
            "BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE")
        self.assertGreater(result["VI_raw_residual_sum"],0.)
        self.assertFalse(result["VI_zero_residual_approximate"])

    def test_boundary_no_artificial_R14_pricing(self):
        net,flows=V.ADR42.one_group_example(1.)
        with patch.object(V,"evaluate_r14",wraps=V.evaluate_r14) as oracle:
            outcome=V.inverse_vi_witness(net,flows)
            self.assertEqual(oracle.call_count,0)
        self.assertFalse(outcome["numeric_solver_implemented"])

    def test_asymmetric_flows_have_positive_cost_vi_residual(self):
        net,flows=V.ADR42.one_group_example(.6)
        result=V.inverse_vi_witness(net,flows)
        self.assertGreater(result["VI_raw_residual_sum"],0)
        self.assertFalse(result["VI_zero_residual_approximate"])
        self.assertEqual(
            result["original_static_certificate_status"],
            "FAIL_STATIC_F072_COMMERCIAL_REGRET")
        self.assertGreater(result["original_F072_relative_gap"],0)

    def test_multivalued_physical_equilibria_are_accepted(self):
        unique=set()
        for a in (0.,.25,.5,.75,1.):
            with self.subTest(a=a):
                net,flows=V.ADR42.two_group_example(a)
                outcome=V.inverse_vi_witness(net,flows)
                self.assertEqual(outcome["VI_raw_residual_sum"],0.)
                self.assertTrue(outcome["VI_zero_residual_approximate"])
                self.assertEqual(outcome["original_F072_relative_gap"],0.)
                self.assertTrue(outcome["original_static_approximate_pass"])
                self.assertEqual(
                    outcome["exact_identity_checks_price_times_inverse_cost"],2)
                unique.add(outcome["flow_sha256"])
        self.assertEqual(len(unique),5)

    def test_inverse_witness_never_ratifies_financial_ties(self):
        net,flows=V.ADR42.two_group_example(.5)
        w=V.inverse_vi_witness(net,flows)
        self.assertFalse(w["financial_F059_ratified"])
        self.assertFalse(w["CSharp_integrated"])
        self.assertFalse(w["production_approved"])
        self.assertFalse(w["mathematical_existence_proven_by_test"])

    def test_structurally_unreachable_market_not_required_to_have_q(self):
        net=Network(
            [Edge(1,0,1),Edge(2,0,2)],
            [Group(1,0,0,0,1.)],
            [Market(1,0,1.),Market(2,0,1.),Market(3,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        w=V.inverse_vi_witness(net,flows)
        self.assertEqual(w["eligible_reachable_market_count"],2)
        self.assertEqual(w["unreachable_markets"],[[3,0]])
        self.assertEqual(w["zero_quantity_reachable_markets"],[])
        self.assertTrue(w["VI_zero_residual_approximate"])

    def test_noncanonical_funded_route_still_enters_VI_regret(self):
        net=Network(
            [Edge(1,0,1,c_free=3.,capacity=100.),
             Edge(2,0,2,c_free=1.,capacity=100.),
             Edge(3,0,3,c_free=.5,capacity=100.),
             Edge(4,3,1,c_free=.5,capacity=100.)],
            [Group(1,0,0,0,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={Column(1,1,(1,)):.5,Column(1,2,(2,)):.5}
        w=V.inverse_vi_witness(net,flows)
        self.assertGreater(w["VI_raw_residual_sum"],0.)
        self.assertGreater(w["independent_route_scans"],0)
        self.assertEqual(w["group_evidence"][0]["separated_market_count"],2)

    def test_seller_specific_tariff_respected_in_reciprocal_cost(self):
        # One company owns the charged edge, the other pays its tariff.
        net=Network(
            [Edge(1,0,1,c_free=1.,capacity=10.,
                  tariff_micro=250000,owners=((0,1_000_000),)),
             Edge(2,0,2,c_free=1.,capacity=10.)],
            [Group(1,0,0,0,1.),Group(2,0,0,1,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={
            Column(1,1,(1,)):.5,Column(1,2,(2,)):.5,
            Column(2,1,(1,)):.5,Column(2,2,(2,)):.5,
        }
        w=V.inverse_vi_witness(net,flows)
        self.assertEqual(w["eligible_reachable_market_count"],2)
        self.assertEqual(w["exact_identity_checks_price_times_inverse_cost"],2)
        self.assertGreater(w["VI_raw_residual_sum"],0.)
        self.assertTrue(w["original_R14_called"])

    def test_normal_small_fixture_replays_original_F072(self):
        net,flows,meta=V.ADR42.R34.capacity_fixture(
            V.ADR42.R21.CASES["small"],"original",1.)
        w=V.inverse_vi_witness(net,flows)
        self.assertEqual(w["zero_quantity_reachable_markets"],[])
        self.assertTrue(w["original_R14_called"])
        self.assertGreater(w["independent_route_scans"],0)
        self.assertEqual(
            w["exact_identity_checks_price_times_inverse_cost"],
            len(net.groups))

    def test_heterogeneous_stressed_small_fixture_replays_original(self):
        net,flows,meta=V.ADR42.R34.capacity_fixture(
            V.ADR42.R21.CASES["small"],"heterogeneous_D_and_supply",.2)
        w=V.inverse_vi_witness(net,flows)
        self.assertEqual(w["zero_quantity_reachable_markets"],[])
        self.assertEqual(
            w["exact_identity_checks_price_times_inverse_cost"],
            len(net.groups))
        self.assertIsNotNone(w["original_F072_relative_gap"])

    def test_medium_fixture_not_limited_to_analytical_toy(self):
        net,flows,meta=V.ADR42.R34.capacity_fixture(
            V.ADR42.R21.CASES["medium"],"original",1.)
        w=V.inverse_vi_witness(net,flows)
        self.assertEqual(w["zero_quantity_reachable_markets"],[])
        self.assertEqual(
            w["exact_identity_checks_price_times_inverse_cost"],
            len(net.groups))
        self.assertGreater(w["global_route_market_separations"],len(net.groups))

    def test_pure_input_no_flow_commits(self):
        net,flows=V.ADR42.one_group_example(.6)
        original=_flow_checksum(flows)
        before=dict(flows)
        V.inverse_vi_witness(net,flows)
        self.assertEqual(_flow_checksum(flows),original)
        self.assertEqual(flows,before)

    def test_reject_invalid_production_or_invalid_route(self):
        net,flows=V.ADR42.one_group_example(.5)
        f=dict(flows)
        f[Column(1,1,(1,))]=-.1
        with self.assertRaises(ValueError):
            V.inverse_vi_witness(net,f)
        f=dict(flows)
        f[Column(1,1,(1,))]=.2
        with self.assertRaises(ValueError):
            V.inverse_vi_witness(net,f)

    def test_contract_runner_distinguishes_analytical_theorem_and_empirical_tests(self):
        r=V.exercise()
        self.assertEqual(r["selection"],V.DECISION)
        self.assertEqual(len(r["analytical_read_only_witnesses"]),8)
        self.assertTrue(
            r["existence_is_mathematical_theorem_not_empirical_test"])
        self.assertTrue(r["no_fourth_solver_implemented"])
        self.assertEqual(
            r["analytical_read_only_witnesses"][2]["evidence"][
                "original_static_certificate_status"],
            "BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE")


if __name__=="__main__":
    unittest.main()
