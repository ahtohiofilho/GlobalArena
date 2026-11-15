#!/usr/bin/env python3
"""GA Gate09 ADR-047: producer-block MCP preconditioning and active columns.

Economic law strictly unchanged: frozen F072 P=sqrt(D/Q), generalized
quartic transport, seller tariffs, full supply conservation, F084F.
The solution is approved ONLY by original ADR042 independent R13/R14
commercial + finance certificate. Never a game C#/Godot engine change.

Two NUMERICAL changes compared to ADR046:
  (1) Exact block-diagonal J^T J grouped by producer (all independent
      routes of each producer plus its multiplier), assembled through
      sparse route/edge/market incidence WITHOUT the global dense J.
  (2) Initial restricted candidates: each producer's currently funded
      columns and up to two best ORIGINAL reciprocal-H destinations.
      All markets currently funded remain covered; global cheapest-route
      separation is repeated after EACH root attempt and all missed
      improving routes are added to the active set. No permanently
      excluded opportunity, no changed economic preference.

All Krylov matvecs, block factorizations and experiments are budgeted.
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
sys.path.insert(0,str(ROOT/"adr046_sparse_mcp"/"ci"))
import run_adr046 as R46
R45=R46.R45
ADR42=R46.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum,_reachable

SCHEMA="GA_GATE09_ADR047_PRODUCER_BLOCK_MCP_RESTRICTED_COLUMNS_V1"
METHOD="exact_producer_block_normal_preconditioner_restricted_market_columns"
F072_TOLERANCE=1e-4
SEED_TARGETS_PER_PRODUCER=2


@dataclass(frozen=True)
class Limits(R46.Limits):
    # Block-coefficient work is a separate measured unit from solver
    # evaluations, routes or matrix-free vector products. It can exceed
    # the legacy 20,000 upper bound without relaxing ANY old budget.
    max_block_coefficients:int=100000

    def __post_init__(self):
        for key,value in vars(self).items():
            ceiling=(250000 if key=="max_block_coefficients" else
                     60000 if key=="max_krylov_products" else 20000)
            if type(value) is not int or not 0<value<=ceiling:
                raise ValueError("Finite positive integer budget required: "+key)


class Work(R46.Work):
    def __init__(self,limits):
        super().__init__(limits)
        self.block_assemblies=0
        self.block_factorizations=0
        self.block_coefficients=0
        self.max_block_width=0
        self.active_columns_highwater=0
        self.initial_candidates_elided=0
        self.forced_global_columns=0
        self.producer_blocks=0
        self.last_interior_candidate=None
        self.last_interior_layout=None

    def evidence(self):
        return {
            **super().evidence(),
            "exact_producer_block_assemblies":self.block_assemblies,
            "small_block_Cholesky_factorizations":self.block_factorizations,
            "normal_block_coefficients_worked":self.block_coefficients,
            "maximum_producer_block_width":self.max_block_width,
            "active_columns_highwater":self.active_columns_highwater,
            "initial_allmarket_columns_avoided":self.initial_candidates_elided,
            "globally_forced_columns":self.forced_global_columns,
            "producer_block_count":self.producer_blocks,
            "no_global_dense_matrix_or_normal_system":True,
            "full_original_F072_sep_after_restricted_root":True,
            "commercial_tolerance":F072_TOLERANCE,
        }


def _sparse_J_column(lin,index,market_rows,edge_rows):
    """Nonzero rows of EXACT analytic R46 Jacobian column, no dense J."""
    layout=lin.layout
    if index>=len(layout.independent):
        gid=layout.groups[index-len(layout.independent)]
        return {i:-lin.db[i] for i in layout.by[gid]
                if lin.db[i]!=0}
    j=layout.independent[index]
    a=layout.pivot_for[j]
    result={}
    for idx,sgn in ((j,1.),(a,-1.)):
        result[idx]=result.get(idx,0.)+sgn*lin.da[idx]
        m=lin.market[idx]
        for i in market_rows.get(m,()):
            result[i]=result.get(i,0.)+sgn*(
                lin.db[i]*lin.market_coefficient[i])
        for eid in lin.edges[idx]:
            slope=sgn*lin.edge_slopes[eid]
            if slope==0:continue
            for i in edge_rows.get(eid,()):
                result[i]=result.get(i,0.)+slope*(
                    lin.db[i]*lin.ratio[i])
    return {i:v for i,v in result.items() if v!=0}


def _dot_sparse(a,b):
    if len(a)>len(b):a,b=b,a
    return math.fsum(v*b.get(k,0.) for k,v in a.items())


def _small_cholesky(matrix,lam):
    n=len(matrix)
    L=[[0.]*n for _ in range(n)]
    for i in range(n):
        for j in range(i+1):
            s=matrix[i][j]-math.fsum(L[i][k]*L[j][k]
                                    for k in range(j))
            if i==j:
                if s<=0 or not math.isfinite(s):
                    raise ArithmeticError("Producer block not SPD")
                L[i][i]=math.sqrt(s)
            else:
                L[i][j]=s/L[j][j]
    return L


def _small_cholesky_solve(L,b):
    n=len(b)
    y=[0.]*n
    for i in range(n):
        y[i]=(b[i]-math.fsum(L[i][k]*y[k] for k in range(i)))/L[i][i]
    x=[0.]*n
    for i in range(n-1,-1,-1):
        x[i]=(y[i]-math.fsum(L[k][i]*x[k] for k in range(i+1,n)))/L[i][i]
    return x


class ProducerBlocks:
    """Exact block-Jacobi normal-equations preconditioner per PRODUCER.

    Computes each local principal block of J^T J using incidence-sparse
    column gradients. Store only sum_g width_g², not (sum_g width_g)².
    Cholesky is computed once for each LM lambda per frozen snapshot.
    """
    def __init__(self,lin,lam):
        self.lin=lin
        self.work=lin.work
        self.layout=lin.layout
        if not hasattr(lin,"_producer_block_normal_core"):
            self._assemble_cores()
        cores=lin._producer_block_normal_core
        self.blocks=[]
        for coord,core in cores:
            factor=_small_cholesky([
                [v+(lam if i==j else 0.) for j,v in enumerate(row)]
                for i,row in enumerate(core)],lam)
            self.blocks.append((coord,factor))
            self.work.block_factorizations+=1

    def _assemble_cores(self):
        lin=self.lin
        work=self.work
        layout=lin.layout
        market_rows={}
        edge_rows={}
        for i,m in enumerate(lin.market):
            market_rows.setdefault(m,[]).append(i)
            for eid in lin.edges[i]:
                edge_rows.setdefault(eid,[]).append(i)
        by_group={gid:[] for gid in layout.groups}
        for k,j in enumerate(layout.independent):
            by_group[layout.cols[j].group_id].append(k)
        for gid in layout.groups:
            by_group[gid].append(layout.mu_index[gid])
        core=[]
        work.block_assemblies+=1
        for gid in layout.groups:
            coords=by_group[gid]
            width=len(coords)
            work.max_block_width=max(work.max_block_width,width)
            if work.block_coefficients+width*width>work.limits.max_block_coefficients:
                raise R45.BudgetExceeded("producer block assembly coefficient budget")
            work.block_coefficients+=width*width
            gradients=[
                _sparse_J_column(lin,k,market_rows,edge_rows)
                for k in coords]
            block=[[0.]*width for _ in range(width)]
            for i in range(width):
                for j in range(i+1):
                    v=_dot_sparse(gradients[i],gradients[j])
                    block[i][j]=v
                    block[j][i]=v
            core.append((coords,block))
        work.producer_blocks=len(core)
        lin._producer_block_normal_core=core

    def apply(self,vector):
        out=[0.]*len(vector)
        for coord,L in self.blocks:
            rhs=[vector[k] for k in coord]
            x=_small_cholesky_solve(L,rhs)
            for k,v in zip(coord,x):
                out[k]=v
        return out


def _pcg_block(lin,lam,limits,relative_tolerance=5e-5):
    """Exact group block preconditioner, same SPD equation as ADR046."""
    work=lin.work
    work.cg_solves+=1
    b=[-x for x in lin.Jtv(lin.residual)]
    norm=math.sqrt(R46._dot(b,b))
    n=len(b)
    if norm==0:return [0.]*n,True,0
    pre=ProducerBlocks(lin,lam)
    x=[0.]*n
    r=list(b)
    z=pre.apply(r)
    p=list(z)
    rz=R46._dot(r,z)
    if not (rz>0 and math.isfinite(rz)):
        raise ArithmeticError("Nonpositive block-preconditioned residual")
    target=max(1e-14,relative_tolerance*norm)
    for iteration in range(limits.max_cg_iterations):
        ap=lin.Jtv(lin.Jv(p))
        ap=[v+lam*p[i] for i,v in enumerate(ap)]
        denom=R46._dot(p,ap)
        if not (denom>0 and math.isfinite(denom)):
            raise ArithmeticError("Block CG denominator not positive")
        alpha=rz/denom
        x=[v+alpha*p[i] for i,v in enumerate(x)]
        r=[v-alpha*ap[i] for i,v in enumerate(r)]
        work.cg_iterations+=1
        if math.sqrt(R46._dot(r,r))<=target:
            return x,True,iteration+1
        z=pre.apply(r)
        nrz=R46._dot(r,z)
        if not (nrz>=0 and math.isfinite(nrz)):
            raise ArithmeticError("Nonfinite CG residual")
        beta=nrz/rz
        p=[z[i]+beta*p[i] for i in range(n)]
        rz=nrz
    work.cg_inexact_returns+=1
    return x,False,limits.max_cg_iterations


def _inner_block(layout,z,work,trace):
    """Same smoothed F-B and projected LM steps; ONLY linear algebra changes."""
    for eps in R45.EPS_SCHEDULE:
        for _ in range(22):
            lin=R46.Linearization(layout,z,eps,work)
            norm=max(abs(v) for v in lin.residual)
            if norm<=max(5e-11,eps*.01):
                break
            prior=R45._merit(lin.residual)
            accepted=False
            for k_lm in range(9):
                lam=10.**(-7+1.5*k_lm)
                try:
                    direction,converged,steps=_pcg_block(lin,lam,work.limits)
                except ArithmeticError:
                    continue
                for line in range(work.limits.max_backtrack):
                    alpha=2.**(-line)
                    try:
                        proposal=R45._feasible_projected_step(
                            layout,z,direction,alpha)
                    except ValueError:
                        work.candidates_rejected+=1
                        continue
                    try:
                        trial_r,_,_=R45._ncp_eval(
                            layout,proposal,eps,work,False)
                    except ValueError:
                        work.candidates_rejected+=1
                        work.nonfinite_q_trials_rejected+=1
                        continue
                    merit=R45._merit(trial_r)
                    if merit<prior-max(1e-28,1e-8*prior):
                        work.charge("newton_steps")
                        z=proposal
                        work.last_interior_layout=layout
                        work.last_interior_candidate=dict(layout.flows(z))
                        trace.append({
                            "epsilon":eps,
                            "step":work.newton_steps,
                            "lambda":lam,"alpha":alpha,
                            "CG_steps":steps,"CG_converged":converged,
                            "merit_before":prior,"merit_after":merit,
                            "FB_inf":max(abs(v) for v in trial_r),
                        })
                        accepted=True
                        break
                    work.candidates_rejected+=1
                if accepted:break
            if not accepted:
                trace.append({"status":"STALLED_NO_BLOCK_LM_DESCENT",
                              "epsilon":eps,"FB_inf":norm})
                return z,"STALLED_NO_BLOCK_LM_DESCENT"
    unsmoothed,_,_=R45._ncp_eval(layout,z,0.,work,False)
    norm=max(abs(v) for v in unsmoothed)
    trace.append({"unsmoothed_FB_inf":norm})
    return z,("ROOT_NCP_RESIDUAL_OK" if
              norm<=R45.ROOT_INFINITY_NORM_GATE
              else "NO_GO_NCP_RESIDUAL_TOO_LARGE")


def _restricted_initial_columns(net,flows,all_candidates,work,forced=()):
    """Choose top-two ORIGINAL H cheapest columns per group, not score hacks.

    All existing funded cols stay. Missing global opportunities are
    discovered by the original frozen shortest-route separation after
    the restricted root and added to forced routes in the next round.
    """
    loads,q,_=net.snapshots(flows)
    weight_by_seller={}
    physical={
        eid:edge.congestion_cost(loads.get(eid,0.),4)
        for eid,edge in net.edges.items()}
    funding=set(c for c,v in flows.items() if v>0)
    grouped={gid:[] for gid in net.groups}
    for c in all_candidates:
        g=net.groups[c.group_id]
        if g.seller not in weight_by_seller:
            weight_by_seller[g.seller]={
                eid:physical[eid]+float(edge.weight_for(g.seller))
                for eid,edge in net.edges.items()}
        w=weight_by_seller[g.seller]
        market=(c.destination,g.commodity)
        h=(1+sum(w[eid] for eid in c.edges))*math.sqrt(
            q.get(market,0.)/net.markets[market].demand)
        grouped[c.group_id].append((h,c))
    chosen=set(funding).union(forced)
    for gid in sorted(net.groups):
        ranked=sorted(grouped[gid],key=lambda p:(p[0],p[1]))
        chosen.update(col for _,col in ranked[:SEED_TARGETS_PER_PRODUCER])
    work.initial_candidates_elided+=len(set(all_candidates)-chosen)
    if len(chosen)>work.limits.max_columns:
        raise R45.BudgetExceeded("initial restricted-column count ceiling")
    return tuple(sorted(chosen))


def solve_block(net,flows,limits=None,settings=None,restricted=True):
    """General multigroup ACTIVE NCP solver: independently certifiable only."""
    limits=limits or Limits()
    settings=settings or ClearingSettings(exponent=4)
    if settings.exponent!=4:raise ValueError("Economic exponent unchanged 4")
    source_hash=_flow_checksum(flows)
    net.snapshots(flows)
    work=Work(limits)
    t0=perf_counter()
    trace=[]
    generations=[]
    certificate=None
    shadow=None
    status="NO_GO_NOT_STARTED"
    final=None
    try:
        _,q,_=net.snapshots(flows)
        _,_,unreachable=_reachable(net,4)
        work.charge("route_scans",len(net.groups))
        if all(q.get(m,0.)>0 for m in net.markets if m not in unreachable):
            certificate=R45._certify(net,flows,settings,work)
            if certificate["static_complementarity_approximate"]:
                final=dict(flows)
                status="PASS_EXISTING_F072_NO_UPDATES"
        if final is None:
            current=dict(flows)
            forced=set()
            for gen in range(limits.max_generations):
                all_candidates,_=R45._route_candidates(
                    net,current,settings,work,unreachable)
                selected=(
                    _restricted_initial_columns(
                        net,current,all_candidates,work,forced)
                    if restricted else all_candidates)
                columns=R45._build_active(net,current,selected,work)
                work.active_columns_highwater=max(
                    work.active_columns_highwater,len(columns))
                current=R45._cover_empty_markets(
                    net,current,columns,work,unreachable)
                layout=R45.Layout(net,columns,current,unreachable)
                z=layout.initial(current)
                initial=R46.Linearization(
                    layout,z,R45.EPS_SCHEDULE[0],work)
                for gid in layout.groups:
                    z[layout.mu_index[gid]]=min(
                        initial.H[i] for i in layout.by[gid])
                # Initial state itself is an explicitly provisional candidate.
                work.last_interior_layout=layout
                work.last_interior_candidate=dict(current)
                from_counter=work.h_evaluations
                state,root_status=_inner_block(layout,z,work,trace)
                candidate=layout.flows(state)
                missed,sep=R45._global_separate(
                    net,candidate,columns,settings,work,unreachable)
                record={
                    "generation":gen+1,"active_columns":len(columns),
                    "root_status":root_status,
                    "H_inside_root":work.h_evaluations-from_counter,
                    "missing_cheapest_paths":len(missed),
                    "missing_route_examples":list(map(repr,missed[:8])),
                    "independent_route_comparison":sep,
                }
                generations.append(record)
                if missed:
                    for c in missed:
                        forced.add(c)
                    work.forced_global_columns+=len(missed)
                    current={**candidate,**{c:0. for c in missed}}
                    continue
                _,final_q,_=net.snapshots(candidate)
                if any(final_q.get(m,0.)<=0 for m in net.markets
                       if m not in unreachable):
                    status="NO_GO_UNPRICED_REACHABLE_MARKET"
                    break
                certificate=R45._certify(net,candidate,settings,work)
                record["economic_certificate"]=certificate["status"]
                record["global_F072_gap"]=certificate.get(
                    "gap_global_complementarity")
                record["worst_group_F072_gap"]=certificate.get(
                    "gap_worst_group_complementarity")
                if certificate["static_complementarity_approximate"]:
                    final=candidate
                    status="PASS_BLOCK_MCP_FULL_ORIGINAL_F072_CERTIFIED"
                else:
                    status="NO_GO_ORIGINAL_F072_REJECTED_CANDIDATE"
                break
            else:
                status="NO_GO_GENERATION_CEILING"
    except R45.BudgetExceeded as err:
        status="NO_GO_PREDECLARED_WORK_BUDGET"
        trace.append({"budget":str(err)})
    except (ValueError,ArithmeticError,OverflowError,ZeroDivisionError) as err:
        status="NO_GO_INVALID_PHYSICAL_OR_LINEAR_STATE"
        trace.append({"numeric":repr(err)})
    # The shadow candidate is NEVER returned as a solved allocation,
    # even if it is valid and improves the original static gap.
    if final is None and work.last_interior_candidate is not None:
        tentative=work.last_interior_candidate
        try:
            _,q,_=net.snapshots(tentative)
            _,_,unreachable=_reachable(net,4)
            if all(q.get(m,0.)>0 for m in net.markets if m not in unreachable):
                if work.original_certificates<work.limits.max_original_certificates:
                    tentative_certificate=R45._certify(
                        net,tentative,settings,work)
                    shadow={
                        "status":"PROVISIONAL_NOT_COMMITTED",
                        "flow_sha256":_flow_checksum(tentative),
                        "original_F072_gap":tentative_certificate.get(
                            "gap_global_complementarity"),
                        "worst_group_gap":tentative_certificate.get(
                            "gap_worst_group_complementarity"),
                        "snapshot_only_never_a_PASS":True,
                    }
        except (ValueError,ArithmeticError,OverflowError):
            shadow={"status":"UNQUOTABLE_SHADOW"}
    if _flow_checksum(flows)!=source_hash:
        raise AssertionError("Input physical flows were mutated")
    if final is not None and (
        certificate is None or
        not certificate["static_complementarity_approximate"]):
        raise AssertionError("Invalid or unsupported commercial PASS")
    return {
        "schema":SCHEMA,"method":METHOD,
        "status":status,
        "initial_flow_sha256":source_hash,
        "final_flow_sha256":None if final is None else _flow_checksum(final),
        "final_allocations":None if final is None else [
            [c.group_id,c.destination,list(c.edges),float(q)]
            for c,q in sorted(final.items()) if q>0],
        "certificate":None if certificate is None else {
            "status":certificate["status"],
            "global_F072_gap":certificate.get("gap_global_complementarity"),
            "worst_group_F072_gap":certificate.get(
                "gap_worst_group_complementarity"),
            "F084F":certificate.get("F084F_conservation_witness"),
            "F059_ties":certificate.get("financial_F059_tie_groups"),
            "independent_R13":certificate.get("independent_R13_checks"),
            "original_R14":certificate.get("original_R14_oracle_calls"),
        },
        "provisional_last_snapshot":shadow,
        "work":work.evidence(),
        "generations":generations,
        "trace_tail":trace[-15:],
        "elapsed_ms_observed_python":round((perf_counter()-t0)*1000,3),
        "original_congestion_exponent":4,
        "original_F072_tolerance":F072_TOLERANCE,
        "production_approved":False,
        "CSharp_Godot_untouched":True,
        "financial_F059_unratified":True,
        "input_unmodified":True,
    }


def _run(name,net,flows,limits,restricted=True):
    out=solve_block(net,flows,limits=limits,restricted=restricted)
    cert=out["certificate"]
    return {
        "fixture":name,
        "algorithm":METHOD+("_restricted" if restricted else "_all_columns"),
        "status":out["status"],
        "node_count":len(set(
            v for e in net.edges.values() for v in (e.a,e.b))),
        "edge_count":len(net.edges),
        "group_count":len(net.groups),"market_count":len(net.markets),
        "global_gap":None if cert is None else cert["global_F072_gap"],
        "worst_gap":None if cert is None else cert["worst_group_F072_gap"],
        "certified":bool(cert and
            cert["status"]=="PASS_STATIC_F072_FIRST_ORDER_ONLY"),
        "provisional_uncommitted":out["provisional_last_snapshot"],
        "work":out["work"],
        "elapsed_ms":out["elapsed_ms_observed_python"],
        "last_trace":out["trace_tail"][-1] if out["trace_tail"] else None,
    }


def exercise(profile):
    if profile not in ("smoke","ci","extended"):
        raise ValueError("Unregistered work profile")
    if ADR42.R21.validate_manifest()!=16:
        raise AssertionError("R20 SHA16 immutable baseline changed")
    cases=[
        ("two_group_nonoptimal",*R45.two_group_nonoptimal_fixture()),
        ("two_group_alternative_routes",*R45.route_switch_fixture()),
    ]
    if profile!="smoke":
        cases.extend([
            ("42_original",*ADR42.R34.capacity_fixture(
                ADR42.R21.CASES["small"],"original",1.)[:2]),
            ("162_heterogeneous_K02",*ADR42.R34.capacity_fixture(
                ADR42.R21.CASES["medium"],"heterogeneous_D_and_supply",.2)[:2]),
        ])
    rows=[]
    for name,net,flows in cases:
        limit=Limits(max_h_evaluations=1250,max_route_scans=800,
                     max_newton_steps=74,max_columns=400,
                     max_generations=5,max_krylov_products=15000,
                     max_cg_iterations=150)
        row=_run(name,net,flows,limit)
        rows.append(row)
        print("[ADR047:{}] status={} F072={} worst={} ms={} "
              "H={} Krylov={} CG={} blocks={} active={}".format(
                  name,row["status"],row["global_gap"],row["worst_gap"],
                  row["elapsed_ms"],
                  row["work"]["H_evaluations_including_rejected_trials"],
                  row["work"]["matrixfree_Jv_and_Jtv_products"],
                  row["work"]["CG_iterations_total"],
                  row["work"]["small_block_Cholesky_factorizations"],
                  row["work"]["active_columns_highwater"]),flush=True)
    if profile in ("ci","extended"):
        net,flows,meta=R46._large_fixture()
        # Same world R46 previously failed, but richer work budget:
        # less than R46 20,000 J products, explicit fixed budget now 14k.
        if profile=="extended":
            # Separate operational stress envelope. Predeclared after the
            # 65/65 Newton NO-GO of the initial 100k-block experiment.
            # Only the numerical work limits change, NOT F072 economics.
            limit=Limits(
                max_h_evaluations=650,max_route_scans=1200,
                max_original_certificates=6,max_columns=500,
                max_generations=5,max_newton_steps=165,
                max_backtrack=10,max_krylov_products=36000,
                max_cg_iterations=130,max_block_coefficients=180000)
        else:
            limit=Limits(
                max_h_evaluations=430,max_route_scans=900,
                max_original_certificates=4,max_columns=440,
                max_generations=5,max_newton_steps=65,
                max_backtrack=10,max_krylov_products=14000,
                max_cg_iterations=130,max_block_coefficients=100000)
        row=_run("2562_nodes_original_48_groups_24_markets",
                 net,flows,limit)
        row["large_world_descriptor"]=meta
        rows.append(row)
        print("[ADR047:2562_nodes] status={} F072={} worst={} "
              "shadow={} H={} Krylov={} CG={} blocks={} active={} ms={}".format(
                  row["status"],row["global_gap"],row["worst_gap"],
                  row["provisional_uncommitted"],
                  row["work"]["H_evaluations_including_rejected_trials"],
                  row["work"]["matrixfree_Jv_and_Jtv_products"],
                  row["work"]["CG_iterations_total"],
                  row["work"]["small_block_Cholesky_factorizations"],
                  row["work"]["active_columns_highwater"],
                  row["elapsed_ms"]),flush=True)
    pass_set={
        x["fixture"] for x in rows
        if x["certified"] and x["status"].startswith("PASS")}
    required={"two_group_nonoptimal","two_group_alternative_routes"}
    return {
        "cases":rows,
        "controlled_multigroup_gate_pass":required.issubset(pass_set),
        "full_F072_static_pass_2562_nodes":(
            "2562_nodes_original_48_groups_24_markets" in pass_set),
        "big_world_status_transparent":True,
        "R20_immutable_file_manifest_count":16,
        "no_economic_law_change":True,
        "financial_F059_unratified":True,
        "CSharp_production_approved":False,
        "scope":"limited experimental Python MCP not game engine",
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=("smoke","ci","extended"),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "profile":a.profile,
        "utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),
        "python":platform.python_version(),
        "status":"FAILED",
        "production_approved":False,
        "F059_ratified":False,
    }
    try:
        report.update(exercise(a.profile))
        report["status"]=(
            "PASS_CONTROLLED_BLOCK_MCP_AND_BOUNDED_SCALE_AUDIT" if
            report["controlled_multigroup_gate_pass"] else
            "NO_GO_BLOCK_MCP_CONTROLLED_CASES")
    except Exception as error:
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[ADR047] FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    json_path=a.output/("GA-Gate09-ADR047-"+a.profile+"-"+stamp+".json")
    json_path.write_text(
        json.dumps(report,indent=2,sort_keys=True,ensure_ascii=False)+"\n",
        encoding="utf-8")
    archive=a.output/("GA-Gate09-ADR047-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(json_path,json_path.name)
    print("[ADR047] STATUS:",report["status"],flush=True)
    print("[ADR047] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASS_CONTROLLED_BLOCK_MCP_AND_BOUNDED_SCALE_AUDIT" else 1


if __name__=="__main__":
    raise SystemExit(main())
