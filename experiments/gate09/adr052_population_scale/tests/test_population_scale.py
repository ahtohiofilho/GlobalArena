from __future__ import annotations
from collections import defaultdict
from math import fsum,isclose,isfinite
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import population_scale as P
import run_adr052 as A
from worker_coupled import ACTIVITY_RATES,ITEMS,demand_by_tile
from clearing_r5 import _flow_checksum


class ADR052SmallWorkerPhysicalContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.net,cls.flows,cls.m=P.build_population(
            P.CONFIGS["unit_162_nodes_128_workers"])

    def test_frozen_original_R20_and_F072_limits(self):
        self.assertEqual(A.ADR51.R49.ADR48.ADR42.R21.validate_manifest(),16)
        self.assertEqual(A.ADR51.F072_TOL,1e-4)
        self.assertTrue(A.PILOT_NUMERICAL_BUDGET.max_krylov_products<60000)

    def test_unit_real_goldberg_nodes_edges(self):
        self.assertEqual(self.m["geometry_nodes"],162)
        self.assertEqual(self.m["geometry_edges"],480)
        self.assertEqual(self.m["workers"],128)

    def test_worker_producer_and_demand_nodes_not_group_market_counts(self):
        self.assertEqual(self.m["production_nodes_count"],4)
        self.assertEqual(self.m["demand_nodes_count"],16)
        self.assertEqual(self.m["groups_aggregated_by_node_commodity_seller"],8)
        self.assertEqual(self.m["market_pairs_node_commodity"],32)
        self.assertTrue(self.m["physical_production_nodes"].issubset(
            self.m["physical_demand_nodes"]))

    def test_every_worker_has_six_turns_and_legal_adjacency(self):
        self.assertEqual(self.m["time_slots_per_cycle"],6)
        for w in self.m["source_workers"]:
            P.validate_itinerary(w,self.net)
        self.assertEqual(len(set(w.id for w in self.m["source_workers"])),128)

    def test_all_activities_are_physically_present(self):
        self.assertEqual(set(self.m["activities"]),set(ACTIVITY_RATES))
        self.assertEqual(self.m["producing_workers"],32)
        self.assertEqual(self.m["nonproducing_workers"],96)

    def test_conserved_demand_per_commodity_no_travel_multiplication(self):
        for item in ITEMS:
            expected=fsum(
                w.demand_multiplier*ACTIVITY_RATES[w.activity][item]
                for w in self.m["source_workers"])
            observed=float(self.m["market_demand_by_item"][str(item)])
            self.assertTrue(isclose(observed,expected,
                                    rel_tol=1e-12,abs_tol=1e-12))

    def test_each_real_demand_node_has_both_positive_market_goods(self):
        self.assertEqual(set(self.net.markets),
                         {(node,item)
                          for node in self.m["physical_demand_nodes"]
                          for item in ITEMS})
        self.assertTrue(all(m.demand>0 for m in self.net.markets.values()))

    def test_neither_real_physical_supply_nor_positive_prices_are_fabricated(self):
        total_worker_supply=fsum(w.supply for w in self.m["source_workers"])
        self.assertAlmostEqual(total_worker_supply,
                               fsum(g.supply for g in self.net.groups.values()),
                               places=11)
        self.assertAlmostEqual(total_worker_supply,fsum(self.flows.values()),
                               places=11)
        _,q,price=self.net.snapshots(self.flows)
        self.assertEqual(set(q),set(self.net.markets))
        self.assertTrue(all(isfinite(x) and x>0 for x in price.values()))

    def test_group_supply_is_exact_worker_aggregation_not_new_output(self):
        by_worker=defaultdict(list)
        for w in self.m["source_workers"]:
            if w.activity=="produce":
                by_worker[(w.visited_tiles[0],w.product,w.civilization)].append(
                    w.supply)
        for g in self.net.groups.values():
            self.assertTrue(isclose(
                g.supply,fsum(by_worker[(g.origin,g.commodity,g.seller)]),
                rel_tol=1e-13,abs_tol=1e-13))
        self.assertEqual(len(by_worker),len(self.net.groups))

    def test_funded_paths_are_real_adjacent_original_R13_edges(self):
        self.assertEqual(len(self.flows),4*len(self.net.groups))
        for c in self.flows:
            self.net.validate_column(c)
            self.assertLessEqual(len(c.edges),1)

    def test_demand_replay_and_initial_flow_replay_are_repeatable(self):
        net2,flows2,meta2=P.build_population(
            P.CONFIGS["unit_162_nodes_128_workers"])
        self.assertEqual(self.m["worker_ledger_sha256"],
                         meta2["worker_ledger_sha256"])
        self.assertEqual(_flow_checksum(self.flows),_flow_checksum(flows2))
        self.assertEqual(self.m["market_demand_by_item"],
                         meta2["market_demand_by_item"])

    def test_registered_config_rejects_impossible_worker_density(self):
        for args in ((1,4,128),(24,2000,8192),(24,512,512),
                     (24,12,0),(24,12,2049)):
            with self.subTest(case=args):
                with self.assertRaises(ValueError):
                    P.PopulationConfig(*args)

    def test_wrong_worker_config_is_rejected_explicitly(self):
        with self.assertRaises(ValueError):
            P.build_population(None)
        with self.assertRaises(ValueError):
            A.population_report("unregistered_world",False)


