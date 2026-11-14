from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r35 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14


class R35ConservativeDirectionalContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self, regime="original", factor=1.):
        net, flows, meta = R.R34.capacity_fixture(
            self.cfg, regime, factor)
        settings = ClearingSettings(exponent=4)
        observed = evaluate_r14(net, flows, settings, ())
        return net, flows, settings, observed

    def direction(self, regime="original", factor=1.):
        net, flows, settings, observed = self.frozen(regime, factor)
        directions, scans = R.select_directions(net, flows, observed)
        self.assertGreater(len(directions), 0)
        return net, flows, settings, observed, directions[0], scans

    def test_frozen_r20_sha256_manifest_16_files(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_capacity_cases_cover_stress_boundary_and_baseline(self):
        self.assertEqual(tuple(sorted(R.FACTORS)), R.FACTORS)
        self.assertIn(.15, R.FACTORS)
        self.assertIn(.25, R.FACTORS)
        self.assertIn(.40, R.FACTORS)
        self.assertIn(.50, R.FACTORS)
        self.assertIn(1., R.FACTORS)
        self.assertIn(4., R.FACTORS)
        self.assertEqual(R.REGIMES, R.R34.REGIMES)

    def test_selected_direction_uses_real_positive_source_and_other_market(self):
        net, flows, settings, observed, direction, scans = self.direction()
        group = net.groups[direction["group"]]
        self.assertEqual(direction["source"].group_id, group.id)
        self.assertEqual(direction["sink"].group_id, group.id)
        self.assertNotEqual(direction["source"].destination,
                            direction["sink"].destination)
        self.assertGreater(direction["source_amount"], 0)
        self.assertGreater(scans, 0)
        net.validate_column(direction["source"])
        net.validate_column(direction["sink"])
        self.assertTrue(direction["chosen_from_highest_regret"])

    def test_signed_transfer_preserves_each_group_supply_and_input_hash(self):
        net, flows, settings, observed, direction, _ = self.direction()
        source, sink = direction["source"], direction["sink"]
        original = _flow_checksum(flows)
        candidate = R.transfer(net, flows, source, sink, 0.02)
        reversed_direction = R.transfer(net, candidate, source, sink, -0.01)
        for changed in (candidate, reversed_direction):
            net.snapshots(changed)
            for gid, group in net.groups.items():
                sold = sum(amount for col, amount in changed.items()
                           if col.group_id == gid)
                self.assertAlmostEqual(sold, group.supply, delta=1e-12)
        self.assertEqual(_flow_checksum(flows), original)
        with self.assertRaises(ValueError):
            R.transfer(net, flows, source, sink, 1e9)
        with self.assertRaises(ValueError):
            R.transfer(net, flows, source, source, .001)

    def test_transferred_quantity_moves_between_exactly_two_markets(self):
        net, flows, settings, observed, direction, _ = self.direction()
        source, sink = direction["source"], direction["sink"]
        moved = .01
        next_flows = R.transfer(net, flows, source, sink, moved)
        old_q = observed["quantities"]
        _, new_q, new_price = net.snapshots(next_flows)
        commodity = net.groups[source.group_id].commodity
        self.assertAlmostEqual(
            new_q[(source.destination, commodity)] -
            old_q[(source.destination, commodity)], -moved, delta=1e-12)
        self.assertAlmostEqual(
            new_q[(sink.destination, commodity)] -
            old_q[(sink.destination, commodity)], moved, delta=1e-12)
        for key, market in net.markets.items():
            self.assertAlmostEqual(new_price[key],
                                   math.sqrt(market.demand/new_q[key]),
                                   delta=1e-12)

    def test_analytic_price_slope_is_always_negative_for_two_markets(self):
        net, flows, settings, observed, direction, _ = self.direction()
        source, sink = direction["source"], direction["sink"]
        cache = FrozenRouteCache(net, observed["loads"], 4)
        result = R.local_analytic_slopes(
            net, observed, source, sink, 4, cache)
        self.assertLess(result["price"], 0)
        self.assertAlmostEqual(result["price"] +
                               result["transport"],
                               result["total_score"], delta=1e-12)
        self.assertEqual(result["source_sink_edge_incidence"],
                         sorted(result["source_sink_edge_incidence"],
                                key=lambda row: row["edge_id"]))

    def test_five_r14_calls_charged_and_flows_unchanged(self):
        net, flows, settings, observed, direction, _ = self.direction()
        original = _flow_checksum(flows)
        with patch.object(R, "evaluate_r14",
                          wraps=R.evaluate_r14) as oracle:
            diagnostic = R.inspect_direction(
                net, flows, observed, settings, direction)
            self.assertEqual(oracle.call_count, 5)
        self.assertEqual(diagnostic["additional_r14_oracle_calls"], 5)
        self.assertEqual(len(diagnostic["epsilons"]), 2)
        self.assertEqual([e["epsilon_fraction_of_interior_shift"]
                          for e in diagnostic["epsilons"]],
                         list(R.EPS_FRACTIONS))
        self.assertEqual(diagnostic["original_flow_sha256"], original)
        self.assertEqual(_flow_checksum(flows), original)
        self.assertGreater(diagnostic["extra_shortest_path_scans"], 0)
        self.assertTrue(diagnostic["no_flow_committed_from_probes"])

    def test_local_sqrt_price_slope_matches_centered_difference(self):
        net, flows, settings, observed, direction, _ = self.direction()
        diag = R.inspect_direction(
            net, flows, observed, settings, direction)
        for width in diag["epsilons"]:
            self.assertAlmostEqual(
                width["centered_fd_log_relative_market_score"],
                width["centered_fd_log_relative_price"] +
                width["centered_fd_log_relative_transport_score"],
                delta=1e-9)
            self.assertLessEqual(
                width["abs_price_slope_error"],
                R.PRICE_SLOPE_TOL*max(
                    1, abs(diag["analytic_slopes_at_anchor"]["price"])))
            self.assertTrue(width["quantity_price_identity_unchanged"])

    def test_transport_derivative_is_verified_only_on_smooth_path(self):
        net, flows, settings, observed, direction, _ = self.direction(
            "heterogeneous_D_and_supply", 0.15)
        diag = R.inspect_direction(
            net, flows, observed, settings, direction)
        for width in diag["epsilons"]:
            if not width["canonical_market_path_changed"]:
                self.assertLessEqual(
                    width["abs_transport_slope_error"],
                    R.TRANSPORT_SLOPE_TOL*max(
                        1, abs(diag["analytic_slopes_at_anchor"]["transport"])))
            else:
                self.assertTrue(
                    width["transport_derivative_not_certified_if_path_changed"])

    def test_each_probe_preserves_full_sale_and_f084f_money(self):
        net, flows, settings, observed, direction, _ = self.direction()
        source, sink = direction["source"], direction["sink"]
        center = R.transfer(net, flows, source, sink, .02)
        plus = R.transfer(net, center, source, sink, .001)
        minus = R.transfer(net, center, source, sink, -.001)
        for allocation in (center, plus, minus):
            self.assertTrue(R.R33.verify_price_law(
                net, evaluate_r14(net, allocation, settings, ())))
            result = R.R21.settlement_check(net, allocation)
            self.assertEqual(result["gross_micro"], result["credits_micro"])

    def test_checkpoint_reports_only_bounded_targeted_directions(self):
        net, flows, settings, observed = self.frozen()
        p = R.inspect_checkpoint(net, flows, observed, settings, 0)
        self.assertLessEqual(p["direction_count"], R.GROUPS_PER_CHECKPOINT)
        self.assertEqual(p["paid_extra_r14_oracle_calls"],
                         5*p["direction_count"])
        self.assertGreater(p["independent_shortest_path_scans"], 0)
        self.assertTrue(p["selection_is_deterministic_high_regret_not_random"])

    def test_r34_reference_replay_and_policy_only_oracle_budget(self):
        result = R.run_case(self.cfg, "original", 1., "decay_sqrt",
                            5, audit_every=2)
        baseline = R.R34.run_case(self.cfg, "original", 1.,
                                  "decay_sqrt", 5, audit_every=2)
        self.assertEqual(result["final_flow_sha256"],
                         baseline["final_flow_sha256"])
        self.assertEqual(result["final_gap"],
                         baseline["final_global_gap"])
        self.assertEqual(result["policy_r14_calls"],
                         baseline["policy_r14_calls"])
        self.assertEqual(result["conservative_probe_r14_calls"],
                         5*result["number_of_probed_directions"])
        self.assertEqual(result["total_r14_calls"],
                         result["policy_r14_calls"] +
                         result["conservative_probe_r14_calls"])
        self.assertEqual(result["financial_audits"][-1]["step"], 5)
        self.assertFalse(result["global_equilibrium_proven"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["production_approved"])

    def test_backtracking_replay_and_prefix_determinism(self):
        short = R.run_case(self.cfg, "original", .25,
                           "backtracking", 4, audit_every=2)
        long = R.run_case(self.cfg, "original", .25,
                          "backtracking", 7, audit_every=2)
        self.assertEqual(short["rows"], long["rows"][:5])
        self.assertGreaterEqual(short["policy_r14_calls"], 5)
        self.assertTrue(R.R24.check_r21_reference(
            self.cfg, "backtracking", 6))

    def test_stressed_fixture_does_not_change_D_Q_pricing(self):
        net, flows, settings, observed = self.frozen(
            "heterogeneous_D_and_supply", 0.15)
        self.assertTrue(R.R33.verify_price_law(net, observed))
        record = R.run_case(self.cfg, "heterogeneous_D_and_supply",
                            .15, "decay_sqrt", 4, audit_every=2)
        self.assertEqual(record["financial_audits"][-1]["step"], 4)
        self.assertFalse(record["causality_proven"])

    def test_invalid_diagnostic_selection_and_runs(self):
        net, flows, settings, observed = self.frozen()
        with self.assertRaises(ValueError):
            R.select_directions(net, flows, observed, limit=0)
        with self.assertRaises(ValueError):
            R.run_case(self.cfg, "original", .333,
                       "decay_sqrt", 4)
        with self.assertRaises(ValueError):
            R.run_case(self.cfg, "original", .25,
                       "unknown_solver", 4)

    def test_aggregate_counts_are_exact_and_not_statistical_samples(self):
        first = R.run_case(
            self.cfg, "original", 1., "decay_sqrt", 3, audit_every=2)
        another = R.run_case(
            self.cfg, "heterogeneous_D_and_supply", .25,
            "decay_sqrt", 3, audit_every=2)
        total = R.aggregate([first, another])
        self.assertEqual(total["total_runs"], 2)
        self.assertEqual(total["total_probed_directions"],
                         first["number_of_probed_directions"] +
                         another["number_of_probed_directions"])
        self.assertEqual(total["probe_r14_calls"],
                         first["conservative_probe_r14_calls"] +
                         another["conservative_probe_r14_calls"])
        self.assertTrue(total["not_random_sampling_not_statistical_independence"])


if __name__=="__main__":
    unittest.main()
