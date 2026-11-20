using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R104: immutable pre-scene resolution contract shared by the strategic
/// globe, tactical refinement and authoritative simulation. Exactly one
/// source of Goldberg parameters for a running Godot preview.
/// </summary>
internal sealed class GaGoldbergStartupConfiguration
{
    public WorldGenerationRequest WorldRequest { get; }
    public GoldbergScaledRefinement Refinement { get; }
    public GoldbergParameters Coarse => Refinement.CoarseParameters;
    public GoldbergParameters Fine => Refinement.FineParameters;
    public bool AlternativeResolutionQa { get; }

    private GaGoldbergStartupConfiguration(
        WorldGenerationRequest request, GoldbergScaledRefinement refinement,
        bool alternativeResolutionQa)
    {
        if (request.StrategicParameters != refinement.CoarseParameters)
            throw new ArgumentException(
                "The strategic request and Goldberg refinement must match.");
        WorldRequest = request;
        Refinement = refinement;
        AlternativeResolutionQa = alternativeResolutionQa;
    }

    public static GaGoldbergStartupConfiguration FromEnvironment(
        bool resolutionSmoke, bool interactiveFineFirst)
    {
        // Deliberately bounded scope. Do not silently generalize sixfold
        // refinement, economy balance or arbitrary map sizes here.
        string? coarseProfile = Environment.GetEnvironmentVariable(
            "GA_NEXT_GOLDBERG_COARSE_M");
        if (coarseProfile is not null &&
            coarseProfile != "2" && coarseProfile != "3")
            throw new InvalidOperationException(
                "Only audited coarse G(2,0) and G(3,0) are permitted.");
        int coarseM = coarseProfile == "2" ? 2 : 3;
        if (coarseM == 2 && !resolutionSmoke && !interactiveFineFirst)
            throw new InvalidOperationException(
                "G2/G12 requires explicit QA smoke or fine-first interactive mode.");

        var coarse = new GoldbergParameters(coarseM, 0);
        var fine = new GoldbergParameters(checked(coarseM * 6), 0);
        var refinement = new GoldbergScaledRefinement(coarse, fine);
        if (refinement.Scale != 6)
            throw new InvalidOperationException(
                "This QA bootstrap uses the approved scale-six hierarchy.");
        return new GaGoldbergStartupConfiguration(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, coarse),
            refinement, coarseM == 2);
    }

    public void VerifyWorldAndIncidence(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        if (world.Request.Seed != WorldRequest.Seed ||
            world.Request.Version != WorldRequest.Version ||
            world.Request.StrategicParameters != Coarse ||
            incidence.Refinement.CoarseParameters != Coarse ||
            incidence.Refinement.FineParameters != Fine ||
            incidence.Refinement.Scale != Refinement.Scale ||
            incidence.CoarseTopology.Parameters != Coarse ||
            incidence.FineTopology.Parameters != Fine)
            throw new InvalidOperationException(
                "A single Goldberg startup contract must govern world, " +
                "incidence, globe and economic host.");
    }
}
