#!/usr/bin/env python3
"""GA ADR-051: experimental bounded small-block LM and worker-coupled audit.

No change to ADR047 solver, original R13 data or authoritative C#/Godot.
A temporary in-process injection replaces only the SPD PRECONDITIONER
for this laboratory experiment, preserving the exact global Jv/Jtv,
F-B equations, quartic congestion and independent ADR042 acceptance.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"adr049_geography_density"/"ci"))
import run_adr049 as R49
R47=R49.R47
R45=R49.R45
R46=R47.R46
from clearing_r5 import _flow_checksum
from worker_coupled import (
    ACTIVITY_RATES,CYCLE_TURNS,build_worker_network,demand_by_tile,
)

SCHEMA="GA_GATE09_ADR051_WORKER_TIME_DEMAND_AND_CHUNKED_SPD_MCP_V1"
OLD_WORK=R47.Work
OLD_BLOCK=R47.ProducerBlocks
F072_TOL=1e-4


class Work(OLD_WORK):
    def __init__(self,limits):
        super().__init__(limits)
        self.chunk_count=0
        self.max_chunk_width=0
        self.full_group_square_charge_counterfactual=0
        self.full_group_triangular_pairs_counterfactual=0
        self.actual_sparse_symmetric_dot_products=0

    def evidence(self):
        return {
            **super().evidence(),
            "local_SPD_chunk_count_last_linearization":self.chunk_count,
            "maximum_SPD_chunk_width":self.max_chunk_width,
            "full_group_square_charge_counterfactual":
                self.full_group_square_charge_counterfactual,
            "full_group_triangular_dot_counterfactual":
                self.full_group_triangular_pairs_counterfactual,
            "actual_sparse_symmetric_dot_products":
                self.actual_sparse_symmetric_dot_products,
            "charged_block_coefficients_are_actual_triangular_dots":True,
            "not_comparable_to_ADR047_square_width_budget_as_same_unit":True,
            "preserves_exact_global_Jv_Jtv":True,
        }


class ChunkBlocks(OLD_BLOCK):
    """SPD preconditioner with exact principal normal sub-blocks (width<=2).

    The global LM equation is unchanged. Removing cross-chunk entries
    affects ONLY the approximate inverse/preconditioner; exact global
    Jv/Jtv still appears in every CG matvec and the economic certificate.
    """
    MAX_CHUNK_WIDTH=2

    def _assemble_cores(self):
        lin=self.lin
        work=self.work
        layout=lin.layout
        market_rows={}
        edge_rows={}
        for i,m in enumerate(lin.market):
            market_rows.setdefault(m,[]).append(i)
            for e in lin.edges[i]:
                edge_rows.setdefault(e,[]).append(i)
        coords_by_group={gid:[] for gid in layout.groups}
        for k,j in enumerate(layout.independent):
            coords_by_group[layout.cols[j].group_id].append(k)
        for gid in layout.groups:
            coords_by_group[gid].append(layout.mu_index[gid])
        blocks=[]
        work.block_assemblies+=1
        for gid in layout.groups:
            full=coords_by_group[gid]
            full_width=len(full)
            work.full_group_square_charge_counterfactual+=full_width**2
            work.full_group_triangular_pairs_counterfactual+=(
                full_width*(full_width+1)//2)
            # Keep mu adjacent to the last route variable in the same
            # local preconditioner sub-block; every coordinate occurs ONCE.
            partitions=[]
            prefix=full[:-self.MAX_CHUNK_WIDTH]
            for start in range(0,len(prefix),self.MAX_CHUNK_WIDTH):
                partitions.append(prefix[start:start+self.MAX_CHUNK_WIDTH])
            partitions.append(full[-self.MAX_CHUNK_WIDTH:])
            assert sorted(k for block in partitions for k in block)==sorted(full)
            for coords in partitions:
                width=len(coords)
                work.max_chunk_width=max(work.max_chunk_width,width)
                work.max_block_width=max(work.max_block_width,width)
                # The original code computes only lower triangular
                # sparse dot products but charged full width**2. This
                # new metric counts the dot products ACTUALLY evaluated.
                actual=width*(width+1)//2
                if work.block_coefficients+actual>work.limits.max_block_coefficients:
                    raise R45.BudgetExceeded("actual chunked-block dot product budget")
                work.block_coefficients+=actual
                work.actual_sparse_symmetric_dot_products+=actual
                gradients=[
                    R47._sparse_J_column(lin,k,market_rows,edge_rows)
                    for k in coords]
                core=[[0.]*width for _ in range(width)]
                for i in range(width):
                    for j in range(i+1):
                        v=R47._dot_sparse(gradients[i],gradients[j])
                        core[i][j]=v
                        core[j][i]=v
                blocks.append((coords,core))
        work.producer_blocks=len(layout.groups)
        work.chunk_count=max(work.chunk_count,len(blocks))
        lin._producer_block_normal_core=blocks


def solve_chunked(net,flows,limits):
    """Injection scoped to a single synchronous lab call, always restored."""
    original_work=R47.Work
    original_block=R47.ProducerBlocks
    if original_work is not OLD_WORK or original_block is not OLD_BLOCK:
        raise RuntimeError("Concurrent/external ADR047 patch detected")
    try:
        R47.Work=Work
        R47.ProducerBlocks=ChunkBlocks
        return R47.solve_block(net,flows,limits=limits,restricted=True)
    finally:
        R47.Work=original_work
        R47.ProducerBlocks=original_block


def is_certified(out):
    cert=out.get("certificate")
    return bool(out["status"].startswith("PASS_") and cert
                and cert.get("status")=="PASS_STATIC_F072_FIRST_ORDER_ONLY"
                and out.get("final_flow_sha256")
                and cert.get("global_F072_gap") is not None
                and cert.get("worst_group_F072_gap") is not None
                and cert["global_F072_gap"]<=F072_TOL
                and cert["worst_group_F072_gap"]<=F072_TOL
                and out.get("original_congestion_exponent")==4)


def _summary(name,net,flows,out):
    ok=is_certified(out)
    if out["status"].startswith("PASS") and not ok:
        raise AssertionError("False pass without independent F072 original")
    if not ok and out["final_flow_sha256"] is not None:
        raise AssertionError("Uncertified flow published")
    cert=out.get("certificate")
    return {
        "case":name,
        "nodes_in_original_geography":len(net.adj),
        "groups":len(net.groups),
        "markets_node_product":len(net.markets),
        "status":out["status"],
        "independent_F072_certified":ok,
        "F072_global_FINAL_if_pass":cert["global_F072_gap"] if ok else None,
        "F072_worst_group_FINAL_if_pass":
            cert["worst_group_F072_gap"] if ok else None,
        "last_audited_global_not_necessarily_final":
            cert["global_F072_gap"] if cert else None,
        "last_audited_worst_not_necessarily_final":
            cert["worst_group_F072_gap"] if cert else None,
        "provisional_uncommitted":out["provisional_last_snapshot"],
        "time_python_solver_ms":out["elapsed_ms_observed_python"],
        "work":out["work"],
        "generation_summary":out["generations"],
        "not_a_game_snapshot":True,
        "F059_ratified":False,
    }


def worker_fixture_trial():
    net,flows,metadata=build_worker_network()
    before=_flow_checksum(flows)
    limits=R47.Limits(
        max_h_evaluations=240,max_route_scans=130,
        max_original_certificates=4,max_columns=120,
        max_generations=3,max_newton_steps=35,
        max_backtrack=8,max_krylov_products=1800,
        max_cg_iterations=50,max_block_coefficients=6500)
    out=solve_chunked(net,flows,limits)
    if _flow_checksum(flows)!=before:
        raise AssertionError("Physical workers' production modified")
    row=_summary("worker_coupled_42node_timeline",net,flows,out)
    row["workers"]=len(metadata["workers"])
    row["nonproducing_workers"]=metadata["nonproducing_workers"]
    row["production_nodes"]=sorted(metadata["physical_production_nodes"])
    row["demand_nodes"]=sorted(metadata["physical_demand_nodes"])
    row["production_nodes_subset_demand_nodes"]=(
        metadata["physical_production_nodes"].issubset(
            metadata["physical_demand_nodes"]))
    row["demand_total_by_item"]={
        str(item):math.fsum(v for (tile,k),v in metadata["demand"].items()
                           if k==item)
        for item in metadata["items"]}
    row["demand_not_multiplied_by_travel"]=True
    row["worker_coupled_demand_is_laboratory_only"]=True
    return row


def existing_small_reference():
    net,flows=R45.two_group_nonoptimal_fixture()
    before=_flow_checksum(flows)
    original=R47.solve_block(net,flows)
    chunked=solve_chunked(net,flows,R47.Limits(
        max_h_evaluations=750,max_route_scans=300,
        max_original_certificates=6,max_columns=192,
        max_generations=4,max_newton_steps=60,
        max_backtrack=14,max_krylov_products=8000,
        max_cg_iterations=100,max_block_coefficients=5000))
    if _flow_checksum(flows)!=before:
        raise AssertionError("Input flows mutated")
    return {
        "reference_ADR047":_summary("two_group_original",net,flows,original),
        "chunked":_summary("two_group_chunked",net,flows,chunked),
        "both_independently_original_F072_certified":(
            is_certified(original) and is_certified(chunked)),
        "not_a_constant_budget_speedup_comparison":True,
    }


def maximum_density_trial():
    cfg=R49.CASES["maximum_R13_density_2562_240_120"]
    net,flows=R49.construct_scenario(cfg)
    initial=_flow_checksum(flows)
    fingerprint=R49.ADR48._economics_fingerprint(net)
    # SAME ORIGINAL old limit values, but block-coefficient counter now
    # counts actual triangular sparse products and chunks can be smaller.
    # Hence the units of block work differ from ADR050's square-width
    # counter; do not call this a direct budget-equivalent speedup.
    limits=R49.BUDGETS["maximum_R13_density_2562_240_120"]
    out=solve_chunked(net,flows,limits)
    if (_flow_checksum(flows)!=initial or
        R49.ADR48._economics_fingerprint(net)!=fingerprint):
        raise AssertionError("Original R13 F072 source mutated")
    row=_summary("R13_2562nodes_240groups_120markets",net,flows,out)
    row["scenario_R13_original"]=True
    row["scenario_worker_coupled"]=False
    row["ceiling_same_numeric_248000_new_metric_actual_triangular_dots"]=True
    row["not_comparable_to_old_square_width_248000_as_equal_work"]=True
    row["work_limits"]=vars(limits)
    return row


def exercise(profile):
    if profile not in ("smoke","ci"):
        raise ValueError("Unknown ADR051 experiment profile")
    if R49.ADR48.ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Original R20 SHA manifest drift")
    worker=worker_fixture_trial()
    ref=existing_small_reference()
    if not worker["production_nodes_subset_demand_nodes"]:
        raise AssertionError("Worker demands do not cover producer nodes")
    if not ref["both_independently_original_F072_certified"]:
        raise AssertionError("Chunked solver broke small F072 reference")
    result={
        "schema":SCHEMA,
        "profile":profile,
        "worker_timeline_experiment":worker,
        "small_original_vs_chunked_reference":ref,
        "contracts_pass":True,
        "R20_SHA_manifest_16":True,
        "original_F072_price_and_quartic_congestion_unchanged":True,
        "physical_worker_coupling_implemented_in_Python_lab_only":True,
        "travel_demand_allocated_by_time_and_not_duplicated":True,
        "F059_financial_tie_policy_unratified":True,
        "game_CSharp_Godot_unchanged":True,
        "world_scale_production_authorized":False,
    }
    print("[ADR051:worker] workers={} production_nodes={} demand_nodes={} "
          "F072_GO={} status={} time_ms={}".format(
              worker["workers"],len(worker["production_nodes"]),
              len(worker["demand_nodes"]),
              worker["independent_F072_certified"],worker["status"],
              worker["time_python_solver_ms"]),flush=True)
    if profile=="ci":
        large=maximum_density_trial()
        result["max_density_R13_exploratory"]=large
        print("[ADR051:240groups] F072_GO={} status={} global={} worst={} "
              "new_triangle_dots={} original_square_counterfactual={} "
              "Krylov={} time_ms={}".format(
                large["independent_F072_certified"],large["status"],
                large["F072_global_FINAL_if_pass"],
                large["F072_worst_group_FINAL_if_pass"],
                large["work"]["actual_sparse_symmetric_dot_products"],
                large["work"]["full_group_square_charge_counterfactual"],
                large["work"]["matrixfree_Jv_and_Jtv_products"],
                large["time_python_solver_ms"]),flush=True)
        result["max_density_240_group_independently_certified"]=(
            large["independent_F072_certified"])
    return result


def gate_status(profile,worker_certified,max_density_certified=False):
    """Mandatory F072 gates: never permit green CI on the new baselines."""
    if profile not in ("smoke","ci"):
        raise ValueError("Unknown controlled gate profile")
    if type(worker_certified) is not bool or type(max_density_certified) is not bool:
        raise ValueError("Original F072 gate must be explicit booleans")
    if not worker_certified:
        return "NO_GO_MANDATORY_WORKER_COUPLED_F072"
    if profile=="ci" and not max_density_certified:
        return "NO_GO_MANDATORY_240_GROUP_F072_REGRESSION"
    return "PASS_ADR051_WORKER_AND_INDEPENDENT_F072"


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=("smoke","ci"),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,"profile":a.profile,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),"platform":platform.platform(),
        "status":"FAILED_LAB_CONTRACT",
        "CSharp_or_Godot_integrated":False,
        "financial_F059_unratified":True,
    }
    try:
        report.update(exercise(a.profile))
        report["status"]=gate_status(
            a.profile,
            bool(report["worker_timeline_experiment"][
                "independent_F072_certified"]),
            bool(report.get(
                "max_density_240_group_independently_certified",False)))
    except Exception as err:
        report["status"]="FAILED_LAB_CONTRACT"
        report["error"]=repr(err)
        report["traceback"]=traceback.format_exc()
        print("[ADR051] CONTRACT ERROR:",repr(err),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    file=a.output/("GA-Gate09-ADR051-"+a.profile+"-"+stamp+".json")
    file.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=a.output/("GA-Gate09-ADR051-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(file,file.name)
    print("[ADR051] STATUS:",report["status"],flush=True)
    print("[ADR051] EVIDENCE:",archive,flush=True)
    # Independent static economic 240/120 pass remains explicitly
    # separate; the first gate is correctness of new algebra and workers.
    return 0 if report["status"].startswith("PASS_") else 1


if __name__=="__main__":
    raise SystemExit(main())
