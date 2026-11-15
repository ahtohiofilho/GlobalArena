"""ADR-051 LAB ONLY: worker-coupled demand by time in each Goldberg tile.

This is deliberately a small physically consistent benchmark, NOT the
authoritative C#/Godot worker model. Commodity rate weights below are
positive illustrative fixture coefficients, NOT game calibration.

Each worker w creates demand for EACH item k during a T-turn cycle:
    demand[n,k] += multiplier[w]*rate[activity[w],k]*time[w,n]/T
with sum_n time[w,n] = T. Producing workers ALSO demand locally.
Transit does not multiply their demand, and nonproducers still demand.
"""
from __future__ import annotations

from dataclasses import dataclass
from math import fsum, isclose, isfinite
from collections import defaultdict

from gate09 import Column, Group, Market, Network
from multimarket_r13 import R13Scenario,construct_scenario

CYCLE_TURNS=6
# Only TEST SCENARIO parameters. Not authoritative demand weights.
ACTIVITY_RATES={
    "produce":(1.1,0.35),
    "travel":(1.35,0.27),
    "build":(0.92,1.48),
    "military":(1.72,0.73),
    "idle":(1.0,0.24),
}
ITEMS=(0,1)


@dataclass(frozen=True)
class Worker:
    id:int
    civilization:int
    activity:str
    product:int|None
    supply:float
    demand_multiplier:float
    visited_tiles:tuple[int,...]

    def __post_init__(self):
        if (type(self.id) is not int or self.id<=0
            or type(self.civilization) is not int
            or self.civilization<0
            or self.activity not in ACTIVITY_RATES
            or not isfinite(self.supply) or self.supply<0
            or not isfinite(self.demand_multiplier)
            or self.demand_multiplier<=0
            or not self.visited_tiles or
            any(type(n) is not int or n<0 for n in self.visited_tiles)):
            raise ValueError("Invalid physical worker")
        if self.activity=="produce":
            if (self.product not in ITEMS or self.supply<=0 or
                len(set(self.visited_tiles))!=1):
                raise ValueError("Producing workers must stay in their production tile")
        elif self.product is not None or self.supply!=0:
            raise ValueError("Nonproducing workers cannot create goods")


def _chain(adj,size=4):
    """Find an actual edge-adjacent simple path, no teleportation."""
    def dfs(path):
        if len(path)==size:
            return tuple(path)
        for next_node,_ in adj[path[-1]]:
            if next_node not in path:
                candidate=dfs(path+[next_node])
                if candidate is not None:
                    return candidate
        return None
    for start in sorted(adj):
        candidate=dfs([start])
        if candidate is not None:return candidate
    raise ValueError("Goldberg world lacks requested walk")


def validate_itinerary(worker,network,turns=CYCLE_TURNS):
    if len(worker.visited_tiles)!=turns:
        raise ValueError("Worker must account for every turn of cycle")
    for previous,current in zip(worker.visited_tiles,worker.visited_tiles[1:]):
        if (current!=previous and not any(
            nbr==current for nbr,_ in network.adj[previous])):
            raise ValueError("Worker illegally teleported between tiles")
    if any(node not in network.adj for node in worker.visited_tiles):
        raise ValueError("Worker visited unknown tile")


def demand_by_tile(workers,network,turns=CYCLE_TURNS):
    """Demand generated once per worker per cycle; pure, no monetary buyer."""
    if type(turns) is not int or turns<=0:
        raise ValueError("Invalid turn count")
    if len({w.id for w in workers})!=len(workers) or not workers:
        raise ValueError("Duplicate or missing workers")
    increments=defaultdict(list)
    for w in workers:
        validate_itinerary(w,network,turns)
        rates=ACTIVITY_RATES[w.activity]
        tile_turns=defaultdict(int)
        for tile in w.visited_tiles:
            tile_turns[tile]+=1
        if sum(tile_turns.values())!=turns:
            raise AssertionError("Worker travel occupancy is not conserved")
        for tile,count in sorted(tile_turns.items()):
            for item,rate in zip(ITEMS,rates):
                increments[(tile,item)].append(
                    w.demand_multiplier*rate*count/turns)
    totals={key:fsum(values) for key,values in sorted(increments.items())}
    if not all(isfinite(v) and v>0 for v in totals.values()):
        raise AssertionError("All fixture demands must be finite positive")
    for item in ITEMS:
        expected=fsum(w.demand_multiplier*ACTIVITY_RATES[w.activity][item]
                      for w in workers)
        actual=fsum(v for (tile,k),v in totals.items() if k==item)
        if not isclose(actual,expected,rel_tol=1e-13,abs_tol=1e-13):
            raise AssertionError("Demand multiplied or lost during travel")
    return totals


