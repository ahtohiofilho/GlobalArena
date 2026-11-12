#!/usr/bin/env python3
"""GA Gate09 R19 self-contained regression: exact source, .NET execution and parity.

This CI/lab driver does not write to GlobalArena source, save, journal, or economy.
The numerical benchmark is observational and never promotes production use.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import traceback
import zipfile

ROOT = Path(__file__).resolve().parent.parent
MARKER = ROOT / 'MANIFEST-R19-SHA256.json'


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_manifest() -> int:
    obj = json.loads(MARKER.read_text(encoding='utf-8'))
    allowed = set(obj['files'])
    actual = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob('*') if p.is_file()
              and not any(x in {'bin', 'obj', '__pycache__', '.git'} for x in p.relative_to(ROOT).parts)
              and p.name != MARKER.name}
    # Allow CI output artifacts/working files outside the immutable source root only.
    if actual != allowed:
        raise ValueError(f'Manifest scope drift missing={sorted(allowed-actual)} unlisted={sorted(actual-allowed)}')
    for name, expected in obj['files'].items():
        if not (ROOT/name).is_file() or digest(ROOT/name) != expected:
            raise ValueError(f'Bad SHA256: {name}')
    assert obj['baselineR18ZipSha256'] == '2e88d96542c179de12101ba56ea22b617f369c18948ec16569b47e0f95ce588e'
    return len(actual)


def verify_same(reference: object, actual: object, prefix: str='') -> None:
    # Same structural and tolerance contract as independent R15 comparator.
    if type(reference) is dict:
        if type(actual) is not dict or set(reference) != set(actual):
            raise AssertionError(prefix+': object keys differ')
        for k, v in reference.items(): verify_same(v,actual[k],prefix+'.'+k)
    elif type(reference) is list:
        if type(actual) is not list or len(reference)!=len(actual):
            raise AssertionError(prefix+': list sizes differ')
        for i,(r,a) in enumerate(zip(reference,actual)):
            verify_same(r,a,prefix+f'[{i}]')
    elif type(reference) is bool:
        if type(actual) is not bool or reference != actual:raise AssertionError(prefix+': bool differs')
    elif type(reference) is int:
        if type(actual) is not int or reference != actual:raise AssertionError(prefix+': integer differs')
    elif type(reference) is float:
        if type(actual) not in (int,float) or not math.isfinite(actual) or not math.isclose(reference,actual,rel_tol=2e-9,abs_tol=2e-11):
            raise AssertionError(prefix+': float differs')
    elif actual!=reference: raise AssertionError(prefix+': value differs')


def run(stage: str, cmd: list[str], cwd: Path, evidence: Path, *, expected_failure=False) -> subprocess.CompletedProcess[str]:
    print(f'[{stage}] ...', flush=True)
    proc = subprocess.run(cmd,cwd=cwd,capture_output=True,text=True,encoding='utf-8',errors='replace')
    logs = evidence / 'logs'; logs.mkdir(parents=True,exist_ok=True)
    (logs/f'{stage}.txt').write_text(f'ARGV: {cmd!r}\nEXIT: {proc.returncode}\nSTDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}',encoding='utf-8')
    if (proc.returncode==0)==expected_failure:
        raise RuntimeError(f'{stage}: unexpected return {proc.returncode}: {(proc.stderr or proc.stdout)[-1000:]}')
    print(f'[{stage}] '+('EXPECTED REJECTION' if expected_failure else 'PASS'),flush=True)
    return proc


def ensure_evidence_directories(evidence: Path) -> None:
    """Create destinations BEFORE passing their paths to the C# File.WriteAllText calls."""
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / 'json').mkdir(parents=True, exist_ok=True)
    (evidence / 'logs').mkdir(parents=True, exist_ok=True)


def write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')


