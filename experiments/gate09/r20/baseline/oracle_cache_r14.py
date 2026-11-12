"""R14 deterministic per-snapshot routing cache for the F-072 laboratory.

The snapshot cache is ephemeral: it MUST be rebuilt after any flow/load update.
It only caches physical edge impedance, seller-specific external tariff weights,
and all-destination shortest-path results by (origin, seller). It never caches
market prices, flow provenance, financial credits, or tie policy across states.

The two-phase route routine intentionally reproduces the R2 path_audit's
floating-point operations, tolerance and canonical (hop_count, IDs) choices.
No production authority or algorithmic assertion of global convergence.
"""
from __future__ import annotations
from math import inf, isfinite
import heapq

from gate09 import Column
from certificate_r2 import ABS_TOL, REL_TOL, near
from clearing_r5 import ClearingIncomplete


class FrozenRouteCache:
    def __init__(self, net, loads, exponent):
        if exponent not in (3, 4, 5):
            raise ValueError('Unapproved exponent')
        self.net = net
        self.exponent = exponent
        self.loads = loads   # contract: caller never mutates this ephemeral snapshot
        self._physical = {}
        self._weight_by_seller = {}
        self._routes = {}
        self.shortest_path_scans = 0
        self.weight_tables = 0
        for eid, edge in net.edges.items():
            cost = edge.congestion_cost(loads.get(eid, 0), exponent)
            if cost <= 0 or not isfinite(cost):
                raise ValueError('Invalid physical cost')
            self._physical[eid] = cost

    def weights(self, seller):
        if seller not in self._weight_by_seller:
            weights = {}
            for eid, edge in self.net.edges.items():
                # SAME operation/order as path_audit and _evaluate in the baseline.
                w = self._physical[eid] + float(edge.weight_for(seller))
                if w <= 0 or not isfinite(w):
                    raise ValueError('Invalid generalized edge weight')
                weights[eid] = w
            self._weight_by_seller[seller] = weights
            self.weight_tables += 1
        return self._weight_by_seller[seller]

    def paths(self, origin, seller):
        key = (origin, seller)
        if key in self._routes:
            return self._routes[key]
        weights = self.weights(seller)
        adjacency = self.net.adj
        # Exact-order counterpart to certificate_r2.path_audit().
        distance = {origin: 0.0}
        heap = [(0.0, origin)]
        while heap:
            d, u = heapq.heappop(heap)
            if d > distance[u] + ABS_TOL:
                continue
            for v, eid in adjacency.get(u, ()):
                cost = d + weights[eid]
                if cost < distance.get(v, inf) - max(ABS_TOL, REL_TOL * abs(cost)):
                    distance[v] = cost
                    heapq.heappush(heap, (cost, v))
        canon = {origin: ()}
        route_ways = {origin: 1}
        for u in sorted(distance, key=lambda x: (distance[x], x)):
            if u not in canon:
                continue
            for v, eid in adjacency.get(u, ()):
                if v not in distance or distance[u] >= distance[v]:
                    continue
                if near(distance[u] + weights[eid], distance[v]):
                    candidate = canon[u] + (eid,)
                    if v not in canon or (len(candidate), candidate) < (len(canon[v]), canon[v]):
                        canon[v] = candidate
                    route_ways[v] = min(2, route_ways.get(v, 0) + route_ways[u])
        result = (distance, canon, route_ways)
        self._routes[key] = result
        self.shortest_path_scans += 1
        return result

    def counters(self):
        return {'physical_edge_cost_calculations': len(self._physical),
                'seller_weight_tables': self.weight_tables,
                'shortest_path_scans': self.shortest_path_scans,
                'distinct_source_seller_pairs': len(self._routes)}


def evaluate_r14(net, flows, cfg, unreachable, stats_sink=None):
    """Economically equivalent to clearing_r5._evaluate for the SAME snapshot.

    This preserves the original group and flow iteration order, F072 pricing,
    route multiplicity flagging, and financial ownership semantics. Never
    mixes flows, and therefore cannot reuse costs after a load change.
    """
    loads, q, prices = net.snapshots(flows)
    missing = tuple(sorted(k for k in net.markets if k not in unreachable and
                           (q.get(k, 0.) <= 0 or not isfinite(prices[k]))))
    if missing:
        raise ClearingIncomplete('A reachable market has undefined price')
    cache = FrozenRouteCache(net, loads, cfg.exponent)
    target = {}
    groups = {}
    ties = []
    log_regret_sum = 0.0
    best_weight = 0.0
    max_group_gap = 0.0
    flow_rows = sorted(flows.items())
    market_keys = sorted(net.markets)
    for gid, g in sorted(net.groups.items()):
        distances, paths, ways = cache.paths(g.origin, g.seller)
        weights = cache.weights(g.seller)
        options = []
        for key in market_keys:
            if key in unreachable or key[1] != g.commodity or key[0] not in paths:
                continue
            score = prices[key] / (1 + distances[key[0]])
            options.append((score, Column(gid, key[0], paths[key[0]]), ways[key[0]]))
        if not options:
            raise ClearingIncomplete('No finite priced destination available')
        options.sort(key=lambda t: (-t[0], t[1]))
        score, col, ways = options[0]
        target[gid] = col
        if ways > 1 or any(abs(score - v[0]) <= 1e-10 * max(1., abs(score)) for v in options[1:]):
            ties.append(gid)
        gap_sum = 0.0
        # Sorted order is deliberately unchanged; no unstable parallel reduction.
        for existing, amount in flow_rows:
            if existing.group_id != gid or amount <= 0:
                continue
            generalized = sum(weights[e] for e in existing.edges)
            actual = prices[(existing.destination, g.commodity)] / (1 + generalized)
            gap_sum += amount * max(0.0, score - actual)
        group_gap = gap_sum / (g.supply * score) if score > 0 else 0.0
        max_group_gap = max(max_group_gap, group_gap)
        log_regret_sum += gap_sum
        best_weight += g.supply * score
        groups[gid] = {'gap': group_gap, 'best': col, 'score': score,
                       'tie_or_equivalent_route': gid in ties}
    result = {
        'relative_gap': log_regret_sum / best_weight if best_weight else inf,
        'max_group_gap': max_group_gap,
        'active_columns': sum(int(v > 0) for v in flows.values()),
        'reachable_unpriced': missing,
        'structurally_unreachable_markets': unreachable,
        'tie_groups': tuple(ties),
        'groups': groups,
        'loads': loads, 'quantities': q, 'prices': prices,
        'target': target,
        'financial_F059_policy_resolved': False,
        'global_convergence_proven': False,
    }
    if not (isfinite(result['relative_gap']) and isfinite(result['max_group_gap'])):
        raise ClearingIncomplete('Non-finite regret')
    if stats_sink is not None:
        stats_sink.append(cache.counters())
    return result