def make_workers(network,turns=CYCLE_TURNS):
    if turns!=CYCLE_TURNS:
        raise ValueError("This deterministic fixture has a six-turn timeline")
    chain=_chain(network.adj,4)
    workers=[]
    # Production workers are physical, each creates one real Group
    # and contributes positive consumption to their own production tile.
    for idx in range(6):
        tile=chain[idx//2]
        workers.append(Worker(
            id=idx+1,civilization=idx%3,activity="produce",
            product=idx%2,supply=1.+.2*(idx%3),
            demand_multiplier=1.+.1*(idx%4),
            visited_tiles=(tile,)*turns))
    # Crossing tiles takes turns; each of the six segments contributes
    # its OWN share. No origin holdover and no duplicated demand.
    walk=(chain[0],chain[1],chain[2],chain[3],chain[2],chain[1])
    workers.append(Worker(
        7,0,"travel",None,0.,1.2,walk))
    workers.append(Worker(
        8,1,"build",None,0.,1.1,(chain[3],)*turns))
    workers.append(Worker(
        9,2,"military",None,0.,0.95,(chain[3],)*turns))
    workers.append(Worker(
        10,0,"idle",None,0.,1.05,(chain[1],)*turns))
    for w in workers:
        validate_itinerary(w,network,turns)
    return tuple(workers),chain


def build_worker_network(turns=CYCLE_TURNS):
    """Build ORIGINAL Goldberg transport/ownership with worker-coupled demand."""
    cfg=R13Scenario(m=2,products=2,markets_per_product=2,
                    groups_per_product=3,civilizations=3,
                    iterations=0,exponent=4)
    original,_=construct_scenario(cfg)
    workers,chain=make_workers(original,turns)
    demands=demand_by_tile(workers,original,turns)
    producing=[w for w in workers if w.activity=="produce"]
    groups=[
        Group(w.id,w.visited_tiles[0],w.product,
              w.civilization,w.supply)
        for w in producing]
    markets=[Market(tile,item,value)
             for (tile,item),value in sorted(demands.items())]
    net=Network(list(original.edges.values()),groups,markets)
    market_nodes=tuple(sorted({tile for tile,item in demands}))
    # Every producer distributes REAL supply across physically reachable
    # markets. This is only an initial positive-Q solver seed, never a
    # claim of economic equilibrium or a fictitious demand injection.
    flows={}
    for g in sorted(groups,key=lambda x:x.id):
        _,paths,_=net.dijkstra(g.origin,g.seller,{})
        portion=g.supply/len(market_nodes)
        for tile in market_nodes:
            c=Column(g.id,tile,paths[tile])
            net.validate_column(c)
            flows[c]=portion
    _,q,prices=net.snapshots(flows)
    if set(q)!=set(net.markets) or any(not isfinite(p) for p in prices.values()):
        raise AssertionError("All worker-funded market nodes must have Q>0")
    production_nodes={g.origin for g in groups}
    demand_nodes={tile for tile,_ in demands}
    if not production_nodes.issubset(demand_nodes):
        raise AssertionError("Producer location lacks its own worker demand")
    for g in groups:
        if any(demands.get((g.origin,item),0.)<=0 for item in ITEMS):
            raise AssertionError("Producer worker must demand all goods")
    return net,flows,{
        "turns":turns,
        "workers":workers,
        "chain":chain,
        "nodes":42,
        "items":ITEMS,
        "demand":demands,
        "physical_production_nodes":production_nodes,
        "physical_demand_nodes":demand_nodes,
        "producing_workers":len(producing),
        "nonproducing_workers":len(workers)-len(producing),
        "groups":len(groups),
        "markets_by_tile_and_item":len(markets),
        "original_quartic_congestion":True,
        "production_node_subset_of_demand_nodes":True,
        "travel_demand_not_multiplied":True,
        "not_a_game_worker_model":True,
    }
