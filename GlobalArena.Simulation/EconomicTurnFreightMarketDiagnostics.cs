namespace GlobalArena.Simulation;

/// <summary>
/// Non-authoritative diagnostic counters for one reference allocation call.
/// This mutable object must not be shared across concurrent calls. Counters
/// affect neither economic decisions nor WorldState or canonical hashing.
/// A new Resolve call resets them, including when a later validation fails.
/// </summary>
public sealed class EconomicTurnFreightMarketDiagnostics
{
    public int SupplyCount { get; private set; }
    public int DemandCount { get; private set; }
    public int QuoteCount { get; private set; }
    public int CandidateLaneCount { get; private set; }
    public long DecisionRounds { get; private set; }
    public long LaneExaminations { get; private set; }
    public long MarginalValueEvaluations { get; private set; }
    public long IntegratedValueEvaluations { get; private set; }
    public long QuantitySearchCalls { get; private set; }
    public long BinarySearchIterations { get; private set; }
    public long AcceptedTrades { get; private set; }

    internal void Reset()
    {
        SupplyCount = DemandCount = QuoteCount = CandidateLaneCount = 0;
        DecisionRounds = LaneExaminations = MarginalValueEvaluations = 0;
        IntegratedValueEvaluations = QuantitySearchCalls = 0;
        BinarySearchIterations = AcceptedTrades = 0;
    }

    internal void SetInputs(int supplies, int demands, int quotes, int lanes)
    {
        SupplyCount = supplies;
        DemandCount = demands;
        QuoteCount = quotes;
        CandidateLaneCount = lanes;
    }

    internal void Round() => DecisionRounds++;
    internal void ExamineLane() => LaneExaminations++;
    internal void EvaluateMarginal() => MarginalValueEvaluations++;
    internal void EvaluateIntegral() => IntegratedValueEvaluations++;
    internal void SearchQuantity() => QuantitySearchCalls++;
    internal void SearchStep() => BinarySearchIterations++;
    internal void AcceptTrade() => AcceptedTrades++;
}
