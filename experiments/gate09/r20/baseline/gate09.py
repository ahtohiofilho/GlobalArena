"""GA-Next Gate 09 R1: transient, non-authoritative economic path oracle.

No WorldState mutation, no stored logistics. Candidate F-084 parameters are NOT ratified.
"""
from __future__ import annotations
from dataclasses import dataclass
from decimal import Decimal, ROUND_HALF_EVEN, ROUND_FLOOR
from fractions import Fraction
from collections import defaultdict
import heapq
import math

MICRO = 1_000_000
EPS = 1e-11


@dataclass(frozen=True)
class Edge:
    id: int
    a: int
    b: int
    c_free: float = 1.0
    capacity: float = 10.0
    tariff_micro: int = 0
    owners: tuple[tuple[int, int], ...] = ()  # (civilization, capital fraction / 1e6)

    def __post_init__(self):
        if self.id < 0 or self.a == self.b or min(self.a,self.b) < 0:
            raise ValueError("Invalid logical edge")
        if not math.isfinite(self.c_free) or self.c_free <= 0:
            raise ValueError("Physical impedance must be positive")
        if not math.isfinite(self.capacity) or self.capacity <= 0:
            raise ValueError("Physical capacity must be positive")
        if self.tariff_micro < 0 or sum(v for _, v in self.owners) not in (0, MICRO):
            raise ValueError("Invalid tariff/ownership")
        if any(v <= 0 for _,v in self.owners) or len(set(c for c,_ in self.owners)) != len(self.owners):
            raise ValueError("Duplicate or invalid owners")
        if self.tariff_micro and not self.owners:
            raise ValueError("Tariff weight requires beneficiaries")

    def congestion_cost(self, load: float, exponent: int = 4) -> float:
        if load < -EPS or exponent not in (3,4,5):
            raise ValueError("Invalid load or experimental p")
        return self.c_free * (1.0 + (max(0.,load)/self.capacity)**exponent)

    def weight_for(self, civil: int) -> Fraction:
        own = sum(v for c, v in self.owners if c == civil)
        return Fraction(self.tariff_micro * (MICRO-own), MICRO*MICRO)

    def external_beneficiaries(self, civil: int) -> dict[int, Fraction]:
        return {c:Fraction(self.tariff_micro*v,MICRO*MICRO) for c,v in self.owners if c!=civil and self.tariff_micro}


@dataclass(frozen=True)
class Group:
    id: int
    origin: int
    commodity: int
    seller: int
    supply: float

    def __post_init__(self):
        if self.id < 0 or min(self.origin,self.commodity,self.seller)<0 or not math.isfinite(self.supply) or self.supply<=0:
            raise ValueError("Invalid production group")


@dataclass(frozen=True)
class Market:
    node: int
    commodity: int
    demand: float
    def __post_init__(self):
        if min(self.node,self.commodity)<0 or not math.isfinite(self.demand) or self.demand<=0:
            raise ValueError("Invalid positive market demand")


@dataclass(frozen=True, order=True)
class Column:
    group_id: int
    destination: int
    edges: tuple[int,...]


