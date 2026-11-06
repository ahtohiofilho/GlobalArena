#!/usr/bin/env python3
"""GA-CI-FAST-02: zero-dependency regression of the PR test selector."""
from __future__ import annotations

import unittest

from ga_ci_lanes import ROOT, changed_test_classes


class GaCiFast02SelectorTests(unittest.TestCase):
    def test_new_nutrition_and_policy_classes_are_not_ignored(self):
        for name in (
            "GaCanonDirectives01PolicyAuditTests",
            "GaNutrIntegration01ExecutedConsumptionTests",
            "GaNutrArch01CandidateNutritionTests",
        ):
            source = (ROOT / "GlobalArena.Tests" / f"{name}.cs").read_text(
                encoding="utf-8"
            )
            self.assertIn(name, changed_test_classes(source))

    def test_class_selection_is_independent_of_vs_naming(self):
        source = """
namespace Example.Tests;
public sealed class NutritionActuallyExecutedTests
{
    [Fact]
    public void Works() {}
}
"""
        self.assertEqual(
            {"NutritionActuallyExecutedTests"}, changed_test_classes(source)
        )

    def test_theory_marker_is_selected(self):
        source = """
internal partial class PolicyTheoryTests
{
    [Theory]
    [InlineData(1)]
    public void Checks(int input) {}
}
"""
        self.assertEqual({"PolicyTheoryTests"}, changed_test_classes(source))

    def test_multiple_test_types_in_one_file_are_selected(self):
        source = """
public sealed class FirstPolicyTests {
    [Fact]
    public void A() {}
}
public class SecondPolicyTests {
    [Fact]
    public void B() {}
}
"""
        self.assertEqual(
            {"FirstPolicyTests", "SecondPolicyTests"},
            changed_test_classes(source),
        )

    def test_helper_file_without_tests_does_not_claim_test_coverage(self):
        self.assertEqual(set(), changed_test_classes("public class TestHelper {}"))

    def test_malformed_test_source_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, "discoverable class"):
            changed_test_classes("[Fact]\npublic void MissingClass() {}")


if __name__ == "__main__":
    unittest.main()
