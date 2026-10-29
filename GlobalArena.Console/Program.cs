using System.Globalization;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

if (args.Length > 0 && args[0] == "tactical-bridge")
{
    Environment.ExitCode = GaVs03TacticalBridge.Run(args.Skip(1).ToArray());
    return;
}

if (args.Length > 0 && args[0] == "godot-bridge")
{
    Environment.ExitCode = GaVs02GodotBridge.Run(args.Skip(1).ToArray());
    return;
}

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine("Global Arena — recorte neolítico inspecionável (GA-VS01)");
    Console.WriteLine("Uso: dotnet run --project GlobalArena.Console -- demo [seed]");
    Console.WriteLine("     dotnet run --project GlobalArena.Console -- play [seed]");
    return;
}

if (args[0] is not ("demo" or "play") ||
    args.Length > 2 ||
    (args.Length == 2 && !ulong.TryParse(args[1],
        NumberStyles.None, CultureInfo.InvariantCulture, out _)))
{
    Console.Error.WriteLine("Comando ou seed inválido. Use 'demo' ou 'play' [seed].");
    Environment.ExitCode = 2;
    return;
}

var seed = args.Length == 2
    ? ulong.Parse(args[1], CultureInfo.InvariantCulture) : 0UL;
var session = new EconomicNeolithicInteractionSession(seed);
Console.WriteLine($"GA-VS01 — seed {seed} — prévia offline, sem save nem combate");
ShowStatus();

if (args[0] == "demo")
{
    var from = new EconomicPointId(1UL);
    var targets = session.AvailableLandNeighbors(from);
    if (targets.Count == 0)
        throw new InvalidOperationException("Expected a land neighbor in the demo.");
    Console.WriteLine($"Terra adjacente livre: {string.Join(", ", targets.Select(x => x.Value))}");
    var point = session.Found(from, targets[0], 3UL);
    Console.WriteLine($"Fundação básica: ponto {point.Value}, tile {targets[0].Value}, 3 trabalhadores.");
    ShowStatus();
    ShowPreview(session.AdvancePreview());
    Console.WriteLine("Fim do recorte demonstrativo. Produção/consumo foram projetados, não liquidados.");
    return;
}

Console.WriteLine("Comandos: status | vizinhos <ponto> | fundar <ponto> <tile> <trabalhadores> | prever | proximo | sair");
while (true)
{
    Console.Write("GA> ");
    var line = Console.ReadLine();
    if (line is null)
        break;
    var tokens = line.Trim().Split(' ',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (tokens.Length == 0)
        continue;
    if (tokens[0] is "sair" or "quit" or "exit")
        break;
    try
    {
        switch (tokens[0])
        {
            case "status" when tokens.Length == 1:
                ShowStatus();
                break;
            case "vizinhos" when tokens.Length == 2:
                var source = ParsePoint(tokens[1]);
                Console.WriteLine($"Células livres: {string.Join(", ", session.AvailableLandNeighbors(source).Select(x => x.Value))}");
                break;
            case "fundar" when tokens.Length == 4:
                var origin = ParsePoint(tokens[1]);
                var target = new StrategicCellId(ParsePositive(tokens[2]));
                var workers = ParsePositive(tokens[3]);
                var newPoint = session.Found(origin, target, workers);
                Console.WriteLine($"Fundado ponto {newPoint.Value} na célula {target.Value} com {workers} trabalhadores.");
                ShowStatus();
                break;
            case "prever" when tokens.Length == 1:
                ShowPreview(session.PreviewGeographicEconomy());
                break;
            case "proximo" when tokens.Length == 1:
                ShowPreview(session.AdvancePreview());
                break;
            default:
                Console.WriteLine("Comando desconhecido. Use status, vizinhos, fundar, prever, proximo ou sair.");
                break;
        }
    }
    catch (Exception ex) when (ex is ArgumentException or
        InvalidOperationException or OverflowException)
    {
        Console.WriteLine($"Ordem recusada: {ex.Message}");
    }
}

ulong ParsePositive(string raw)
{
    if (!ulong.TryParse(raw, NumberStyles.None,
            CultureInfo.InvariantCulture, out var value) || value == 0UL)
        throw new ArgumentException("Esperado um número inteiro positivo.");
    return value;
}

EconomicPointId ParsePoint(string raw) => new(ParsePositive(raw));

void ShowStatus()
{
    var world = session.CurrentWorld;
    Console.WriteLine($"Revisão {world.Revision} | prévias encerradas {session.CompletedPreviewTurns} | pontos {world.Economy.EconomicPoints.Count}");
    foreach (var p in world.Economy.EconomicPoints)
        Console.WriteLine($"  Ponto {p.Id.Value} • tile {p.StrategicCellId.Value} • trabalhadores {p.Workforce} (agri {Count(p, EconomicActivityKind.Agriculture)}, madeira {Count(p, EconomicActivityKind.Forestry)}, pedra {Count(p, EconomicActivityKind.Mining)})");
    Console.WriteLine($"  Total de trabalhadores: {world.Economy.EconomicPoints.Aggregate(0UL, (sum, p) => checked(sum + p.Workforce))}");
}

ulong Count(EconomicPointRuntimeState p, EconomicActivityKind kind) =>
    p.ActivityAllocations.FirstOrDefault(x => x.Kind == kind)?.Workforce ?? 0UL;

void ShowPreview(EconomicProductionDemandResult result)
{
    Console.WriteLine($"Prévia geográfica: {result.SupplyFlows.Count} fluxos de oferta, {result.DemandFlows.Count} fluxos de demanda");
    foreach (var flow in result.SupplyFlows)
        Console.WriteLine($"  Oferta P{flow.EconomicPointId.Value}: {Commodity(flow.CommodityId)} {Format(flow.RawQuantity)}");
    foreach (var flow in result.DemandFlows)
        Console.WriteLine($"  Demanda P{flow.EconomicPointId.Value}: {Commodity(flow.CommodityId)} {Format(flow.RawQuantity)}");
    Console.WriteLine("Observação: não há liquidação de mercado, formação de estoque ou save neste recorte.");
}

string Commodity(CommodityId id) => id.Value switch
{
    1U => "alimento",
    2U => "pedra",
    3U => "madeira",
    _ => $"item-{id.Value}"
};

string Format(ulong raw) =>
    ((decimal)raw / EconomicTransferPolicy.FixedPointDenominator)
        .ToString("0.###", CultureInfo.InvariantCulture);
