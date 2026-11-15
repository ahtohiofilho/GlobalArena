"""ADR-052 LAB ONLY: real workers across thousands of Goldberg nodes.

Production and demand both derive from physical Worker instances, never
from independent synthetic R13 market lists. Each worker creates demand
even if not producing. Travel allocates demand by six time slots at
visited, edge-adjacent tiles without multiplication.

The original R13 is used ONLY for unchanged network edges, owners,
seller-specific tariffs, capacities and quartic transport. Original
R13 groups and markets are discarded and rebuilt from workers.

Aggregating several producing workers into one Group is exact for
total supply by (node,commodity,seller) in THIS STATIC LAB, not a
claim that worker-level decisions, welfare or money are equivalent.
"""
from __future__ import annotations

from collections import Counter,defaultdict
from dataclasses import dataclass
from hashlib import sha256
from math import fsum,gcd,isclose,isfinite
from pathlib import Path
import sys

GATE09=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(GATE09/"adr051_worker_block"/"ci"))
sys.path.insert(0,str(GATE09/"adr049_geography_density"/"ci"))
import run_adr049 as ADR49
from worker_coupled import (
    Worker, ACTIVITY_RATES, ITEMS, CYCLE_TURNS, demand_by_tile,
    validate_itinerary,
)
from gate09 import Column,Group,Market,Network
from multimarket_r13 import R13Scenario,construct_scenario
from clearing_r5 import _flow_checksum


@dataclass(frozen=True)
class PopulationConfig:
    m:int
    hubs:int
    workers:int

    def __post_init__(self):
        if any(type(v) is not int for v in (self.m,self.hubs,self.workers)):
            raise ValueError("Population counts must be integers")
        n=10*self.m*self.m+2
        if not (2<=self.m<=30 and 1<=self.hubs<=n//4
                and self.workers%4==0 and self.workers>=8*self.hubs
                and self.workers<=100000):
            raise ValueError("Unregistered physical population geometry")


CONFIGS={
    "unit_162_nodes_128_workers":PopulationConfig(4,4,128),
    "pilot_5762_nodes_2048_workers":PopulationConfig(24,12,2048),
    "spatial_5762_nodes_8192_workers":PopulationConfig(24,512,8192),
}


