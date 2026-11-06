using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Whether a PRODUCT target has been activated in the canonical physical
/// engine. Existing V9-V30 save policies are never silently relabelled as
/// the target simply because they remain supported for replay.
/// </summary>
public enum GaProductDirectionReadiness : byte
{
    LegacyRuntimeOnly = 1,
    DesignRequiresNewVersionedRule = 2,
    FuturePhysicalEvidenceMissing = 3,
    TargetActive = 4
}

/// <summary>
/// Immutable diagnosis, not a command to migrate, modify a save, or change
/// the current economic cycle. All target formulas and constraints are
/// versioned in GA-CANON-DIRECTIVES-01.
/// </summary>
public sealed record GaProductDirectionFinding(
    string DirectiveId,
    GaProductDirectionReadiness Readiness,
    string Target,
    string Observed,
    bool CanAuthorizeTargetGameplay);

/// <summary>
/// An explicit anti-drift gate. It distinguishes the target design from
/// existing gameplay and historical replay rules, and fails closed when
/// a caller tries to certify legacy gameplay as target-canonical.
/// No monetary, movement, nutritional or economic calculation occurs here.
/// </summary>
public sealed class GaCanonicalProductDirectionAudit
{
    public const string PolicyVersion = "GA-CANON-DIRECTIVES-01/2026-11-06";

    private readonly GaProductDirectionFinding[] _findings;

    public string ObservedWorldHashHex { get; }
    public ulong ObservedMovementTurn { get; }
    public IReadOnlyList<GaProductDirectionFinding> Findings { get; }

    public bool CanPromoteAsTargetCanonical =>
        _findings.All(x =>
            x.Readiness == GaProductDirectionReadiness.TargetActive &&
            x.CanAuthorizeTargetGameplay);

    private GaCanonicalProductDirectionAudit(
        string hash, ulong movementTurn,
        GaProductDirectionFinding[] findings)
    {
        ObservedWorldHashHex = hash;
        ObservedMovementTurn = movementTurn;
        _findings = findings;
        Findings = Array.AsReadOnly(_findings);
    }

    public static GaCanonicalProductDirectionAudit Inspect(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var hash = session.CurrentHash.HexDigest;
        var movementTurn = session.State.MovementTurn;

        var cadence = session.CadenceMode switch
        {
            PhysicalEconomicCadenceMode.AdaptiveThirty =>
                "Historical AdaptiveThirty: elapsed-turn bands, 1/1 then 1/2 then 1/3.",
            PhysicalEconomicCadenceMode.LegacyFixedSix =>
                "Historical LegacyFixedSix: one settlement every six movement turns.",
            _ => throw new InvalidOperationException(
                "Unrecognized physical economic cadence; no automatic promotion.")
        };
        var movement = session.MovementMode switch
        {
            PhysicalWorkerMovementMode.TimedAdjacent =>
                "Historical TimedAdjacent: fixed two turns per adjacent edge.",
            PhysicalWorkerMovementMode.LegacyInstant =>
                "Historical LegacyInstant: movement without the future scalar schedule.",
            _ => throw new InvalidOperationException(
                "Unrecognized physical movement ruleset; no automatic promotion.")
        };
        var cash = session.MarketMode switch
        {
            PhysicalFoodMarketMode.LocalCashSettlement =>
                session.State.FoodCash is null
                    ? throw new InvalidOperationException(
                        "Cash settlement policy lacks its required historical ledger.")
                    : "Historical LocalCashSettlement: household/producer balances and food purchase cap.",
            PhysicalFoodMarketMode.LegacyDirectConsumption =>
                session.State.FoodCash is not null
                    ? throw new InvalidOperationException(
                        "Direct-consumption historical mode has an unexpected cash ledger.")
                    : "Historical LegacyDirectConsumption: provisional quantitative food demand.",
            _ => throw new InvalidOperationException(
                "Unrecognized physical market ruleset; no automatic promotion.")
        };

        // The future target cannot inherit current legacy policy just because
        // older saves and baseline UI intentionally continue to execute it.
        var findings = new[]
        {
            new GaProductDirectionFinding(
                "GA-DIR-CADENCE-MOBILITY",
                GaProductDirectionReadiness.DesignRequiresNewVersionedRule,
                "Economic cadence must depend on physical mobility/technology, not the count of elapsed turns. Exact activation/rounding requires an approved policy.",
                cadence, false),
            new GaProductDirectionFinding(
                "GA-DIR-MOVEMENT-SCALAR",
                GaProductDirectionReadiness.DesignRequiresNewVersionedRule,
                "Integral movement points, terrain-sensitive edge costs, multi-edge carry within a turn, and versioned arrival effects; no imposed powers of two.",
                movement, false),
            new GaProductDirectionFinding(
                "GA-DIR-ECON-ABSTRACT",
                GaProductDirectionReadiness.LegacyRuntimeOnly,
                "Target: P real residents, N from authenticated prior real economic activity; D=P*N, x=S/D, p=f(x), R_ref=p*D. No household wallet or artificial quantity cap.",
                cash, false),
            new GaProductDirectionFinding(
                "GA-DIR-OFFER-ABSORPTION",
                GaProductDirectionReadiness.DesignRequiresNewVersionedRule,
                "Unretained physical offer has market destination; prices and reference returns have diminishing marginal response; D is not a sales/consumption quota.",
                "Historical food closure may leave unsold output or cap delivered quantities by provisional need and wallet.", false),
            new GaProductDirectionFinding(
                "GA-DIR-NUTR-SIX",
                GaProductDirectionReadiness.FuturePhysicalEvidenceMissing,
                "Actual six-food identities, executed by-type ingestion and calibrated conversion are prerequisites for Q; no productivity modifier without evidence.",
                "Physical closing cycle currently consumes aggregate food CommodityId(1); nutrition candidate is not live.", false)
        };

        if (session.CurrentHash.HexDigest != hash ||
            session.State.MovementTurn != movementTurn)
            throw new InvalidOperationException(
                "Policy audit may not span a changing canonical state.");

        return new GaCanonicalProductDirectionAudit(hash, movementTurn, findings);
    }

    public void RequireTargetCanonical()
    {
        if (!CanPromoteAsTargetCanonical)
            throw new InvalidOperationException(
                "Target gameplay is not activated. Missing directives: " +
                string.Join(", ", _findings
                    .Where(x => x.Readiness !=
                        GaProductDirectionReadiness.TargetActive ||
                        !x.CanAuthorizeTargetGameplay)
                    .Select(x => x.DirectiveId)));
    }
}
