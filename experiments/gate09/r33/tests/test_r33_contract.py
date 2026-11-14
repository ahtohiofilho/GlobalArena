from dataclasses import replace
import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r33 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R33ExogenousFixtureContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def test_r20_manifest_16_files(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_baseline_fixture_is_exact_original(self):
        source, flows = construct_scenario(self.cfg)
        fixture, baseline_flow, audit = R.fixture_variant(self.cfg, "baseline")
        self.assertEqual(R.fixture_fingerprint(source),
                         R.fixture_fingerprint(fixture))
        self.assertEqual(audit["fixture_fingerprint"],
                         audit["reference_fingerprint"])
        self.assertEqual(_flow_checksum(flows), _flow_checksum(baseline_flow))
        self.assertEqual(audit["input_axes_changed"], [])

    def test_only_allowed_exogenous_input_fields_change(self):
        original, flows = construct_scenario(self.cfg)
        for variant in R.VARIANTS:
            with self.subTest(variant=variant):
                net, initial, audit = R.fixture_variant(self.cfg, variant)
                self.assertEqual(set(net.edges), set(original.edges))
                self.assertEqual(set(net.groups), set(original.groups))
                self.assertEqual(set(net.markets), set(original.markets))
                self.assertEqual(R.fixture_fingerprint(original),
                                 audit["reference_fingerprint"])
                for eid, edge in net.edges.items():
                    self.assertEqual(
                        replace(edge, capacity=original.edges[eid].capacity),
                        original.edges[eid])
                for gid, group in net.groups.items():
                    self.assertEqual(
                        replace(group, supply=original.groups[gid].supply),
                        original.groups[gid])
                for key, market in net.markets.items():
                    self.assertEqual(
                        replace(market, demand=original.markets[key].demand),
                        original.markets[key])
                self.assertTrue(R.verify_price_law(
                    net, evaluate_r14(
                        net, initial, ClearingSettings(exponent=4), ())))

    def test_uniform_demand_4x_doubles_price_but_not_quantities_or_targets(self):
        net, flows, _ = R.fixture_variant(self.cfg, "baseline")
        shifted, other, _ = R.fixture_variant(self.cfg, "demand_uniform_4x")
        self.assertEqual(flows, other)
        settings = ClearingSettings(exponent=4)
        before = evaluate_r14(net, flows, settings, ())
        after = evaluate_r14(shifted, other, settings, ())
        self.assertEqual(before["quantities"], after["quantities"])
        self.assertEqual(before["loads"], after["loads"])
        self.assertEqual(before["target"], after["target"])
        self.assertAlmostEqual(before["relative_gap"], after["relative_gap"],
                               delta=1e-12)
        for key in net.markets:
            self.assertAlmostEqual(after["prices"][key],
                                   before["prices"][key] * 2, delta=1e-12)

    def test_supply_variation_preserves_exact_physical_sums_and_finance(self):
        net, flows, info = R.fixture_variant(self.cfg, "supply_mosaic")
        self.assertIn("producer_supply", info["input_axes_changed"])
        for gid, group in net.groups.items():
            total = sum(amount for col, amount in flows.items()
                        if col.group_id == gid)
            self.assertAlmostEqual(total, group.supply, delta=1e-12)
        original = R.R21.settlement_check(net, flows)
        self.assertEqual(original["gross_micro"], original["credits_micro"])
        self.assertGreater(original["sold_micro"], 0)

    def test_capacity_tightness_increases_load_over_capacity(self):
        first, flows, _ = R.fixture_variant(self.cfg, "baseline")
        second, changed, info = R.fixture_variant(self.cfg, "capacity_quarter")
        self.assertEqual(flows, changed)
        loads, q, prices = first.snapshots(flows)
        tight_loads, *_ = second.snapshots(changed)
        self.assertEqual(loads, tight_loads)
        self.assertIn("edge_capacity", info["input_axes_changed"])
        stressed = R.R22.edge_stress(second, {"loads": tight_loads})
        original = R.R22.edge_stress(first, {"loads": loads})
        self.assertAlmostEqual(stressed["maximum_load_capacity_ratio"],
                               4*original["maximum_load_capacity_ratio"],
                               delta=1e-12)

    def test_variant_fingerprints_are_distinct_and_reproducible(self):
        fingerprints = []
        for variant in R.VARIANTS:
            a, f, info = R.fixture_variant(self.cfg, variant)
            b, g, repeated = R.fixture_variant(self.cfg, variant)
            fingerprints.append(info["fixture_fingerprint"])
            self.assertEqual(info, repeated)
            self.assertEqual(R.fixture_fingerprint(a), R.fixture_fingerprint(b))
            self.assertEqual(_flow_checksum(f), _flow_checksum(g))
        self.assertEqual(len(set(fingerprints)), len(R.VARIANTS))
        with self.assertRaises(ValueError):
            R.fixture_variant(self.cfg, "user_modified_demand")

    def test_all_four_hybrid_rankings_preserve_real_oracle_winners(self):
        net, flows, _ = R.fixture_variant(self.cfg, "combined_stress")
        settings = ClearingSettings(exponent=4)
        old = evaluate_r14(net, flows, settings, ())
        new_flows, future, delta, calls, alpha = R.R29.step_forward(
            net, flows, old, settings, "decay_sqrt", 1)
        with patch.object(R.R32, "evaluate_r14",
                          side_effect=AssertionError("Hybrid called R14")):
            rankings = R.R32.frozen_hybrid_rankings(net, old, future, 4)
        self.assertEqual(rankings["extra_r14_oracle_calls"], 0)
        self.assertGreater(rankings["independent_shortest_path_scans"], 0)
        for gid, row in rankings["groups"].items():
            self.assertEqual(row["four_market_winners"]["old_actual"],
                             old["target"][gid].destination)
            self.assertEqual(row["four_market_winners"]["new_actual"],
                             future["target"][gid].destination)
        R.R21.frozen_check(net, new_flows, future)

    def test_run_audits_nonbaseline_sellout_and_frozen_oracles(self):
        for variant in ("demand_mosaic", "supply_mosaic",
                        "capacity_quarter", "combined_stress"):
            with self.subTest(variant=variant):
                report = R.run_case(self.cfg, variant, "decay_sqrt",
                                    5, audit_every=2)
                self.assertEqual(report["financial_audits"][-1]["step"], 5)
                self.assertEqual(report["proposal_oracle_calls"], 6)
                self.assertEqual(report["additional_hybrid_oracle_calls"], 0)
                self.assertFalse(report["global_equilibrium_proven"])
                self.assertFalse(report["causal_inference_proven"])
                self.assertFalse(report["production_approved"])
                self.assertTrue(report["rows"][-1]["hybrid"]
                                ["mixed_quotes_are_not_economic_clearing_states"])

    def test_reference_r32_baseline_path_identical(self):
        for policy in R.POLICIES:
            current = R.run_case(self.cfg, "baseline", policy, 5,
                                 audit_every=2)
            previous = R.R32.run_case(self.cfg, policy, 5, audit_every=2)
            self.assertEqual(current["final_gap"], previous["final_gap"])
            self.assertEqual(current["final_flow_sha256"],
                             previous["final_flow_sha256"])
            self.assertEqual(current["proposal_oracle_calls"],
                             previous["proposal_oracles_used"])
            self.assertEqual(current["summary"]["market_switches"],
                             previous["summary"]["market_switches"])

    def test_deterministic_variant_prefix(self):
        small = R.run_case(self.cfg, "combined_stress",
                           "backtracking", 4, audit_every=2)
        long = R.run_case(self.cfg, "combined_stress",
                          "backtracking", 7, audit_every=2)
        self.assertEqual(small["rows"], long["rows"][:5])

    def test_partition_and_undefined_share_without_switches(self):
        record = {
            "summary": {"market_switches": 0,
                        "price_only_winner_is_actual": 0,
                        "transport_only_winner_is_actual": 0,
                        "price_only_full_new_winner_share": None,
                        "transport_only_full_new_winner_share": None}}
        s = R.cross_fixture_summary([record])
        self.assertIsNone(s["event_weighted_price_only_full_winner_share"])
        self.assertIsNone(s["minimum_nonempty_run_price_only_full_winner_share"])
        self.assertEqual(s["runs_without_market_switches"], 1)
        net, flows, _ = R.fixture_variant(self.cfg, "capacity_mosaic")
        settings = ClearingSettings(exponent=4)
        before = evaluate_r14(net, flows, settings, ())
        proposed, after, _, _, _ = R.R29.step_forward(
            net, flows, before, settings, "backtracking", 1)
        r = R.R32.frozen_hybrid_rankings(net, before, after, 4)["counts"]
        self.assertEqual(r["market_switches"],
                         sum(r[k] for k in (
                             "pair_both_individual_cross",
                             "pair_price_only_cross",
                             "pair_transport_only_cross",
                             "pair_neither_individual_cross")))
        self.assertEqual(r["market_switches"],
                         r["price_log_effect_dominant"] +
                         r["transport_log_effect_dominant"])

    def test_original_r21_references_remain_unchanged(self):
        for policy in ("decay_sqrt", "backtracking"):
            self.assertTrue(R.R24.check_r21_reference(
                self.cfg, policy, 6))


if __name__ == "__main__":
    unittest.main()
