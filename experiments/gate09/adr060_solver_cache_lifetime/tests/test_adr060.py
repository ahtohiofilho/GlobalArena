"""ADR060 source-frozen F072 cache-factory observation and negative gates."""
from __future__ import annotations

import gc
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
import weakref

CI = Path(__file__).resolve().parents[1]/"ci"
sys.path.insert(0,str(CI))
import run_adr060 as A


class FakeOriginalRouteCache:
    def __init__(self, net, loads, exponent):
        if exponent!=4:
            raise ValueError("Only original quartic exponent")
        self.loads=dict(loads)
    def paths(self, origin, seller):
        return {origin:0.},{origin:()},{origin:1}


class ADR060ObservedOriginalCache(unittest.TestCase):
    def factory(self, policy):
        return A.ObservedCacheFactory(policy,lambda:(123456,"native-test"))

    def test_only_original_worker_192_384_registered(self):
        self.assertEqual(tuple(A.FIXTURES),("192","384"))
        self.assertEqual((A.FIXTURES["192"]["config"].workers,
                          A.FIXTURES["384"]["config"].workers),(4096,4096))
        for invalid in ("", "48", "96", "2048"):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                A.run_solver(invalid,"baseline")

    def test_only_baseline_and_explicit_collect_policy(self):
        self.assertEqual(A.POLICIES,("baseline","gc_before_cache"))
        for invalid in ("", "release_first", "overlap"):
            with self.subTest(invalid=invalid),self.assertRaises(ValueError):
                self.factory(invalid)

    def test_original_F072_budgets_and_tolerance_unchanged(self):
        self.assertEqual(A.TOL,1e-4)
        self.assertEqual(
            A.R51.R49.ADR48.ADR42.R21.validate_manifest(),16)
        self.assertIs(A.FIXTURES["192"]["budget"], A.R55.R54.LIMITS_192)
        self.assertIs(A.FIXTURES["384"]["budget"], A.R55.LIMITS_384)
        self.assertIs(A.ORIGINAL_CACHE_TYPE, A.R45.FrozenRouteCache)

    def test_factory_returns_UNMODIFIED_original_route_cache_type(self):
        track=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            instance=track(None,{1:0.5},4)
        self.assertIs(type(instance), FakeOriginalRouteCache)
        self.assertEqual(instance.loads,{1:0.5})
        self.assertEqual(track.report()["constructed_R45_route_cache_instances"],1)
        self.assertEqual(track.report()["explicit_gc_before_each_cache_construction"],0)
        del instance
        gc.collect()
        self.assertEqual(track.report()["original_R45_caches_still_live_after_solve"],0)

    def test_old_cache_liveness_measured_without_strong_shadow_reference(self):
        tracker=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            first=tracker(None,{1:1.0},4)
            weak=weakref.ref(first)
            self.assertEqual(tracker.live(),1)
            second=tracker(None,{1:2.0},4)
            self.assertEqual(tracker.report()["maximum_old_R45_caches_live_before_next_creation"],1)
            self.assertEqual(tracker.report()["maximum_simultaneously_live_R45_caches"],2)
            del first
            gc.collect()
            self.assertIsNone(weak())
            self.assertEqual(tracker.live(),1)
            third=tracker(None,{1:3.0},4)
            self.assertEqual(tracker.events[-1]["live_original_caches_before_creation"],1)
            del second,third
        gc.collect()
        self.assertEqual(tracker.live(),0)

    def test_naturally_released_previous_cache_means_no_overlap(self):
        track=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            first=track(None,{1:1.},4)
            del first
            gc.collect()
            second=track(None,{1:2.},4)
        self.assertEqual(track.report()["maximum_old_R45_caches_live_before_next_creation"],0)
        self.assertEqual(track.report()["maximum_simultaneously_live_R45_caches"],1)
        del second
        gc.collect()

    def test_extra_GC_calls_equal_original_cache_constructions(self):
        track=self.factory("gc_before_cache")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            first=track(None,{1:1.},4)
            second=track(None,{1:2.},4)
        self.assertEqual(track.gc_calls,2)
        self.assertEqual(track.events[1]["live_original_caches_after_optional_gc"],1)
        self.assertEqual(track.report()["maximum_simultaneously_live_R45_caches"],2)
        # Collecting does NOT allow us to fake releasing still-owned A.
        del first,second
        gc.collect()

    def test_invalid_exponent_blocks_original_constructor(self):
        track=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            with self.assertRaisesRegex(AssertionError,"quartic"):
                track(None,{1:1.},2)
        self.assertEqual(len(track.events),0)

    def test_non_weakref_cache_is_rejected_not_claimed_as_observed(self):
        class WithoutWeakrefs:
            __slots__=()
            def __init__(self,*args):pass
        track=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",WithoutWeakrefs):
            with self.assertRaisesRegex(AssertionError,"weak-referenceable"):
                track(None,{1:1.},4)

    def test_nested_scoped_patch_restores_original_R45_factory(self):
        original=A.R45.FrozenRouteCache
        with patch.object(A.R45,"FrozenRouteCache",self.factory("baseline")):
            self.assertIsNot(original,A.R45.FrozenRouteCache)
        self.assertIs(original,A.R45.FrozenRouteCache)

    def test_distinct_load_epochs_reported_without_route_change(self):
        track=self.factory("baseline")
        with patch.object(A,"ORIGINAL_CACHE_TYPE",FakeOriginalRouteCache):
            one=track(None,{1:1.},4)
            two=track(None,{1:2.},4)
        self.assertEqual(track.report()["distinct_physical_edge_load_epochs"],2)
        self.assertNotEqual(track.events[0]["load_epoch_sha256_within_same_network"],
                            track.events[1]["load_epoch_sha256_within_same_network"])
        del one,two

    def test_original_solver_exception_restores_factory(self):
        original=A.R45.FrozenRouteCache
        with patch.object(A.R51,"solve_chunked",
                          side_effect=RuntimeError("intentional input failure")):
            with self.assertRaisesRegex(RuntimeError,"intentional input failure"):
                A.run_solver("192","baseline")
        self.assertIs(original,A.R45.FrozenRouteCache)

    def test_new_runner_no_F059_or_CSharp_Godot_claim(self):
        self.assertFalse(getattr(A,"production_approved",False))
        self.assertNotEqual(A.POLICIES, A.R59.POLICIES)
        self.assertEqual(A.SCHEMA.endswith("_V1"),True)


if __name__=="__main__":
    unittest.main()