def main() -> int:
    ap=argparse.ArgumentParser()
    ap.add_argument('--output',type=Path,help='Evidence ZIP destination folder (default: Downloads or current directory)')
    ap.add_argument('--benchmark',action='store_true',help='Release observational benchmark, only after parity')
    args=ap.parse_args()
    default_output = Path.home()/'Downloads'
    out = (args.output or (default_output if default_output.is_dir() else Path.cwd())).resolve()
    out.mkdir(parents=True,exist_ok=True)
    stamp = datetime.now().strftime('%Y%m%d-%H%M%S')
    prefix=f'GA-Gate09-R19-Evidencias-{stamp}'
    evidence=out/prefix
    ensure_evidence_directories(evidence)
    status={'gate':'Gate09-R19','result':'FAILED','productionApproved':False,
            'source':'ExperimentalStandalone','benchmarkRequested':args.benchmark,
            'startedUtc':datetime.now(timezone.utc).isoformat(), 'steps':[],
            'python':platform.python_version()}
    current='preflight'
    try:
        if sys.version_info < (3,10):raise RuntimeError('Python 3.10+ required')
        current='manifest'
        count=validate_manifest()
        print(f'[manifest] PASS {count} files',flush=True)
        status['steps'].append('MANIFEST_SHA256_PASS')
        current='Python contract tests'
        run('python-contract',[sys.executable,'-m','unittest','discover','-s',str(ROOT/'tests'),'-p','test_*.py','-v'],ROOT,evidence)
        status['steps'].append('R19_PYTHON_CONTRACT_PASS')
        current='reference fixtures'
        full=json.loads((ROOT/'vectors/expected-r18.json').read_text(encoding='utf-8'))
        ratio=json.loads((ROOT/'vectors/ratio-expected.json').read_text(encoding='utf-8'))
        rinput=json.loads((ROOT/'vectors/ratio-input.json').read_text(encoding='utf-8'))
        assert len(full['cases'])==23 and len(ratio['cases'])==270 and len(rinput['cases'])==270
        # Reference vectors are independently rederived using arbitrary precision integer division.
        import struct
        for index,(row,target) in enumerate(zip(rinput['cases'],ratio['cases'])):
            if row['id']!=target['id']:raise ValueError(f'vector ID mismatch at {index}')
            n=row['tariffMicro']*(1_000_000-row['selfMicro'])
            # Fraction -> float performs one correctly-rounded conversion in CPython.
            from fractions import Fraction
            bits=struct.unpack('<q',struct.pack('<d',float(Fraction(n,10**12))))[0]
            if bits!=target['bits']:raise ValueError(f'bad independent Python reference: {row["id"]}')
        print('[reference] PASS 23 cases + 270 exact witnesses',flush=True)
        status['steps'].append('INDEPENDENT_PYTHON_RATIO_270_PASS')
        current='dotnet'
        exe=shutil.which('dotnet')
        if not exe:raise RuntimeError('.NET SDK 10 not available')
        ver=run('dotnet-version',[exe,'--version'],ROOT,evidence).stdout.strip()
        if not ver.startswith('10.'):raise RuntimeError('Need .NET SDK 10, got '+ver)
        status['dotnetSdk']=ver
        dlls={}
        for role,name in [('port','GA.Gate09.R19.CacheParity'),('witness','GA.Gate09.R19.RatioWitness')]:
            current=f'build-{role}'
            project=ROOT/'src'/name/f'{name}.csproj'
            run(current,[exe,'build',str(project),'-c','Release','--nologo','-v','minimal'],ROOT,evidence)
            dll=ROOT/'src'/name/'bin'/'Release'/'net10.0'/f'{name}.dll'
            if not dll.exists():raise RuntimeError(f'Missing built DLL: {dll}')
            dlls[role]=dll
            status['steps'].append(f'BUILD_{role.upper()}_PASS')
        for role,input_name,output_name in [('port','input-r18.json','actual-port.json'),('witness','ratio-input.json','actual-witness.json')]:
            current=f'run-{role}'
            dest=evidence/'json'/output_name
            run(current,[exe,str(dlls[role]),'--input',str(ROOT/'vectors'/input_name),'--output',str(dest)],ROOT,evidence)
            status['steps'].append(f'EXECUTE_{role.upper()}_PASS')
        current='verify parity'
        actual=json.loads((evidence/'json'/'actual-port.json').read_text(encoding='utf-8-sig'))
        numeric=json.loads((evidence/'json'/'actual-witness.json').read_text(encoding='utf-8-sig'))
        verify_same(full,actual)
        if numeric!=ratio:raise AssertionError('Bit-exact IEEE-754 tariff witness differs')
        print('[verify] PASS full=23 witness=270 bit-exact',flush=True)
        status['steps'].append('R19_PARITY_23_AND_270_PASS')
        status['positiveCases']=23
        status['bitExactWitnesses']=270
        status['executedDllSha256']={name:digest(path) for name,path in dlls.items()}
        status['sourceSha256']={p.relative_to(ROOT).as_posix():digest(p) for p in sorted((ROOT/'src').rglob('*.cs'))}
        if args.benchmark:
            current='build-benchmark'
            bench='GA.Gate09.R19.Benchmark'
            proj=ROOT/'bench'/bench/(bench+'.csproj')
            run(current,[exe,'build',str(proj),'-c','Release','--nologo','-v','minimal'],ROOT,evidence)
            current='run-benchmark'
            dll=ROOT/'bench'/bench/'bin'/'Release'/'net10.0'/(bench+'.dll')
            benchout=evidence/'json'/'benchmark.json'
            run(current,[exe,str(dll),'--input',str(ROOT/'vectors/ratio-input.json'),'--output',str(benchout)],ROOT,evidence)
            status['benchmark']='OBSERVATIONAL_NO_GATE'
            status['steps'].append('BENCHMARK_RELEASE_RECORDED')
        status['result']='PASSED_R19_BINARY_PARITY'
    except Exception as ex:
        print(f'[{current}] FAILED: {ex}',flush=True)
        status['failedStage']=current
        status['reason']=str(ex)
        (evidence/'error.txt').write_text(traceback.format_exc(),encoding='utf-8')
    finally:
        status['endedUtc']=datetime.now(timezone.utc).isoformat()
        write_json(evidence/'status.json',status)
        bundle=out/(prefix+'.zip')
        with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as z:
            for p in sorted(evidence.rglob('*')):
                if p.is_file():z.write(p,p.relative_to(evidence).as_posix())
        print(f'\nSTATUS: {status["result"]}\nEVIDENCE ZIP: {bundle}',flush=True)
    return 0 if status['result']=='PASSED_R19_BINARY_PARITY' else 1

if __name__=='__main__':raise SystemExit(main())
