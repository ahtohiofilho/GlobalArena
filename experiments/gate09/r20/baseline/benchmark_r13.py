"""R13: observational bounded end-to-end multi-good clearing *sweep* benchmark.

No steady-state claim: all scenarios use predetermined update counts, not
an equilibrium tolerance. Uses Python R5/R2 + exact F-084F-style settlement.
"""
from __future__ import annotations
from pathlib import Path
from statistics import median
from time import perf_counter
from math import isfinite
import json, platform, sys, tracemalloc

from multimarket_r13 import R13Scenario,run_scenario
from audit_r13 import verify_r13_snapshots

ROOT=Path(__file__).resolve().parent
CASES=(
 ('small',R13Scenario(m=2,products=2,markets_per_product=3,groups_per_product=6,civilizations=3,iterations=3),3),
 ('medium',R13Scenario(m=4,products=3,markets_per_product=4,groups_per_product=8,civilizations=4,iterations=3),3),
 ('large',R13Scenario(m=8,products=4,markets_per_product=5,groups_per_product=10,civilizations=6,iterations=2),3),
 ('larger',R13Scenario(m=12,products=6,markets_per_product=6,groups_per_product=12,civilizations=8,iterations=2),2),
)


def _case(label,cfg,repeat):
    # One unmeasured warmup for the two smallest; larger cases already take
    # several seconds and are measured without warmup, explicitly disclosed.
    if label in ('small','medium'):run_scenario(cfg)
    runs=[run_scenario(cfg) for _ in range(repeat)]
    stable={(r['final_flow_sha256'],r['final_gross_micro'],r['final_credits_micro']) for r in runs}
    if len(stable)!=1:raise AssertionError('Non-deterministic economic result across timing repetitions')
    if any(r['final_gross_micro']!=r['final_credits_micro'] for r in runs):
        raise AssertionError('Financial conservation failed')
    per_stage=sorted({k for r in runs for k in r['stage_total_ms_observed']})
    medians={s:round(median(r['stage_total_ms_observed'].get(s,0.) for r in runs),3) for s in per_stage}
    totals=[r['overall_elapsed_ms_observed'] for r in runs]
    sample=runs[0]
    all_oracle=medians['f072_oracle_and_regret_ms']
    return {
       'label':label,'nodes':sample['nodes'],'edges':sample['edges'],
       'products':sample['products'],'markets':sample['markets'],
       'producer_groups':sample['producer_groups'],'civilizations':sample['civilizations'],
       'iterations':cfg.iterations,'financial_settlements_per_run':cfg.iterations+1,
       'sample_count':repeat,'warmup_applied':label in ('small','medium'),
       'total_elapsed_samples_ms':[round(t,3) for t in totals],
       'total_elapsed_median_ms':round(median(totals),3),
       'per_stage_median_observed_ms':medians,
       'oracle_share_percent_of_sum_of_stage_medians':round(100*all_oracle/sum(medians.values()),2),
       'initial_gap':sample['initial_gap'],'final_gap':sample['final_gap'],
       'initial_max_group_gap':sample['initial_max_group_gap'],
       'final_max_group_gap':sample['final_max_group_gap'],
       'final_gross_micro':sample['final_gross_micro'],
       'final_credits_micro':sample['final_credits_micro'],
       'final_flow_sha256':sample['final_flow_sha256'],
       'frozen_gap_tolerance_met':bool(sample['final_gap']<=1e-4 and sample['final_max_group_gap']<=1e-4),
       'equilibrium_convergence_proven':False,
       'financial_owner_tie_policy_ratified':False,
    }


def _memory_profile():
    # Python tracemalloc does not measure interpreter RSS or memory in C#.
    # This is a separate traced run: do not present its time as comparable
    # to untraced timing samples.
    cfg=R13Scenario(m=4,products=3,markets_per_product=4,
                    groups_per_product=8,civilizations=4,iterations=1)
    tracemalloc.start()
    try:
        r=run_scenario(cfg)
        _,peak=tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    return {'nodes':r['nodes'],'groups':r['producer_groups'],'iterations':cfg.iterations,
            'python_tracemalloc_peak_bytes':peak,
            'memory_scope':'traced Python allocations only; not full RSS or C#',
            'excluded_from_untraced_timing_medians':True}


def run():
    rows=[_case(label,cfg,n) for label,cfg,n in CASES]
    witness=run_scenario(CASES[0][1],capture_witness=True)
    replay=verify_r13_snapshots(witness)
    (ROOT/'trilha-r13-multimercados-pequena.json').write_text(
        json.dumps(witness,ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
    evidence={
        'schema':'GA_GATE09_R13_MULTIGOOD_BOUNDED_SWEEP_EVIDENCE_V1',
        'date':'2026-11-12',
        'environment':{'python':sys.version.split()[0],'platform':platform.platform()},
        'baseline_R12_tests':497,'new_R13_tests':84,'total_tests':581,
        'benchmarks':rows,'memory':_memory_profile(),
        'independent_small_graph_snapshot_audit':replay,
        'metric_interpretation':'observational fixed-budget clearing sweeps, not an equilibrium benchmark',
        'active_F072_contract':'P/(1+L+W)',
        'financial_projection':'F084F destination settlement, fixed-point Python port only',
        'F059_tie_policy_ratified':False,
        'all_production_sold':True,
        'global_equilibrium_proven':False,
        'three_step_temporal_stability_proven_for_benchmark':False,
        'CSharp_binary_parity_executed':False,
        'original_gate08_dataset_available':False,
        'gate08_exact_replay':False,
        'authoritative_world_modified':False,
        'github_modified':False,
        'production_approved':False,
    }
    (ROOT/'evidencia-gate09-r13.json').write_text(
        json.dumps(evidence,ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
    print('GA GATE09 R13 — multigood bounded F072 clearing + F084F per-snapshot settlement')
    for x in rows:
        print(x['label'],f"N={x['nodes']}",f"products={x['products']}",
              f"markets={x['markets']}",f"groups={x['producer_groups']}",
              f"med_ms={x['total_elapsed_median_ms']}",
              f"oracle_pct={x['oracle_share_percent_of_sum_of_stage_medians']}",
              f"gross={x['final_gross_micro']}",
              f"gap={x['final_gap']:.6f}")
    print('F059 OPEN | FIXED ITERATIONS, NO CONVERGENCE CLAIM | C# PARITY NOT EXECUTED | PRODUCTION BLOCKED')
    return evidence

if __name__=='__main__':run()
