"""GA-Next Gate09 R2: independent Python port of GA F-084F monetary projection.

Reference: PR #307, DestinationRevenueLaboratory.cs (2026-11-10).
All amounts, prices, quantities and tariff weights are integer micro-units (S=10**6).
This module has no side effects on any game state. The Python port has NOT been
verified against a running C# binary; such a cross-language fixture is pending.
"""
from __future__ import annotations
from dataclasses import dataclass
from collections import defaultdict
from math import isqrt
from typing import Mapping, Sequence

S = 1_000_000
MAX_LONG = (1 << 63)-1


def long(value: int) -> int:
    if type(value) is not int or not 0 <= value <= MAX_LONG:
        raise ValueError(f"Expected nonnegative signed 64-bit integer: {value!r}")
    return value


def price_and_gross(demand_raw: int, quantity_raw: int) -> tuple[int, int]:
    """Exact integer identities from C# F-084F Quote. Q=0 has no quoted price."""
    d, q = long(demand_raw), long(quantity_raw)
    if q == 0:
        return (0, 0)
    if d == 0:
        raise ValueError("Positive sale with zero demand has no F-043 price")
    price = isqrt((d * S * S) // q)
    if price == 0:
        raise OverflowError("Positive price underflows scale")
    return long(price), long((q * price) // S)


def largest_remainder(total: int, numerators: Mapping[int, int], denominator: int) -> dict[int, int]:
    """Deterministic Hamilton apportionment using exact integer remainders."""
    long(total)
    if not numerators or denominator <= 0 or any(v < 0 for v in numerators.values()):
        raise ValueError("Invalid apportionment")
    if sum(numerators.values()) != denominator:
        raise ValueError("Weights do not sum to denominator")
    out = {k: (total * v)//denominator for k,v in numerators.items()}
    rem = {k: (total*v)%denominator for k,v in numerators.items()}
    residual = total-sum(out.values())
    assert 0 <= residual <= len(out)
    for key in sorted(out, key=lambda k:(-rem[k], k))[:residual]:
        out[key] += 1
    assert sum(out.values()) == total
    return dict(sorted(out.items()))


@dataclass(frozen=True)
class EdgeContract:
    edge_id: int
    tariff_raw: int
    holdings: tuple[tuple[int, int], ...] # actual capital, not ownership %; sum is total capital

    def validate(self):
        long(self.edge_id); long(self.tariff_raw)
        if len(self.holdings) != len({c for c,_ in self.holdings}):
            raise ValueError("Duplicated owner")
        for civil, capital in self.holdings:
            long(civil);long(capital)
        if not self.holdings and self.tariff_raw:
            raise ValueError("No capital for nonzero tariff")
        if sum(v for _,v in self.holdings)==0 and self.tariff_raw:
            raise ValueError("No incorporated capital for nonzero tariff")
        return self


@dataclass(frozen=True)
class Sale:
    flow_id: int
    origin: int
    destination: int
    commodity: int
    seller: int
    quantity_raw: int
    edges: tuple[int, ...] = ()

    def validate(self):
        for v in (self.flow_id,self.origin,self.destination,self.commodity,self.seller,self.quantity_raw):long(v)
        if self.quantity_raw==0:raise ValueError("Zero sale: exclude it from transient sale list")
        if (self.origin==self.destination)!=(len(self.edges)==0):
            raise ValueError("Local sale iff usedEdges are empty")
        if len(self.edges)!=len(set(self.edges)): raise ValueError("Repeated edge within sale")
        for e in self.edges:long(e)
        return self


def _external_weights(edge:EdgeContract, seller:int) -> dict[int,int]:
    """C# per-edge flooring + largest-remainder across EXTERNAL owners."""
    e=edge.validate()
    capital_total=sum(v for _,v in e.holdings)
    if capital_total==0 or e.tariff_raw==0:return {}
    own=sum(c for cid,c in e.holdings if cid==seller)
    payable=(e.tariff_raw*(capital_total-own))//capital_total
    if payable==0:return {}
    candidates={cid:v for cid,v in e.holdings if cid!=seller and v>0}
    out={cid:(e.tariff_raw*v)//capital_total for cid,v in candidates.items()}
    remainders={cid:(e.tariff_raw*v)%capital_total for cid,v in candidates.items()}
    residual=payable-sum(out.values())
    if not 0<=residual<=len(out):raise AssertionError("Owner weights did not reconcile")
    for cid in sorted(out,key=lambda x:(-remainders[x],x))[:residual]:out[cid]+=1
    if sum(out.values())!=payable:raise AssertionError("Bad external weight")
    return {cid:v for cid,v in sorted(out.items()) if v}


def settle(markets:Sequence[tuple[int,int,int,int]],sales:Sequence[Sale],edges:Sequence[EdgeContract]):
    """F-084F equivalent algebra on EXACT raw integers, with flow-level witnesses.

    markets: (destination, commodity, demand_raw, total_Q_raw)
    C# has only ID-based 'UsedEdges' and does not itself prove physical topology;
    for R2, callers must separately validate each path in their Network.
    """
    emap={e.edge_id:e.validate() for e in edges}
    if len(emap)!=len(edges):raise ValueError("Duplicate edge")
    m={}
    for dest,good,demand,q in markets:
        long(dest);long(good)
        k=(dest,good)
        if k in m:raise ValueError("Duplicate market")
        p,gross=price_and_gross(demand,q)
        m[k]={'demand_raw':demand,'quantity_raw':q,'price_raw':p,'gross_raw':gross}
    smap={s.flow_id:s.validate() for s in sales}
    if len(smap)!=len(sales):raise ValueError("Duplicate flow ID")
    for s in smap.values():
        if (s.destination,s.commodity) not in m:raise ValueError("Market missing")
        if any(eid not in emap for eid in s.edges):raise ValueError("Used edge missing")
    by_market=defaultdict(list)
    for s in smap.values():by_market[(s.destination,s.commodity)].append(s)
    credits=defaultdict(int);receipts=[]
    for k in sorted(m):
        group=sorted(by_market[k],key=lambda x:x.flow_id)
        entry=m[k]
        q=sum(s.quantity_raw for s in group)
        if q!=entry['quantity_raw']:raise ValueError("Market sold Q differs from attributed sales")
        if q==0:continue
        gross_shares=largest_remainder(entry['gross_raw'],{s.flow_id:s.quantity_raw for s in group},q)
        for s in group:
            # One line for producer, one per EXTERNAL owner per EDGE (do not merge first).
            shares=[(-1,s.seller,S)]
            for eid in sorted(s.edges):
                e=emap[eid]
                for cid,w in _external_weights(e,s.seller).items():shares.append((eid,cid,w))
            denominator=sum(w for _,_,w in shares)
            gross=gross_shares[s.flow_id]
            base=[(gross*w)//denominator for _,_,w in shares]
            remains=[(gross*w)%denominator for _,_,w in shares]
            residue=gross-sum(base)
            rank=sorted(range(len(shares)),key=lambda i:(-remains[i],shares[i][0],shares[i][1]))
            if not 0<=residue<=len(shares):raise AssertionError("Impossible cent split")
            for i in rank[:residue]:base[i]+=1
            if sum(base)!=gross:raise AssertionError("Monetary nonconservation")
            for (_,cid,_),v in zip(shares,base):credits[cid]+=v
            receipts.append({'flow_id':s.flow_id,'seller':s.seller,
                'destination':s.destination,'commodity':s.commodity,
                'quantity_raw':s.quantity_raw,'market_price_raw':entry['price_raw'],
                'gross_raw':gross,'seller_net_raw':base[0],
                'external_weight_raw':denominator-S,
                'owners':[{'edge':e,'civilization':cid,'credit_raw':val}
                          for (e,cid,_),val in zip(shares[1:],base[1:]) if val]})
    total=sum(x['gross_raw'] for x in m.values())
    if sum(credits.values())!=total:raise AssertionError("Batch financial nonconservation")
    for v in credits.values():long(v)
    long(total)
    return {'markets':{str(k):v for k,v in sorted(m.items())},
        'sales':sorted(receipts,key=lambda x:x['flow_id']),
        'credits':dict(sorted((k,v) for k,v in credits.items() if v)),
        'total_gross_raw':total,'total_credits_raw':sum(credits.values())}


def frozen_float_flows_to_raw(net, flows):
    """One explicit experimental conversion boundary from R1 float to raw integers.

    Per producer group, group supply quantizes to 1e-6 and sale quantities are
    apportioned by largest remainder to preserve exact supply conservation.
    This is NOT the authorized rounding policy of the runtime.
    """
    from decimal import Decimal, ROUND_HALF_EVEN
    net.snapshots(flows)
    groups=defaultdict(list)
    for col,q in flows.items():
        if q>0:groups[col.group_id].append((col,Decimal(str(q))))
    sales=[];flow_id=1
    for group_id in sorted(groups):
        g=net.groups[group_id]
        qtarget=int((Decimal(str(g.supply))*S).to_integral_value(rounding=ROUND_HALF_EVEN))
        values={i:val for i,(_,val) in enumerate(sorted(groups[group_id]))}
        total=sum(values.values())
        exact={i:Decimal(qtarget)*v/total for i,v in values.items()}
        import decimal
        base={i:int(a.to_integral_value(rounding=decimal.ROUND_FLOOR)) for i,a in exact.items()}
        remain=qtarget-sum(base.values())
        for i in sorted(base,key=lambda i:(-(exact[i]-base[i]),i))[:remain]:base[i]+=1
        for i,(col,_) in enumerate(sorted(groups[group_id])):
            if base[i]==0:continue
            net.validate_column(col)
            sales.append(Sale(flow_id,g.origin,col.destination,g.commodity,g.seller,base[i],col.edges));flow_id+=1
    markets=[]
    for key,m in sorted(net.markets.items()):
        dest,good=key
        demand=int((Decimal(str(m.demand))*S).to_integral_value(rounding=ROUND_HALF_EVEN))
        q=sum(s.quantity_raw for s in sales if s.destination==dest and s.commodity==good)
        markets.append((dest,good,demand,q))
    edges=[EdgeContract(e.id,e.tariff_micro,e.owners) for e in sorted(net.edges.values(),key=lambda e:e.id)]
    return markets,sales,edges
