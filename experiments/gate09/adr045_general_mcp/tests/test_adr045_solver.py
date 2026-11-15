from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr045 as R
from clearing_r5 import ClearingSettings,_flow_checksum
from gate09 import Edge,Group,Market,Column,Network


class ADR045ActualMultigroupMCPContracts(unittest.TestCase):
    def setUp(self):
        self.settings=ClearingSettings(exponent=4)

    def test_manifest_original_and_new_family_not_best_response(self):
        self.assertEqual(R.ADR42.R21.validate_manifest(),16)
        self.assertIn("smoothed_FB",R.METHOD)
        self.assertIn("analytic_Jacobian",R.METHOD)
        self.assertNotIn("best_response",R.METHOD)
        self.assertEqual(R.ROOT_INFINITY_NORM_GATE,1e-8)

    def test_immutable_simplex_decode_multiple_groups(self):
        net,flows=R.two_group_nonoptimal_fixture()
        work=R.Work(R.Limits())
        candidate,_=R._route_candidates(net,flows,self.settings,work)
        layout=R.Layout(net,R._build_active(net,flows,candidate,work),flows)
        z=layout.initial(flows)
        for gid,g in net.groups.items():
            actual=sum(v for c,v in layout.flows(z).items() if c.group_id==gid)
            self.assertAlmostEqual(actual,g.supply,delta=1e-14)
        self.assertEqual(len(z),len(layout.cols))
        self.assertEqual(_flow_checksum(flows),_flow_checksum(dict(flows)))

    def test_invalid_independent_mass_rejected_before_prices(self):
        net,flows=R.two_group_nonoptimal_fixture()
        work=R.Work(R.Limits())
        cols,_=R._route_candidates(net,flows,self.settings,work)
        layout=R.Layout(net,R._build_active(net,flows,cols,work),flows)
        z=layout.initial(flows)
        z[0]=-1
        with self.assertRaises(ValueError):
            layout.decode(z)

    def test_original_H_and_NCP_are_finite_in_two_groups(self):
        net,flows=R.two_group_nonoptimal_fixture()
        w=R.Work(R.Limits())
        cols,_=R._route_candidates(net,flows,self.settings,w)
        lay=R.Layout(net,R._build_active(net,flows,cols,w),flows)
        z=lay.initial(flows)
        residual,J,H=R._ncp_eval(lay,z,1e-3,w,True)
        self.assertEqual(len(residual),len(cols))
        self.assertEqual(len(J),len(cols))
        self.assertEqual(len(J[0]),len(z))
        self.assertTrue(all(math.isfinite(v) for v in residual+H))
        self.assertTrue(all(math.isfinite(v) for row in J for v in row))
        self.assertEqual(w.h_evaluations,1)

    def test_analytic_jacobian_matches_perturbed_physical_H(self):
        net,flows=R.two_group_nonoptimal_fixture()
        w=R.Work(R.Limits())
        cols,_=R._route_candidates(net,flows,self.settings,w)
        lay=R.Layout(net,R._build_active(net,flows,cols,w),flows)
        z=lay.initial(flows)
        _,J,_=R._ncp_eval(lay,z,1e-3,w,True)
        for k in range(len(z)):
            h=1e-6
            up=list(z);up[k]+=h
            down=list(z);down[k]-=h
            if k<len(lay.independent):
                # Original .8/.2 and .7/.3 ensure strictly feasible perturbations.
                self.assertTrue(all(x>=0 for x in lay.decode(down)))
            fp,_,_=R._ncp_eval(lay,up,1e-3,w,False)
            fm,_,_=R._ncp_eval(lay,down,1e-3,w,False)
            for i in range(len(J)):
                estimate=(fp[i]-fm[i])/(2*h)
                self.assertAlmostEqual(J[i][k],estimate,delta=2e-5)

    def test_dense_normal_equations_solve_exactly(self):
        J=[[3.,1.],[1.,2.]]
        r=[2.,-3.]
        A,b=R._normal_equations(J,r,1e-8)
        d=R._solve_linear(A,b)
        self.assertAlmostEqual(3*d[0]+d[1],-2,delta=1e-7)
        self.assertAlmostEqual(d[0]+2*d[1],3,delta=1e-7)

    def test_no_fictitious_supply_added_by_coverage_seed(self):
        net,flows=R.ADR42.one_group_example(1.)
        w=R.Work(R.Limits())
        candidates,_=R._route_candidates(net,flows,self.settings,w)
        cols=R._build_active(net,flows,candidates,w)
        seeded=R._cover_empty_markets(net,flows,cols,w)
        self.assertGreater(seeded[Column(1,2,(2,))],0)
        self.assertLess(seeded[Column(1,1,(1,))],1)
        self.assertAlmostEqual(sum(seeded.values()),1.,delta=1e-14)
        self.assertEqual(w.physical_seed_transfers,1)
        self.assertEqual(flows[Column(1,2,(2,))],0.)

    def test_global_route_generation_finds_original_cheaper_path(self):
        net,flows=R.route_switch_fixture()
        w=R.Work(R.Limits())
        cols,_=R._route_candidates(net,flows,self.settings,w)
        self.assertIn(Column(1,1,(3,4)),cols)
        self.assertIn(Column(2,1,(3,4)),cols)
        active=R._build_active(net,flows,cols,w)
        self.assertIn(Column(1,1,(1,)),active)
        self.assertIn(Column(1,1,(3,4)),active)
        self.assertGreater(w.column_additions,0)

    def test_existing_exact_multigroup_equilibrium_is_not_moved(self):
        net,flows=R.ADR42.two_group_example(.25)
        original=_flow_checksum(flows)
        r=R.solve_mcp(net,flows)
        self.assertEqual(r["status"],"PASS_EXISTING_STATIC_F072_NO_UPDATE")
        self.assertEqual(r["final_flow_sha256"],original)
        self.assertEqual(r["work"]["newton_accepted_steps"],0)
        self.assertEqual(r["certificate"]["global_F072_gap"],0.)

    def test_existing_F059_ties_do_not_block_commercial_certificate(self):
        net,flows=R.ADR42.two_group_example(.5)
        r=R.solve_mcp(net,flows)
        self.assertTrue(r["status"].startswith("PASS"))
        self.assertEqual(r["certificate"]["F059_tie_groups"],[1,2])
        self.assertFalse(r["financial_F059_ratified"])

    def test_new_multigroup_candidate_must_pass_independent_economy(self):
        net,flows=R.two_group_nonoptimal_fixture()
        digest=_flow_checksum(flows)
        r=R.solve_mcp(net,flows)
        self.assertEqual(_flow_checksum(flows),digest)
        self.assertFalse(r["CSharp_parity_proven"])
        self.assertFalse(r["production_approved"])
        if r["status"].startswith("PASS"):
            self.assertEqual(
                r["certificate"]["status"],
                "PASS_STATIC_F072_FIRST_ORDER_ONLY")
            self.assertLessEqual(r["certificate"]["global_F072_gap"],1e-4)
            self.assertLessEqual(r["certificate"]["worst_group_F072_gap"],1e-4)
            self.assertGreater(r["work"]["newton_accepted_steps"],0)
        else:
            self.assertTrue(r["status"].startswith("NO_GO"))
            self.assertIsNone(r["final_group_market_flows"])

    def test_three_destination_f072_must_be_rechecked(self):
        net,flows=R.three_market_fixture()
        r=R.solve_mcp(net,flows)
        self.assertEqual(r["input_unmodified"],True)
        if r["status"].startswith("PASS"):
            self.assertEqual(r["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
        else:
            self.assertTrue(r["status"].startswith("NO_GO"))

    def test_empty_market_initial_has_no_artificial_price(self):
        net,flows=R.ADR42.one_group_example(1.)
        digest=_flow_checksum(flows)
        r=R.solve_mcp(net,flows)
        self.assertEqual(r["input_unmodified"],True)
        self.assertEqual(_flow_checksum(flows),digest)
        self.assertGreater(r["work"]["Q_zero_initial_physical_seed_transfers"],0)
        self.assertTrue(r["status"].startswith(("PASS","NO_GO")))
        self.assertFalse(r["production_approved"])

    def test_newton_trials_are_billed_including_rejections(self):
        net,flows=R.two_group_nonoptimal_fixture()
        r=R.solve_mcp(net,flows)
        w=r["work"]
        self.assertLessEqual(w["H_evaluations_including_rejected_trials"],
                             w["limits"]["max_h_evaluations"])
        self.assertLessEqual(w["newton_accepted_steps"],
                             w["limits"]["max_newton_steps"])
        self.assertLessEqual(w["separate_shortest_path_scans"],
                             w["limits"]["max_route_scans"])
        self.assertGreater(w["analytic_jacobians_assembled"],0)
        self.assertGreater(w["H_evaluations_including_rejected_trials"],
                           w["newton_accepted_steps"])

    def test_hard_budget_rejects_without_committing_anything(self):
        net,flows=R.two_group_nonoptimal_fixture()
        original=_flow_checksum(flows)
        r=R.solve_mcp(net,flows,limits=R.Limits(
            max_h_evaluations=1,max_newton_steps=1,max_route_scans=3,
            max_original_certificates=1,max_columns=32,
            max_generations=1,max_backtrack=2))
        self.assertEqual(r["status"],"NO_GO_PREDECLARED_BUDGET")
        self.assertIsNone(r["final_flow_sha256"])
        self.assertEqual(_flow_checksum(flows),original)
        self.assertFalse(r["production_approved"])

    def test_invalid_limits_rejected(self):
        with self.assertRaises(ValueError):
            R.Limits(max_h_evaluations=0)
        with self.assertRaises(ValueError):
            R.Limits(max_route_scans=-2)
        with self.assertRaises(ValueError):
            R.Limits(max_columns=1.3)

    def test_real_tariffs_are_not_modified(self):
        net=Network(
            [Edge(1,0,1,c_free=1.,capacity=10.,tariff_micro=250000,
                  owners=((0,1_000_000),)),
             Edge(2,0,2,c_free=1.,capacity=10.)],
            [Group(1,0,0,0,1.),Group(2,0,0,1,1.)],
            [Market(1,0,1.),Market(2,0,1.)])
        flows={
            Column(1,1,(1,)):.9,Column(1,2,(2,)):.1,
            Column(2,1,(1,)):.8,Column(2,2,(2,)):.2,
        }
        edges=dict(net.edges)
        R.solve_mcp(net,flows)
        self.assertEqual(edges,net.edges)

    def test_42_node_normal_fixture_returns_explicit_result(self):
        net,flows,meta=R.ADR42.R34.capacity_fixture(
            R.ADR42.R21.CASES["small"],"original",1.)
        r=R.solve_mcp(net,flows,limits=R.Limits(
            max_h_evaluations=240,max_newton_steps=12,
            max_original_certificates=2,max_columns=192))
        self.assertTrue(r["status"].startswith(("PASS","NO_GO")))
        self.assertFalse(r["global_convergence_proven"])
        self.assertFalse(r["production_approved"])
        self.assertEqual(r["input_unmodified"],True)

    def test_scope_no_unapproved_CSharp_or_F059_go(self):
        net,flows=R.two_group_nonoptimal_fixture()
        out=R.solve_mcp(net,flows)
        self.assertFalse(out["financial_F059_ratified"])
        self.assertFalse(out["CSharp_parity_proven"])
        self.assertFalse(out["production_approved"])
        self.assertTrue(out["no_changed_economic_parameters"])

    def test_trivial_smoke_runner_exposes_real_go_no_go(self):
        out=R.exercise("smoke")
        self.assertEqual(len(out["fixtures"]),5)
        self.assertTrue(out["general_solver_implemented"])
        self.assertEqual(out["required_controlled_multigroup"],
                         ["toy_two_groups_equal_preexisting",
                          "toy_two_groups_two_markets"])
        self.assertFalse(out["production_approved"])


if __name__=="__main__":
    unittest.main()
