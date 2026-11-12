"""Standalone pure-Python contract checks; these do not substitute for C# parity."""
import hashlib
import json
from pathlib import Path
import struct
import unittest
from fractions import Fraction

ROOT=Path(__file__).resolve().parent.parent

class Contract(unittest.TestCase):
    def test_official_exact_bits(self):
        cases=json.loads((ROOT/'vectors/ratio-input.json').read_text())['cases']
        expected=json.loads((ROOT/'vectors/ratio-expected.json').read_text())['cases']
        self.assertEqual(270,len(cases))
        self.assertEqual(len(cases),len(expected))
        for case,reference in zip(cases,expected):
            self.assertEqual(case['id'],reference['id'])
            n=case['tariffMicro']*(1_000_000-case['selfMicro'])
            bits=struct.unpack('<q',struct.pack('<d',float(Fraction(n,10**12))))[0]
            self.assertEqual(reference['bits'],bits,case['id'])
    def test_evidence_destinations_exist_before_csharp_runs(self):
        import importlib.util
        import tempfile
        spec=importlib.util.spec_from_file_location('ga_r19_runner', ROOT/'ci/run_r19.py')
        module=importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as temp:
            evidence=Path(temp)/'evidence'
            module.ensure_evidence_directories(evidence)
            self.assertTrue((evidence/'json').is_dir())
            self.assertTrue((evidence/'logs').is_dir())
            module.ensure_evidence_directories(evidence)  # idempotent on rerun

    def test_source_matches_rev4_approved(self):
        expected={
          'src/ExactRatioBinary64.cs':'55489852b3c8afded05edd07247963ab20bbbb179f363a992d51cadacd4848dc',
          'src/GA.Gate09.R19.CacheParity/Program.cs':'ff1b3d325554d0a59c9b0a57250c02dca6f85b3882a63eac21796702e959c467',
          'src/GA.Gate09.R19.RatioWitness/Program.cs':'716c02962a417a3bfa9e86f44c6298bf2dea854dff5d4cc35c28647fe6917acb',
        }
        # Git checks out source files with LF; the approved REV4 artifact used CRLF.
        # Reconstruct REV4's line endings strictly for its provenance digest.
        for p,h in expected.items():
            raw = (ROOT/p).read_bytes()
            crlf = raw.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
            self.assertEqual(h, hashlib.sha256(crlf).hexdigest(), p)

if __name__=='__main__':unittest.main()
