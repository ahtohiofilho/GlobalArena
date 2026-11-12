"""Gate 09 R5: experimental simultaneous competitive F-072 clearing.

Pure, non-authoritative. A producer is a price-taking infinitesimal fraction.
Prices, flows and edge congestion are re-evaluated after every common update.
No stored itineraries, money mutations, inventory or strategic group utility.
F-073, F-084 p/capacity and F-059 tie allocation remain unratified.
"""
from __future__ import annotations
from dataclasses import dataclass
from collections import defaultdict
from math import isfinite, inf, sqrt, log, exp
import hashlib
import json

from gate09 import Column, Network
from certificate_r2 import path_audit


class ClearingIncomplete(RuntimeError):
    """Numerical budget, physical availability, or precision blocked convergence."""


@dataclass(frozen=True)
class ClearingSettings:
    iterations: int = 4000
    tolerance: float = 1e-4
    price_tolerance: float = 1e-4
    load_tolerance: float = 1e-4
    seed_fraction: float = 1e-7
    step_size: float = 0.025
    step_decay: float = 0.66
    temperature_start: float = 0.012
    temperature_floor: float = 0.0003
    anneal_steps: int = 80
    stable_checkpoints: int = 3
    check_every: int = 25
    exponent: int = 4
    max_columns: int = 4096

    def __post_init__(self):
        if self.iterations < 0 or self.anneal_steps <= 0 or self.check_every <= 0 or self.stable_checkpoints <= 0 or self.max_columns <= 0:
            raise ValueError('Invalid iteration or work budget')
        for x in (self.tolerance, self.price_tolerance, self.load_tolerance, self.seed_fraction, self.step_size, self.step_decay, self.temperature_start, self.temperature_floor):
            if not isfinite(x): raise ValueError('Non-finite setting')
        if not (0 < self.tolerance < 1 and 0 < self.price_tolerance < 1 and
                0 < self.load_tolerance < 1 and 0 < self.seed_fraction < 0.01 and
                0 < self.step_size <= 1 and 0 < self.step_decay <= 1 and 0 < self.temperature_floor <= self.temperature_start < 1):
            raise ValueError('Invalid precision or search setting')
        if self.exponent not in (3,4,5):raise ValueError('Unapproved exponent not supported by laboratory')


def _reachable(net, exponent):
    """Reachability checked topologically, without using infinite hypothetical prices."""
    reachable=defaultdict(list)
    origins={}
    for gid,g in sorted(net.groups.items()):
        dist,paths,_=path_audit(net,g.origin,g.seller,{},exponent)
        origins[gid]=paths
        for key in sorted(net.markets):
            if key[1]==g.commodity and key[0] in paths:
                reachable[key].append(gid)
    unreachable=tuple(sorted(k for k in net.markets if not reachable[k]))
    if any(not any(k[1]==g.commodity and k[0] in origins[g.id] for k in net.markets) for g in net.groups.values()):
        raise ValueError('Producer has no accessible demand')
    return reachable,origins,unreachable


def seed_accessible_markets(net:Network, flows, cfg:ClearingSettings):
    """Minimal real mass for reachable Q=0 markets; no fabricated transaction price.

    Seeds are physically attributed, drawn from the same producer's existing
    fully-allocated supply, and use continuous valid edge paths. This changes
    the initial numerical guess, not the underlying economy.
    """
    net.snapshots(flows)
    reachable,origin_paths,unreachable=_reachable(net,cfg.exponent)
    result={c:float(v) for c,v in sorted(flows.items())}
    seeded=[]
    for market in sorted(reachable):
        if market in unreachable:continue
        _,q,_=net.snapshots(result)
        if q.get(market,0.)>0:continue
        gid=reachable[market][0]
        g=net.groups[gid]
        source=max((c for c in result if c.group_id==gid and result[c]>0),
                   key=lambda c:(result[c],-c.destination,tuple(-i for i in c.edges)),default=None)
        if source is None:raise ClearingIncomplete('No source mass for accessible market seeding')
        amount=min(g.supply*cfg.seed_fraction,result[source]*0.5)
        if amount<=0 or result[source]-amount==result[source]:
            raise ClearingIncomplete('Seed falls below floating point resolution')
        col=Column(gid,market[0],origin_paths[gid][market[0]])
        net.validate_column(col)
        if col==source: raise AssertionError('Empty market cannot already contain source')
        result[source]-=amount
        result[col]=result.get(col,0.)+amount
        seeded.append((market,gid,amount))
    net.snapshots(result)
    return result,tuple(seeded),unreachable


