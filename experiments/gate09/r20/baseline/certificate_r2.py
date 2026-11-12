"""GA-Next Gate09 R2: independent path audit + finite-deviation diagnostics.

R2 does NOT prove convergence/existence/uniqueness of a global market equilibrium.
A frozen-snapshot gap is not a certificate of group-wise strategic equilibrium.
"""
from collections import defaultdict
from dataclasses import dataclass
from math import inf, isfinite
import heapq

from gate09 import Column

ABS_TOL = 1e-10
REL_TOL = 1e-10


def near(a,b):
    return abs(a-b)<=max(ABS_TOL,REL_TOL*max(abs(a),abs(b)))


def path_audit(net, origin, civil, loads, exponent=4):
    """Independent path oracle: Dijkstra distances and shortest-path DAG count.

    Returns all-node cheapest generalized distance, a stable route minimizing
    (hop_count, edge_id_sequence) among shortest routes, and capped number of
    cost-optimal routes. Capping avoids exponential cardinalities.
    """
    distance={origin:0.0}
    heap=[(0.0,origin)]
    while heap:
        d,u=heapq.heappop(heap)
        if d>distance[u]+ABS_TOL:continue
        for v,eid in net.adj.get(u,()):
            e=net.edges[eid]
            weight=e.congestion_cost(loads.get(eid,0),exponent)+float(e.weight_for(civil))
            if weight <=0 or not isfinite(weight):raise ValueError('Invalid edge weight')
            cost=d+weight
            if cost<distance.get(v,inf)-max(ABS_TOL,REL_TOL*abs(cost)):
                distance[v]=cost
                heapq.heappush(heap,(cost,v))
    # Strictly positive weights imply the shortest-path subgraph is acyclic.
    canon={origin:()}; route_ways={origin:1}
    for u in sorted(distance,key=lambda x:(distance[x],x)):
        if u not in canon:continue
        for v,eid in net.adj.get(u,()):
            if v not in distance or distance[u]>=distance[v]:continue
            e=net.edges[eid]
            w=e.congestion_cost(loads.get(eid,0),exponent)+float(e.weight_for(civil))
            if near(distance[u]+w,distance[v]):
                candidate=canon[u]+(eid,)
                if v not in canon or (len(candidate),candidate)<(len(canon[v]),canon[v]):
                    canon[v]=candidate
                route_ways[v]=min(2,route_ways.get(v,0)+route_ways[u])
    return distance,canon,route_ways


def full_path_audit(net,flows,exponent=4):
    """Market and route-choice audit with financially conservative tie classification.

    A tie in routes or destinations is marked policy-unresolved even if there
    is no evidence of a changed payment. This prevents false financial claims.
    """
    loads,q,prices=net.snapshots(flows)
    unresolved_markets=[k for k,p in prices.items() if p==inf]
    result={};price_ties=[];route_ties=[];omitted=[]
    weighted_regret=0.;score_weight=0.
    for gid,g in sorted(net.groups.items()):
        dist,paths,ways=path_audit(net,g.origin,g.seller,loads,exponent)
        choices=[]
        for key,market in sorted(net.markets.items()):
            if market.commodity!=g.commodity or market.node not in dist:continue
            score=prices[key]/(1+dist[market.node])
            choices.append((score,Column(gid,market.node,paths[market.node]),ways[market.node]))
        if not choices:raise ValueError('Group has no reachable market')
        choices.sort(key=lambda x:(-x[0],x[1]))
        bestscore,bestcol,route_count=choices[0]
        if route_count>1:route_ties.append(gid)
        if any(near(bestscore,sc) for sc,col,_ in choices[1:]):price_ties.append(gid)
        if bestcol not in flows:omitted.append(bestcol)
        score_weight+=g.supply*bestscore
        regret=0.0
        for col,qty in flows.items():
            if col.group_id!=gid or qty<=0:continue
            cost=sum(net.edges[e].congestion_cost(loads.get(e,0),exponent)+float(net.edges[e].weight_for(g.seller)) for e in col.edges)
            score=prices[(col.destination,g.commodity)]/(1+cost)
            regret+=qty*max(0.0,bestscore-score)
        result[gid]={'regret':regret,'relative_regret':regret/(g.supply*bestscore) if isfinite(bestscore) and bestscore>0 else inf,
            'chosen_best':bestcol,'shortest_path_tie':route_count>1,'market_score_tie':gid in price_ties}
        weighted_regret+=regret
    gap=weighted_regret/score_weight if score_weight and isfinite(score_weight) else inf
    return {'frozen_gap':gap,'max_group_gap':max((v['relative_regret'] for v in result.values()),default=0.),
        'groups':result,'omitted':sorted(omitted),'route_ties':sorted(route_ties),'market_ties':sorted(price_ties),
        'unserved':sorted(unresolved_markets),
        'financial_policy_certifiable':not(route_ties or price_ties),
        'global_equilibrium_proven':False}


def group_utility(net, flows,group_id,exponent=4):
    """Revenue-equivalent experimental utility as sum(q_group * A(Q,B)).

    This is a diagnostic hypothesis, NOT a ratified player objective.
    """
    loads,q,prices=net.snapshots(flows)
    g=net.groups[group_id]
    total=0.0
    for col,qty in flows.items():
        if col.group_id!=group_id or qty<=0:continue
        score=prices[(col.destination,g.commodity)] / (1+sum(net.edges[e].congestion_cost(loads.get(e,0),exponent)+float(net.edges[e].weight_for(g.seller)) for e in col.edges))
        if not isfinite(score):return inf
        total+=qty*score
    return total


def finite_deviation_stress(net,flows,steps=(0.05,0.25,1.),exponent=4,max_trials=500):
    """Tests a bounded set of unilateral finite reallocations with repricing.

    Only examines: one currently used source column -> one cheapest path to a
    market, for selected move fractions. A positive witness refutes the
    interpretation that zero frozen regret means no profitable GROUP change;
    absence of a witness is NOT a proof of global equilibrium.
    """
    net.snapshots(flows)
    loads,_,_=net.snapshots(flows)
    witnesses=[];trials=0
    for gid,g in sorted(net.groups.items()):
        dist,paths,_=path_audit(net,g.origin,g.seller,loads,exponent)
        targets=[Column(gid,m.node,paths[m.node]) for k,m in sorted(net.markets.items()) if m.commodity==g.commodity and m.node in dist]
        base=group_utility(net,flows,gid,exponent)
        if not isfinite(base):continue
        for src in sorted(c for c,x in flows.items() if c.group_id==gid and x>0):
            for target in targets:
                if src==target:continue
                for frac in steps:
                    if not 0<frac<=1:raise ValueError('Invalid deviation fraction')
                    if trials>=max_trials:
                        return {'trials':trials,'positive_witnesses':witnesses,'truncated':True,'global_equilibrium_proven':False}
                    qty=flows[src]*frac
                    if qty<=0:continue
                    candidate=dict(flows)
                    candidate[src]-=qty
                    candidate[target]=candidate.get(target,0.)+qty
                    value=group_utility(net,candidate,gid,exponent)
                    trials+=1
                    if value>base+max(1e-10,abs(base)*1e-9):
                        witnesses.append({'group':gid,'from':src,'to':target,'fraction':frac,
                            'baseline_utility':base,'deviated_utility':value,'gain':value-base})
    return {'trials':trials,'positive_witnesses':sorted(witnesses,key=lambda x:-x['gain']),
            'truncated':False,'global_equilibrium_proven':False}
