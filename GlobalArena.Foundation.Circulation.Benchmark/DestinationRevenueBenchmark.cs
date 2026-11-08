using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

internal static class DestinationRevenueBenchmark
{
    public static void Run()
    {
        Console.WriteLine("GA F-084F - destination-priced, treasury-conserving transient settlement");
        Console.WriteLine("lots,iteration,elapsed_ms,gross_raw,owner_raw,checksum");

        const long S = DestinationRevenueLaboratory.Scale;
        var natural = NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Land, 0, 0, 0),
            NaturalEdgeReferenceProfiles.CandidateA);
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, S, S);
        var tariff = EdgeTariffReferenceLaboratory.Evaluate(natural, improved, 4);
        var edge = new TransientEdgeUse(99, tariff,
            new[] { new EdgeCapitalHolding(2, S) });
        IReadOnlyList<TransientEdgeUse> usedEdges = new[] { edge };

        foreach (int count in new[] { 10_000, 100_000 })
        {
            var sales = new TransientDestinationSale[count];
            for (int i = 0; i < count; i++)
            {
                sales[i] = new TransientDestinationSale(
                    i, OriginNodeId: 1, DestinationNodeId: 2,
                    CommodityId: 7, SellerCivilizationId: 1,
                    SoldQuantityRaw: S, UsedEdges: usedEdges);
            }

            var markets = new[] {
                new DestinationMarketInput(2, 7, count * S, count * S)
            };
            for (int pass = 1; pass <= 2; pass++)
            {
                var clock = Stopwatch.StartNew();
                var snapshot = DestinationRevenueLaboratory.Settle(markets, sales);
                clock.Stop();

                if (snapshot.TotalGrossRaw != snapshot.TotalCreditsRaw ||
                    snapshot.Sales.Count != count)
                    throw new InvalidOperationException("F-084F settlement regression.");

                var ownerRaw = snapshot.CivilizationCredits
                    .Single(c => c.CivilizationId == 2).CreditRaw;
                var checksum = checked(snapshot.TotalGrossRaw * 7
                    + snapshot.TotalCreditsRaw * 3
                    + ownerRaw);
                Console.WriteLine($"{count},{pass},{clock.Elapsed.TotalMilliseconds:F3}," +
                    $"{snapshot.TotalGrossRaw},{ownerRaw},{checksum}");
            }
        }
    }
}
