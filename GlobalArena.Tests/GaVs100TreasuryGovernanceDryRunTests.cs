using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

internal static class GaVs100TreasuryDryRunFixture
{
    internal sealed class Reviewer(bool accept) :
        ICanonicalTreasuryGovernanceReviewer
    {
        public int Calls { get; private set; }
        public bool AcceptReadOnlyGovernance(
            CanonicalTreasuryGovernanceDeclaration declaration)
        {
            Calls++;
            return accept;
        }
    }

    internal sealed record Data(
        GaVs99CalibratedTreasuryFixture.Data Source,
        CanonicalCalibratedStateRevenuePreflight Proposed99,
        CanonicalTreasuryGovernanceDeclaration Policy);

    internal static Data Build(bool empty = false)
    {
        var source = GaVs99CalibratedTreasuryFixture.Build(empty);
        var nominal = GaVs99CalibratedTreasuryFixture.Preview(source);
        var declaration = new CanonicalTreasuryGovernanceDeclaration(
            "GA-VS100-EXPLICIT-READONLY-LAB", 1,
            "GA-VS100-GOVERNANCE-TEST-NOT-RATIFICATION",
            nominal.PolicyId, nominal.PolicyVersion,
            nominal.CurrencyUnitId,
            source.Source.Physical.Fixture.Session.CurrentHash.HexDigest,
            nominal.CompletedEconomicCycleMovementTurn);
        return new(source, nominal, declaration);
    }

    internal static CanonicalTreasuryGovernanceDryRun Preview(Data d,
        CanonicalTreasuryGovernanceDeclaration? declaration = null,
        CanonicalCalibratedStateRevenuePreflight? proposal = null,
        Reviewer? reviewer = null) =>
        CanonicalTreasuryGovernanceDryRun.FromReviewedGovernance(
            d.Source.Source.Physical.Fixture.Session,
            proposal ?? d.Proposed99, declaration ?? d.Policy,
            reviewer ?? new Reviewer(true));
}

public sealed class GaVs100TreasuryGovernanceDryRunTests
{
    [Fact]
    public void ReadOnlyStagingReconcilesPublicByProductAndReplayingIsIdempotent()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var session = d.Source.Source.Physical.Fixture.Session;
        var before = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var cash = session.FoodCash!.Accounts.ToArray();
        var reviewer = new GaVs100TreasuryDryRunFixture.Reviewer(true);
        var audit = GaVs100TreasuryDryRunFixture.Preview(d, reviewer: reviewer);
        Assert.Equal(1, reviewer.Calls);
        Assert.True(audit.UsesOnlyPreviouslyCertifiedEconomicallyOriginatedProposals);
        Assert.True(audit.ExplicitlyReviewsInstitutionalGovernance);
        Assert.False(audit.RequiresHouseholdOrBuyerAccount);
        Assert.False(audit.DebitsPopulationMoney);
        Assert.False(audit.ConvertsUnratifiedRatesIntoRealCurrency);
        Assert.False(audit.RatifiesTaxOrOwnershipRights);
        Assert.False(audit.PostsRealStateTreasuryRevenue);
        Assert.False(audit.CreatesSpendableMoneyOrLoans);
        Assert.False(audit.TouchesWorldOrLegacyCashLedger);
        Assert.False(audit.CanAuthorizeSettlement);
        Assert.Equal(64, audit.ProposalFingerprintHex.Length);
        Assert.All(audit.PublicEntries, x =>
            Assert.Equal(ExperimentalTransportInstitutionKind.PublicAuthority,
                x.Beneficiary.Kind));

        var journal = audit.StageInLaboratory();
        Assert.False(journal.IsRealWorldStateLedger);
        Assert.False(journal.CreatesSpendableTreasuryBalance);
        Assert.Equal(d.Policy.CurrencyUnitId, journal.CurrencyUnitId);
        Assert.Equal(d.Policy.WorldHashHex, journal.WorldHashHex);
        Assert.Equal(audit.ProposalFingerprintHex,
            journal.ProposalFingerprintHex);
        Assert.Equal(d.Proposed99.ProposedPublicNonpostedTotal,
            journal.ProposedNonpostedTotal);
        Assert.Equal(journal.Entries, audit.PublicEntries);
        for(var i=0;i<3;i++)
        {
            var replay = audit.StageInLaboratory(journal);
            Assert.Same(journal, replay);
            audit.VerifyIndependent();
        }
        Assert.Equal(before, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(cash, session.FoodCash.Accounts);
    }

