from pathlib import Path
import math
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"ci"))
import run_adr049 as R
from clearing_r5 import _flow_checksum

class ADR049GeographyAndDensityContracts(unittest.TestCase):
    def test_original_R20_sha16_unchanged(self):
        self.assertEqual(R.ADR48.ADR42.R21.validate_manifest(),16)

    def test_original_price_quartic_and_economic_tolerance(self):
        self.assertEqual(R.F072_TOLERANCE,1e-4)
        self.assertTrue(all(c.exponent==4 for c in R.CASES.values()))
        self.assertEqual(R.R45.EPS_SCHEDULE,(1e-3,1e-6,1e-9))

    def test_geography_5762_uses_identical_producer_market_density(self):
        baseline=R.ADR48.FIXTURES["density_2562_144_48"]
        bigger=R.CASES["geography_5762_144_48"]
        self.assertEqual((baseline.products,baseline.groups_per_product,
                          baseline.markets_per_product),
                         (bigger.products,bigger.groups_per_product,
                          bigger.markets_per_product))
        self.assertEqual(10*bigger.m**2+2,5762)
        self.assertEqual(30*bigger.m**2,17280)
        self.assertEqual(bigger.groups_per_product*bigger.products,144)
        self.assertEqual(bigger.products*bigger.markets_per_product,48)

    def test_maximum_R13_density_really_has_240_producers_120_markets(self):
        cfg=R.CASES["maximum_R13_density_2562_240_120"]
        self.assertEqual(10*cfg.m**2+2,2562)
        self.assertEqual(cfg.products*cfg.groups_per_product,240)
        self.assertEqual(cfg.products*cfg.markets_per_product,120)
        self.assertEqual(cfg.groups_per_product*cfg.markets_per_product*
                         cfg.products,2880)

    def test_density_reaches_original_R13_cap_not_claimed_as_unbounded(self):
        cfg=R.CASES["maximum_R13_density_2562_240_120"]
        with self.assertRaises(ValueError):
            R.R13(m=cfg.m,products=10,groups_per_product=25,
                  markets_per_product=12,civilizations=6,iterations=0)
        with self.assertRaises(ValueError):
            R.R13(m=cfg.m,products=11,groups_per_product=24,
                  markets_per_product=12,civilizations=6,iterations=0)

    def test_registered_budget_is_separate_and_finite(self):
        for name in R.CASES:
            limits=R.BUDGETS[name]
            self.assertGreater(limits.max_krylov_products,0)
            self.assertGreater(limits.max_block_coefficients,0)
            self.assertGreater(limits.max_route_scans,0)
            self.assertLessEqual(limits.max_original_certificates,6)

    def test_geography_is_paired_at_identical_solver_budget_as_ADR048(self):
        self.assertEqual(
            vars(R.BUDGETS["geography_5762_144_48"]),
            vars(R.ADR48.limits_for("density_2562_144_48")))

    def test_original_R13_large_geography_has_real_price_and_routes(self):
        cfg=R.CASES["geography_5762_144_48"]
        net,flows=R.construct_scenario(cfg)
        original=_flow_checksum(flows)
        meta=R.physical_properties(net,flows,cfg)
        self.assertEqual(meta["nodes"],5762)
        self.assertEqual(meta["edges"],17280)
        self.assertEqual(meta["producer_groups"],144)
        self.assertEqual(meta["markets"],48)
        self.assertEqual(meta["canonical_destination_options"],1152)
        self.assertEqual(meta["initial_funded_columns"],144)
        self.assertEqual(original,_flow_checksum(flows))

    def test_all_R13_markets_are_covered_without_fictitious_output(self):
        cfg=R.R13(m=2,products=2,markets_per_product=2,
                  groups_per_product=4,civilizations=3,iterations=0)
        net,flows=R.construct_scenario(cfg)
        meta=R.physical_properties(net,flows,cfg)
        self.assertEqual(meta["markets"],4)
        self.assertEqual(len(net.snapshots(flows)[1]),4)
        self.assertAlmostEqual(sum(flows.values()),
                               sum(g.supply for g in net.groups.values()),
                               delta=1e-11)

    def test_gate_rejects_geography_no_go_without_silent_ci_green(self):
        rows=[{"name":"geography_5762_144_48",
               "original_F072_independent_certified":False}]
        self.assertEqual(
            R.classify("geography",rows),
            ("NO_GO_5762_GEOGRAPHY_COMMERCIAL_GATE",False))

    def test_gate_accepts_ONLY_original_certified_geography(self):
        rows=[{"name":"geography_5762_144_48",
               "original_F072_independent_certified":True}]
        self.assertEqual(
            R.classify("geography",rows),
            ("PASS_5762_GEOGRAPHY_ORIGINAL_F072_CERTIFIED",True))

    def test_max_density_must_have_own_independent_certificate(self):
        rows=[{"name":"maximum_R13_density_2562_240_120",
               "original_F072_independent_certified":False}]
        self.assertEqual(
            R.classify("maximum_density",rows),
            ("NO_GO_240_GROUP_STRESS_NOT_CERTIFIED",False))

    def test_incomplete_profile_cannot_be_called_pass(self):
        with self.assertRaises(ValueError):
            R.classify("geography",[])
        with self.assertRaises(ValueError):
            R.classify("geography",[
                {"name":"maximum_R13_density_2562_240_120",
                 "original_F072_independent_certified":True}])
        with self.assertRaises(ValueError):
            R.classify("does_not_exist",[])

    def test_static_certificate_not_overridden_by_numerical_status(self):
        fake={
            "status":"PASS_BLOCK_MCP_FULL_ORIGINAL_F072_CERTIFIED",
            "certificate":{
                "status":"FAIL_STATIC_F072_COMMERCIAL_REGRET",
                "global_F072_gap":0.4,
                "worst_group_F072_gap":.5,
            },
            "final_flow_sha256":"no_cert",
            "final_allocations":[[1,1,[1],1.]],
            "original_congestion_exponent":4,
            "input_unmodified":True,
        }
        self.assertFalse(R.approve_original_F072(fake))
        fake["certificate"]["status"]="PASS_STATIC_F072_FIRST_ORDER_ONLY"
        self.assertFalse(R.approve_original_F072(fake))
        fake["certificate"]["global_F072_gap"]=1e-5
        fake["certificate"]["worst_group_F072_gap"]=1e-5
        self.assertTrue(R.approve_original_F072(fake))
        fake["original_congestion_exponent"]=3
        self.assertFalse(R.approve_original_F072(fake))

    def test_no_go_cannot_publish_allocation_or_final_gap(self):
        cfg=R.R13(m=2,products=2,markets_per_product=2,
                  groups_per_product=4,civilizations=3,iterations=0)
        limits=R.R47.Limits(
            max_h_evaluations=1,max_route_scans=12,
            max_original_certificates=2,max_columns=40,
            max_generations=1,max_newton_steps=1,
            max_backtrack=1,max_krylov_products=2,
            max_cg_iterations=3,max_block_coefficients=15)
        with patch.dict(R.CASES,{"geography_5762_144_48":cfg}),\
             patch.dict(R.BUDGETS,{"geography_5762_144_48":limits}):
            row=R.execute("geography_5762_144_48")
        self.assertFalse(row["original_F072_independent_certified"])
        self.assertIsNone(row["original_F072_global_gap_final_if_pass"])
        self.assertIsNone(row["original_F072_worst_group_gap_final_if_pass"])
        self.assertFalse(row["production_approved"])
        self.assertFalse(row["runtime_preemption_or_scheduler_present"])

    def test_invalid_profile_has_no_implicit_fallback(self):
        with self.assertRaises(ValueError):
            R.run("invalid")

    def test_always_disclose_unapproved_engine_and_finance(self):
        self.assertEqual(set(R.PROFILES),
                         {"geography","maximum_density"})
        self.assertTrue(all(k in R.BUDGETS for k in R.CASES))
        self.assertFalse(getattr(R,"CSharp_integrated",False))


if __name__=="__main__":
    unittest.main()
