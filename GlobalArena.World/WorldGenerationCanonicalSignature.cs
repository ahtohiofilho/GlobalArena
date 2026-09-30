using System.Buffers.Binary;
using System.Security.Cryptography;

namespace GlobalArena.World;

public static class WorldGenerationCanonicalSignature
{
    private const ulong Magic =
        0x47415747454E3031UL;

    private const int RequestSection =
        10;

    private const int TopologySection =
        20;

    private const int PhysicalSection =
        30;

    private const int ClimateSection =
        40;

    private const int HydrologySection =
        50;

    private const int BiomeSection =
        60;

    private const int ResourceSection =
        70;

    private const int HabitabilitySection =
        80;

    private const int PlacementSuitabilitySection =
        90;

    public static WorldGenerationSignature Compute(
        WorldGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        using var writer =
            new CanonicalHashWriter();

        writer.WriteUInt64(
            Magic);

        writer.WriteInt32(
            WorldGenerationSignature.CurrentFormatVersion);

        WriteRequest(
            writer,
            result.Request);

        WriteTopology(
            writer,
            result.StrategicTopology);

        WritePhysical(
            writer,
            result.StrategicPhysicalFields);

        WriteClimate(
            writer,
            result.StrategicClimateFields);

        WriteHydrology(
            writer,
            result.StrategicHydrologyFields);

        WriteBiomes(
            writer,
            result.StrategicBiomes);

        WriteScalarField(
            writer,
            ResourceSection,
            result.StrategicResourcePotentialFields.GeneralPotential);

        WriteScalarField(
            writer,
            HabitabilitySection,
            result.StrategicHabitability);

        WriteScalarField(
            writer,
            PlacementSuitabilitySection,
            result.StrategicCivilizationPlacementSuitability);

        var digest =
            writer.GetHashAndReset();

        return new WorldGenerationSignature(
            WorldGenerationSignature.CurrentFormatVersion,
            Convert.ToHexString(
                    digest)
                .ToLowerInvariant());
    }

    private static void WriteRequest(
        CanonicalHashWriter writer,
        WorldGenerationRequest request)
    {
        writer.WriteInt32(
            RequestSection);

        writer.WriteUInt64(
            request.Seed.Value);

        writer.WriteInt32(
            request.Version.Value);

        writer.WriteInt32(
            request.StrategicParameters.M);

        writer.WriteInt32(
            request.StrategicParameters.N);

        writer.WriteUInt64(
            request.StrategicParameters.TriangulationNumber);

        writer.WriteUInt64(
            request.StrategicParameters.StrategicCellCount);

        writer.WriteUInt64(
            request.StrategicParameters.StrategicEdgeCount);

        writer.WriteUInt64(
            request.StrategicParameters.StrategicVertexCount);

        writer.WriteUInt64(
            request.StrategicParameters.PentagonCount);

        writer.WriteUInt64(
            request.StrategicParameters.HexagonCount);

        writer.WriteInt32(
            StrategicHabitabilityPolicy.Default.Version);

        writer.WriteInt32(
            StrategicCivilizationPlacementPolicy.Default.Version);
    }

    private static void WriteTopology(
        CanonicalHashWriter writer,
        StrategicTopology topology)
    {
        writer.WriteInt32(
            TopologySection);

        writer.WriteInt32(
            topology.Cells.Count);

        foreach (var cell in topology.Cells)
        {
            writer.WriteUInt64(
                cell.Id.Value);

            writer.WriteInt32(
                (int)cell.Kind);

            writer.WriteInt32(
                cell.AdjacentCellIds.Count);

            foreach (var adjacent in cell.AdjacentCellIds)
            {
                writer.WriteUInt64(
                    adjacent.Value);
            }

            writer.WriteInt32(
                cell.IncidentEdgeIds.Count);

            foreach (var edge in cell.IncidentEdgeIds)
            {
                writer.WriteUInt64(
                    edge.Value);
            }

            writer.WriteInt32(
                cell.IncidentVertexIds.Count);

            foreach (var vertex in cell.IncidentVertexIds)
            {
                writer.WriteUInt64(
                    vertex.Value);
            }
        }

        writer.WriteInt32(
            topology.Edges.Count);

        foreach (var edge in topology.Edges)
        {
            writer.WriteUInt64(
                edge.Id.Value);

            writer.WriteInt32(
                edge.IncidentCellIds.Count);

            foreach (var cell in edge.IncidentCellIds)
            {
                writer.WriteUInt64(
                    cell.Value);
            }

            writer.WriteInt32(
                edge.IncidentVertexIds.Count);

            foreach (var vertex in edge.IncidentVertexIds)
            {
                writer.WriteUInt64(
                    vertex.Value);
            }
        }

        writer.WriteInt32(
            topology.Vertices.Count);

        foreach (var vertex in topology.Vertices)
        {
            writer.WriteUInt64(
                vertex.Id.Value);

            writer.WriteInt32(
                vertex.IncidentCellIds.Count);

            foreach (var cell in vertex.IncidentCellIds)
            {
                writer.WriteUInt64(
                    cell.Value);
            }

            writer.WriteInt32(
                vertex.IncidentEdgeIds.Count);

            foreach (var edge in vertex.IncidentEdgeIds)
            {
                writer.WriteUInt64(
                    edge.Value);
            }
        }
    }

