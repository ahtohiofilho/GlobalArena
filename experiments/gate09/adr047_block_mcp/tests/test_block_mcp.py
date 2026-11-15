from pathlib import Path
import math
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr047 as S
R46=S.R46
R45=S.R45
ADR42=S.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum
from gate09 import Column


def controlled_state():
    net,flows=R45.two_group_nonoptimal_fixture()
    work=S.Work(S.Limits(max_krylov_products=6000))
    candidates,unreachable=R45._route_candidates(
        net,flows,ClearingSettings(exponent=4),work)
    cols=R45._build_active(net,flows,candidates,work)
    lay=R45.Layout(net,cols,flows,unreachable)
    z=lay.initial(flows)
    lin=R46.Linearization(lay,z,1e-3,work)
    return net,flows,lay,z,lin,work


class ADR047BlockMCPTests(unittest.TestCase):
    def test_original_R20_manifest_sha16(self):
        self.assertEqual(ADR42.R21.validate_manifest(),16)

    def test_quartic_F072_tolerance_and_smoothing_unchanged(self):
        self.assertEqual(S.F072_TOLERANCE,1e-4)
        self.assertEqual(R45.EPS_SCHEDULE,(1e-3,1e-6,1e-9))
        self.assertEqual(S.SEED_TARGETS_PER_PRODUCER,2)
        self.assertFalse(R46.solve_sparse.__doc__ is None)

    def test_sparse_gradient_single_column_matches_dense_original(self):
        net,flows,lay,z,lin,work=controlled_state()
        _,J,_=R45._ncp_eval(lay,z,1e-3,work,True)
        market_rows={}
        edge_rows={}
        for i,m in enumerate(lin.market):
            market_rows.setdefault(m,[]).append(i)
            for e in lin.edges[i]:
                edge_rows.setdefault(e,[]).append(i)
        for k in range(len(z)):
            gradient=S._sparse_J_column(lin,k,market_rows,edge_rows)
            for i in range(len(J)):
                self.assertAlmostEqual(gradient.get(i,0.),J[i][k],
                                       delta=4e-11)

    def test_exact_blocks_match_dense_J_transpose_J_principal_blocks(self):
        net,flows,lay,z,lin,work=controlled_state()
        _,J,_=R45._ncp_eval(lay,z,1e-3,work,True)
        pre=S.ProducerBlocks(lin,.02)
        self.assertEqual(work.block_assemblies,1)
        self.assertEqual(len(pre.blocks),len(lay.groups))
        for coords,core in lin._producer_block_normal_core:
            for i,k in enumerate(coords):
                for j,l in enumerate(coords):
                    expected=math.fsum(row[k]*row[l] for row in J)
                    self.assertAlmostEqual(core[i][j],expected,
                                           delta=3e-10)

    def test_block_assembly_reused_across_damping_values(self):
        net,flows,lay,z,lin,work=controlled_state()
        S.ProducerBlocks(lin,.01)
        before=work.block_assemblies
        S.ProducerBlocks(lin,.3)
        self.assertEqual(work.block_assemblies,before)
        self.assertEqual(work.block_factorizations,2*len(lay.groups))

    def test_block_factorization_small_symmetric_positive_definite(self):
        a=[[4.,1.],[1.,3.]]
        L=S._small_cholesky(a,.1)
        x=S._small_cholesky_solve(L,[1.,2.])
        self.assertAlmostEqual(4*x[0]+x[1],1.,delta=1e-12)
        self.assertAlmostEqual(x[0]+3*x[1],2.,delta=1e-12)

    def test_block_preconditioner_matches_dense_block_solves(self):
        net,flows,lay,z,lin,work=controlled_state()
        lam=.003
        pre=S.ProducerBlocks(lin,lam)
        rhs=[(.3 if i%2 else -.19) for i in range(len(z))]
        observed=pre.apply(rhs)
        for coords,core in lin._producer_block_normal_core:
            A=[[v+(lam if i==j else 0)
                for j,v in enumerate(row)] for i,row in enumerate(core)]
            b=[rhs[k] for k in coords]
            expected=R45._solve_linear(A,b)
            for k,x in zip(coords,expected):
                self.assertAlmostEqual(observed[k],x,delta=5e-10)

    def test_block_CG_solves_exact_original_dense_normal_equations(self):
        net,flows,lay,z,lin,work=controlled_state()
        residual,J,_=R45._ncp_eval(lay,z,1e-3,work,True)
        lam=.1
        A,b=R45._normal_equations(J,residual,lam)
        reference=R45._solve_linear(A,b)
        candidate,converged,iters=S._pcg_block(
            lin,lam,work.limits,relative_tolerance=1e-11)
        self.assertTrue(converged)
        self.assertGreater(iters,0)
        for p,q in zip(candidate,reference):
            self.assertAlmostEqual(p,q,delta=2e-6)

    def test_all_krylov_products_and_block_coefficients_billed(self):
        net,flows,lay,z,lin,work=controlled_state()
        S._pcg_block(lin,.1,work.limits)
        self.assertGreater(work.krylov_products,0)
        self.assertGreater(work.block_coefficients,0)
        self.assertEqual(work.block_assemblies,1)
        self.assertEqual(work.producer_blocks,len(net.groups))
        self.assertLess(work.max_block_width,len(z))
        self.assertTrue(work.evidence()[
            "no_global_dense_matrix_or_normal_system"])

    def test_block_work_budget_fails_explicitly(self):
        net,flows,lay,z,lin,work=controlled_state()
        work.limits=S.Limits(max_block_coefficients=1)
        with self.assertRaises(R45.BudgetExceeded):
            S.ProducerBlocks(lin,.01)

    def test_global_funded_routes_never_dropped_from_initial_columns(self):
        net,flows=R45.route_switch_fixture()
        w=S.Work(S.Limits())
        all_cols,unreachable=R45._route_candidates(
            net,flows,ClearingSettings(exponent=4),w)
        selected=S._restricted_initial_columns(net,flows,all_cols,w)
        self.assertTrue(
            set(c for c,v in flows.items() if v>0).issubset(set(selected)))
        self.assertLessEqual(len(selected),len(all_cols)+len(flows))
        self.assertIn(Column(1,1,(1,)),selected)

    def test_restriction_selects_only_best_two_per_producer(self):
        net,flows,meta=R46._large_fixture()
        work=S.Work(S.Limits(max_columns=450,max_route_scans=900))
        all_cols,unreachable=R45._route_candidates(
            net,flows,ClearingSettings(exponent=4),work)
        restricted=S._restricted_initial_columns(net,flows,all_cols,work)
        self.assertEqual(len(all_cols),288)
        self.assertLess(len(restricted),len(all_cols))
        self.assertGreaterEqual(len(restricted),48)
        self.assertGreater(work.initial_candidates_elided,0)
        self.assertTrue(all(any(c.group_id==gid for c in restricted)
                            for gid in net.groups))

    def test_forced_global_path_is_accepted_even_if_not_best_two(self):
        net,flows,_=R46._large_fixture()
        work=S.Work(S.Limits(max_columns=450,max_route_scans=900))
        all_cols,unreachable=R45._route_candidates(
            net,flows,ClearingSettings(exponent=4),work)
        preliminary=S._restricted_initial_columns(net,flows,all_cols,work)
        omitted=next(c for c in all_cols if c not in preliminary)
        next_choice=S._restricted_initial_columns(
            net,flows,all_cols,work,forced=(omitted,))
        self.assertIn(omitted,next_choice)

    def test_existing_equilibrium_is_not_moved(self):
        net,flows=ADR42.two_group_example(.25)
        sha=_flow_checksum(flows)
        out=S.solve_block(net,flows)
        self.assertEqual(out["status"],"PASS_EXISTING_F072_NO_UPDATES")
        self.assertEqual(out["final_flow_sha256"],sha)
        self.assertEqual(out["work"]["newton_accepted_steps"],0)
        self.assertFalse(out["production_approved"])

    def test_multigroup_nonoptimal_is_truly_solved_or_no_go(self):
        net,flows=R45.two_group_nonoptimal_fixture()
        sha=_flow_checksum(flows)
        out=S.solve_block(net,flows)
        self.assertEqual(_flow_checksum(flows),sha)
        if out["status"].startswith("PASS"):
            self.assertEqual(out["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
            self.assertLessEqual(out["certificate"]["global_F072_gap"],1e-4)
            self.assertLessEqual(out["certificate"]["worst_group_F072_gap"],1e-4)
            self.assertGreater(out["work"]["newton_accepted_steps"],0)
        else:
            self.assertTrue(out["status"].startswith("NO_GO"))
            self.assertIsNone(out["final_flow_sha256"])

    def test_noncanonical_paid_routes_still_get_original_score_audit(self):
        net,flows=R45.route_switch_fixture()
        original=_flow_checksum(flows)
        out=S.solve_block(net,flows)
        self.assertEqual(_flow_checksum(flows),original)
        if out["status"].startswith("PASS"):
            self.assertEqual(out["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
        else:
            self.assertTrue(out["status"].startswith("NO_GO"))

    def test_initial_reachable_Q_zero_produces_no_fake_market_supply(self):
        net,flows=ADR42.one_group_example(1.)
        sha=_flow_checksum(flows)
        out=S.solve_block(net,flows)
        self.assertEqual(_flow_checksum(flows),sha)
        self.assertGreater(out["work"][
            "Q_zero_initial_physical_seed_transfers"],0)
        self.assertEqual(out["original_congestion_exponent"],4)
        if out["status"].startswith("PASS"):
            self.assertEqual(out["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
        else:
            self.assertTrue(out["status"].startswith("NO_GO"))

    def test_small_budget_never_returns_uncertified_allocation(self):
        net,flows=R45.two_group_nonoptimal_fixture()
        out=S.solve_block(net,flows,limits=S.Limits(
            max_h_evaluations=1,max_route_scans=3,
            max_original_certificates=2,max_columns=25,
            max_block_coefficients=10,max_krylov_products=1,
            max_newton_steps=1,max_generations=1))
        self.assertEqual(out["status"],"NO_GO_PREDECLARED_WORK_BUDGET")
        self.assertIsNone(out["final_flow_sha256"])
        self.assertFalse(out["production_approved"])

    def test_source_F059_not_ratified_even_when_F072_passes(self):
        net,flows=ADR42.two_group_example(.5)
        out=S.solve_block(net,flows)
        self.assertFalse(out["financial_F059_unratified"] is False)
        self.assertEqual(out["certificate"]["F059_ties"],[1,2])
        self.assertFalse(out["CSharp_Godot_untouched"] is False)

    def test_limit_validation_includes_block_budget(self):
        with self.assertRaises(ValueError):
            S.Limits(max_block_coefficients=0)
        with self.assertRaises(ValueError):
            S.Limits(max_cg_iterations=-4)

    def test_sparse_memory_not_square_in_number_of_total_columns(self):
        net,flows,_=R46._large_fixture()
        w=S.Work(S.Limits(max_columns=450,max_route_scans=900))
        all_cols,unreachable=R45._route_candidates(
            net,flows,ClearingSettings(exponent=4),w)
        selected=S._restricted_initial_columns(net,flows,all_cols,w)
        columns=R45._build_active(net,flows,selected,w)
        lay=R45.Layout(net,columns,flows,unreachable)
        z=lay.initial(flows)
        lin=R46.Linearization(lay,z,1e-3,w)
        pre=S.ProducerBlocks(lin,.1)
        self.assertLess(w.block_coefficients,len(columns)**2)
        self.assertEqual(w.producer_blocks,len(net.groups))
        self.assertFalse(hasattr(lin,"J"))

    def test_smoke_reports_real_multigroup_result_and_economy_unchanged(self):
        outcome=S.exercise("smoke")
        self.assertEqual(len(outcome["cases"]),2)
        self.assertTrue(outcome["no_economic_law_change"])
        self.assertFalse(outcome["CSharp_production_approved"])
        self.assertEqual(outcome["R20_immutable_file_manifest_count"],16)


if __name__=="__main__":
    unittest.main()
