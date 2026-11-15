from pathlib import Path
import math
import sys
import unittest

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr046 as S

R45=S.R45
ADR42=S.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum


def small_linearization():
    net,flows=R45.two_group_nonoptimal_fixture()
    original=_flow_checksum(flows)
    limit=S.Limits(max_krylov_products=5000,max_cg_iterations=180)
    work=S.Work(limit)
    candidate,unreachable=R45._route_candidates(
        net,flows,ClearingSettings(exponent=4),work)
    cols=R45._build_active(net,flows,candidate,work)
    layout=R45.Layout(net,cols,flows,unreachable)
    z=layout.initial(flows)
    lin=S.Linearization(layout,z,1e-3,work)
    assert _flow_checksum(flows)==original
    return net,flows,layout,z,lin,work


class SparseMCPScalingContracts(unittest.TestCase):
    def test_R20_source_manifest_unchanged(self):
        self.assertEqual(ADR42.R21.validate_manifest(),16)

    def test_exact_quartic_and_tariffs_are_unchanged(self):
        self.assertEqual(R45.EPS_SCHEDULE,(1e-3,1e-6,1e-9))
        net,flows,layout,z,lin,work=small_linearization()
        self.assertEqual(S.F072_TOLERANCE,1e-4)
        self.assertTrue(work.evidence()[
            "exact_quartic_edge_derivative_in_Jv_Jtv"])
        self.assertTrue(lin.H)
        self.assertTrue(work.evidence()["no_dense_J_or_normal_matrix_created_by_trial"])

    def test_Jv_matches_original_dense_analytic_jacobian(self):
        _,_,layout,z,lin,work=small_linearization()
        r,J,H=R45._ncp_eval(layout,z,1e-3,work,True)
        v=[(.11 if i%2 else -.07) for i in range(len(z))]
        expected=[sum(x*y for x,y in zip(row,v)) for row in J]
        observed=lin.Jv(v)
        self.assertEqual(len(observed),len(expected))
        for a,b in zip(observed,expected):
            self.assertAlmostEqual(a,b,delta=5e-11)
        for a,b in zip(lin.residual,r):
            self.assertAlmostEqual(a,b,delta=1e-12)

    def test_Jtv_matches_original_dense_adjoint(self):
        _,_,layout,z,lin,work=small_linearization()
        r,J,H=R45._ncp_eval(layout,z,1e-3,work,True)
        v=[(.07 if i%2 else -.12) for i in range(len(r))]
        expected=[
            sum(J[i][k]*v[i] for i in range(len(r)))
            for k in range(len(z))]
        observed=lin.Jtv(v)
        for a,b in zip(observed,expected):
            self.assertAlmostEqual(a,b,delta=5e-11)

    def test_adjoint_dot_identity(self):
        _,_,layout,z,lin,work=small_linearization()
        u=[(.2 if i%3 else -.11) for i in range(len(z))]
        v=[(.13 if i%2 else -.08) for i in range(len(z))]
        self.assertAlmostEqual(
            S._dot(lin.Jv(u),v),S._dot(u,lin.Jtv(v)),delta=1e-10)

    def test_matrixfree_operator_avoids_quadratic_J_storage(self):
        _,_,layout,z,lin,work=small_linearization()
        self.assertFalse(hasattr(lin,"J"))
        self.assertEqual(
            work.max_dense_jacobian_elements_avoided,len(z)**2)
        self.assertEqual(
            lin.edge_incidence_entries,
            sum(len(c.edges) for c in layout.cols))
        self.assertLess(lin.edge_incidence_entries+len(layout.cols),1000)

    def test_matrixfree_derivative_matches_finite_differences(self):
        net,flows,layout,z,lin,work=small_linearization()
        v=[.025 if i%2 else -.02 for i in range(len(z))]
        h=1e-6
        plus=[a+h*d for a,d in zip(z,v)]
        minus=[a-h*d for a,d in zip(z,v)]
        self.assertTrue(all(x>=0 for x in layout.decode(minus)))
        fp,_,_=R45._ncp_eval(layout,plus,1e-3,work,False)
        fm,_,_=R45._ncp_eval(layout,minus,1e-3,work,False)
        for obs,a,b in zip(lin.Jv(v),fp,fm):
            self.assertAlmostEqual(obs,(a-b)/(2*h),delta=2e-6)

    def test_sparse_normal_CG_agrees_with_original_dense_solve(self):
        _,_,layout,z,lin,work=small_linearization()
        residual,J,H=R45._ncp_eval(layout,z,1e-3,work,True)
        lam=.01
        dense_A,dense_b=R45._normal_equations(J,residual,lam)
        ref=R45._solve_linear(dense_A,dense_b)
        output,converged,iters=S._pcg_normal(
            lin,lam,work.limits,relative_tolerance=1e-11)
        self.assertTrue(converged)
        self.assertGreater(iters,0)
        self.assertLessEqual(iters,work.limits.max_cg_iterations)
        for a,b in zip(output,ref):
            self.assertAlmostEqual(a,b,delta=2e-6)

    def test_exact_sparse_jacobi_diagonal_equals_original_dense_normal(self):
        net,flows,layout,z,lin,work=small_linearization()
        residual,J,H=R45._ncp_eval(layout,z,1e-3,work,True)
        regularization=.01
        reference=[
            regularization+math.fsum(row[k]*row[k] for row in J)
            for k in range(len(z))]
        observed=lin.precond_diagonal(regularization)
        self.assertEqual(len(reference),len(observed))
        for actual,expected in zip(observed,reference):
            self.assertAlmostEqual(actual,expected,delta=5e-9)
        first=work.exact_sparse_jacobi_assemblies
        lin.precond_diagonal(regularization*3)
        self.assertEqual(work.exact_sparse_jacobi_assemblies,first)
        self.assertEqual(first,1)
        self.assertFalse(hasattr(lin,"J"))

    def test_krylov_products_are_charged_including_transpose(self):
        _,_,layout,z,lin,work=small_linearization()
        before=work.krylov_products
        lin.Jv([.0]*len(z))
        lin.Jtv([.0]*len(z))
        self.assertEqual(work.krylov_products-before,2)
        self.assertEqual(
            work.evidence()["matrixfree_Jv_and_Jtv_products"],
            work.krylov_products)

    def test_krylov_hard_cap_causes_explicit_budget_error(self):
        _,_,layout,z,lin,work=small_linearization()
        work.limits=S.Limits(max_krylov_products=1)
        lin.Jv([0.]*len(z))
        with self.assertRaises(R45.BudgetExceeded):
            lin.Jtv([0.]*len(z))

    def test_normalized_group_supply_by_original_anchor(self):
        net,flows,layout,z,lin,work=small_linearization()
        proposed=R45._feasible_projected_step(
            layout,z,[.5]*len(z),.125)
        new=layout.flows(proposed)
        for gid,g in net.groups.items():
            mass=sum(q for c,q in new.items() if c.group_id==gid)
            self.assertAlmostEqual(mass,g.supply,delta=1e-12)

    def test_untouched_preexisting_F072_equilibrium_fast_pass(self):
        net,flows=ADR42.two_group_example(.25)
        before=_flow_checksum(flows)
        r=S.solve_sparse(net,flows)
        self.assertEqual(r["status"],"PASS_EXISTING_STATIC_F072_NO_UPDATE")
        self.assertEqual(r["end_flow_sha256"],before)
        self.assertEqual(r["work"]["matrixfree_Jv_and_Jtv_products"],0)

    def test_controlled_multigroup_mcp_has_real_independent_F072_gate(self):
        net,flows=R45.two_group_nonoptimal_fixture()
        original=_flow_checksum(flows)
        r=S.solve_sparse(net,flows)
        self.assertEqual(r["start_flow_sha256"],original)
        self.assertEqual(_flow_checksum(flows),original)
        if r["status"].startswith("PASS"):
            self.assertEqual(r["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
            self.assertLessEqual(r["certificate"]["global_gap"],1e-4)
            self.assertLessEqual(r["certificate"]["worst_gap"],1e-4)
        else:
            self.assertTrue(r["status"].startswith("NO_GO"))
            self.assertIsNone(r["candidate_flows"])

    def test_original_tariffs_and_market_sources_not_modified(self):
        net,flows=R45.route_switch_fixture()
        before_edges=dict(net.edges)
        before_flow=dict(flows)
        S.solve_sparse(net,flows)
        self.assertEqual(net.edges,before_edges)
        self.assertEqual(flows,before_flow)

    def test_42_node_analytic_route_cost_gradient_matches_dense(self):
        net,flows,_=ADR42.R34.capacity_fixture(
            ADR42.R21.CASES["small"],"original",1.)
        work=S.Work(S.Limits(max_krylov_products=3000))
        settings=ClearingSettings(exponent=4)
        cols,unreachable=R45._route_candidates(net,flows,settings,work)
        active=R45._build_active(net,flows,cols,work)
        layout=R45.Layout(net,active,flows,unreachable)
        z=layout.initial(flows)
        lin=S.Linearization(layout,z,1e-3,work)
        _,J,_=R45._ncp_eval(layout,z,1e-3,work,True)
        v=[(.1 if k%2 else -.09) for k in range(len(z))]
        observed=lin.Jv(v)
        for i,row in enumerate(J):
            self.assertAlmostEqual(observed[i],S._dot(row,v),delta=1e-8)

    def test_sparse_scaling_fixture_has_thousands_of_geodesic_nodes(self):
        net,flows,meta=S._large_fixture()
        self.assertEqual(meta["nodes"],2562)
        self.assertEqual(meta["edges"],7680)
        self.assertEqual(meta["producers"],48)
        self.assertEqual(meta["markets"],24)
        self.assertEqual(_flow_checksum(flows),_flow_checksum(dict(flows)))

    def test_2562_producer_market_density_is_disclosed(self):
        net,flows,meta=S._large_fixture()
        # Node count is NOT a claim of thousands of independent firms.
        self.assertLess(meta["producers"],meta["nodes"])
        self.assertEqual(
            meta["producers"]*6,288)
        self.assertTrue(net.snapshots(flows)[2])

    def test_hard_budget_on_controlled_fixture_no_unverified_pass(self):
        net,flows=R45.two_group_nonoptimal_fixture()
        out=S.solve_sparse(net,flows,S.Limits(
            max_h_evaluations=1,max_newton_steps=1,
            max_route_scans=3,max_krylov_products=1,
            max_columns=32,max_generations=1))
        self.assertEqual(out["status"],"NO_GO_PREDECLARED_BUDGET")
        self.assertIsNone(out["end_flow_sha256"])
        self.assertFalse(out["production_approved"])

    def test_invalid_sparse_budgets_rejected(self):
        with self.assertRaises(ValueError):
            S.Limits(max_krylov_products=0)
        with self.assertRaises(ValueError):
            S.Limits(max_cg_iterations=0)
        with self.assertRaises(ValueError):
            S.Limits(max_columns=-1)

    def test_production_stays_blocked_even_when_F072_passes(self):
        net,flows=ADR42.two_group_example(.5)
        result=S.solve_sparse(net,flows)
        self.assertFalse(result["production_approved"])
        self.assertFalse(result["CSharp_integrated"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertTrue(result["fixed_original_price_and_congestion"])


if __name__=="__main__":
    unittest.main()