def _evaluate(net:Network, flows:dict[Column,float], cfg:ClearingSettings, unreachable:tuple):
    loads,q,prices=net.snapshots(flows)
    missing=tuple(sorted(k for k in net.markets if k not in unreachable and (q.get(k,0.)<=0 or not isfinite(prices[k]))))
    if missing:raise ClearingIncomplete('A reachable market has undefined price')
    target={}
    groups={}
    ties=[]
    log_regret_sum=0.0; best_weight=0.0
    max_group_gap=0.0
    # Recompute from CURRENT simultaneous loads and CURRENT destination prices.
    for gid,g in sorted(net.groups.items()):
        distances,paths,ways=path_audit(net,g.origin,g.seller,loads,cfg.exponent)
        options=[]
        for key in sorted(net.markets):
            if key in unreachable or key[1]!=g.commodity or key[0] not in paths:continue
            p=prices[key]
            score=p/(1+distances[key[0]])
            options.append((score,Column(gid,key[0],paths[key[0]]),ways[key[0]]))
        if not options:raise ClearingIncomplete('No finite priced destination available')
        options.sort(key=lambda t:(-t[0],t[1]))
        score,col,ways=options[0]
        target[gid]=col
        if ways>1 or any(abs(score-v[0]) <= 1e-10*max(1.,abs(score)) for v in options[1:]):ties.append(gid)
        gap_sum=0.0
        for existing,amount in sorted(flows.items()):
            if existing.group_id != gid or amount<=0:continue
            generalized=sum(net.edges[e].congestion_cost(loads.get(e,0.),cfg.exponent)+float(net.edges[e].weight_for(g.seller)) for e in existing.edges)
            actual=prices[(existing.destination,g.commodity)]/(1+generalized)
            gap_sum+=amount*max(0.0,score-actual)
        group_gap=gap_sum/(g.supply*score) if score>0 else 0.0
        max_group_gap=max(max_group_gap,group_gap)
        log_regret_sum+=gap_sum
        best_weight+=g.supply*score
        groups[gid]={'gap':group_gap,'best':col,'score':score,'tie_or_equivalent_route':gid in ties}
    result={
        'relative_gap':log_regret_sum/best_weight if best_weight else inf,
        'max_group_gap':max_group_gap,
        'active_columns':sum(int(v>0) for v in flows.values()),
        'reachable_unpriced':missing,
        'structurally_unreachable_markets':unreachable,
        'tie_groups':tuple(ties),
        'groups':groups,
        'loads':loads,'quantities':q,'prices':prices,
        'target':target,
        'financial_F059_policy_resolved':False,
        'global_convergence_proven':False,
    }
    if not (isfinite(result['relative_gap']) and isfinite(result['max_group_gap'])):
        raise ClearingIncomplete('Non-finite regret')
    return result


def _mix(flows, net, target, alpha):
    if not 0<alpha<=1:raise ValueError('Invalid relaxation factor')
    out={c:v*(1-alpha) for c,v in flows.items()}
    for gid,g in net.groups.items():
        c=target[gid]
        out[c]=out.get(c,0.)+alpha*g.supply
    # Exact floating supply conservation: assign residual to one deterministic route.
    for gid,g in sorted(net.groups.items()):
        cols=sorted(c for c in out if c.group_id==gid)
        delta=g.supply-sum(out[c] for c in cols)
        out[target[gid]]+=delta
        if any(out[c]<0 for c in cols):raise ClearingIncomplete('Negative numerical flow after mixing')
    net.snapshots(out)
    return out


def _relative_change(before,after,keys):
    return max((abs(after.get(k,0.)-before.get(k,0.))/max(1.,abs(after.get(k,0.)),abs(before.get(k,0.))) for k in keys),default=0.)


def _change_metrics(net,old,new):
    loads_old,q_old,p_old=net.snapshots(old)
    loads_new,q_new,p_new=net.snapshots(new)
    return {
      'max_relative_load_change':_relative_change(loads_old,loads_new,net.edges),
      'max_relative_quantity_change':_relative_change(q_old,q_new,net.markets),
      'max_relative_price_change':max((abs(p_new[k]-p_old[k])/max(1.,p_new[k],p_old[k]) for k in net.markets if isfinite(p_old[k]) and isfinite(p_new[k])),default=0.),
    }


def _flow_checksum(flows):
    rows=[(c.group_id,c.destination,c.edges,format(v,'.17g')) for c,v in sorted(flows.items()) if v!=0]
    return hashlib.sha256(json.dumps(rows,separators=(',',':')).encode('utf-8')).hexdigest()


