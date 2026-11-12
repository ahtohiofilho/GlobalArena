#!/usr/bin/env python3
"""Gate09 R20 reproducible multi-good clearing sweeps (research only).

Uses unmodified R13/R14 Python economic lab, including F072 route evaluation,
price repricing, congestion, flow reallocation and F084F-style settlement.
No convergence, F059 resolution, C#-solver integration, or gameplay claims.
"""
from __future__ import annotations
import argparse
from dataclasses import asdict, replace
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
from statistics import median
import sys
import traceback
import zipfile

ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'baseline'))
from multimarket_r13 import R13Scenario, run_scenario as baseline_run
from multimarket_r14 import run_scenario as cached_run
from benchmark_r14 import matching_results
from audit_r13 import verify_r13_snapshots

CASES={
  'small':R13Scenario(m=2,products=2,markets_per_product=3,groups_per_product=6,civilizations=3,iterations=3),
  'medium':R13Scenario(m=4,products=3,markets_per_product=4,groups_per_product=8,civilizations=4,iterations=3),
  'large':R13Scenario(m=8,products=4,markets_per_product=5,groups_per_product=10,civilizations=6,iterations=2),
  'larger':R13Scenario(m=12,products=6,markets_per_product=6,groups_per_product=12,civilizations=8,iterations=2),
}


def digest(path:Path)->str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_manifest()->int:
    manifest=json.loads((ROOT/'MANIFEST-R20-SHA256.json').read_text(encoding='utf-8'))
    actual={p.relative_to(ROOT).as_posix():digest(p) for p in ROOT.rglob('*') if p.is_file()
            and not any(c in ('__pycache__','.git','bin','obj') for c in p.relative_to(ROOT).parts)
            and p.name!='MANIFEST-R20-SHA256.json'}
    if actual!=manifest['files']:
        missing=sorted(manifest['files'].keys()-actual.keys())
        extra=sorted(actual.keys()-manifest['files'].keys())
        different=sorted(k for k in actual.keys()&manifest['files'].keys() if actual[k]!=manifest['files'][k])
        raise ValueError(f'Manifest drift missing={missing} extra={extra} changed={different}')
    provenance=json.loads((ROOT/'baseline-provenance.json').read_text(encoding='utf-8'))
    for f,h in provenance['sourceFileSha256'].items():
        if actual['baseline/'+f]!=h:raise ValueError('Upstream baseline drift: '+f)
    return len(actual)


def verify_fiscal_and_gap(result:dict):
    rows=result['checkpoints']
    if len(rows)!=result['updates']+1:raise AssertionError('Wrong number of checkpoints')
    sold=rows[0]['sold_micro_units']
    if any(row['sold_micro_units']!=sold or row['gross_micro']!=row['credits_micro'] for row in rows):
        raise AssertionError('Lost supply or treasury mismatch')
    if not result['all_supply_fully_sold'] or not result['all_markets_priced']:
        raise AssertionError('Supply unallocated or market unpriced')
    if result['clearing_convergence_certified'] or result['production_approved'] or result['f059_financial_tie_policy_ratified']:
        raise AssertionError('Premature economic certification')
    return True


def pair(cfg:R13Scenario, *, replay=False):
    original=baseline_run(cfg,capture_details=True,capture_witness=replay)
    optimized=cached_run(cfg,oracle_mode='r14_cached',capture_details=True,capture_witness=replay)
    matching_results(original,optimized)
    verify_fiscal_and_gap(original)
    verify_fiscal_and_gap(optimized)
    audit=verify_r13_snapshots(optimized) if replay else None
    return original,optimized,audit


def run_case(label:str,cfg:R13Scenario,repeat:int=1,replay=False):
    measurements={'original':[],'cached':[],'oracle_original':[],'oracle_cached':[]}
    reference=None; auditor=None
    for sample in range(repeat):
        # Alternate order to reduce first-call bias; timings remain observational.
        if sample%2:
            cached=cached_run(cfg,oracle_mode='r14_cached',capture_details=True,capture_witness=replay and sample==0)
            original=baseline_run(cfg,capture_details=True,capture_witness=replay and sample==0)
        else:
            original=baseline_run(cfg,capture_details=True,capture_witness=replay and sample==0)
            cached=cached_run(cfg,oracle_mode='r14_cached',capture_details=True,capture_witness=replay and sample==0)
        matching_results(original,cached)
        verify_fiscal_and_gap(cached)
        if reference is not None and reference!=cached['checkpoints']:
            raise AssertionError('Repeated run changed economic snapshots')
        reference=cached['checkpoints']
        if replay and sample==0:auditor=verify_r13_snapshots(cached)
        measurements['original'].append(original['overall_elapsed_ms_observed'])
        measurements['cached'].append(cached['overall_elapsed_ms_observed'])
        for k,r in [('oracle_original',original),('oracle_cached',cached)]:
            measurements[k].append(r['stage_total_ms_observed']['f072_oracle_and_regret_ms'])
    rows=reference
    gaps=[r['f072_gap'] for r in rows]
    maxg=[r['max_group_gap'] for r in rows]
    return {'name':label,'cfg':asdict(cfg),'nodes':cached['nodes'],
       'products':cached['products'],'markets':cached['markets'],'groups':cached['producer_groups'],
       'snapshots':len(rows),'financial_conservation_all_snapshots':True,'all_supply_sold':True,
       'flow_checksum':cached['final_flow_sha256'],'gross_micro':cached['final_gross_micro'],
       'credits_micro':cached['final_credits_micro'],
       'gap_trajectory':gaps,'max_group_gap_trajectory':maxg,
       'tie_groups_trajectory':[r['unresolved_tie_group_count'] for r in rows],
       'gap_nonincreasing':all(b<=a+1e-12 for a,b in zip(gaps,gaps[1:])),
       'gap_initial':gaps[0],'gap_final':gaps[-1],
       'gap_below_1e_4':gaps[-1]<=1e-4 and maxg[-1]<=1e-4,
       'original_total_ms':measurements['original'],
       'cached_total_ms':measurements['cached'],
       'original_oracle_ms':measurements['oracle_original'],
       'cached_oracle_ms':measurements['oracle_cached'],
       'speedup_observational':median(measurements['original'])/median(measurements['cached']),
       'observed_stage_total_ms_cached':cached['stage_total_ms_observed'],
       'independent_replay':auditor,
       'checkpoint_hashes':[r['flow_sha256'] for r in rows],
       'solver_language':'Python',
       'equilibrium_certified':False}


