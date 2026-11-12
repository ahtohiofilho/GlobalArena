"""Gate09 R14 optimized observational end-to-end sweep.

This module preserves R13 original files and manifests byte-for-byte.
The legacy oracle remains available for A/B evaluation of the same fixture.
"""
from math import isfinite
from clearing_r5 import ClearingSettings, _evaluate, _mix, _flow_checksum
from finance_fixed import frozen_float_flows_to_raw, settle
from oracle_cache_r14 import evaluate_r14
from multimarket_r13 import R13Scenario, construct_scenario, _timer
from gate09 import MICRO


def run_scenario(cfg: R13Scenario, *, capture_details:bool=False, capture_witness:bool=False, oracle_mode:str="baseline"):
    """End-to-end, measured, bounded clearings with real settlement at EACH state.

    The update uses R5's deterministic F072 target and damped update; it is a
    stress harness, not a proven converged equilibrium. F059 is unresolved.
    """
    if oracle_mode not in ('baseline', 'r14_cached'):
        raise ValueError('Unknown F072 oracle engine')
    route_stats=[]
    measures={}
    net,flows=_timer(lambda:construct_scenario(cfg),measures,'fixture_and_initial_routing_ms')
    settings=ClearingSettings(iterations=max(1,cfg.iterations),exponent=cfg.exponent)
    rows=[]
    witnesses=[]
    for step in range(cfg.iterations+1):
        loads,q,prices=_timer(lambda:net.snapshots(flows),measures,'reprice_and_load_snapshot_ms')
        if set(q)!=set(net.markets) or any(not isfinite(v) for v in prices.values()):
            raise AssertionError('Market has no defined positive quote')
        observation=_timer(
            (lambda:_evaluate(net,flows,settings,())) if oracle_mode=='baseline' else
            (lambda:evaluate_r14(net,flows,settings,(),route_stats)),
            measures,'f072_oracle_and_regret_ms')
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
    if oracle_mode=='r14_cached':
        result['r14_cache_statistics']={
            'physical_edge_cost_calculations':sum(x['physical_edge_cost_calculations'] for x in route_stats),
            'seller_weight_tables':sum(x['seller_weight_tables'] for x in route_stats),
            'shortest_path_scans':sum(x['shortest_path_scans'] for x in route_stats),
            'snapshots':len(route_stats),
            'snapshot_scope':'cache discarded after each evaluate call',
        }
    if capture_details:result['checkpoints']=rows
    if capture_witness:
        result['checkpoints']=rows
        result['witnesses']=witnesses
    return result
