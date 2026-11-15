from pathlib import Path
import math
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr048 as R
from multimarket_r13 import R13Scenario,construct_scenario
from clearing_r5 import _flow_checksum


class ADR048EconomicDensityAndCycleReserve(unittest.TestCase):
    def test_frozen_original_R20_sha16(self):
        self.assertEqual(R.ADR42.R21.validate_manifest(),16)

    def test_unchanged_commercial_tolerance_and_quartic(self):
        self.assertEqual(R.ORIGINAL_TOLERANCE,1e-4)
        self.assertEqual(R.R45.EPS_SCHEDULE,(1e-3,1e-6,1e-9))
        self.assertTrue(all(cfg.exponent==4 for cfg in R.FIXTURES.values()))

    def test_density_not_confused_with_geographic_size(self):
        a=R.FIXTURES["baseline_2562_48_24"]
        b=R.FIXTURES["density_2562_96_32"]
        c=R.FIXTURES["density_2562_144_48"]
        self.assertEqual(a.m,b.m)
        self.assertEqual(b.m,c.m)
        self.assertEqual(a.groups_per_product*a.products,48)
        self.assertEqual(b.groups_per_product*b.products,96)
        self.assertEqual(c.groups_per_product*c.products,144)
        self.assertEqual(a.markets_per_product*a.products,24)
        self.assertEqual(b.markets_per_product*b.products,32)
        self.assertEqual(c.markets_per_product*c.products,48)

    def test_geography_profile_uses_5762_nodes(self):
        cfg=R.FIXTURES["geography_5762_144_48"]
        self.assertEqual(10*cfg.m*cfg.m+2,5762)
        self.assertEqual(30*cfg.m*cfg.m,17280)
        self.assertEqual(cfg.products*cfg.groups_per_product,144)
        self.assertEqual(cfg.products*cfg.markets_per_product,48)

    def test_density_fixture_uses_real_R13_groups_and_market_demand(self):
        cfg=R.FIXTURES["density_2562_96_32"]
        net,flows=construct_scenario(cfg)
        self.assertEqual(len(net.groups),96)
        self.assertEqual(len(net.markets),32)
        self.assertEqual(len(net.edges),7680)
        self.assertEqual(len(flows),96)
        self.assertTrue(all(m.demand>0 for m in net.markets.values()))
        self.assertEqual(len(net.snapshots(flows)[1]),32)
        self.assertEqual(_flow_checksum(flows),_flow_checksum(dict(flows)))

    def test_benchmark_profiles_are_explicit(self):
        self.assertEqual(R.PROFILES["ci"],("density_2562_96_32",))
        self.assertEqual(R.PROFILES["extended"],(
            "density_2562_96_32","density_2562_144_48"))
        self.assertEqual(R.PROFILES["geography"],(
            "geography_5762_144_48",))

    def test_economics_fingerprint_distinguishes_commercial_density(self):
        c=R.FIXTURES["baseline_2562_48_24"]
        d=R.FIXTURES["density_2562_96_32"]
        net1,_=construct_scenario(c)
        net2,_=construct_scenario(d)
        self.assertNotEqual(R._economics_fingerprint(net1),
                            R._economics_fingerprint(net2))

    def test_work_caps_are_finite_and_include_krylov_and_block_cost(self):
        for name in R.FIXTURES:
            with self.subTest(name=name):
                lim=R.limits_for(name)
                self.assertGreater(lim.max_krylov_products,0)
                self.assertGreater(lim.max_route_scans,0)
                self.assertGreater(lim.max_block_coefficients,0)
                self.assertGreater(lim.max_columns,0)
        with self.assertRaises(ValueError):
            R.limits_for("unregistered_world")

    def test_invalid_cycle_budget_is_rejected(self):
        for total,reserve in ((0,0),(10,10),(10,11),
                              (10,-1),(float("nan"),2),
                              (float("inf"),0)):
            with self.subTest(t=total,r=reserve):
                with self.assertRaises(ValueError):
                    R.CycleReservation(total,reserve)

    def test_explicit_non_economic_cpu_reserve(self):
        b=R.CycleReservation(60000,15000)
        self.assertEqual(b.economy_ms,45000)
        result=R.model_cycle_reservation(True,40000,b)
        self.assertEqual(
            result["decision"],
            "ADMISSIBLE_FOR_HYPOTHETICAL_PERIODIC_CYCLE")
        self.assertEqual(result["reserved_for_non_economic_layers_ms"],
                         15000)
        self.assertFalse(result["game_thread_executed"])
        self.assertFalse(result["asynchronous_preemption_implemented"])

    def test_over_budget_even_certified_candidate_is_deferred(self):
        b=R.CycleReservation(60000,15000)
        outcome=R.model_cycle_reservation(True,45000.01,b)
        self.assertEqual(
            outcome["decision"],
            "DEFER_CERTIFIED_SNAPSHOT_AS_TOO_COSTLY_FOR_PLAN")
        self.assertTrue(outcome["no_game_snapshot_committed"])

    def test_uncertified_state_never_admitted_even_with_zero_cost(self):
        b=R.CycleReservation(60000,15000)
        outcome=R.model_cycle_reservation(False,0,b)
        self.assertEqual(outcome["decision"],
                         "BLOCK_UNCERTIFIED_ECONOMIC_SNAPSHOT")
        self.assertTrue(outcome["never_accept_uncertified_commercial_state"])

    def test_invalid_reservation_measurements_rejected(self):
        b=R.CycleReservation(100,20)
        for value in (-1,float("nan"),float("inf")):
            with self.assertRaises(ValueError):
                R.model_cycle_reservation(True,value,b)
        with self.assertRaises(ValueError):
            R.model_cycle_reservation(1,10,b)
        with self.assertRaises(ValueError):
            R.model_cycle_reservation(True,10,None)

    def test_original_R13_market_supply_remains_positive(self):
        cfg=R13Scenario(m=2,products=2,markets_per_product=2,
                        groups_per_product=3,civilizations=3,iterations=0)
        net,flows=construct_scenario(cfg)
        self.assertTrue(all(q>0 for q in net.snapshots(flows)[1].values()))
        self.assertEqual(sum(flows.values()),
                         sum(g.supply for g in net.groups.values()))

    def test_small_fixture_F072_candidate_never_accepted_by_numerical_status_alone(self):
        cfg=R13Scenario(m=2,products=2,markets_per_product=2,
                        groups_per_product=3,civilizations=3,iterations=0)
        limit=R.ADR47.Limits(
            max_h_evaluations=20,max_route_scans=40,
            max_original_certificates=2,max_columns=24,
            max_generations=1,max_newton_steps=2,
            max_backtrack=2,max_krylov_products=40,
            max_cg_iterations=5,max_block_coefficients=200)
        with patch.dict(R.FIXTURES,{"baseline_2562_48_24":cfg}), \
             patch.object(R,"limits_for",return_value=limit):
            result=R.run_fixture("baseline_2562_48_24")
        self.assertEqual(result["geometry_nodes"],42)
        self.assertEqual(result["physical_producer_groups"],6)
        self.assertFalse(result["game_CSharp_or_Godot_executed"])
        self.assertFalse(result["runtime_scheduler_or_yield_implemented"])
        self.assertFalse(result["production_approved"])
        if result["static_original_F072_go"]:
            self.assertLessEqual(result["final_global_F072_gap_if_quoted"],
                                 1e-4)
        else:
            self.assertIsNone(result["final_global_F072_gap_if_quoted"])
            self.assertIsNone(result["final_worst_group_F072_gap_if_quoted"])

    def test_false_F072_pass_is_rejected_even_when_status_looks_green(self):
        cfg=R13Scenario(m=2,products=2,markets_per_product=2,
                        groups_per_product=3,civilizations=3,iterations=0)
        with patch.dict(R.FIXTURES,{"baseline_2562_48_24":cfg}), \
             patch.object(R,"limits_for",return_value=R.ADR47.Limits()), \
             patch.object(R.ADR47,"solve_block",return_value={
                "status":"PASS_FAKE_GREEN",
                "certificate":{"status":"FAIL_STATIC_F072_COMMERCIAL_REGRET",
                    "global_F072_gap":.5,"worst_group_F072_gap":.9},
                "final_flow_sha256":"fake",
                "final_allocations":[],
                "work":{},
                "elapsed_ms_observed_python":0.,
                "provisional_last_snapshot":None,
                "generations":[],
             }):
            with self.assertRaises(AssertionError):
                R.run_fixture("baseline_2562_48_24")

    def test_potentially_slow_Python_is_not_claimed_real_game_budget(self):
        b=R.CycleReservation(120000,30000)
        result=R.model_cycle_reservation(True,89999,b)
        self.assertFalse(result["actual_CSharp_CPU_ms_known"])
        self.assertFalse(result["render_frames_not_measured"] is False)
        self.assertTrue(result["no_game_snapshot_committed"])


if __name__=="__main__":
    unittest.main()
