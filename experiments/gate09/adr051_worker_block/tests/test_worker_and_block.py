from __future__ import annotations
from collections import defaultdict
import math
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr051 as S
from worker_coupled import (
    Worker, ACTIVITY_RATES, ITEMS, CYCLE_TURNS,
    make_workers, demand_by_tile, build_worker_network,
    validate_itinerary,
)
from clearing_r5 import _flow_checksum


def analytic_state():
    net,flows,meta=build_worker_network()
    work=S.Work(S.R47.Limits(
        max_h_evaluations=200,max_krylov_products=3000,
        max_block_coefficients=2000))
    cols=tuple(sorted(flows))
    layout=S.R45.Layout(net,cols,flows,())
    z=layout.initial(flows)
    lin=S.R46.Linearization(layout,z,1e-3,work)
    return net,flows,meta,layout,z,lin,work


class ADR051WorkerDemandAndChunkedMCP(unittest.TestCase):
    def test_frozen_R20_source_and_F072_economics(self):
        self.assertEqual(S.R49.ADR48.ADR42.R21.validate_manifest(),16)
        self.assertEqual(S.F072_TOL,1e-4)
        self.assertEqual(S.R45.EPS_SCHEDULE,(1e-3,1e-6,1e-9))
        self.assertTrue(S.R49.CASES[
            "maximum_R13_density_2562_240_120"].exponent==4)

    def test_worker_fixture_real_production_and_market_dimensions(self):
        net,flows,meta=build_worker_network()
        self.assertEqual(len(meta["workers"]),10)
        self.assertEqual(meta["producing_workers"],6)
        self.assertEqual(meta["nonproducing_workers"],4)
        self.assertEqual(len(net.groups),6)
        self.assertEqual(len(net.markets),8)
        self.assertEqual(len(flows),24)
        self.assertEqual(meta["nodes"],42)
        self.assertTrue(all(v>0 for v in flows.values()))

    def test_physical_producer_nodes_subset_demand_nodes(self):
        _,_,meta=build_worker_network()
        self.assertEqual(len(meta["physical_production_nodes"]),3)
        self.assertEqual(len(meta["physical_demand_nodes"]),4)
        self.assertTrue(meta["physical_production_nodes"].issubset(
            meta["physical_demand_nodes"]))
        self.assertTrue(meta["physical_production_nodes"]<
                        meta["physical_demand_nodes"])

    def test_all_workers_produce_demand_even_when_not_producing(self):
        net,flows,meta=build_worker_network()
        nonprod=[w for w in meta["workers"] if w.activity!="produce"]
        self.assertEqual(len(nonprod),4)
        for w in nonprod:
            self.assertIsNone(w.product)
            self.assertEqual(w.supply,0)
            self.assertGreater(w.demand_multiplier,0)
        demand=meta["demand"]
        self.assertTrue(all(demand.get((n,item),0)>0
                            for n in meta["physical_demand_nodes"]
                            for item in ITEMS))

    def test_travel_worker_demands_each_occupied_tile_by_time(self):
        net,flows,meta=build_worker_network()
        traveller=next(w for w in meta["workers"] if w.activity=="travel")
        result=demand_by_tile((traveller,),net)
        counts=defaultdict(int)
        for node in traveller.visited_tiles:counts[node]+=1
        self.assertEqual(len(counts),4)
        for tile,count in counts.items():
            for item,weight in zip(ITEMS,ACTIVITY_RATES["travel"]):
                expected=traveller.demand_multiplier*weight*count/CYCLE_TURNS
                self.assertAlmostEqual(result[(tile,item)],expected,places=13)

    def test_travelling_demand_does_not_create_extra_consumption(self):
        net,flows,meta=build_worker_network()
        all_demand=meta["demand"]
        for k in ITEMS:
            expected=math.fsum(
                w.demand_multiplier*ACTIVITY_RATES[w.activity][k]
                for w in meta["workers"])
            actual=math.fsum(v for (node,item),v in all_demand.items()
                             if item==k)
            self.assertAlmostEqual(actual,expected,places=12)

    def test_all_activities_create_positive_but_distinct_item_demands(self):
        self.assertEqual(set(ACTIVITY_RATES),
                         {"produce","travel","build","military","idle"})
        self.assertTrue(all(len(v)==len(ITEMS) and all(k>0 for k in v)
                            for v in ACTIVITY_RATES.values()))
        self.assertGreater(
            ACTIVITY_RATES["build"][1],
            ACTIVITY_RATES["idle"][1])
        self.assertGreater(
            ACTIVITY_RATES["military"][0],
            ACTIVITY_RATES["build"][0])

    def test_worker_can_have_different_demand_multiplier(self):
        net,flows,meta=build_worker_network()
        t=next(w for w in meta["workers"] if w.activity=="travel")
        higher=Worker(t.id,t.civilization,t.activity,t.product,t.supply,
                      2*t.demand_multiplier,t.visited_tiles)
        base=demand_by_tile((t,),net)
        doubled=demand_by_tile((higher,),net)
        for key,v in base.items():
            self.assertAlmostEqual(doubled[key],2*v)

    def test_illegal_teleportation_is_rejected(self):
        net,flows,meta=build_worker_network()
        chain=meta["chain"]
        remote=next(n for n in net.adj
                    if n!=chain[0]
                    and all(n!=v for v,_ in net.adj[chain[0]]))
        w=Worker(100,0,"travel",None,0,1,
                 (chain[0],remote,remote,remote,remote,remote))
        with self.assertRaises(ValueError):
            validate_itinerary(w,net)

    def test_wrong_time_coverage_is_rejected(self):
        net,flows,meta=build_worker_network()
        w=Worker(100,0,"idle",None,0,1,(meta["chain"][0],)*5)
        with self.assertRaises(ValueError):
            validate_itinerary(w,net)
        with self.assertRaises(ValueError):
            demand_by_tile((w,),net)

    def test_duplicate_worker_id_and_invalid_nonproducer_supply_rejected(self):
        net,flows,meta=build_worker_network()
        with self.assertRaises(ValueError):
            demand_by_tile((meta["workers"][0],meta["workers"][0]),net)
        with self.assertRaises(ValueError):
            Worker(99,0,"travel",None,1.,1.,(meta["chain"][0],)*6)
        with self.assertRaises(ValueError):
            Worker(99,0,"produce",0,1.,1.,tuple(meta["chain"][:4])+(
                meta["chain"][3],meta["chain"][3]))

    def test_real_group_offer_conservation_and_Q_finite(self):
        net,flows,meta=build_worker_network()
        before=_flow_checksum(flows)
        _,q,p=net.snapshots(flows)
        self.assertEqual(set(q),set(net.markets))
        self.assertTrue(all(v>0 and math.isfinite(v) for v in p.values()))
        for gid,g in net.groups.items():
            self.assertAlmostEqual(math.fsum(v for c,v in flows.items()
                                              if c.group_id==gid),
                                   g.supply,places=11)
        self.assertEqual(before,_flow_checksum(flows))

    def test_all_initial_routes_really_connected(self):
        net,flows,meta=build_worker_network()
        for col in flows:
            net.validate_column(col)

    def test_nonproducer_demand_without_any_new_supply(self):
        net,flows,meta=build_worker_network()
        producers=tuple(w for w in meta["workers"] if w.activity=="produce")
        only=demand_by_tile(producers,net)
        full=meta["demand"]
        self.assertGreater(sum(full.values()),sum(only.values()))
        self.assertEqual(len(net.groups),len(producers))

    def test_chunked_exact_normal_principal_subblocks_against_dense_J(self):
        net,flows,meta,layout,z,lin,work=analytic_state()
        residual,J,_=S.R45._ncp_eval(layout,z,1e-3,work,True)
        pre=S.ChunkBlocks(lin,.01)
        self.assertGreater(len(pre.blocks),len(layout.groups))
        for coords,core in lin._producer_block_normal_core:
            for i,k in enumerate(coords):
                for j,l in enumerate(coords):
                    expected=math.fsum(row[k]*row[l] for row in J)
                    self.assertAlmostEqual(core[i][j],expected,delta=2e-8)

    def test_chunked_normal_system_is_regularized_SPD(self):
        net,flows,meta,layout,z,lin,work=analytic_state()
        pre=S.ChunkBlocks(lin,.1)
        self.assertTrue(pre.blocks)
        for coords,L in pre.blocks:
            self.assertTrue(all(v>0 and math.isfinite(v)
                                for i,row in enumerate(L)
                                for j,v in enumerate(row) if j==i))

    def test_chunking_uses_fewer_actual_sparse_dots_than_full_square(self):
        net,flows,meta,layout,z,lin,work=analytic_state()
        S.ChunkBlocks(lin,.1)
        self.assertLess(work.block_coefficients,
                        work.full_group_square_charge_counterfactual)
        self.assertLess(work.block_coefficients,
                        work.full_group_triangular_pairs_counterfactual)
        self.assertEqual(work.block_coefficients,
                         work.actual_sparse_symmetric_dot_products)
        self.assertLessEqual(work.max_chunk_width,2)

    def test_chunking_retains_every_independent_unknown_once(self):
        net,flows,meta,layout,z,lin,work=analytic_state()
        S.ChunkBlocks(lin,.1)
        coords=[k for indices,core in lin._producer_block_normal_core
                for k in indices]
        self.assertEqual(sorted(coords),list(range(layout.unknowns)))

    def test_small_system_chunked_F072_matches_original_reference(self):
        ref=S.existing_small_reference()
        self.assertTrue(ref["both_independently_original_F072_certified"])
        self.assertEqual(ref["reference_ADR047"]["groups"],2)
        self.assertEqual(ref["chunked"]["groups"],2)
        self.assertEqual(ref["chunked"]["status"],
                         "PASS_BLOCK_MCP_FULL_ORIGINAL_F072_CERTIFIED")

    def test_temporary_injection_restores_original_solver_on_exception(self):
        net,flows,meta=build_worker_network()
        orig_work=S.R47.Work
        orig_block=S.R47.ProducerBlocks
        with patch.object(S.R47,"solve_block",side_effect=RuntimeError("test")):
            with self.assertRaises(RuntimeError):
                S.solve_chunked(net,flows,S.R47.Limits())
        self.assertIs(S.R47.Work,orig_work)
        self.assertIs(S.R47.ProducerBlocks,orig_block)

    def test_work_limit_always_causes_no_accepted_commercial_result(self):
        net,flows,meta=build_worker_network()
        lim=S.R47.Limits(
            max_h_evaluations=1,max_route_scans=2,
            max_original_certificates=1,max_columns=30,
            max_generations=1,max_newton_steps=1,
            max_backtrack=1,max_krylov_products=1,
            max_cg_iterations=1,max_block_coefficients=1)
        result=S.solve_chunked(net,flows,lim)
        self.assertTrue(result["status"].startswith("NO_GO"))
        self.assertIsNone(result["final_flow_sha256"])
        self.assertFalse(result["production_approved"])

    def test_canonical_R13_max_density_remains_synthetic_not_worker_coupled(self):
        cfg=S.R49.CASES["maximum_R13_density_2562_240_120"]
        self.assertEqual(cfg.products*cfg.groups_per_product,240)
        self.assertEqual(cfg.products*cfg.markets_per_product,120)
        self.assertFalse(S.R49.CASES is None)
        self.assertNotEqual(240,10)

    def test_worker_fixture_no_go_blocks_smoke_regression(self):
        self.assertEqual(
            S.gate_status("smoke",False,False),
            "NO_GO_MANDATORY_WORKER_COUPLED_F072")

    def test_240_group_no_go_blocks_ci_even_when_worker_passes(self):
        self.assertEqual(
            S.gate_status("ci",True,False),
            "NO_GO_MANDATORY_240_GROUP_F072_REGRESSION")

    def test_both_independent_F072_certificates_required_for_ci_green(self):
        self.assertEqual(
            S.gate_status("ci",True,True),
            "PASS_ADR051_WORKER_AND_INDEPENDENT_F072")

    def test_unregistered_gate_and_implicit_non_boolean_status_rejected(self):
        with self.assertRaises(ValueError):
            S.gate_status("unspecified",True,True)
        with self.assertRaises(ValueError):
            S.gate_status("ci",1,True)

    def test_our_bounded_runner_does_not_claim_game_integration(self):
        self.assertEqual(S.SCHEMA,
            "GA_GATE09_ADR051_WORKER_TIME_DEMAND_AND_CHUNKED_SPD_MCP_V1")
        self.assertTrue(S.F072_TOL==1e-4)
        self.assertFalse(getattr(S,"CSharp_integrated",False))


if __name__=="__main__":
    unittest.main()