class Network:
    def __init__(self,edges,groups,markets):
        self.edges = {e.id:e for e in edges}
        self.groups = {g.id:g for g in groups}
        self.markets = {(m.node,m.commodity):m for m in markets}
        if not self.edges or not self.groups or not self.markets:
            raise ValueError("Network cannot be empty")
        if len(self.edges)!=len(edges) or len(self.groups)!=len(groups) or len(self.markets)!=len(markets):
            raise ValueError("Duplicate IDs or market")
        self.adj=defaultdict(list)
        for e in self.edges.values():
            self.adj[e.a].append((e.b,e.id));self.adj[e.b].append((e.a,e.id))
        for n in self.adj: self.adj[n].sort()
        for g in self.groups.values():
            if not any(m.commodity==g.commodity for m in self.markets.values()):
                raise ValueError("No market for produced commodity")

    def validate_column(self,col:Column):
        g=self.groups[col.group_id]
        if (col.destination,g.commodity) not in self.markets:
            raise ValueError("Missing market")
        node=g.origin; visited={node}
        for eid in col.edges:
            e=self.edges[eid]
            if node not in (e.a,e.b): raise ValueError("Disconnected path")
            node=e.b if node==e.a else e.a
            if node in visited: raise ValueError("Not a simple path")
            visited.add(node)
        if node!=col.destination: raise ValueError("Wrong route destination")

    def snapshots(self, flows:dict[Column,float]):
        loads=defaultdict(float); q=defaultdict(float); supplied=defaultdict(float)
        for col,amount in sorted(flows.items()):
            if not math.isfinite(amount) or amount < 0: raise ValueError("Invalid flow")
            self.validate_column(col)
            g=self.groups[col.group_id]
            supplied[g.id]+=amount
            q[(col.destination,g.commodity)]+=amount
            for eid in col.edges: loads[eid]+=amount
        for g in self.groups.values():
            if abs(supplied[g.id]-g.supply)>1e-7*max(1,g.supply):
                raise ValueError("Production not fully allocated")
        prices={key:math.sqrt(m.demand/q[key]) if q[key]>0 else math.inf for key,m in self.markets.items()}
        return dict(loads),dict(q),prices

    def dijkstra(self, origin:int, civil:int, loads:dict[int,float],exponent:int=4):
        """One scan certifies all destinations under frozen B; never claims financial uniqueness."""
        distance={origin:0.0}; paths={origin:()}; ways={origin:1}
        queue=[(0.0,(),origin)]
        while queue:
            d, path,u=heapq.heappop(queue)
            if d > distance.get(u,math.inf)+EPS or path!=paths[u]:
                # A separate tie-path still needs multiplicity propagation; see update below.
                continue
            for v,eid in self.adj[u]:
                e=self.edges[eid]
                w=e.congestion_cost(loads.get(eid,0.),exponent)+float(e.weight_for(civil))
                nd=d+w
                np=path+(eid,)
                old=distance.get(v,math.inf)
                if nd < old-EPS:
                    distance[v]=nd;paths[v]=np;ways[v]=ways[u]
                    heapq.heappush(queue,(nd,np,v))
                elif abs(nd-old)<=EPS:
                    # All edge weights strictly positive; predecessor paths settle first.
                    ways[v]=min(2,ways.get(v,0)+ways[u])
                    if np<paths[v]:
                        paths[v]=np;heapq.heappush(queue,(nd,np,v))
        return distance,paths,ways

    def oracle(self,flows,exponent:int=4):
        loads,q,prices=self.snapshots(flows)
        best={}; alternatives={}; ties=[]
        unserved=[key for key,price in prices.items() if math.isinf(price)]
        for g in sorted(self.groups.values(),key=lambda x:x.id):
            distances,paths,ways=self.dijkstra(g.origin,g.seller,loads,exponent)
            options=[]
            for key,market in sorted(self.markets.items()):
                if market.commodity!=g.commodity or market.node not in distances: continue
                p=prices[key];score=p/(1+distances[market.node])
                col=Column(g.id,market.node,paths[market.node])
                options.append((score,col,ways[market.node]))
            if not options: raise ValueError("No reachable destination for group")
            # infinity may be tied: deterministic canonical order and no false finite certificate
            options.sort(key=lambda x: (-x[0],x[1]))
            top=options[0];best[g.id]=(top[0],top[1])
            alternatives[g.id]=tuple(options)
            # Any co-optimal route or destination can alter the financial allocation.
            is_tied=top[2]>1 or any(
                (opt[1]!=top[1]) and (
                    (math.isinf(opt[0]) and math.isinf(top[0])) or
                    (math.isfinite(opt[0]) and math.isfinite(top[0]) and
                     abs(opt[0]-top[0]) <= 1e-10*max(1.0,top[0]))
                ) for opt in options[1:])
            if is_tied: ties.append(g.id)
        return best,alternatives,tuple(sorted(set(ties))),tuple(sorted(set(unserved)))

    def commercial_gap(self,flows,exponent:int=4):
        loads,q,prices=self.snapshots(flows)
        best,options,ties,unserved=self.oracle(flows,exponent)
        if unserved:
            return {"gap":math.inf,"status":"unserved_demand","omitted":[],"ties":ties,"unserved":unserved}
        weighted_gap=0.;max_scores=0.;omitted=[];group_gap={}
        for gid,g in sorted(self.groups.items()):
            bestscore,bestcol=best[gid]
            max_scores+=g.supply*bestscore
            own_gap=0.0
            for col,amount in flows.items():
                if col.group_id!=gid or amount==0: continue
                generalized=sum(self.edges[eid].congestion_cost(loads.get(eid,0),exponent)+float(self.edges[eid].weight_for(g.seller)) for eid in col.edges)
                score=prices[(col.destination,g.commodity)]/(1+generalized)
                regret=amount*max(0,bestscore-score)
                weighted_gap+=regret
                own_gap+=regret
            group_gap[gid]=own_gap/(g.supply*bestscore) if bestscore>0 else 0.0
            if bestcol not in flows: omitted.append(bestcol)
        gap=weighted_gap/max_scores if max_scores>0 else 0.
        status="commercial_approximation" if gap>1e-7 else "commercial_first_order_pass"
        if ties: status+="_financial_route_tie_unresolved"
        return {"gap":gap,"status":status,"omitted":sorted(omitted),"ties":ties,
                "unserved":unserved,"group_gaps":group_gap,"max_group_gap":max(group_gap.values(),default=0.)}

    def assess_snapshot(self,flows,tolerance:float=1e-4):
        """Certificate of frozen-snapshot first-order conditions only; no global proof."""
        if not (math.isfinite(tolerance) and 0 <= tolerance < 1):
            raise ValueError("Invalid certification tolerance")
        c=self.commercial_gap(flows)
        c["tolerance"]=tolerance
        c["first_order_within_tolerance"]=(
            math.isfinite(c["gap"]) and c["gap"]<=tolerance and
            c["max_group_gap"]<=tolerance and not c["omitted"] and not c["ties"])
        c["global_convergence_proven"]=False
        c["financial_projection_certifiable"]=(not c["ties"])
        return c

    def solve_experimental(self,initial:dict[Column,float],steps:int=2000,alpha:float=0.025,temp:float=0.008):
        """Damped logit exploration, NOT an equilibrium existence/convergence proof."""
        flows=dict(initial)
        for col in flows: self.validate_column(col)
        self.snapshots(flows)
        history=[]; discovered=0
        for it in range(steps):
            loads,q,prices=self.snapshots(flows)
            best,options,_,_=self.oracle(flows)
            for gid,(score,col) in best.items():
                if col not in flows:
                    flows[col]=0.0;discovered+=1
            loads,q,prices=self.snapshots(flows)
            for gid,g in sorted(self.groups.items()):
                cols=sorted(c for c in flows if c.group_id==gid)
                log_scores=[]
                for c in cols:
                    loss=1+sum(self.edges[eid].congestion_cost(loads.get(eid,0))+float(self.edges[eid].weight_for(g.seller)) for eid in c.edges)
                    p=prices[(c.destination,g.commodity)]
                    # Exploit epsilon only for exploratory iterations, never certificates.
                    if math.isinf(p): p=math.sqrt(self.markets[(c.destination,g.commodity)].demand/1e-12)
                    log_scores.append(math.log(p)-math.log(loss))
                mx=max(log_scores)
                exp=[math.exp(max(-700,(x-mx)/temp)) for x in log_scores]
                z=sum(exp)
                old=[flows[c] for c in cols]
                updated=[(1-alpha)*x+alpha*g.supply*w/z for x,w in zip(old,exp)]
                for c,x in zip(cols,updated):flows[c]=x
            if (it+1)%100==0:
                cert=self.commercial_gap(flows)
                history.append((it+1,cert['gap'],len(flows)))
        return flows,{"columns":len(flows),"discovered":discovered,"history":history,"certificate":self.commercial_gap(flows)}


