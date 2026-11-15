#!/usr/bin/env python3
"""GA Gate09 ADR-045: MULTIGROUP, MULTIMARKET NCP with active route columns.

Genuinely solves simultaneous smoothed Fischer-Burmeister equations by
analytic-Jacobian damped Gauss-Newton / Levenberg-Marquardt on producer
simplexes, with global R14-style shortest-path column separation.
NOT an R21/R38-R40 best-response iteration. Never alters the original
F072 price/cost/tariff operator, original sources, C# or Godot.
A result is ONLY a commercial PASS when the INDEPENDENT ADR042
static F072/R13/R14/F084F validator says PASS. Otherwise finite NO-GO.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime,timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"adr044_ncp_contract"/"ci"))
import run_adr044 as ADR44
ADR42=ADR44.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum,_reachable
from gate09 import Column,Edge,Group,Market,Network
from oracle_cache_r14 import FrozenRouteCache

SCHEMA="GA_GATE09_ADR045_MULTIGROUP_GENERAL_MCP_COLUMN_GENERATION_V1"
METHOD="smoothed_FB_analytic_Jacobian_LM_global_column_separation"
EPS_SCHEDULE=(1e-3,1e-6,1e-9)
ROOT_INFINITY_NORM_GATE=1e-8
SEPARATION_RELATIVE_TOL=1e-8
INITIAL_PHYSICAL_COVERAGE_SHARE=1e-6


@dataclass(frozen=True)
class Limits:
    max_h_evaluations:int=1200
    max_route_scans:int=320
    max_original_certificates:int=6
    max_columns:int=192
    max_generations:int=4
    max_newton_steps:int=60
    max_backtrack:int=14

    def __post_init__(self):
        for k,v in vars(self).items():
            if type(v) is not int or v<=0 or v>20000:
                raise ValueError("Finite positive integer budget required: "+k)


class BudgetExceeded(Exception):
    pass


class Work:
    def __init__(self, limits):
        self.limits=limits
        self.h_evaluations=0
        self.route_scans=0
        self.original_certificates=0
        self.newton_steps=0
        self.candidates_rejected=0
        self.column_additions=0
        self.physical_seed_transfers=0
        self.nonfinite_q_trials_rejected=0
        self.jacobian_assemblies=0

    def charge(self,which,count=1):
        allowed={
            "h_evaluations":"max_h_evaluations",
            "route_scans":"max_route_scans",
            "original_certificates":"max_original_certificates",
            "newton_steps":"max_newton_steps",
        }
        if which not in allowed or type(count) is not int or count<0:
            raise ValueError("Unknown work unit")
        current=getattr(self,which)
        limit=getattr(self.limits,allowed[which])
        if current+count>limit:
            raise BudgetExceeded(which+" ceiling reached")
        setattr(self,which,current+count)

    def evidence(self):
        return {
            "H_evaluations_including_rejected_trials":self.h_evaluations,
            "separate_shortest_path_scans":self.route_scans,
            "full_independent_ADR042_certificates":self.original_certificates,
            "newton_accepted_steps":self.newton_steps,
            "line_search_candidates_rejected":self.candidates_rejected,
            "columns_added_to_active_universe":self.column_additions,
            "Q_zero_initial_physical_seed_transfers":self.physical_seed_transfers,
            "nonfinite_Q_trial_rejections":self.nonfinite_q_trials_rejected,
            "analytic_jacobians_assembled":self.jacobian_assemblies,
            "limits":vars(self.limits),
        }


def _route_candidates(net,flows,settings,work):
    """Globally cheapest physical route for EACH compatible reachable market."""
    loads,quantities,_=net.snapshots(flows)
    _,_,unreachable=_reachable(net,settings.exponent)
    cache=FrozenRouteCache(net,loads,settings.exponent)
    candidates={}
    for gid,g in sorted(net.groups.items()):
        distances,paths,_=cache.paths(g.origin,g.seller)
        for market in sorted(net.markets):
            if market in unreachable or market[1]!=g.commodity:
                continue
            if market[0] in paths:
                col=Column(gid,market[0],paths[market[0]])
                net.validate_column(col)
                candidates[col]=True
    work.charge("route_scans",cache.counters()["shortest_path_scans"])
    return tuple(sorted(candidates)),unreachable


def _build_active(net,flows,candidates,work):
    cols=sorted(set(c for c,m in flows.items() if m>0).union(candidates))
    if len(cols)>work.limits.max_columns:
        raise BudgetExceeded("column count ceiling")
    for col in cols:net.validate_column(col)
    if not all(any(c.group_id==gid for c in cols) for gid in net.groups):
        raise ValueError("Some producers have no physical market column")
    work.column_additions+=len(set(cols)-set(c for c,m in flows.items() if m>0))
    return cols


def _cover_empty_markets(net,flows,columns,work):
    """Reallocate REAL existing output, never create funds/D/stock or price floor.

    Numerical feasibility seed only: it does NOT affect the F072 rules and
    is never a PASS unless the fully re-solved state passes ADR042.
    """
    out={c:float(flows.get(c,0.)) for c in columns}
    _,q,_=net.snapshots(out)
    reachable,_,unreachable=_reachable(net,4)
    empties=[m for m in sorted(net.markets)
             if m not in unreachable and q.get(m,0.)<=0]
    for market in empties:
        viable=[c for c in columns
                if (c.destination,net.groups[c.group_id].commodity)==market]
        if not viable:
            raise ValueError("Reachable market absent from active routes")
        dst=viable[0]
        gid=dst.group_id
        owned=[c for c in columns if c.group_id==gid and out[c]>0 and c!=dst]
        if not owned:
            raise ValueError("Cannot seed a vacant market from real production")
        donor=max(owned,key=lambda col:(out[col],tuple(-i for i in col.edges)))
        amount=min(out[donor]/4,
                   INITIAL_PHYSICAL_COVERAGE_SHARE*net.groups[gid].supply)
        if amount<=0:
            raise AssertionError("No real mass available for initial coverage")
        out[donor]-=amount
        out[dst]+=amount
        work.physical_seed_transfers+=1
    net.snapshots(out)
    return out


class Layout:
    """Independent mass variables plus one Lagrange multiplier per group.

    A deterministic ANCHOR column absorbs residual real supply exactly;
    no trial state can create/destroy physical production.
    """
    def __init__(self,net,columns,flows):
        self.net=net
        self.groups=tuple(sorted(net.groups))
        self.cols=tuple(sorted(columns))
        by={gid:[] for gid in self.groups}
        for i,c in enumerate(self.cols):by[c.group_id].append(i)
        self.by=by
        self.anchor={}
        self.independent=[]
        self.pivot_for={}
        for gid in self.groups:
            options=by[gid]
            anchor=max(options,key=lambda i:(flows.get(self.cols[i],0.),
                                            -i))
            self.anchor[gid]=anchor
            for i in options:
                if i!=anchor:
                    self.independent.append(i)
                    self.pivot_for[i]=anchor
        self.ncols=len(self.cols)
        self.unknowns=len(self.independent)+len(self.groups)
        self.mu_offset=len(self.independent)
        self.mu_index={gid:self.mu_offset+j for j,gid in enumerate(self.groups)}
        if self.unknowns!=self.ncols:
            raise AssertionError("Wrong simplex NCP dimension")

    def initial(self,flows):
        # Multipliers start at zero; the first paid H evaluation provides
        # the true market-specific minimum for each producer.
        z=[flows.get(self.cols[i],0.) for i in self.independent]
        z.extend(0. for _ in self.groups)
        return z

    def decode(self,z):
        if len(z)!=self.unknowns:raise ValueError("Wrong NCP vector length")
        x=[0.]*self.ncols
        for k,i in enumerate(self.independent):x[i]=z[k]
        for gid in self.groups:
            anchor=self.anchor[gid]
            g=self.net.groups[gid]
            x[anchor]=g.supply-sum(x[i] for i in self.by[gid] if i!=anchor)
        if any(not math.isfinite(v) or v<0 for v in x):
            raise ValueError("Nonnegative physical simplex violated")
        return x

    def flows(self,z):
        x=self.decode(z)
        return {c:v for c,v in zip(self.cols,x)}

    def lift(self,z):
        x=self.decode(z)
        mu={gid:z[self.mu_index[gid]] for gid in self.groups}
        return x,mu


def _ncp_eval(layout,z,epsilon,work,need_jacobian):
    """H and the FB equations on the EXACT input physical simplex.

    All route cost derivatives analytically include the quartic
    congestion cross-effect and market quantity cross-effect.
    """
    work.charge("h_evaluations")
    net=layout.net
    x,mu=layout.lift(z)
    flows={c:v for c,v in zip(layout.cols,x)}
    loads,q,_=net.snapshots(flows)
    _,_,unreachable=_reachable(net,4)
    if any(q.get(m,0.)<=0 for m in net.markets if m not in unreachable):
        raise ValueError("Cannot differentiate H at uncovered reachable Q=0")
    edge_cost={}
    edge_slope={}
    for eid,e in net.edges.items():
        B=loads.get(eid,0.)
        edge_cost[eid]=e.congestion_cost(B,4)
        edge_slope[eid]=4*e.c_free*(B/e.capacity)**3/e.capacity
    H=[]
    ratios=[]
    levels=[]
    col_edged=[set(c.edges) for c in layout.cols]
    markets=[(c.destination,net.groups[c.group_id].commodity)
             for c in layout.cols]
    for c,m in zip(layout.cols,markets):
        group=net.groups[c.group_id]
        ratio=math.sqrt(q[m]/net.markets[m].demand)
        level=1+sum(
            edge_cost[e]+float(net.edges[e].weight_for(group.seller))
            for e in c.edges)
        h=level*ratio
        if not math.isfinite(h) or h<=0:
            raise ValueError("Nonfinite or nonpositive H")
        H.append(h);ratios.append(ratio);levels.append(level)
    fb=[]
    derivatives=[]
    for i,c in enumerate(layout.cols):
        gid=c.group_id
        a=x[i]
        b=H[i]-mu[gid]
        root=math.sqrt(a*a+b*b+2*epsilon*epsilon)
        fb.append(root-a-b)
        derivatives.append((a/root-1,b/root-1))
    if not need_jacobian:
        return fb,None,H
    work.jacobian_assemblies+=1
    J=[[0.]*layout.unknowns for _ in range(layout.ncols)]
    # d H_i / d z_k, where k shifts real mass from group anchor
    # to its selected non-anchor column.
    for k,j in enumerate(layout.independent):
        a=layout.pivot_for[j]
        d_edges_plus=col_edged[j]
        d_edges_minus=col_edged[a]
        plus_m=markets[j]
        minus_m=markets[a]
        for i,col in enumerate(layout.cols):
            dQ=int(markets[i]==plus_m)-int(markets[i]==minus_m)
            dB=sum(
                edge_slope[e]*(int(e in d_edges_plus)-int(e in d_edges_minus))
                for e in col_edged[i])
            dH=ratios[i]*(
                dB + levels[i]*dQ/(2*q[markets[i]]))
            dx=int(i==j)-int(i==a)
            da,db=derivatives[i]
            J[i][k]=da*dx+db*dH
    for i,col in enumerate(layout.cols):
        J[i][layout.mu_index[col.group_id]]=-derivatives[i][1]
    return fb,J,H


def _solve_linear(A,b):
    """Pure-stdlib dense pivoted linear solve, failure is a real NO-GO."""
    n=len(b)
    M=[list(row)+[rhs] for row,rhs in zip(A,b)]
    if any(len(row)!=n+1 for row in M):
        raise ValueError("Non-square normal system")
    for k in range(n):
        pivot=max(range(k,n),key=lambda i:abs(M[i][k]))
        if abs(M[pivot][k])<1e-20:
            raise ArithmeticError("Singular normal equations")
        if pivot!=k:M[pivot],M[k]=M[k],M[pivot]
        scale=M[k][k]
        for i in range(k+1,n):
            factor=M[i][k]/scale
            if factor==0:continue
            M[i][k]=0.
            for j in range(k+1,n+1):
                M[i][j]-=factor*M[k][j]
    ans=[0.]*n
    for i in range(n-1,-1,-1):
        ans[i]=(M[i][n]-sum(M[i][j]*ans[j] for j in range(i+1,n)))/M[i][i]
    if any(not math.isfinite(v) for v in ans):
        raise ArithmeticError("Nonfinite LM correction")
    return ans


def _normal_equations(J,r,lam):
    n=len(r)
    if len(J)!=n or any(len(row)!=n for row in J):
        raise ValueError("Unexpected Jacobian shape")
    A=[[0.]*n for _ in range(n)]
    b=[0.]*n
    for i in range(n):
        ji=J[i]
        ri=r[i]
        for k,vik in enumerate(ji):
            b[k]-=vik*ri
            for l in range(k+1):
                A[k][l]+=vik*ji[l]
    for k in range(n):
        A[k][k]+=lam
        for l in range(k):
            A[l][k]=A[k][l]
    return A,b


def _merit(r):
    return math.fsum(v*v for v in r)/2


def _feasible_projected_step(layout,z,direction,alpha):
    """Project real proposed masses into EACH physical producer simplex.

    This changes only the *algorithmic Newton search direction*, not
    H, the score, the price or game demand. An unconstrained LM step
    pointing outward at a zero-mass route must not block every alpha.
    """
    proposal=list(z)
    for gid in layout.groups:
        indices=[k for k,i in enumerate(layout.independent)
                 if layout.cols[i].group_id==gid]
        for k in indices:
            proposal[k]=max(0.,z[k]+alpha*direction[k])
        total=math.fsum(proposal[k] for k in indices)
        ceiling=layout.net.groups[gid].supply
        if total>ceiling:
            # Redistribute proposed real mass on the simplex boundary;
            # the anchor gets the exactly conserved remainder.
            scale=ceiling/total
            for k in indices:
                proposal[k]*=scale
        mu_idx=layout.mu_index[gid]
        proposal[mu_idx]=z[mu_idx]+alpha*direction[mu_idx]
    layout.decode(proposal)
    return proposal


def _inner_solve(layout,z,work,trace):
    """Regularized NCP root in an ACTIVE route set, with hard work ceiling."""
    original=list(z)
    for eps in EPS_SCHEDULE:
        for _ in range(22):
            r,J,H=_ncp_eval(layout,z,eps,work,True)
            err=max(abs(v) for v in r)
            if err<=max(5e-11,eps*.01):
                break
            m=_merit(r)
            success=False
            for k_lm in range(9):
                lam=10.**(-7+1.5*k_lm)
                A,b=_normal_equations(J,r,lam)
                try:
                    direction=_solve_linear(A,b)
                except ArithmeticError:
                    continue
                for line in range(work.limits.max_backtrack):
                    alpha=2.**(-line)
                    trial=_feasible_projected_step(
                        layout,z,direction,alpha)
                    try:
                        layout.decode(trial)
                    except ValueError:
                        work.candidates_rejected+=1
                        continue
                    try:
                        trial_r,_,_= _ncp_eval(
                            layout,trial,eps,work,False)
                    except ValueError:
                        work.nonfinite_q_trials_rejected+=1
                        work.candidates_rejected+=1
                        continue
                    if _merit(trial_r)<m-max(1e-28,1e-8*m):
                        work.charge("newton_steps")
                        z=trial
                        trace.append({
                            "epsilon":eps,
                            "accepted_step":work.newton_steps,
                            "lambda_LM":lam,
                            "alpha":alpha,
                            "before_smooth_merit":m,
                            "after_smooth_merit":_merit(trial_r),
                            "FB_infinity_norm_after":max(abs(t) for t in trial_r),
                        })
                        success=True
                        break
                    work.candidates_rejected+=1
                if success:break
            if not success:
                trace.append({
                    "epsilon":eps,
                    "status":"STALLED_SMOOTHED_NCP_LM_NO_DESCENT",
                    "FB_infinity_norm":err})
                return z,"STALLED_SMOOTHED_NCP_LM_NO_DESCENT"
    residual,_,_= _ncp_eval(layout,z,0.,work,False)
    error=max(abs(t) for t in residual)
    trace.append({"final_unsmoothed_FB_infinity":error})
    return z,("ROOT_NCP_RESIDUAL_OK" if error<=ROOT_INFINITY_NORM_GATE
              else "NO_GO_NCP_RESIDUAL_TOO_LARGE")


def _global_separate(net,flows,columns,settings,work):
    """Check a restricted solution against all reachable destinations/routes.

    Tests the frozen minimum H for all active funded routes vs EVERY
    cheapest route to each compatible reachable market; never relies
    only on a restricted NCP.
    """
    candidate,unreachable=_route_candidates(net,flows,settings,work)
    active=set(columns)
    loads,q,_=net.snapshots(flows)
    cache=FrozenRouteCache(net,loads,settings.exponent)
    missed=[]
    diagnostics=[]
    for gid,g in sorted(net.groups.items()):
        distances,paths,_=cache.paths(g.origin,g.seller)
        weights=cache.weights(g.seller)
        minima=[]
        for c in columns:
            if c.group_id!=gid:continue
            m=(c.destination,g.commodity)
            h=(1+sum(weights[e] for e in c.edges))*math.sqrt(
                q.get(m,0.)/net.markets[m].demand)
            minima.append(h)
        current=min(minima)
        best=(math.inf,None)
        for c in candidate:
            if c.group_id!=gid:continue
            m=(c.destination,g.commodity)
            h=(1+distances[c.destination])*math.sqrt(
                q.get(m,0.)/net.markets[m].demand)
            best=min(best,(h,c))
            if c not in active and h < current - (
                    SEPARATION_RELATIVE_TOL*max(1.,current)):
                missed.append(c)
        diagnostics.append({
            "group":gid,
            "restricted_min_H":current,
            "global_min_H":best[0],
            "global_best_col":repr(best[1]),
        })
    work.charge("route_scans",cache.counters()["shortest_path_scans"])
    return tuple(sorted(set(missed))),diagnostics


def _certify(net,flows,settings,work):
    work.charge("original_certificates")
    return ADR42.static_certificate(net,flows,settings)


def solve_mcp(net,initial_flows,limits=None,settings=None):
    """Actual multigroup NCP active-column root with ORIGINAL F072 gate.

    No convergence promise. On failure returns an explicit NO-GO
    with no committed result; original map and flows remain untouched.
    """
    limits=limits or Limits()
    settings=settings or ClearingSettings(exponent=4)
    if settings.exponent!=4:
        raise ValueError("ADR045 derivative only supports original quartic congestion")
    before=_flow_checksum(initial_flows)
    net.snapshots(initial_flows)
    work=Work(limits)
    t0=perf_counter()
    status="NO_GO_UNSPECIFIED"
    trace=[]
    generations=[]
    final=None
    certificate=None
    last_gap=None
    try:
        # Fast path: don't move an existing commercially valid equilibrium
        # simply to satisfy a numerical iteration quota.
        _,q,_=net.snapshots(initial_flows)
        _,_,unreachable=_reachable(net,settings.exponent)
        priced=all(q.get(m,0.)>0 for m in net.markets if m not in unreachable)
        if priced:
            pre=_certify(net,initial_flows,settings,work)
            last_gap={
                "global":pre["gap_global_complementarity"],
                "worst":pre["gap_worst_group_complementarity"],
            }
            if pre["static_complementarity_approximate"]:
                status="PASS_EXISTING_STATIC_F072_NO_UPDATE"
                final=dict(initial_flows)
                certificate=pre
        if final is None:
            current=dict(initial_flows)
            for gen in range(limits.max_generations):
                viable,unreachable=_route_candidates(
                    net,current,settings,work)
                cols=_build_active(net,current,viable,work)
                current=_cover_empty_markets(net,current,cols,work)
                loads,q,_=net.snapshots(current)
                # The only mandatory positivity is in the physical
                # candidate for numerical derivative; no fake prices.
                if any(q.get(m,0.)<=0 for m in net.markets if m not in unreachable):
                    raise ValueError("Cannot initialize differentiable VI interior")
                layout=Layout(net,cols,current)
                z0=layout.initial(current)
                # Explicitly initialize each group's multiplier from the
                # actual current H of its lowest priced active route.
                z0[layout.mu_offset:]=[0.]*len(layout.groups)
                initial_r,_,initial_H=_ncp_eval(layout,z0,1e-3,work,False)
                for gid in layout.groups:
                    z0[layout.mu_index[gid]]=min(
                        initial_H[i] for i in layout.by[gid])
                start_H=work.h_evaluations
                state,inner_status=_inner_solve(layout,z0,work,trace)
                candidate=layout.flows(state)
                missed,sep=_global_separate(
                    net,candidate,cols,settings,work)
                generation={
                    "generation":gen+1,
                    "active_columns":len(cols),
                    "inner_status":inner_status,
                    "H_calls_in_inner":work.h_evaluations-start_H,
                    "new_shorter_route_columns":len(missed),
                    "missed_columns":list(map(repr,missed)),
                    "separation":sep,
                }
                if missed:
                    if len(set(cols).union(missed))>limits.max_columns:
                        raise BudgetExceeded("column limit after route separation")
                    # Preserve all real production and introduce new
                    # globally preferred routes with ZERO initial mass.
                    current={**candidate,**{c:0. for c in missed}}
                    generations.append(generation)
                    continue
                final_q=net.snapshots(candidate)[1]
                if any(final_q.get(m,0.)<=0 for m in net.markets
                       if m not in unreachable):
                    generation["certification"]="BLOCKED_Q_ZERO"
                    generations.append(generation)
                    status="NO_GO_FINAL_REACHABLE_Q_ZERO"
                    break
                cert=_certify(net,candidate,settings,work)
                generation["certification"]=cert["status"]
                generation["global_gap"]=cert.get("gap_global_complementarity")
                generation["worst_gap"]=cert.get("gap_worst_group_complementarity")
                generations.append(generation)
                certificate=cert
                last_gap={
                    "global":cert.get("gap_global_complementarity"),
                    "worst":cert.get("gap_worst_group_complementarity"),
                }
                if cert["static_complementarity_approximate"]:
                    status="PASS_NEW_MULTIGROUP_STATIC_F072_CERTIFIED"
                    final=candidate
                    break
                status="NO_GO_NCP_RESTRICTED_ROOT_NOT_ORIGINAL_F072"
                break
            else:
                status="NO_GO_ROUTE_GENERATION_CEILING"
    except BudgetExceeded as exc:
        status="NO_GO_PREDECLARED_BUDGET"
        trace.append({"budget_exceeded":str(exc)})
    except (ArithmeticError,ValueError,OverflowError,ZeroDivisionError) as exc:
        status="NO_GO_INVALID_OR_UNSTABLE_NUMERIC_STATE"
        trace.append({"numeric_error":repr(exc)})
    if _flow_checksum(initial_flows)!=before:
        raise AssertionError("Experimental solver mutated original input")
    if final is not None:
        # Final re-audit for genuinely unaltered original F072 certificate.
        net.snapshots(final)
        if certificate is None or not certificate["static_complementarity_approximate"]:
            raise AssertionError("Attempted PASS without independent certificate")
    return {
        "schema":SCHEMA,
        "method":METHOD,
        "status":status,
        "initial_flow_sha256":before,
        "final_flow_sha256":None if final is None else _flow_checksum(final),
        "final_group_market_flows":(
            None if final is None else [
                {"group":c.group_id,"destination":c.destination,
                 "edges":list(c.edges),"quantity":v}
                for c,v in sorted(final.items()) if v>0]),
        "certificate":{
            "status":certificate["status"],
            "global_F072_gap":certificate.get("gap_global_complementarity"),
            "worst_group_F072_gap":certificate.get("gap_worst_group_complementarity"),
            "original_R14_calls":certificate.get("original_R14_oracle_calls"),
            "independent_R13_checks":certificate.get("independent_R13_checks"),
            "F084F_conservation":certificate.get("F084F_conservation_witness"),
            "F059_tie_groups":certificate.get("financial_F059_tie_groups"),
        } if certificate else None,
        "last_F072_gaps":last_gap,
        "work":work.evidence(),
        "generations":generations,
        "trace":trace,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "no_changed_economic_parameters":True,
        "global_convergence_proven":False,
        "financial_F059_ratified":False,
        "CSharp_parity_proven":False,
        "production_approved":False,
        "input_unmodified":True,
    }


def three_market_fixture():
    net=Network(
        [Edge(1,0,1,c_free=1.,capacity=1.),
         Edge(2,0,2,c_free=1.,capacity=1.),
         Edge(3,0,3,c_free=1.,capacity=1.)],
        [Group(1,0,0,0,1.)],
        [Market(1,0,1.),Market(2,0,1.),Market(3,0,1.)])
    flow={Column(1,1,(1,)):.7,Column(1,2,(2,)):.2,
          Column(1,3,(3,)):.1}
    return net,flow


def two_group_nonoptimal_fixture():
    net,base=ADR42.two_group_example(.5)
    flow={
        Column(1,1,(1,)):.8,Column(1,2,(2,)):.2,
        Column(2,1,(1,)):.7,Column(2,2,(2,)):.3,
    }
    return net,flow


def route_switch_fixture():
    net=Network(
        [Edge(1,0,1,c_free=3.0,capacity=20.),
         Edge(2,0,2,c_free=1.,capacity=20.),
         Edge(3,0,3,c_free=.5,capacity=20.),
         Edge(4,3,1,c_free=.5,capacity=20.)],
        [Group(1,0,0,0,1.),Group(2,0,0,1,1.)],
        [Market(1,0,1.),Market(2,0,1.)])
    flows={
        Column(1,1,(1,)):.9,Column(1,2,(2,)):.1,
        Column(2,1,(1,)):.8,Column(2,2,(2,)):.2,
    }
    return net,flows


def exercise(profile):
    if ADR42.R21.validate_manifest()!=16:
        raise AssertionError("R20 source was changed")
    suites=[
        ("toy_one_group_three_markets",*three_market_fixture()),
        ("toy_two_groups_two_markets",*two_group_nonoptimal_fixture()),
        ("toy_two_groups_route_alternatives",*route_switch_fixture()),
        ("toy_two_groups_equal_preexisting",*ADR42.two_group_example(.25)),
        ("toy_one_group_Q0_start",*ADR42.one_group_example(1.)),
    ]
    if profile in ("ci","extended"):
        suites.extend([
            ("r34_42_original",*ADR42.R34.capacity_fixture(
                ADR42.R21.CASES["small"],"original",1.)[:2]),
            ("r34_42_heterogeneous_K02",*ADR42.R34.capacity_fixture(
                ADR42.R21.CASES["small"],"heterogeneous_D_and_supply",.2)[:2]),
        ])
    if profile=="extended":
        suites.append(("r34_162_original",*ADR42.R34.capacity_fixture(
            ADR42.R21.CASES["medium"],"original",1.)[:2]))
    rows=[]
    for label,net,flows in suites:
        limit=(Limits(max_h_evaluations=1200,max_newton_steps=60)
               if not label.startswith("r34_162") else
               Limits(max_h_evaluations=700,max_newton_steps=24,
                      max_columns=320))
        result=solve_mcp(net,flows,limits=limit)
        rows.append({"fixture":label,"result":result})
        print("[ADR045:{}] status={} global={} worst={} Hcalls={} "
              "Newton={} routes={} columnsAdded={}".format(
                  label,result["status"],
                  result["certificate"]["global_F072_gap"] if result["certificate"] else None,
                  result["certificate"]["worst_group_F072_gap"] if result["certificate"] else None,
                  result["work"]["H_evaluations_including_rejected_trials"],
                  result["work"]["newton_accepted_steps"],
                  result["work"]["separate_shortest_path_scans"],
                  result["work"]["columns_added_to_active_universe"]),flush=True)
        if result["status"].startswith("PASS") and (
                not result["certificate"] or
                result["certificate"]["status"]!="PASS_STATIC_F072_FIRST_ORDER_ONLY"):
            raise AssertionError("Uncertified solver success")
    # Hard engineering target: two multigroup controlled fixtures
    # must be solved rather than merely documenting another diagnosis.
    required={"toy_two_groups_two_markets",
              "toy_two_groups_equal_preexisting"}
    fulfilled={x["fixture"] for x in rows if x["result"]["status"].startswith("PASS")}
    return {
        "fixtures":rows,
        "profile":profile,
        "required_controlled_multigroup_passed":required.issubset(fulfilled),
        "required_controlled_multigroup":sorted(required),
        "higher_scale_status_descriptive_not_production":True,
        "original_R20_manifest_files":16,
        "general_solver_implemented":True,
        "production_approved":False,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=("smoke","ci","extended"),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "profile":args.profile,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),
        "platform":platform.platform(),
        "status":"FAILED",
        "production_approved":False,
        "CSharp_integrated":False,
        "F059_policy_ratified":False,
        "global_convergence_proven":False,
    }
    try:
        report.update(exercise(args.profile))
        report["status"]=("PASSED_ADR045_MULTIGROUP_SOLVER_CONTRACT" if
                          report["required_controlled_multigroup_passed"] else
                          "NO_GO_CONTROLLED_MULTIGROUP_NOT_SOLVED")
    except Exception as error:
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[ADR045] FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output=args.output/("GA-Gate09-ADR045-"+args.profile+"-"+stamp+".json")
    output.write_text(json.dumps(report,sort_keys=True,indent=2,
                                 ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-ADR045-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as a:
        a.write(output,output.name)
    print("[ADR045] STATUS:",report["status"],flush=True)
    print("[ADR045] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASSED_ADR045_MULTIGROUP_SOLVER_CONTRACT" else 1


if __name__=="__main__":
    raise SystemExit(main())