def disjoint_hubs(network,how_many):
    """Choose disjoint {hub,3 adjacent tiles} clusters, across whole graph.

    Deterministic coprime hop scans the whole Goldberg ID range,
    preventing early concentration at one pole. Every itinerary
    route is checked against ACTUAL R13 logical edge adjacency.
    """
    nodes=tuple(sorted(network.adj))
    if nodes!=tuple(range(len(nodes))):
        raise ValueError("Expected contiguous Goldberg node IDs")
    count=len(nodes)
    if not 1<=how_many<=count//4:
        raise ValueError("Invalid number of physical activity hubs")
    step=max(1,count//how_many)
    while gcd(step,count)!=1:
        step+=1
    used=set()
    clusters=[]
    for k in range(count):
        hub=(count//9+k*step)%count
        if hub in used:
            continue
        neighbors=[v for v,_ in network.adj[hub]
                   if v not in used and v!=hub]
        if len(neighbors)<3:
            continue
        cluster=(hub,*neighbors[:3])
        clusters.append(cluster)
        used.update(cluster)
        if len(clusters)==how_many:
            break
    if len(clusters)!=how_many:
        raise ValueError("Insufficient disjoint adjacent Goldberg clusters")
    if len(used)!=4*how_many:
        raise AssertionError("Physical cluster overlap")
    return tuple(clusters)


def _make_worker_records(network,config,clusters):
    records=[]
    for batch in range(config.workers//4):
        hub_index=batch%config.hubs
        hub,a,b,c=clusters[hub_index]
        item=(batch//config.hubs)%len(ITEMS)
        civil=hub_index%3
        rate=.12+.02*(batch%3)
        common_multiplier=.90+.05*(batch%7)
        start=4*batch+1
        records.extend((
            Worker(start,civil,"produce",item,rate,common_multiplier,
                   (hub,)*CYCLE_TURNS),
            Worker(start+1,civil,"travel",None,0.,common_multiplier+.02,
                   (hub,a,hub,b,hub,c)),
            Worker(start+2,civil,"build",None,0.,common_multiplier+.04,
                   (a,)*CYCLE_TURNS),
            Worker(start+3,civil,
                   "military" if batch%2==0 else "idle",
                   None,0.,common_multiplier+.06,
                   ((b if batch%2==0 else c),)*CYCLE_TURNS),
        ))
    if len(records)!=config.workers:
        raise AssertionError("Physical worker count mismatch")
    return tuple(records)


def _fingerprint_workers(workers):
    h=sha256()
    for w in workers:
        h.update(repr((w.id,w.civilization,w.activity,w.product,
                       w.supply,w.demand_multiplier,w.visited_tiles)).encode())
        h.update(b"\n")
    return h.hexdigest()


def build_population(config:PopulationConfig):
    """Return original physical network, worker-sourced flows, full ledger."""
    if not isinstance(config,PopulationConfig):
        raise ValueError("Explicit population configuration required")
    # Clone ORIGINAL quartic R13 geography and infrastructure only.
    physical,_=construct_scenario(R13Scenario(
        m=config.m,products=2,markets_per_product=2,
        groups_per_product=2,civilizations=3,iterations=0,exponent=4))
    clusters=disjoint_hubs(physical,config.hubs)
    workers=_make_worker_records(physical,config,clusters)
    consumer_demands=demand_by_tile(workers,physical,CYCLE_TURNS)
    output=defaultdict(list)
    for w in workers:
        validate_itinerary(w,physical,CYCLE_TURNS)
        if w.activity=="produce":
            output[(w.visited_tiles[0],w.product,w.civilization)].append(
                w.supply)
    groups=[]
    group_per_key={}
    for group_id,key in enumerate(sorted(output),start=1):
        node,item,seller=key
        quantity=fsum(output[key])
        group=Group(group_id,node,item,seller,quantity)
        groups.append(group)
        group_per_key[key]=group.id
    market_rows=[
        Market(node,item,dem)
        for (node,item),dem in sorted(consumer_demands.items())
    ]
    network=Network(list(physical.edges.values()),groups,market_rows)
    physical_production_nodes={g.origin for g in groups}
    physical_demand_nodes={node for node,item in consumer_demands}
    if not physical_production_nodes.issubset(physical_demand_nodes):
        raise AssertionError("Worker producer must also create local demand")
    if len(physical_demand_nodes)!=4*config.hubs:
        raise AssertionError("Some occupied tiles were omitted from demand")
    if len(network.groups)!=2*config.hubs:
        raise AssertionError("Expected one aggregate group per hub and item")
    if len(network.markets)!=len(ITEMS)*len(physical_demand_nodes):
        raise AssertionError("Missing worker-created market per item and tile")

    # Each group sells REAL supply to its own hub and three neighbor
    # markets: 4 columns per group, no artificial market liquidity.
    by_hub={cluster[0]:cluster for cluster in clusters}
    flow={}
    for group in sorted(groups,key=lambda x:x.id):
        cluster=by_hub[group.origin]
        route_edges={group.origin:()}
        route_edges.update({
            dest:(next(e for nbr,e in network.adj[group.origin]
                       if nbr==dest),)
            for dest in cluster[1:]
        })
        for dest in cluster:
            col=Column(group.id,dest,route_edges[dest])
            network.validate_column(col)
            flow[col]=group.supply/len(cluster)
    _,q,price=network.snapshots(flow)
    if set(q)!=set(network.markets) or not all(
        isfinite(v) and v>0 for v in price.values()):
        raise AssertionError("Physical worker supply did not fund each market")
    if not isclose(fsum(flow.values()),
                   fsum(w.supply for w in workers),
                   rel_tol=1e-12,abs_tol=1e-12):
        raise AssertionError("Worker supply lost during aggregation")

    for k in ITEMS:
        expected=fsum(w.demand_multiplier*ACTIVITY_RATES[w.activity][k]
                      for w in workers)
        actual=fsum(v for (node,item),v in consumer_demands.items() if item==k)
        if not isclose(expected,actual,rel_tol=1e-12,abs_tol=1e-12):
            raise AssertionError("Worker demand was lost or duplicated")
    activities=dict(sorted(Counter(w.activity for w in workers).items()))
    demands_by_item={
        str(k):fsum(v for (node,item),v in consumer_demands.items()
                    if item==k)
        for k in ITEMS
    }
    return network,flow,{
        "source_workers":workers,
        "time_slots_per_cycle":CYCLE_TURNS,
        "workers":len(workers),
        "activities":activities,
        "producing_workers":activities.get("produce",0),
        "nonproducing_workers":len(workers)-activities.get("produce",0),
        "physical_production_nodes":physical_production_nodes,
        "physical_demand_nodes":physical_demand_nodes,
        "production_nodes_count":len(physical_production_nodes),
        "demand_nodes_count":len(physical_demand_nodes),
        "groups_aggregated_by_node_commodity_seller":len(groups),
        "market_pairs_node_commodity":len(network.markets),
        "initial_funded_columns":len(flow),
        "geometry_nodes":10*config.m*config.m+2,
        "geometry_edges":len(network.edges),
        "worker_ledger_sha256":_fingerprint_workers(workers),
        "market_demand_by_item":demands_by_item,
        "initial_real_flow_sha256":_flow_checksum(flow),
        "production_is_subset_of_demand_nodes":True,
        "travel_time_does_not_duplicate_demand":True,
        "uses_original_quartic_transport_and_tariff_edges":True,
        "seller_ownership_from_original_R13":True,
        "no_independent_synthetic_R13_market_demand":True,
        "not_authoritative_worker_model":True,
        "not_game_engine":True,
    }