    [Fact]
    public void AuditRecreatedFromIdenticalEvidenceCanReplaySameWorksheet()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var first = GaVs100TreasuryDryRunFixture.Preview(d);
        var second = GaVs100TreasuryDryRunFixture.Preview(d);
        var old = first.StageInLaboratory();
        Assert.Equal(first.ProposalFingerprintHex, second.ProposalFingerprintHex);
        Assert.Same(old, second.StageInLaboratory(old));
    }

    [Fact]
    public void DifferentCalibratedCurveForSameWorldAndCycleFailsClosed()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var original = GaVs100TreasuryDryRunFixture.Preview(d);
        var old = original.StageInLaboratory();
        var source = d.Source;
        var changed = GaVs99CalibratedTreasuryFixture.Preview(source,
            policy: source.Calibration with {
                Curve = CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand
            });
        var competing = GaVs100TreasuryDryRunFixture.Preview(d, proposal: changed);
        Assert.NotEqual(original.ProposalFingerprintHex,
            competing.ProposalFingerprintHex);
        Assert.Throws<InvalidOperationException>(() =>
            competing.StageInLaboratory(old));
        Assert.False(competing.PostsRealStateTreasuryRevenue);
    }

    [Fact]
    public void ChangingConversionFactorsEvenWhenPhysicalInputsMatchIsConflict()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var old = GaVs100TreasuryDryRunFixture.Preview(d)
            .StageInLaboratory();
        var original = d.Source.Calibration;
        var factors = original.CommodityFactors.ToArray();
        factors[1] = factors[1] with {
            CurrencyNumeratorPerRelativeIndex = 11
        };
        var changed = GaVs99CalibratedTreasuryFixture.Preview(d.Source,
            policy: original with { CommodityFactors = factors });
        var competing = GaVs100TreasuryDryRunFixture.Preview(
            d, proposal: changed);
        Assert.Throws<InvalidOperationException>(() =>
            competing.StageInLaboratory(old));
        Assert.Equal(d.Policy.WorldHashHex, old.WorldHashHex);
    }

    [Fact]
    public void ChangedGovernanceVersionOrEvidenceIsConflictNotSecondRevenue()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var old = GaVs100TreasuryDryRunFixture.Preview(d)
            .StageInLaboratory();
        foreach(var changed in new[] {
            d.Policy with { GovernanceVersion = d.Policy.GovernanceVersion + 1 },
            d.Policy with { EvidenceId = "DIFFERENT-GOVERNANCE-ATTESTATION" },
            d.Policy with { GovernanceId = "ANOTHER-POLICY" }
        })
        {
            var competing = GaVs100TreasuryDryRunFixture.Preview(
                d, declaration: changed);
            Assert.Throws<InvalidOperationException>(() =>
                competing.StageInLaboratory(old));
        }
    }

    [Fact]
    public void ZeroPhysicalOutputGivesNoRevenueButStableReplayFingerprint()
    {
        var d = GaVs100TreasuryDryRunFixture.Build(empty: true);
        var audit = GaVs100TreasuryDryRunFixture.Preview(d);
        var old = audit.StageInLaboratory();
        Assert.Empty(audit.PublicEntries);
        Assert.Empty(old.Entries);
        Assert.Equal(CanonicalCalibratedMonetaryAmount.Zero,
            old.ProposedNonpostedTotal);
        Assert.Same(old, audit.StageInLaboratory(old));
        audit.VerifyIndependent();
    }

    [Fact]
    public void RejectedOrNullGovernanceReviewerCannotCreateWorksheet()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var session = d.Source.Source.Physical.Fixture.Session;
        var before = session.ExportSaveJson();
        var denied = new GaVs100TreasuryDryRunFixture.Reviewer(false);
        Assert.Throws<InvalidOperationException>(() =>
            GaVs100TreasuryDryRunFixture.Preview(d, reviewer: denied));
        Assert.Equal(1, denied.Calls);
        Assert.Throws<ArgumentNullException>(() =>
            CanonicalTreasuryGovernanceDryRun.FromReviewedGovernance(
                session, d.Proposed99, d.Policy, null!));
        Assert.Equal(before, session.ExportSaveJson());
    }

    [Fact]
    public void InvalidWorldPolicyVersionCurrencyOrCycleFailBeforeGovernanceReview()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var p = d.Policy;
        var invalid = new[] {
            p with { GovernanceId = "" },
            p with { GovernanceId = "BAD " },
            p with { GovernanceVersion = 0UL },
            p with { EvidenceId = "" },
            p with { CalibratedPolicyId = "OTHER" },
            p with { CalibratedPolicyVersion = p.CalibratedPolicyVersion + 1 },
            p with { CurrencyUnitId = "ANOTHER-CURRENCY" },
            p with { WorldHashHex = "OLD-WORLD" },
            p with { CompletedEconomicCycleMovementTurn =
                p.CompletedEconomicCycleMovementTurn + 1 }
        };
        foreach(var policy in invalid)
        {
            var reviewer = new GaVs100TreasuryDryRunFixture.Reviewer(true);
            Assert.Throws<ArgumentException>(() =>
                GaVs100TreasuryDryRunFixture.Preview(
                    d, declaration: policy, reviewer: reviewer));
            Assert.Equal(0, reviewer.Calls);
        }
    }

    [Fact]
    public void LengthPrefixedFingerprintDifferentiatesAmbiguousPolicyText()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var first = GaVs100TreasuryDryRunFixture.Preview(d,
            declaration: d.Policy with {
                GovernanceId = "a|b", EvidenceId = "c"
            });
        var second = GaVs100TreasuryDryRunFixture.Preview(d,
            declaration: d.Policy with {
                GovernanceId = "a", EvidenceId = "b|c"
            });
        Assert.NotEqual(first.ProposalFingerprintHex,
            second.ProposalFingerprintHex);
        Assert.Throws<InvalidOperationException>(() =>
            second.StageInLaboratory(first.StageInLaboratory()));
    }

    [Fact]
    public void PermutingVs99FactorsDoesNotChangeCanonicalProposalFingerprint()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var old = GaVs100TreasuryDryRunFixture.Preview(d);
        var f = d.Source.Calibration;
        var reordered = GaVs99CalibratedTreasuryFixture.Preview(d.Source,
            policy: f with {
                CommodityFactors = f.CommodityFactors.Reverse().ToArray()
            });
        var another = GaVs100TreasuryDryRunFixture.Preview(d, proposal: reordered);
        Assert.Equal(old.ProposalFingerprintHex, another.ProposalFingerprintHex);
        Assert.Same(old.StageInLaboratory(),
            another.StageInLaboratory(old.StageInLaboratory()));
    }

    [Fact]
    public void StaleNetworkForeignRestoredSessionAndTurnAreInvalid()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var session = d.Source.Source.Physical.Fixture.Session;
        var preview = GaVs100TreasuryDryRunFixture.Preview(d);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            preview.RequireCurrent(restored));
        session.TransportNetwork.SetAccess(
            d.Source.Source.Physical.Fixture.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            preview.VerifyIndependent());

        var another = GaVs100TreasuryDryRunFixture.Build();
        var fresh = GaVs100TreasuryDryRunFixture.Preview(another);
        another.Source.Source.Physical.Fixture.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            fresh.StageInLaboratory());
    }

    [Fact]
    public void ForeignWorldOrCurrencyJournalCannotBeReusedAsSameCycleRevenue()
    {
        var d = GaVs100TreasuryDryRunFixture.Build();
        var report = GaVs100TreasuryDryRunFixture.Preview(d);
        var first = report.StageInLaboratory();
        var other = GaVs100TreasuryDryRunFixture.Build();
        var second = GaVs100TreasuryDryRunFixture.Preview(other);
        // Even if the independently-created fixture is identical by world,
        // only a matching exact fingerprint is accepted.
        if (first.WorldHashHex == second.Declaration.WorldHashHex)
            Assert.Equal(first.ProposalFingerprintHex,
                second.ProposalFingerprintHex);
        // A different unit is rejected before it can become a journal.
        Assert.Throws<ArgumentException>(() =>
            GaVs100TreasuryDryRunFixture.Preview(d,
                declaration: d.Policy with { CurrencyUnitId = "WRONG-UNIT" }));
    }
}
