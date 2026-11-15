#!/usr/bin/env python3
"""GA Gate09 ADR-046 — matrix-free sparse-incidence MCP/VI scaling trial.

Preserve F072, F084F, route/ownership economics, tolerances and the
independent ADR042 acceptance rule of ADR045. Replace ONLY the expensive
DENSE analytic Jacobian and dense LM normal-equation linear solve with
exact analytic Jacobian-vector + transpose-vector operations on route,
edge, and market incidence, and a bounded preconditioned conjugate
gradient solve of (J^T J + lambda I)delta = -J^T residual.

No claimed general convergence theorem, no C# or Godot authority and
no relaxed congestion exponent. All work units and rejected attempts
are recorded. New numerical success only when ADR042 says PASS.
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
sys.path.insert(0,str(ROOT/"adr045_general_mcp"/"ci"))
import run_adr045 as R45
ADR42=R45.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum,_reachable
from multimarket_r13 import R13Scenario,construct_scenario

SCHEMA="GA_GATE09_ADR046_MATRIX_FREE_SPARSE_INCIDENCE_MCP_V1"
METHOD="analytic_route_edge_market_Jv_Jtv_preconditioned_CG_LM"
EPS_SCHEDULE=R45.EPS_SCHEDULE
F072_TOLERANCE=1e-4


@dataclass(frozen=True)
class Limits(R45.Limits):
    # Count BOTH Jv and J^T v: no hidden inner Krylov work.
    max_krylov_products:int=16000
    max_cg_iterations:int=180

    def __post_init__(self):
        super().__post_init__()


class Work(R45.Work):
    def __init__(self,limits):
        super().__init__(limits)
        self.krylov_products=0
        self.cg_iterations=0
        self.cg_solves=0
        self.cg_inexact_returns=0
        self.linearizations=0
        self.route_incidence_entries=0
        self.max_dense_jacobian_elements_avoided=0
        self.exact_sparse_jacobi_assemblies=0

    def charge_matvec(self):
        if self.krylov_products>=self.limits.max_krylov_products:
            raise R45.BudgetExceeded("matrix-free Krylov products budget")
        self.krylov_products+=1

    def evidence(self):
        return {
            **super().evidence(),
            "matrixfree_Jv_and_Jtv_products":self.krylov_products,
            "CG_iterations_total":self.cg_iterations,
            "CG_linear_solves":self.cg_solves,
            "CG_inexact_returns":self.cg_inexact_returns,
            "matrixfree_linearizations":self.linearizations,
            "exact_sparse_jacobi_assemblies":self.exact_sparse_jacobi_assemblies,
            "peak_route_edge_incidence_entries":self.route_incidence_entries,
            "peak_dense_jacobian_elements_not_allocated":
                self.max_dense_jacobian_elements_avoided,
            "no_dense_J_or_normal_matrix_created_by_trial":True,
            "exact_quartic_edge_derivative_in_Jv_Jtv":True,
        }


def _dot(a,b):
    return math.fsum(x*y for x,y in zip(a,b))


class Linearization:
    """Exact matrix-free Jacobian of smoothed F-B at a F072 physical x.

    Stores O(columns + total route-edge incidence + active edges + markets)
    rather than O(columns**2) Jacobian entries. A transpose product is
    obtained analytically from the SAME frozen market and edge adjoints.
    """
    def __init__(self,layout,z,epsilon,work):
        work.charge("h_evaluations")
        work.linearizations+=1
        self.layout=layout
        self.work=work
        self.cols=layout.cols
        self.groups=layout.groups
        net=layout.net
        x,mu=layout.lift(z)
        self.x=x
        loads,q,_=net.snapshots({c:v for c,v in zip(self.cols,x)})
        if any(q.get(m,0.)<=0 for m in net.markets
               if m not in layout.unreachable):
            raise ValueError("Differentiable VI requires positive reachable Q")
        self.market=tuple(
            (c.destination,net.groups[c.group_id].commodity)
            for c in self.cols)
        self.edges=tuple(c.edges for c in self.cols)
        self.market_q=q
        self.edge_slopes={
            eid:4*e.c_free*(loads.get(eid,0.)/e.capacity)**3/e.capacity
            for eid,e in net.edges.items()}
        edge_cost={
            eid:e.congestion_cost(loads.get(eid,0.),4)
            for eid,e in net.edges.items()}
        self.da=[]
        self.db=[]
        self.ratio=[]
        self.market_coefficient=[]
        self.H=[]
        self.residual=[]
        self.group_mu=mu
        for i,c in enumerate(self.cols):
            g=net.groups[c.group_id]
            m=self.market[i]
            ratio=math.sqrt(q[m]/net.markets[m].demand)
            level=1+sum(edge_cost[e]+float(net.edges[e].weight_for(g.seller))
                        for e in c.edges)
            h=ratio*level
            b=h-mu[c.group_id]
            a=x[i]
            den=math.sqrt(a*a+b*b+2*epsilon*epsilon)
            self.H.append(h)
            self.ratio.append(ratio)
            self.market_coefficient.append(ratio*level/(2*q[m]))
            self.da.append(a/den-1)
            self.db.append(b/den-1)
            self.residual.append(den-a-b)
        self.edge_incidence_entries=sum(len(route) for route in self.edges)
        work.route_incidence_entries=max(
            work.route_incidence_entries,self.edge_incidence_entries)
        n=layout.ncols
        work.max_dense_jacobian_elements_avoided=max(
            work.max_dense_jacobian_elements_avoided,n*n)

    def _physical_dx(self,v):
        layout=self.layout
        dx=[0.]*layout.ncols
        for k,j in enumerate(layout.independent):
            change=v[k]
            dx[j]+=change
            dx[layout.pivot_for[j]]-=change
        return dx

    def Jv(self,v):
        self.work.charge_matvec()
        if len(v)!=self.layout.unknowns:
            raise ValueError("Incorrect Jv dimension")
        dx=self._physical_dx(v)
        dQ={}
        dB={}
        for i,c in enumerate(self.cols):
            dv=dx[i]
            if dv==0:continue
            m=self.market[i]
            dQ[m]=dQ.get(m,0.)+dv
            for e in self.edges[i]:
                dB[e]=dB.get(e,0.)+dv
        edge_dr={
            e:self.edge_slopes[e]*dB.get(e,0.)
            for e in dB}
        return [
            self.da[i]*dx[i]+self.db[i]*(
                self.ratio[i]*sum(edge_dr.get(e,0.)
                                  for e in self.edges[i])
                +self.market_coefficient[i]*dQ.get(self.market[i],0.)
                -v[self.layout.mu_index[c.group_id]])
            for i,c in enumerate(self.cols)
        ]

    def Jtv(self,v):
        self.work.charge_matvec()
        if len(v)!=self.layout.ncols:
            raise ValueError("Incorrect Jtv dimension")
        m_adj={}
        e_adj={}
        group_adj={gid:0. for gid in self.groups}
        for i,c in enumerate(self.cols):
            wi=v[i]*self.db[i]
            m=self.market[i]
            m_adj[m]=m_adj.get(m,0.)+wi*self.market_coefficient[i]
            for e in self.edges[i]:
                e_adj[e]=e_adj.get(e,0.)+wi*self.ratio[i]
            group_adj[c.group_id]-=wi
        grad=[
            self.da[i]*v[i]+m_adj.get(self.market[i],0.)+
            sum(self.edge_slopes[e]*e_adj.get(e,0.)
                for e in self.edges[i])
            for i,c in enumerate(self.cols)
        ]
        answer=[
            grad[j]-grad[self.layout.pivot_for[j]]
            for j in self.layout.independent]
        answer.extend(group_adj[gid] for gid in self.groups)
        return answer

    def precond_diagonal(self,lam):
        """EXACT Jacobi of J^T J + lambda I, WITHOUT allocating dense J.

        Aggregate each independent-column/anchor differential through
        market and edge incidence to compute its squared Jacobian norm.
        Storage O(ncols + route incidences), not O(ncols**2).
        Reuses the diagonal for every LM regularizer at the SAME x.
        """
        if not hasattr(self,"_exact_jacobi_core"):
            market_rows={}
            edge_rows={}
            for i,m in enumerate(self.market):
                market_rows.setdefault(m,[]).append(i)
                for e in self.edges[i]:
                    edge_rows.setdefault(e,[]).append(i)
            market_weight=[
                self.db[i]*self.market_coefficient[i]
                for i in range(len(self.cols))]
            edge_weight=[
                self.db[i]*self.ratio[i]
                for i in range(len(self.cols))]
            diag=[]
            for j in self.layout.independent:
                a=self.layout.pivot_for[j]
                affected={j:self.da[j],a:-self.da[a]}
                for idx,sgn in ((j,1.),(a,-1.)):
                    for i in market_rows[self.market[idx]]:
                        affected[i]=affected.get(i,0.)+(
                            sgn*market_weight[i])
                    for e in self.edges[idx]:
                        slope=sgn*self.edge_slopes[e]
                        for i in edge_rows[e]:
                            affected[i]=affected.get(i,0.)+(
                                slope*edge_weight[i])
                diag.append(math.fsum(v*v for v in affected.values()))
            for gid in self.layout.groups:
                diag.append(math.fsum(
                    self.db[i]*self.db[i]
                    for i in self.layout.by[gid]))
            self._exact_jacobi_core=diag
            self.work.exact_sparse_jacobi_assemblies+=1
        return [
            max(1e-9,lam+d) for d in self._exact_jacobi_core]




def _pcg_normal(lin,lam,limits,relative_tolerance=5e-5):
    """Finite-work PCG on LM SPD normal equations, no dense matrices."""
    work=lin.work
    work.cg_solves+=1
    rhs=[-t for t in lin.Jtv(lin.residual)]
    rhs_norm=math.sqrt(_dot(rhs,rhs))
    n=len(rhs)
    if rhs_norm==0:
        return [0.]*n,True,0
    inv=[1/x for x in lin.precond_diagonal(lam)]
    x=[0.]*n
    r=list(rhs)
    z=[r[i]*inv[i] for i in range(n)]
    p=list(z)
    rz=_dot(r,z)
    if not (rz>0 and math.isfinite(rz)):
        raise ArithmeticError("Ill-conditioned PCG preconditioner")
    tolerance=max(1e-14,relative_tolerance*rhs_norm)
    for iteration in range(limits.max_cg_iterations):
        Ap=lin.Jtv(lin.Jv(p))
        Ap=[Ap[i]+lam*p[i] for i in range(n)]
        denominator=_dot(p,Ap)
        if not (denominator>0 and math.isfinite(denominator)):
            raise ArithmeticError("LM normal equation is not positive definite")
        alpha=rz/denominator
        x=[x[i]+alpha*p[i] for i in range(n)]
        r=[r[i]-alpha*Ap[i] for i in range(n)]
        work.cg_iterations+=1
        if math.sqrt(_dot(r,r))<=tolerance:
            return x,True,iteration+1
        z=[r[i]*inv[i] for i in range(n)]
        next_rz=_dot(r,z)
        if not (math.isfinite(next_rz) and next_rz>=0):
            raise ArithmeticError("PCG lost finite positive residual")
        beta=next_rz/rz
        p=[z[i]+beta*p[i] for i in range(n)]
        rz=next_rz
    work.cg_inexact_returns+=1
    return x,False,limits.max_cg_iterations


def _inner_matrixfree(layout,z,work,trace):
    """R45 continuation and strict physical line search, sparse LM direction."""
    for eps in EPS_SCHEDULE:
        for _ in range(22):
            lin=Linearization(layout,z,eps,work)
            r=lin.residual
            error=max(abs(v) for v in r)
            if error<=max(5e-11,eps*.01):
                break
            old=R45._merit(r)
            success=False
            for k_lm in range(9):
                lam=10.**(-7+1.5*k_lm)
                try:
                    direction,converged,cg_steps=_pcg_normal(
                        lin,lam,work.limits)
                except ArithmeticError:
                    continue
                for line in range(work.limits.max_backtrack):
                    alpha=2.**(-line)
                    try:
                        proposal=R45._feasible_projected_step(
                            layout,z,direction,alpha)
                        layout.decode(proposal)
                    except ValueError:
                        work.candidates_rejected+=1
                        continue
                    try:
                        trial_r,_,_=R45._ncp_eval(
                            layout,proposal,eps,work,False)
                    except ValueError:
                        work.nonfinite_q_trials_rejected+=1
                        work.candidates_rejected+=1
                        continue
                    merit=R45._merit(trial_r)
                    if merit<old-max(1e-28,1e-8*old):
                        work.charge("newton_steps")
                        z=proposal
                        trace.append({
                            "eps":eps,"newton_step":work.newton_steps,
                            "lambda":lam,"alpha":alpha,
                            "CG_steps":cg_steps,
                            "CG_converged":converged,
                            "merit_before":old,"merit_after":merit,
                            "FB_inf_after":max(abs(x) for x in trial_r),
                        })
                        success=True
                        break
                    work.candidates_rejected+=1
                if success:break
            if not success:
                trace.append({
                    "eps":eps,"status":"STALLED_NO_DESCENT_MATRIXFREE_LM",
                    "FB_inf":error})
                return z,"STALLED_NO_DESCENT_MATRIXFREE_LM"
    final,_,_=R45._ncp_eval(layout,z,0.,work,False)
    error=max(abs(v) for v in final)
    trace.append({"final_unsmoothed_FB_inf":error})
    return z,("ROOT_NCP_RESIDUAL_OK" if
              error<=R45.ROOT_INFINITY_NORM_GATE
              else "NO_GO_NCP_RESIDUAL_TOO_LARGE")


def solve_sparse(net,initial_flows,limits=None,settings=None):
    """Actual new multigroup solver, original F072 independently certifies."""
    limits=limits or Limits()
    settings=settings or ClearingSettings(exponent=4)
    if settings.exponent!=4:
        raise ValueError("Only original quartic congestion is supported")
    digest=_flow_checksum(initial_flows)
    net.snapshots(initial_flows)
    work=Work(limits)
    t0=perf_counter()
    generations=[]
    trace=[]
    final=None
    cert=None
    reason="UNSPECIFIED"
    try:
        _,q,_=net.snapshots(initial_flows)
        _,_,unreachable=_reachable(net,settings.exponent)
        work.charge("route_scans",len(net.groups))
        if all(q.get(m,0.)>0 for m in net.markets if m not in unreachable):
            cert=R45._certify(net,initial_flows,settings,work)
            if cert["static_complementarity_approximate"]:
                final=dict(initial_flows)
                reason="PASS_EXISTING_STATIC_F072_NO_UPDATE"
        if final is None:
            current=dict(initial_flows)
            for gen in range(limits.max_generations):
                viable,unreachable=R45._route_candidates(
                    net,current,settings,work,unreachable)
                columns=R45._build_active(net,current,viable,work)
                current=R45._cover_empty_markets(
                    net,current,columns,work,unreachable)
                layout=R45.Layout(net,columns,current,unreachable)
                z=layout.initial(current)
                initial_lin=Linearization(layout,z,EPS_SCHEDULE[0],work)
                for gid in layout.groups:
                    z[layout.mu_index[gid]]=min(
                        initial_lin.H[i] for i in layout.by[gid])
                before=work.h_evaluations
                solved,solver_status=_inner_matrixfree(
                    layout,z,work,trace)
                candidate=layout.flows(solved)
                missed,separation=R45._global_separate(
                    net,candidate,columns,settings,work,unreachable)
                gen_report={
                    "generation":gen+1,
                    "columns":len(columns),
                    "H_in_inner":work.h_evaluations-before,
                    "root_solver_status":solver_status,
                    "globally_dominant_omitted_routes":len(missed),
                    "route_separation":separation,
                }
                generations.append(gen_report)
                if missed:
                    if len(set(columns).union(missed))>limits.max_columns:
                        raise R45.BudgetExceeded("column ceiling after separation")
                    current={**candidate,**{c:0. for c in missed}}
                    continue
                _,new_q,_=net.snapshots(candidate)
                if any(new_q.get(m,0.)<=0 for m in net.markets
                       if m not in unreachable):
                    reason="NO_GO_REACHABLE_MARKET_UNPRICED"
                    break
                cert=R45._certify(net,candidate,settings,work)
                gen_report["economic_certificate"]=cert["status"]
                gen_report["global_gap"]=cert.get("gap_global_complementarity")
                gen_report["worst_gap"]=cert.get("gap_worst_group_complementarity")
                if cert["static_complementarity_approximate"]:
                    reason="PASS_NEW_MATRIXFREE_STATIC_F072_CERTIFIED"
                    final=candidate
                else:
                    reason="NO_GO_MATRIXFREE_ORIGINAL_F072_GAP"
                break
            else:
                reason="NO_GO_COLUMN_GENERATIONS_LIMIT"
    except R45.BudgetExceeded as err:
        reason="NO_GO_PREDECLARED_BUDGET"
        trace.append({"budget_reason":str(err)})
    except (ValueError,ArithmeticError,OverflowError,ZeroDivisionError) as err:
        reason="NO_GO_NUMERICAL_FEASIBILITY"
        trace.append({"numerical_error":repr(err)})
    if _flow_checksum(initial_flows)!=digest:
        raise AssertionError("Sparse solver modified input allocation")
    if final is not None and (
        cert is None or not cert["static_complementarity_approximate"]):
        raise AssertionError("Uncertified economic candidate")
    return {
        "schema":SCHEMA,
        "method":METHOD,
        "status":reason,
        "start_flow_sha256":digest,
        "end_flow_sha256":None if final is None else _flow_checksum(final),
        "candidate_flows":None if final is None else [
            [c.group_id,c.destination,list(c.edges),float(v)]
            for c,v in sorted(final.items()) if v>0],
        "certificate":None if cert is None else {
            "status":cert["status"],
            "global_gap":cert.get("gap_global_complementarity"),
            "worst_gap":cert.get("gap_worst_group_complementarity"),
            "R14_oracle":cert.get("original_R14_oracle_calls"),
            "independent_R13":cert.get("independent_R13_checks"),
            "F084F":cert.get("F084F_conservation_witness"),
            "F059_financial_ties":cert.get("financial_F059_tie_groups"),
        },
        "work":work.evidence(),
        "generations":generations,"trace":trace,
        "wall_ms_observed_python":round((perf_counter()-t0)*1000,3),
        "fixed_original_price_and_congestion":True,
        "F072_tolerance_unchanged":F072_TOLERANCE,
        "no_global_convergence_proof":True,
        "financial_F059_ratified":False,
        "CSharp_integrated":False,
        "production_approved":False,
        "input_unmodified":True,
    }


def _large_fixture():
    # 10*16*16+2=2562 nodes, 7680 edges, 48 producer groups,
    # 24 real priced markets, up to 288 canonical route columns.
    # Bigger topology and commercial matrix in the SAME R13 generator.
    cfg=R13Scenario(m=16,products=4,markets_per_product=6,
                    groups_per_product=12,civilizations=6,
                    iterations=0,exponent=4)
    net,flows=construct_scenario(cfg)
    return net,flows,{
        "nodes":10*cfg.m*cfg.m+2,
        "edges":30*cfg.m*cfg.m,
        "producers":len(net.groups),
        "markets":len(net.markets),
        "original_generator":"R13 deterministic original F072",
    }


def _run(name,net,flows,limits,dense=False):
    result=(R45.solve_mcp(net,flows,limits=R45.Limits(
        max_h_evaluations=limits.max_h_evaluations,
        max_route_scans=limits.max_route_scans,
        max_original_certificates=limits.max_original_certificates,
        max_columns=limits.max_columns,
        max_generations=limits.max_generations,
        max_newton_steps=limits.max_newton_steps,
        max_backtrack=limits.max_backtrack))
        if dense else solve_sparse(net,flows,limits))
    cert=result["certificate"]
    return {
        "fixture":name,
        "algorithm":"original_R45_dense_baseline" if dense else METHOD,
        "status":result["status"],
        "n_nodes":len({e.a for e in net.edges.values()}.union(
            e.b for e in net.edges.values())),
        "n_edges":len(net.edges),"n_groups":len(net.groups),
        "n_markets":len(net.markets),
        "wall_ms":(
            result["elapsed_ms_observational"] if dense
            else result["wall_ms_observed_python"]),
        "global_gap_original_F072":None if not cert else (
            cert["global_F072_gap"] if dense else cert["global_gap"]),
        "worst_gap_original_F072":None if not cert else (
            cert["worst_group_F072_gap"] if dense else cert["worst_gap"]),
        "work":result["work"],
        "last_solver_trace_record":result.get("trace",[])[-1]
            if result.get("trace") else None,
        "candidate_valid_independent_certificate":bool(
            cert and cert["status"]=="PASS_STATIC_F072_FIRST_ORDER_ONLY"),
        "never_called_production_ready":True,
    }


def exercise(profile):
    if profile not in ("smoke","ci","extended"):
        raise ValueError("Unknown bounded matrix-free benchmark profile")
    if ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Immutable original R20 SHA manifest drift")
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
    for label,net,flows in cases:
        limits=Limits(max_h_evaluations=900,max_route_scans=520,
                      max_columns=320,max_newton_steps=54,
                      max_krylov_products=15000,max_cg_iterations=130)
        if label=="162_heterogeneous_K02":
            limits=Limits(max_h_evaluations=1200,max_route_scans=680,
                          max_columns=400,max_newton_steps=70,
                          max_krylov_products=18000,max_cg_iterations=180)
        for dense in ((False,True) if label=="two_group_nonoptimal"
                      else (False,)):
            row=_run(label,net,flows,limits,dense)
            rows.append(row)
            print("[ADR046:{}:{}] status={} global={} worst={} "
                  "ms={} H={} Krylov={} scans={} ".format(
                      label,"dense" if dense else "matrixfree",
                      row["status"],row["global_gap_original_F072"],
                      row["worst_gap_original_F072"],row["wall_ms"],
                      row["work"].get("H_evaluations_including_rejected_trials"),
                      row["work"].get("matrixfree_Jv_and_Jtv_products"),
                      row["work"].get("separate_shortest_path_scans")
                  ),flush=True)
    if profile in ("ci","extended"):
        net,flows,meta=_large_fixture()
        limits=Limits(max_h_evaluations=480,max_route_scans=850,
                      max_original_certificates=2,max_columns=500,
                      max_newton_steps=80,max_generations=2,
                      max_backtrack=10,max_krylov_products=20000,
                      max_cg_iterations=170)
        row=_run("2562_nodes_48_groups_24_markets",net,flows,limits)
        row["fixture_descriptor"]=meta
        rows.append(row)
        print("[ADR046:2562_nodes:matrixfree] status={} global={} "
              "worst={} ms={} H={} Krylov={} scans={} stop={}".format(
                  row["status"],row["global_gap_original_F072"],
                  row["worst_gap_original_F072"],row["wall_ms"],
                  row["work"]["H_evaluations_including_rejected_trials"],
                  row["work"]["matrixfree_Jv_and_Jtv_products"],
                  row["work"]["separate_shortest_path_scans"],
                  row["last_solver_trace_record"]),flush=True)
    expected={x["fixture"] for x in rows if x["fixture"] in (
        "two_group_nonoptimal","two_group_alternative_routes")}
    required={x["fixture"] for x in rows
              if x["algorithm"]==METHOD and
              x["fixture"] in ("two_group_nonoptimal",
                              "two_group_alternative_routes") and
              x["candidate_valid_independent_certificate"]}
    return {
        "cases":rows,
        "profile":profile,
        "controlled_cases_all_pass":required==expected and len(required)==2,
        "original_dense_F072_economics_unchanged":True,
        "large_case_may_be_NO_GO_under_hard_budget":True,
        "future_more_worker_market_density_not_established":True,
        "no_economy_param_relaxation":True,
        "F059_not_ratified":True,
        "production_approved":False,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={"schema":SCHEMA,
            "platform":platform.platform(),
            "python":platform.python_version(),
            "generated_utc":datetime.now(timezone.utc).isoformat(),
            "profile":args.profile,
            "status":"FAILED",
            "original_F072_congestion_exponent":4,
            "original_merchant_tariffs_and_ownership_unchanged":True,
            "financial_F059_ratified":False,
            "CSharp_authority_untouched":True,
            "global_equilibrium_algorithm_proven":False}
    try:
        report.update(exercise(args.profile))
        report["status"]=(
            "PASSED_SPARSE_CONTROLLED_MCP_AND_SCALE_AUDIT" if
            report["controlled_cases_all_pass"] else
            "NO_GO_MATRIXFREE_CONTROLLED_CERTIFICATION")
    except Exception as err:
        report["error"]=repr(err)
        report["traceback"]=traceback.format_exc()
        print("[ADR046] FAILED:",repr(err),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=args.output/("GA-Gate09-ADR046-"+args.profile+"-"+stamp+".json")
    path.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-ADR046-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as a:
        a.write(path,path.name)
    print("[ADR046] STATUS:",report["status"],flush=True)
    print("[ADR046] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASSED_SPARSE_CONTROLLED_MCP_AND_SCALE_AUDIT" else 1


if __name__=="__main__":
    raise SystemExit(main())
