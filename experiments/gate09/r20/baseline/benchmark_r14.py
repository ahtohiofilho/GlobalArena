"""Gate09 R14 paired observational benchmarks of unchanged R13 and R14 evaluator.

The benchmark checks checkpoint JSON, financial conservation and flow SHA
before recording timings. No comparison may silently accept economic drift.
Fixed-iteration sweep only; never implies time-to-convergence.
"""
from __future__ import annotations
from pathlib import Path
from statistics import median
from math import isfinite
import json
import platform
import sys
import tracemalloc

from benchmark_r13 import CASES
from multimarket_r13 import run_scenario as baseline_run
from multimarket_r14 import run_scenario as optimized_run
from audit_r13 import verify_r13_snapshots

ROOT = Path(__file__).resolve().parent


def canonical(obj):
    return json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(',', ':'))


def matching_results(baseline, cached):
    compare = ('nodes','edges','products','markets','producer_groups','civilizations',
               'updates','settlement_batches','initial_gap','final_gap',
               'initial_max_group_gap','final_max_group_gap',
               'final_flow_sha256','final_gross_micro','final_credits_micro',
               'total_financial_batches_reconciled','all_supply_fully_sold',
               'all_markets_priced','f059_financial_tie_policy_ratified',
               'clearing_convergence_certified','csharp_binary_parity_executed',
               'game_runtime_benchmarked','production_approved', 'scenario')
    for key in compare:
        if canonical(baseline[key]) != canonical(cached[key]):
            raise AssertionError('R14 changed a semantic field: '+key)
    if canonical(baseline['checkpoints']) != canonical(cached['checkpoints']):
        raise AssertionError('R14 altered an economic checkpoint')
    if 'witnesses' in baseline and canonical(baseline['witnesses']) != canonical(cached['witnesses']):
        raise AssertionError('R14 altered serialized flow provenance')
    if baseline['final_gross_micro'] != baseline['final_credits_micro']:
        raise AssertionError('Financial conservation invalid')
    if not cached['r14_cache_statistics']['snapshots']==cached['settlement_batches']:
        raise AssertionError('Cache lifetime mismatched checkpoint count')
    if cached['r14_cache_statistics']['physical_edge_cost_calculations'] != cached['edges']*cached['settlement_batches']:
        raise AssertionError('Physical costs were not computed once per edge per snapshot')
    return True


def run_case(label, cfg, samples):
    # Same paired benchmark methodology; order alternates to mitigate warming.
    if label in ('small','medium'):
        matching_results(baseline_run(cfg,capture_details=True),
                         optimized_run(cfg,oracle_mode='r14_cached',capture_details=True))
    elapsed={'baseline':[], 'r14_cached':[]}
    oracle={'baseline':[], 'r14_cached':[]}
    gross=[]
    for i in range(samples):
        if i%2:
            new=optimized_run(cfg,oracle_mode='r14_cached',capture_details=True)
            old=baseline_run(cfg,capture_details=True)
        else:
            old=baseline_run(cfg,capture_details=True)
            new=optimized_run(cfg,oracle_mode='r14_cached',capture_details=True)
        matching_results(old,new)
        for name,value in (('baseline',old),('r14_cached',new)):
            elapsed[name].append(value['overall_elapsed_ms_observed'])
            oracle[name].append(value['stage_total_ms_observed']['f072_oracle_and_regret_ms'])
        gross.append((new['final_flow_sha256'],new['final_gross_micro']))
    if len(set(gross))!=1:
        raise AssertionError('Non-deterministic state across paired runs')
    oracle_median={name:median(xs) for name,xs in oracle.items()}
    elapsed_median={name:median(xs) for name,xs in elapsed.items()}
    return {
        'case':label, 'nodes':old['nodes'],'edges':old['edges'],
        'products':old['products'], 'markets':old['markets'],
        'producer_groups':old['producer_groups'],
        'sample_count_per_variant':samples,
        'baseline_total_samples_ms':[round(v,3) for v in elapsed['baseline']],
        'optimized_total_samples_ms':[round(v,3) for v in elapsed['r14_cached']],
        'baseline_oracle_samples_ms':[round(v,3) for v in oracle['baseline']],
        'optimized_oracle_samples_ms':[round(v,3) for v in oracle['r14_cached']],
        'baseline_total_median_ms':round(elapsed_median['baseline'],3),
        'optimized_total_median_ms':round(elapsed_median['r14_cached'],3),
        'baseline_oracle_median_ms':round(oracle_median['baseline'],3),
        'optimized_oracle_median_ms':round(oracle_median['r14_cached'],3),
        'observed_total_speedup':round(elapsed_median['baseline']/elapsed_median['r14_cached'],3),
        'observed_oracle_speedup':round(oracle_median['baseline']/oracle_median['r14_cached'],3),
        'end_to_end_checkpoints_byte_equivalent':True,
        'gross_micro':old['final_gross_micro'],
        'credits_micro':old['final_credits_micro'],
        'final_flow_sha256':old['final_flow_sha256'],
        'final_f072_gap':old['final_gap'],
        'final_max_group_gap':old['final_max_group_gap'],
        'cache_stats':new['r14_cache_statistics'],
    }