def solve_competitive(net:Network,initial:dict[Column,float],settings:ClearingSettings=ClearingSettings(),*,observer=None):
    """Competitive, damped logit-continuation lab; re-evaluates all endogenous fields.

    The logit temperature is a numerical regularizer annealed toward zero, NOT
    a permanent taste or buyer demand rule. Success requires unregularized
    post-update F-072 regret, group regret, price & load stability checks.
    No exact/global convergence or monetary tie allocation is claimed.
    """
    flows,seeded,unreachable=seed_accessible_markets(net,initial,settings)
    initial_columns=set(flows)
    measured=_evaluate(net,flows,settings,unreachable)
    if observer is not None:
        observer(0, dict(flows), None, False, 0)
    history=[]
    last_delta={'max_relative_price_change':inf,'max_relative_load_change':inf,'max_relative_quantity_change':inf}
    stable=0;status='ITERATION_BUDGET_EXHAUSTED';iterations_done=0;temperature=None
    for i in range(1,settings.iterations+1):
        iterations_done=i
        # Discover cheapest routes under the current B, and retain prior routes
        # as transient options; no enumeration of every simple path.
        if len(set(flows)|set(measured['target'].values()))>settings.max_columns:
            status='COLUMN_BUDGET_EXCEEDED';break
        for col in measured['target'].values():
            flows.setdefault(col,0.)
        loads,q,prices=net.snapshots(flows)
        temperature=max(settings.temperature_floor,
                        settings.temperature_start/(1+i/settings.anneal_steps)**settings.step_decay)
        new={}
        for gid,g in sorted(net.groups.items()):
            cols=sorted(c for c in flows if c.group_id==gid)
            logs=[]
            for col in cols:
                loss=1+sum(net.edges[e].congestion_cost(loads.get(e,0.),settings.exponent)+float(net.edges[e].weight_for(g.seller)) for e in col.edges)
                p=prices[(col.destination,g.commodity)]
                if not isfinite(p) or p<=0 or loss<=0:raise ClearingIncomplete('Undefined positive market or transport score')
                logs.append(log(p)-log(loss))
            maximum=max(logs)
            weights=[exp(max(-700.,(value-maximum)/temperature)) for value in logs]
            denom=sum(weights)
            alpha=min(settings.step_size,4.0*temperature)
            for col,w in zip(cols,weights):
                new[col]=(1-alpha)*flows[col]+alpha*g.supply*w/denom
            delta=g.supply-sum(new[c] for c in cols)
            # A deterministic sum correction keeps supply conserved in double.
            new[measured['target'][gid]]+=delta
        net.snapshots(new)
        last_delta=_change_metrics(net,flows,new)
        flows=new
        measured=_evaluate(net,flows,settings,unreachable)
        if i%settings.check_every==0 or i==1:
            history.append({'iteration':i,'temperature':temperature,
              'relative_gap':measured['relative_gap'],
              'max_group_gap':measured['max_group_gap'],
              'columns':measured['active_columns'],**last_delta})
        candidate=(measured['relative_gap']<=settings.tolerance and
             measured['max_group_gap']<=settings.tolerance and
             last_delta['max_relative_price_change']<=settings.price_tolerance and
             last_delta['max_relative_load_change']<=settings.load_tolerance)
        stable=stable+1 if candidate else 0
        if observer is not None:
            observer(i, dict(flows), dict(last_delta), bool(candidate), stable)
        if stable>=settings.stable_checkpoints:
            status=('COMPETITIVE_APPROXIMATION_UNREACHABLE_MARKET' if unreachable else
                    'COMPETITIVE_APPROXIMATION_F059_PENDING' if measured['tie_groups'] else
                    'COMPETITIVE_APPROXIMATION_NUMERICAL_ONLY');break
    # Independent final snapshot; no frozen snapshot applied to an older flow.
    final=_evaluate(net,flows,settings,unreachable)
    certificate={
      'status':status,
      'commercial_tolerance_met':(final['relative_gap']<=settings.tolerance and final['max_group_gap']<=settings.tolerance),
      'stable_rechecks':stable,
      'price_load_change_tolerance_met':(last_delta['max_relative_price_change']<=settings.price_tolerance and
          last_delta['max_relative_load_change']<=settings.load_tolerance),
      'financial_policy_F059_pending':bool(final['tie_groups']),
      'financial_tie_policy_ratified':False,
      'unreachable_markets':unreachable,
      'convergence_to_exact_equilibrium_proven':False,
      'uniqueness_proven':False,
      'CSharp_binary_parity_proven':False,
      'production_approved':False,
      'iteration_count':iterations_done,'seeded_markets':len(seeded),
      'discovered_columns':len(set(flows)-initial_columns),
      'flow_sha256':_flow_checksum(flows),
      'last_delta':last_delta,
      'history':history,
      'relative_gap':final['relative_gap'],'max_group_gap':final['max_group_gap'],
      'final_quantities':final['quantities'],'final_loads':final['loads'],
      'final_prices':final['prices'],
      'final_best_columns':final['target'],
      'temperature_final':temperature if iterations_done else None,
      'method':'ANNEALED_LOGIT_DAMPING_F072_PRICE_TAKING'
    }
    return flows,certificate
