import hashlib,importlib.util,json,sys
from dataclasses import replace
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'ci'))
import run_r20 as R

class TestR20(unittest.TestCase):
    def test_provenance_and_manifest(self):
        self.assertEqual(R.validate_manifest(),16)  # eleven frozen modules plus five R20 files
    def test_multigood_fiscal_parity(self):
        cfg=replace(R.CASES['small'],iterations=2)
        original,cached,audit=R.pair(cfg,replay=True)
        self.assertEqual([x['flow_sha256'] for x in original['checkpoints']],
                         [x['flow_sha256'] for x in cached['checkpoints']])
        self.assertTrue(audit['all_batches_financially_reconciled'])
        self.assertTrue(audit['all_frozen_gap_values_independently_recomputed'])
        self.assertFalse(audit['global_equilibrium_proven'])
    def test_prefix_and_no_premature_convergence(self):
        cfg=replace(R.CASES['small'],iterations=1)
        shorter=R.cached_run(cfg,oracle_mode='r14_cached',capture_details=True)
        longer=R.cached_run(replace(cfg,iterations=3),oracle_mode='r14_cached',capture_details=True)
        self.assertTrue(R.prefix_check(shorter,longer))
        self.assertFalse(longer['clearing_convergence_certified'])
        self.assertFalse(longer['production_approved'])

if __name__=='__main__':unittest.main()