def prefix_check(a:dict,b:dict):
    """Two runs share their initial state and step-size; longer run may not rewrite history."""
    shorter,longer=(a,b) if len(a['checkpoints'])<=len(b['checkpoints']) else (b,a)
    for step,row in enumerate(shorter['checkpoints']):
        if row!=longer['checkpoints'][step]:
            raise AssertionError(f'Prefix drift at checkpoint {step} despite identical parameters')
    return True


def exercise(profile:str):
    report=[]
    if profile=='smoke': specs=[('small',2,1,True)]
    elif profile=='ci': specs=[('small',4,2,True),('medium',3,2,False),('large',2,1,False)]
    else: specs=[('small',8,3,True),('medium',6,2,False),('large',3,2,False),('larger',2,1,False)]
    for label,iters,reps,replay in specs:
        cfg=replace(CASES[label],iterations=iters)
        data=run_case(label,cfg,repeat=reps,replay=replay)
        report.append(data)
        print(f"[{label}] PASS N={data['nodes']} products={data['products']} "
              f"gap={data['gap_initial']:.6f}->{data['gap_final']:.6f} "
              f"A/B speedup={data['speedup_observational']:.2f}x "
              f"replayed={bool(data['independent_replay'])}",flush=True)
    # Shared initial fixture, larger iteration budget must be prefix-consistent.
    cfg=replace(CASES['small'],iterations=2)
    short=cached_run(cfg,oracle_mode='r14_cached',capture_details=True)
    long=cached_run(replace(cfg,iterations=6),oracle_mode='r14_cached',capture_details=True)
    prefix_check(short,long)
    print('[prefix] PASS same checkpoint trajectory across iteration budgets',flush=True)
    # F059 tie sensitivity / damping: no requirement that each update decreases the gap.
    if profile=='smoke': alphas=[0.125]
    else:alphas=[0.0625,0.125,0.25]
    sensitivity=[]
    for alpha in alphas:
        cfg=replace(CASES['small'],iterations=6,alpha=alpha)
        state=cached_run(cfg,oracle_mode='r14_cached',capture_details=True)
        verify_fiscal_and_gap(state)
        sensitivity.append({'alpha':alpha,'gaps':[r['f072_gap'] for r in state['checkpoints']],
             'tie_counts':[r['unresolved_tie_group_count'] for r in state['checkpoints']],
             'final_flow_sha256':state['final_flow_sha256'],
             'all_snapshots_financially_reconciled':True,
             'reaches_gap_target':state['final_gap']<=1e-4 and state['final_max_group_gap']<=1e-4})
    print('[damping] PASS sensitivity recorded, no convergence asserted',flush=True)
    return {'cases':report,'sensitivity':sensitivity,'iteration_prefix_consistent':True}


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--profile',choices=['smoke','ci','extended'],default='ci')
    p.add_argument('--output',type=Path,help='Where to save the ZIP and JSON audit (default Downloads)')
    args=p.parse_args()
    out=(args.output or Path.home()/'Downloads').resolve();out.mkdir(parents=True,exist_ok=True)
    status={'schema':'GA_GATE09_R20_BOUNDED_MULTIGOOD_PERF_AND_GAP_V1',
       'startedUtc':datetime.now(timezone.utc).isoformat(),
       'profile':args.profile,'platform':platform.platform(),'python':platform.python_version(),
       'productionApproved':False,'csharpIntegratedSolverBenchmarked':False,
       'f059FinancialTiePolicyRatified':False,'equilibriumConvergenceProven':False,
       'benchmarkInterpretation':'fixed-budget Python simulation with repricing, congestion, routing, and financial settlement; not time-to-equilibrium or C# runtime',
       'result':'FAILED'}
    try:
        count=validate_manifest()
        print(f'[manifest] PASS {count} tracked source files',flush=True)
        status['manifestFiles']=count
        status.update(exercise(args.profile))
        status['result']='PASSED_R20_BOUNDED_SWEEPS'
    except Exception as ex:
        status['error']=repr(ex)
        status['traceback']=traceback.format_exc()
        print('[R20] FAILED:',repr(ex),flush=True)
    finally:
        status['finishedUtc']=datetime.now(timezone.utc).isoformat()
        stamp=datetime.now().strftime('%Y%m%d-%H%M%S')
        path=out/f'GA-Gate09-R20-{args.profile}-{stamp}.json'
        path.write_text(json.dumps(status,ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf-8')
        zippath=out/f'GA-Gate09-R20-Evidencias-{stamp}.zip'
        with zipfile.ZipFile(zippath,'w',zipfile.ZIP_DEFLATED) as z:z.write(path,path.name)
        print('STATUS:',status['result'])
        print('EVIDENCE ZIP:',zippath)
    return 0 if status['result']=='PASSED_R20_BOUNDED_SWEEPS' else 1

if __name__=='__main__':raise SystemExit(main())
