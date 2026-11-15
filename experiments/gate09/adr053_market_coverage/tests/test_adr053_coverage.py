from __future__ import annotations
from math import fsum,isclose,isfinite
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr053 as A
from population_scale import build_population
from worker_coupled import ACTIVITY_RATES,ITEMS,validate_itinerary
from clearing_r5 import _flow_checksum


def proxy(name,cert=False):
    return {
        "name":name,
        "F072_independent_certified":cert,
        "F072_attempted":name in ("baseline_48_demand_nodes",
                                  "candidate_96_demand_nodes"),
        "demand_nodes":{
            "baseline_48_demand_nodes":48,
            "candidate_96_demand_nodes":96,
            "spatial_192_demand_nodes":192,
            "spatial_2048_demand_nodes":2048}[name],
        "production_nodes_subset_demand_nodes":True,
        "worker_demand_conserved_without_extra_travel_demand":True,
    }


class ADR053PopulationCoverageContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.net24,cls.flows24,cls.meta24=build_population(
            A.CONFIGS["candidate_96_demand_nodes"])
        cls.net48,cls.flows48,cls.meta48=build_population(
            A.CONFIGS["spatial_192_demand_nodes"])

    def test_immutable_original_F072_and_R20_manifest(self):
        self.assertEqual(
            A.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest(),16)
        self.assertEqual(A.TOLERANCE,1e-4)
        self.assertEqual(A.R52.ADR51.R49.R45.EPS_SCHEDULE,
                         (1e-3,1e-6,1e-9))

    def test_all_geographies_are_the_same_5762_nodes(self):
        self.assertEqual({cfg.m for cfg in A.CONFIGS.values()},{24})
        self.assertTrue(all(10*cfg.m**2+2==5762
                            for cfg in A.CONFIGS.values()))

    def test_24_hubs_double_demand_locations_control_workers(self):
        a=A.CONFIGS["baseline_48_demand_nodes"]
        b=A.CONFIGS["candidate_96_demand_nodes"]
        self.assertEqual(a.workers,b.workers)
        self.assertEqual((a.hubs,b.hubs),(12,24))
        self.assertEqual((4*a.hubs,4*b.hubs),(48,96))

    def test_96_demand_nodes_stem_from_actual_workers(self):
        meta=self.meta24
        self.assertEqual(meta["workers"],2048)
        self.assertEqual(meta["producing_workers"],512)
        self.assertEqual(meta["nonproducing_workers"],1536)
        self.assertEqual(meta["production_nodes_count"],24)
        self.assertEqual(meta["demand_nodes_count"],96)
        self.assertEqual(meta["groups_aggregated_by_node_commodity_seller"],48)
        self.assertEqual(meta["market_pairs_node_commodity"],192)
        self.assertEqual(len(self.flows24),192)

    def test_192_demand_nodes_are_spatial_only(self):
        meta=self.meta48
        self.assertEqual(meta["workers"],4096)
        self.assertEqual(meta["production_nodes_count"],48)
        self.assertEqual(meta["demand_nodes_count"],192)
        self.assertEqual(len(self.net48.groups),96)
        self.assertEqual(len(self.net48.markets),384)
        self.assertEqual(len(self.flows48),384)

    def test_worker_demands_include_production_construction_travel_and_other(self):
        for meta in (self.meta24,self.meta48):
            self.assertEqual(set(meta["activities"]),set(ACTIVITY_RATES))
            self.assertEqual(meta["producing_workers"]+
                             meta["nonproducing_workers"],meta["workers"])

    def test_producer_locations_subset_of_demander_locations(self):
        for meta in (self.meta24,self.meta48):
            self.assertTrue(meta["physical_production_nodes"].issubset(
                meta["physical_demand_nodes"]))
            self.assertLess(meta["production_nodes_count"],
                            meta["demand_nodes_count"])

    def test_all_two_item_market_demands_strictly_positive(self):
        for net,meta in ((self.net24,self.meta24),
                         (self.net48,self.meta48)):
            expected={(n,item) for n in meta["physical_demand_nodes"]
                      for item in ITEMS}
            self.assertEqual(set(net.markets),expected)
            self.assertTrue(all(m.demand>0 and isfinite(m.demand)
                                for m in net.markets.values()))

    def test_every_worker_demand_conserved_by_time_and_activity(self):
        for meta in (self.meta24,self.meta48):
            workers=meta["source_workers"]
            for k in ITEMS:
                amount=fsum(w.demand_multiplier*
                            ACTIVITY_RATES[w.activity][k]
                            for w in workers)
                self.assertTrue(isclose(
                    amount,meta["market_demand_by_item"][str(k)],
                    rel_tol=1e-12,abs_tol=1e-12))

    def test_travel_workers_never_teleport(self):
        for net,meta in ((self.net24,self.meta24),
                         (self.net48,self.meta48)):
            for w in meta["source_workers"]:
                if w.activity=="travel":
                    self.assertEqual(len(w.visited_tiles),6)
                    validate_itinerary(w,net)
                    self.assertEqual(len(set(w.visited_tiles)),4)

    def test_worker_supply_not_duplicated_by_markets(self):
        for net,flow,meta in ((self.net24,self.flows24,self.meta24),
                              (self.net48,self.flows48,self.meta48)):
            expected=fsum(w.supply for w in meta["source_workers"])
            self.assertTrue(isclose(expected,fsum(g.supply for g
                              in net.groups.values()),rel_tol=1e-11))
            self.assertTrue(isclose(expected,fsum(flow.values()),
                                    rel_tol=1e-11))

    def test_positive_market_prices_are_only_initial_seeds(self):
        for net,flow in ((self.net24,self.flows24),
                         (self.net48,self.flows48)):
            _,q,prices=net.snapshots(flow)
            self.assertEqual(set(q),set(net.markets))
            self.assertTrue(all(v>0 and isfinite(v)
                                for v in prices.values()))

    def test_routes_use_real_edges_and_seller_tariffs(self):
        for net,flow in ((self.net24,self.flows24),
                         (self.net48,self.flows48)):
            self.assertTrue(any(e.tariff_micro>0 for e in net.edges.values()))
            for c in flow:
                net.validate_column(c)
                self.assertLessEqual(len(c.edges),1)

    def test_deterministic_worker_ledger_and_supply_replay(self):
        net2,flows2,meta2=build_population(
            A.CONFIGS["candidate_96_demand_nodes"])
        self.assertEqual(meta2["worker_ledger_sha256"],
                         self.meta24["worker_ledger_sha256"])
        self.assertEqual(meta2["market_demand_by_item"],
                         self.meta24["market_demand_by_item"])
        self.assertEqual(_flow_checksum(flows2),
                         _flow_checksum(self.flows24))

    def test_budget_is_finite_and_separate_from_csharp_cpu(self):
        budget=A.registered_budget("candidate_96_demand_nodes")
        self.assertEqual(budget.max_krylov_products,34000)
        self.assertEqual(budget.max_block_coefficients,145000)
        self.assertGreater(budget.max_route_scans,0)
        with self.assertRaises(ValueError):
            A.registered_budget("spatial_192_demand_nodes")
        with self.assertRaises(ValueError):
            A.registered_budget("spatial_2048_demand_nodes")

    def test_do_not_accidentally_classify_spatial_stage_as_equilibrium(self):
        with patch.object(A.R52.ADR51,"solve_chunked",
                          side_effect=AssertionError("Unexpected F072 solve")):
            row=A.run_stage("spatial_192_demand_nodes",False)
        self.assertFalse(row["F072_attempted"])
        self.assertIsNone(row["original_F072_global_FINAL_if_pass"])
        self.assertIsNone(row["solver_work"])
        self.assertEqual(row["solver_status"],"NOT_RUN_SPATIAL_LEDGER_ONLY")

    def test_mandatory_baseline_failure_blocks_ci(self):
        rows=[
            proxy("baseline_48_demand_nodes",False),
            proxy("candidate_96_demand_nodes",True),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        self.assertEqual(A.classify("ci",rows),
            ("NO_GO_MANDATORY_48_DEMAND_NODE_F072_REGRESSION",False))

    def test_mandatory_96_no_go_blocks_ci_after_initial_dual_os_pass(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",False),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        self.assertEqual(A.classify("ci",rows),
            ("NO_GO_MANDATORY_96_DEMAND_NODE_F072_REGRESSION",False))

    def test_original_F072_pass_for_96_is_independent(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",True),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        self.assertEqual(A.classify("ci",rows),
                         ("PASS_ADR053_96_DEMAND_NODE_ORIGINAL_F072_CERTIFIED",
                          True))

    def test_96_no_go_never_publishes_a_certified_final_F072_result(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",False),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        status,accepted=A.classify("ci",rows)
        self.assertFalse(accepted)
        self.assertTrue(status.startswith("NO_GO_MANDATORY_96_"))

    def test_missing_candidate_F072_certificate_flag_rejected(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",False),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        rows[1]["F072_independent_certified"]=None
        with self.assertRaises(ValueError):
            A.classify("ci",rows)

    def test_smoke_reference_gate_independent_of_96_trial(self):
        rows=[proxy("baseline_48_demand_nodes",True)]
        self.assertEqual(
            A.classify("smoke",rows),
            ("PASS_ADR053_MANDATORY_48_NODE_F072_REFERENCE",True))

    def test_unverified_192_node_ledger_never_passes_ci(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",True),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        rows[2]["production_nodes_subset_demand_nodes"]=False
        self.assertEqual(A.classify("ci",rows),
            ("NO_GO_MANDATORY_PHYSICAL_DEMAND_COVERAGE",False))

    def test_unverified_2048_node_ledger_never_passes_ci(self):
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("candidate_96_demand_nodes",True),
            proxy("spatial_192_demand_nodes"),
            proxy("spatial_2048_demand_nodes"),
        ]
        rows[3]["demand_nodes"]=2047
        self.assertEqual(A.classify("ci",rows),
            ("NO_GO_MANDATORY_PHYSICAL_DEMAND_COVERAGE",False))

    def test_missing_or_reordered_evidence_is_rejected(self):
        with self.assertRaises(ValueError):
            A.classify("ci",[])
        with self.assertRaises(ValueError):
            A.classify("bad",[])
        rows=[
            proxy("baseline_48_demand_nodes",True),
            proxy("spatial_192_demand_nodes"),
            proxy("candidate_96_demand_nodes",True),
            proxy("spatial_2048_demand_nodes"),
        ]
        with self.assertRaises(ValueError):
            A.classify("ci",rows)

    def test_2048_nodes_are_an_unfinished_economic_gate(self):
        cfg=A.CONFIGS["spatial_2048_demand_nodes"]
        self.assertEqual((cfg.hubs,cfg.workers),(512,8192))
        self.assertEqual(A.STAGES["ci"][-1],
                         ("spatial_2048_demand_nodes",False))

    def test_original_congestion_and_F059_not_ratified(self):
        self.assertEqual(A.TOLERANCE,1e-4)
        self.assertEqual(A.CONFIGS["baseline_48_demand_nodes"].m,24)
        self.assertFalse(getattr(A,"F059_ratified",False))

    def test_only_two_stages_can_run_a_commercial_solver(self):
        self.assertEqual([name for name,commercial in A.STAGES["ci"]
                         if commercial],
                         ["baseline_48_demand_nodes",
                          "candidate_96_demand_nodes"])


if __name__=="__main__":
    unittest.main()
