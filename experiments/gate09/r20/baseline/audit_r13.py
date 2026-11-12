"""Independent R13 *snapshot* audit: replays listed flows and F-084F settlements.

This audit deliberately does not assert convergence, provenance of run,
completeness of explored columns, F059 policy, or reproduction of every
unobserved rejected proposal. Small graph F072 checks use R2 oracle, not R5.
"""
from __future__ import annotations
from math import isfinite
from gate09 import Column, MICRO
from multimarket_r13 import R13Scenario, construct_scenario
from clearing_r5 import _flow_checksum
from finance_fixed import frozen_float_flows_to_raw, settle
from certificate_r2 import full_path_audit


def verify_r13_snapshots(record):
    if not isinstance(record,dict) or 'scenario' not in record or 'witnesses' not in record or 'checkpoints' not in record:
        raise ValueError('Missing R13 snapshot witnesses')
    cfg=R13Scenario(**record['scenario'])
    net,_=construct_scenario(cfg)
    witnesses=record['witnesses']
    checkpoints=record['checkpoints']
    if not isinstance(witnesses,list) or len(witnesses)!=cfg.iterations+1 or len(checkpoints)!=len(witnesses):
        raise ValueError('Incomplete R13 witness chain')
    if record['final_flow_sha256']!=checkpoints[-1]['flow_sha256']:
        raise ValueError('Final checksum mismatch')
    previous=None
    audited=[]
    for i,(w,expected) in enumerate(zip(witnesses,checkpoints)):
        if w.get('iteration')!=i or expected.get('iteration')!=i:
            raise ValueError('Wrong iteration ordinal')
        raw=w.get('flows')
        if not isinstance(raw,list):raise ValueError('Malformed flow witness')
        flows={}
        for entry in raw:
            if not isinstance(entry,list) or len(entry)!=4:raise ValueError('Wrong flow row')
            gid,dest,edges,value=entry
            if type(gid) is not int or type(dest) is not int or not isinstance(edges,list) or any(type(e) is not int for e in edges) or not isinstance(value,str):
                raise ValueError('Noncanonical row')
            v=float(value)
            if not isfinite(v) or v<=0 or format(v,'.17g')!=value:
                raise ValueError('Noncanonical numeric flow')
            col=Column(gid,dest,tuple(edges))
            if col in flows:raise ValueError('Duplicate column')
            flows[col]=v
        net.snapshots(flows)
        if _flow_checksum(flows)!=expected['flow_sha256']:
            raise ValueError('Witness hash differs from checkpoint')
        independent=full_path_audit(net,flows,cfg.exponent)
        if independent['unserved']:
            raise ValueError('Unserved market in certificate')
        for k in ('f072_gap','max_group_gap'):
            x=independent['frozen_gap' if k=='f072_gap' else k]
            if abs(expected[k]-x)>max(1e-9,1e-7*abs(x)):
                raise ValueError('Commercial gap differs from independent audit')
        markets,sales,edges=frozen_float_flows_to_raw(net,flows)
        financial=settle(markets,sales,edges)
        sold=sum(s.quantity_raw for s in sales)
        for k,measured in (('raw_sale_count',len(sales)),
                    ('sold_micro_units',sold),('gross_micro',financial['total_gross_raw']),
                    ('credits_micro',financial['total_credits_raw'])):
            if expected[k]!=measured:raise ValueError('Financial witness mismatch: '+k)
        if sold!=sum(round(g.supply*MICRO) for g in net.groups.values()):
            raise ValueError('Production loss')
        audited.append(expected['flow_sha256'])
        previous=flows
    if record['final_gross_micro']!=checkpoints[-1]['gross_micro'] or record['final_credits_micro']!=checkpoints[-1]['credits_micro']:
        raise ValueError('Incorrect final accounting')
    return {'audited_snapshots':len(audited),'all_batches_financially_reconciled':True,
            'all_frozen_gap_values_independently_recomputed':True,
            'all_production_sold':True,'last_sha256':audited[-1],
            'trajectory_completeness_certified':False,'F059_policy_ratified':False,
            'global_equilibrium_proven':False,'production_approved':False}
