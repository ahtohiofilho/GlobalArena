"""Gate 09 R13: bounded multi-good simultaneous clearing *sweep* laboratory.

Executes real F-072 route/market evaluation, damped flow updates, repricing,
congestion, and F-084F-style destination settlement at each checkpoint.
Uses synthetic logical Goldberg graphs. Does NOT establish market convergence,
financial tie policy F-059, .NET parity, or game runtime performance.
"""
from __future__ import annotations
from dataclasses import dataclass
from math import isfinite
from time import perf_counter
from hashlib import sha256
from pathlib import Path
import json

from goldberg_r8 import logical_goldberg
from gate09 import Edge, Group, Market, Network, Column, MICRO
from clearing_r5 import ClearingSettings, _evaluate, _mix, _flow_checksum
from finance_fixed import frozen_float_flows_to_raw, settle


@dataclass(frozen=True)
class R13Scenario:
    m: int = 2
    products: int = 2
    markets_per_product: int = 3
    groups_per_product: int = 6
    civilizations: int = 3
    iterations: int = 2
    exponent: int = 4
    alpha: float = 0.125

    def __post_init__(self):
        for name in ('m','products','markets_per_product','groups_per_product','civilizations','iterations','exponent'):
            if type(getattr(self,name)) is not int:
                raise ValueError(f'Invalid integer parameter {name}')
        if (not 1 <= self.m <= 30 or not 1 <= self.products <= 12 or
            not 1 <= self.markets_per_product <= 12 or
            not self.markets_per_product <= self.groups_per_product <= 30 or
            not 2 <= self.civilizations <= 12 or not 0 <= self.iterations <= 10 or
            self.exponent not in (3,4,5) or
            self.products*self.groups_per_product > 240 or
            self.products*self.markets_per_product > 120 or
            type(self.alpha) not in (int,float) or not isfinite(self.alpha) or
            not 0 < self.alpha < 1):
            raise ValueError('Invalid/budget-exceeding R13 scenario')