def hamilton(total:int, weights:dict):
    """Stable largest-remainder allocation of exact nonnegative decimal/fraction weights."""
    if total<0 or not weights: raise ValueError("Invalid allocation")
    ws={k:Decimal(v.numerator)/Decimal(v.denominator) if isinstance(v,Fraction) else Decimal(str(v)) for k,v in weights.items()}
    if any(not x.is_finite() or x<0 for x in ws.values()):raise ValueError("Invalid weight")
    den=sum(ws.values())
    if den==0:
        if total==0:return {k:0 for k in weights}
        raise ValueError("Zero allocation denominator")
    exact={k:Decimal(total)*v/den for k,v in ws.items()}
    parts={k:int(x.to_integral_value(rounding=ROUND_FLOOR)) for k,x in exact.items()}
    residue=total-sum(parts.values())
    for k in sorted(exact,key=lambda k: (-(exact[k]-parts[k]),k))[:residue]:parts[k]+=1
    assert sum(parts.values())==total
    return parts


def settle_projection(net:Network,flows:dict[Column,float]):
    """Transient monetary projection. No consumer wallet or authoritative treasury writes."""
    loads,q,prices=net.snapshots(flows)
    by_market=defaultdict(dict)
    for col,qty in flows.items():
        if qty>0: by_market[(col.destination,net.groups[col.group_id].commodity)][col]=qty
    ledger=defaultdict(int);sales={};gross_markets={};gross_total=0
    for key in sorted(by_market):
        p=prices[key]
        if not math.isfinite(p):raise ValueError("No sale value without market Q")
        money=(Decimal(str(p))*Decimal(str(q[key]))*100).to_integral_value(rounding=ROUND_HALF_EVEN)
        cents=int(money);gross_markets[key]=cents;gross_total+=cents
        sale_parts=hamilton(cents,by_market[key])
        for col,gross in sorted(sale_parts.items()):
            g=net.groups[col.group_id]
            ext=defaultdict(Fraction)
            for eid in col.edges:
                for civil,w in net.edges[eid].external_beneficiaries(g.seller).items():ext[civil]+=w
            weights={g.seller:Fraction(1)}
            for civil,w in ext.items():weights[civil]=w
            alloc=hamilton(gross,weights)
            for civil,val in alloc.items():ledger[civil]+=val
            sales[col]={"gross_cents":gross,"credits_cents":dict(sorted(alloc.items()))}
    assert sum(ledger.values())==gross_total
    return {"gross_cents":gross_total,"credits_cents":dict(sorted(ledger.items())),"markets_cents":gross_markets,"sales":sales,"loads":loads}
