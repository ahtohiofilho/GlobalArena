using System.Text.Json;
using GlobalArena.World;

if (args.Length != 4 || !int.TryParse(args[0], out var m) || !int.TryParse(args[1], out var n) || !ulong.TryParse(args[2], out var seed))
{
    Console.Error.WriteLine("Usage: Worldgen3DViewer <m> <n> <seed> <output-json>");
    return 2;
}
if (m < 0 || n < 0 || m + n > 15 || m + n == 0)
{
    Console.Error.WriteLine("Strategic prototype requires 1 <= m+n <= 15.");
    return 2;
}
var request = new WorldGenerationRequest(new WorldSeed(seed), WorldGenerationVersion.Initial, new GoldbergParameters(m, n));
var world = new DeterministicWorldGenerator().Generate(request);
var geo = world.StrategicSphericalGeometry;
var cells = geo.Cells.Select(c => new {
    id = c.CellId.Value,
    center = new[] { c.Center.X, c.Center.Y, c.Center.Z },
    boundary = c.BoundaryVertexIds.Select(v => v.Value).ToArray(),
    sides = c.BoundaryVertexIds.Count,
    biome = world.StrategicBiomes.GetKind(c.CellId).ToString()
}).ToArray();
var vertices = geo.Vertices.Select(v => new { id = v.VertexId.Value, position = new[] { v.Position.X, v.Position.Y, v.Position.Z } }).ToArray();
var document = new { contract = "GA-M4.5-D.3-strategic-sphere-v1", m, n, seed, cellCount = cells.Length, vertexCount = vertices.Length, cells, vertices };
var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
File.WriteAllText(args[3], json);
Console.WriteLine($"EXPORT_PASS cells={cells.Length} vertices={vertices.Length} pentagons={cells.Count(c=>c.sides==5)} hexagons={cells.Count(c=>c.sides==6)}");
return cells.Count(c => c.sides == 5) == 12 ? 0 : 3;