    private static void WritePhysical(
        CanonicalHashWriter writer,
        StrategicPhysicalFieldSet fields)
    {
        writer.WriteInt32(
            PhysicalSection);

        writer.WriteInt64(
            fields.SeaLevel.RawValue);

        WriteRawValues(
            writer,
            fields.Elevation);

        WriteRawValues(
            writer,
            fields.Relief);

        writer.WriteInt32(
            fields.LandWater.Count);

        for (var index = 0;
             index < fields.LandWater.Count;
             index++)
        {
            writer.WriteInt32(
                (int)fields.LandWater.GetKind(
                    index));
        }
    }

    private static void WriteClimate(
        CanonicalHashWriter writer,
        StrategicClimateFieldSet fields)
    {
        writer.WriteInt32(
            ClimateSection);

        WriteRawValues(
            writer,
            fields.Temperature);

        WriteRawValues(
            writer,
            fields.Moisture);

        WriteRawValues(
            writer,
            fields.WaterAvailability);
    }

    private static void WriteHydrology(
        CanonicalHashWriter writer,
        StrategicHydrologyFieldSet fields)
    {
        writer.WriteInt32(
            HydrologySection);

        writer.WriteInt32(
            fields.Count);

        for (var index = 0;
             index < fields.Count;
             index++)
        {
            writer.WriteInt32(
                (int)fields.GetNodeKind(
                    index));

            writer.WriteInt32(
                fields.GetDownstreamNodeIndex(
                    index)
                ?? -1);

            writer.WriteInt64(
                fields.FlowAccumulation.GetRawValue(
                    index));
        }
    }

    private static void WriteBiomes(
        CanonicalHashWriter writer,
        StrategicBiomeMap biomes)
    {
        writer.WriteInt32(
            BiomeSection);

        writer.WriteInt32(
            biomes.Count);

        for (var index = 0;
             index < biomes.Count;
             index++)
        {
            writer.WriteInt32(
                (int)biomes.GetKind(
                    index));
        }
    }

    private static void WriteScalarField(
        CanonicalHashWriter writer,
        int section,
        StrategicScalarField field)
    {
        writer.WriteInt32(
            section);

        WriteRawValues(
            writer,
            field);
    }

    private static void WriteRawValues(
        CanonicalHashWriter writer,
        StrategicScalarField field)
    {
        writer.WriteInt32(
            field.Count);

        for (var index = 0;
             index < field.Count;
             index++)
        {
            writer.WriteInt64(
                field.GetRawValue(
                    index));
        }
    }

    private sealed class CanonicalHashWriter : IDisposable
    {
        private readonly IncrementalHash _hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        public void WriteInt32(
            int value)
        {
            Span<byte> bytes =
                stackalloc byte[4];

            BinaryPrimitives.WriteInt32BigEndian(
                bytes,
                value);

            _hash.AppendData(
                bytes);
        }

        public void WriteInt64(
            long value)
        {
            Span<byte> bytes =
                stackalloc byte[8];

            BinaryPrimitives.WriteInt64BigEndian(
                bytes,
                value);

            _hash.AppendData(
                bytes);
        }

        public void WriteUInt64(
            ulong value)
        {
            Span<byte> bytes =
                stackalloc byte[8];

            BinaryPrimitives.WriteUInt64BigEndian(
                bytes,
                value);

            _hash.AppendData(
                bytes);
        }

        public byte[] GetHashAndReset()
        {
            return _hash.GetHashAndReset();
        }

        public void Dispose()
        {
            _hash.Dispose();
        }
    }
}