def construct_scenario(cfg: R13Scenario):
    """Deterministic fixture, with >=1 initial seller per product-market."""
    n, lines = logical_goldberg(cfg.m)
    edges=[]
    for eid,a,b in lines:
        if eid % 7 == 0:
            holders=()
            tariff=0
        elif eid % 5 == 0:
            own=eid % cfg.civilizations
            other=(own+1) % cfg.civilizations
            holders=((own,250000),(other,750000))
            tariff=90000+(eid%3)*20000
        else:
            holders=((eid%cfg.civilizations,MICRO),)
            tariff=90000+(eid%3)*20000
        edges.append(Edge(eid,a,b,c_free=0.8+0.1*(eid%5),
                    capacity=float(12+4*(eid%7)),tariff_micro=tariff,owners=holders))
    markets=[]; groups=[]
    market_nodes={}
    for product in range(cfg.products):
        positions=tuple(sorted({((j+1)*n//(cfg.markets_per_product+1)+product*7)%n
                                for j in range(cfg.markets_per_product)}))
        if len(positions)!=cfg.markets_per_product:
            raise AssertionError('Deterministic market nodes unexpectedly collide')
        market_nodes[product]=positions
        for j,node in enumerate(positions):
            markets.append(Market(node,product,4.0+float((product+j)%4)))
        for j in range(cfg.groups_per_product):
            gid=1+product*cfg.groups_per_product+j
            origin=((j+1)*n//(cfg.groups_per_product+1)+product*11)%n
            groups.append(Group(gid,origin,product,j%cfg.civilizations,
                                1.0+0.25*(j%4)))
    net=Network(edges,groups,markets)
    initial={}
    for g in sorted(groups,key=lambda x:x.id):
        j=(g.id-1)%cfg.groups_per_product
        dest=market_nodes[g.commodity][j%cfg.markets_per_product]
        _,paths,_=net.dijkstra(g.origin,g.seller,{})
        col=Column(g.id,dest,paths[dest])
        net.validate_column(col)
        initial[col]=g.supply
    _,quantities,prices=net.snapshots(initial)
    if len(quantities)!=len(markets) or any(not isfinite(p) for p in prices.values()):
        raise AssertionError('Every reachable market must be seeded with real production')
    return net,initial


def _timer(run, measures, key):
    start=perf_counter()
    result=run()
    measures[key]=measures.get(key,0.0)+(perf_counter()-start)*1000.0
    return result


def run_scenario(cfg: R13Scenario, *, capture_details:bool=False, capture_witness:bool=False):
    """End-to-end, measured, bounded clearings with real settlement at EACH state.

    The update uses R5's deterministic F072 target and damped update; it is a
    stress harness, not a proven converged equilibrium. F059 is unresolved.
    """
    measures={}
    net,flows=_timer(lambda:construct_scenario(cfg),measures,'fixture_and_initial_routing_ms')
    settings=ClearingSettings(iterations=max(1,cfg.iterations),exponent=cfg.exponent)
    rows=[]
    witnesses=[]
    for step in range(cfg.iterations+1):
        loads,q,prices=_timer(lambda:net.snapshots(flows),measures,'reprice_and_load_snapshot_ms')
        if set(q)!=set(net.markets) or any(not isfinite(v) for v in prices.values()):
            raise AssertionError('Market has no defined positive quote')
        observation=_timer(lambda:_evaluate(net,flows,settings,()),measures,'f072_oracle_and_regret_ms')
        inputs=_timer(lambda:frozen_float_flows_to_raw(net,flows),measures,'float_to_fixed_and_provenance_ms')
        markets,sales,edge_contracts=inputs
        financial=_timer(lambda:settle(markets,sales,edge_contracts),measures,'destination_settlement_ms')
        production_raw=sum(round(g.supply*MICRO) for g in net.groups.values())
        if sum(s.quantity_raw for s in sales)!=production_raw:
            raise AssertionError('Production sold must reconcile after quantization')
        if financial['total_gross_raw']!=financial['total_credits_raw']:
            raise AssertionError('Broken external-owner credit conservation')
        if any(item['gross_raw']<0 for item in financial['sales']):
            raise AssertionError('Negative gross')
        # Prices Q>0 are reconstructed at the fixed point market boundary.
        if any(key not in prices for key in net.markets):
            raise AssertionError('Market quote missing')
        row={'iteration':step,'f072_gap':observation['relative_gap'],
             'max_group_gap':observation['max_group_gap'],
             'unresolved_tie_group_count':len(observation['tie_groups']),
             'active_columns':observation['active_columns'],
             'raw_sale_count':len(sales),
             'sold_micro_units':production_raw,
             'gross_micro':financial['total_gross_raw'],
             'credits_micro':financial['total_credits_raw'],
             'flow_sha256':_flow_checksum(flows)}
        rows.append(row)
        if capture_witness:
            witnesses.append({'iteration':step,'flows':[[c.group_id,c.destination,list(c.edges),format(v,'.17g')]
                     for c,v in sorted(flows.items()) if v>0]})
        if step<cfg.iterations:
            flows=_timer(lambda:_mix(flows,net,observation['target'],cfg.alpha),
                         measures,'damped_f072_flow_update_ms')
    result={
        'scenario':dict(cfg.__dict__),
        'nodes':10*cfg.m*cfg.m+2,'edges':30*cfg.m*cfg.m,
        'products':cfg.products,'markets':len(net.markets),
        'producer_groups':len(net.groups),'civilizations':cfg.civilizations,
        'updates':cfg.iterations,'settlement_batches':cfg.iterations+1,
        'stage_total_ms_observed':{k:round(v,4) for k,v in sorted(measures.items())},
        'overall_elapsed_ms_observed':round(sum(measures.values()),4),
        'initial_gap':rows[0]['f072_gap'], 'final_gap':rows[-1]['f072_gap'],
        'initial_max_group_gap':rows[0]['max_group_gap'],
        'final_max_group_gap':rows[-1]['max_group_gap'],
        'final_flow_sha256':rows[-1]['flow_sha256'],
        'final_gross_micro':rows[-1]['gross_micro'],
        'final_credits_micro':rows[-1]['credits_micro'],
        'total_financial_batches_reconciled':len(rows),
        'all_supply_fully_sold':True,
        'all_markets_priced':True,
        'f059_financial_tie_policy_ratified':False,
        'clearing_convergence_certified':False,
        'only_fixed_iteration_sweep_benchmarked':True,
        'csharp_binary_parity_executed':False,
        'game_runtime_benchmarked':False,
        'production_approved':False,
    }
    if capture_details:result['checkpoints']=rows
    if capture_witness:
        result['checkpoints']=rows
        result['witnesses']=witnesses
    return result