def memory_profile():
    cfg = CASES[0][1]  # small scenario; separately traced, not timing comparable
    from dataclasses import replace
    cfg = replace(cfg, iterations=1)
    result={}
    for name,fn,opts in (
        ('baseline',baseline_run,{}),
        ('r14_cached',optimized_run,{'oracle_mode':'r14_cached'})):
        tracemalloc.start()
        try:
            x=fn(cfg,**opts)
            _,peak=tracemalloc.get_traced_memory()
        finally:
            tracemalloc.stop()
        result[name]={'traced_peak_bytes':peak,'final_flow_sha256':x['final_flow_sha256']}
    if result['baseline']['final_flow_sha256']!=result['r14_cached']['final_flow_sha256']:
        raise AssertionError('Memory profiling changed economic result')
    return {'scope':'Python tracemalloc allocations, not process RSS or C# memory',
            'excluded_from_uninstrumented_benchmark_medians':True,'variants':result}


def run():
    cases=[]
    for label,cfg,_ in CASES:
        n=3 if label in ('small','medium') else 2
        result=run_case(label,cfg,n)
        cases.append(result)
        print(f"{label:7} N={result['nodes']:4} groups={result['producer_groups']:3} "
              f"total baseline/optimized={result['baseline_total_median_ms']:.1f}/{result['optimized_total_median_ms']:.1f}ms "
              f"speedup={result['observed_total_speedup']:.2f}x "
              f"oracle={result['observed_oracle_speedup']:.2f}x")
    witness=optimized_run(CASES[0][1],oracle_mode='r14_cached',capture_witness=True)
    original=baseline_run(CASES[0][1],capture_witness=True)
    matching_results(original,witness)
    audit=verify_r13_snapshots(witness)
    memory=memory_profile()
    evidence={
        'schema':'GA_GATE09_R14_SNAPSHOT_CACHE_AB_PARITY_V1',
        'date':'2026-11-12',
        'environment':{'python':sys.version.split()[0], 'platform':platform.platform()},
        'baseline_tests':581,'new_tests':33,'total_tests':614,
        'measurements':cases,
        'independent_small_graph_snapshot_replay':audit,
        'memory_profile':memory,
        'cache_policy':'reconstructed for each frozen evaluation; never survives a flow update',
        'prices_cached':False,'flows_cached':False,'settlements_cached':False,
        'economic_identity':'byte-equal JSON checkpoint rows, identical SHA256 flows and financial batch totals',
        'benchmarks_measure':'fixed-iteration cycle, NOT time to equilibrium',
        'original_gate08_replay_executed':False,
        'csharp_python_binary_parity_executed':False,
        'f059_policy_ratified':False,
        'equilibrium_globally_proven':False,
        'github_modified':False,
        'production_approved':False,
    }
    (ROOT/'evidencia-gate09-r14.json').write_text(json.dumps(evidence,ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
    (ROOT/'trilha-r14-paridade-pequena.json').write_text(json.dumps(witness,ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
    print('R14 paired A/B checkpoints and independent small-graph audit: PASS')
    print('C#/Python binary parity, Gate08 original replay, F059 and production: PENDING')
    return evidence

if __name__=='__main__':run()