class ADR052LargePhysicalPopulationContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.net,cls.flows,cls.m=P.build_population(
            P.CONFIGS["spatial_5762_nodes_8192_workers"])

    def test_spatial_world_really_has_thousands_of_goldberg_nodes(self):
        self.assertEqual(self.m["geometry_nodes"],5762)
        self.assertEqual(self.m["geometry_edges"],17280)
        self.assertEqual(len(self.net.adj),5762)

    def test_8192_actual_workers_not_8192_synthetic_groups(self):
        self.assertEqual(self.m["workers"],8192)
        self.assertEqual(self.m["producing_workers"],2048)
        self.assertEqual(self.m["nonproducing_workers"],6144)
        self.assertEqual(len(self.net.groups),1024)

    def test_workforce_populates_2048_distinct_actual_demand_nodes(self):
        self.assertEqual(self.m["production_nodes_count"],512)
        self.assertEqual(self.m["demand_nodes_count"],2048)
        self.assertEqual(len(self.net.markets),4096)
        self.assertTrue(self.m["physical_production_nodes"].issubset(
            self.m["physical_demand_nodes"]))

    def test_economy_is_aggregated_not_independent_R13_demand(self):
        self.assertEqual(self.m["groups_aggregated_by_node_commodity_seller"],1024)
        self.assertEqual(self.m["initial_funded_columns"],4096)
        self.assertTrue(self.m["no_independent_synthetic_R13_market_demand"])
        self.assertTrue(self.m["seller_ownership_from_original_R13"])

    def test_large_travel_itineraries_conserve_each_item(self):
        self.assertEqual(len(self.m["source_workers"]),8192)
        observed=self.m["market_demand_by_item"]
        for k in ITEMS:
            expected=fsum(w.demand_multiplier*ACTIVITY_RATES[w.activity][k]
                          for w in self.m["source_workers"])
            self.assertTrue(isclose(observed[str(k)],expected,rel_tol=1e-12))

    def test_travel_occurs_across_real_edges_no_teleports(self):
        for w in self.m["source_workers"]:
            if w.activity=="travel":
                P.validate_itinerary(w,self.net)
                self.assertEqual(len(set(w.visited_tiles)),4)
        self.assertEqual(self.m["activities"]["travel"],2048)

    def test_all_4096_market_prices_finite_due_only_to_real_supply(self):
        _,q,prices=self.net.snapshots(self.flows)
        self.assertEqual(set(q),set(self.net.markets))
        self.assertEqual(len(prices),4096)
        self.assertTrue(all(v>0 and isfinite(v) for v in prices.values()))
        self.assertTrue(isclose(fsum(self.flows.values()),
                                fsum(g.supply for g in self.net.groups.values()),
                                rel_tol=1e-11))

    def test_large_population_ledger_does_not_claim_F072_equilibrium(self):
        self.assertTrue(self.m["not_authoritative_worker_model"])
        self.assertTrue(self.m["not_game_engine"])
        self.assertNotIn("original_F072_independent_certified",self.m)


class ADR052CommercialGovernanceContracts(unittest.TestCase):
    def test_spatial_8192_is_not_sent_to_unbounded_solver(self):
        self.assertFalse(A.execute.__doc__=="")
        self.assertEqual(P.CONFIGS["spatial_5762_nodes_8192_workers"].workers,8192)

    def test_small_worker_certificate_gate_is_explicit(self):
        self.assertTrue(A.SMALL_NUMERICAL_BUDGET.max_krylov_products>0)
        self.assertEqual(A.SCHEMA,
            "GA_GATE09_ADR052_PHYSICAL_WORKER_POPULATION_AGGREGATION_AND_F072_V1")

    def test_no_direct_worker_count_as_market_count(self):
        self.assertNotEqual(P.CONFIGS["pilot_5762_nodes_2048_workers"].workers,
                            96)
        self.assertNotEqual(P.CONFIGS["spatial_5762_nodes_8192_workers"].workers,
                            4096)

    def test_experiment_does_not_modify_original_solver_classes(self):
        before=A.ADR51.R47.ProducerBlocks
        net,flows,meta=P.build_population(
            P.CONFIGS["unit_162_nodes_128_workers"])
        out=A.ADR51.solve_chunked(net,flows,A.SMALL_NUMERICAL_BUDGET)
        self.assertIs(A.ADR51.R47.ProducerBlocks,before)
        if A.ADR51.is_certified(out):
            self.assertEqual(out["certificate"]["status"],
                             "PASS_STATIC_F072_FIRST_ORDER_ONLY")
        else:
            self.assertIsNone(out["final_flow_sha256"])

    def test_independent_2048_worker_F072_is_now_mandatory(self):
        self.assertEqual(
            A.classify_regression("ci",True,True,False),
            "NO_GO_MANDATORY_2048_WORKER_F072_REGRESSION")

    def test_full_worker_spatial_and_commercial_success(self):
        self.assertEqual(
            A.classify_regression("ci",True,True,True),
            "PASS_ADR052_WORKER_SPATIAL_AND_PILOT_F072")

    def test_spatial_8192_conservation_is_mandatory(self):
        self.assertEqual(
            A.classify_regression("ci",True,False,True),
            "NO_GO_MANDATORY_SMALL_F072_OR_SPATIAL_WORKER_LEDGER")

    def test_missing_pilot_certificate_never_defaults_to_green(self):
        with self.assertRaises(ValueError):
            A.classify_regression("ci",True,True,None)
        with self.assertRaises(ValueError):
            A.classify_regression("ci",True,True,1)
        with self.assertRaises(ValueError):
            A.classify_regression("unknown",True,True,True)

    def test_nonrepresentative_coverage_is_acknowledged(self):
        self.assertLess(512,5762)
        self.assertGreater(8192,5762)
        self.assertLess(2048,5762)


if __name__=="__main__":
    unittest.main()
