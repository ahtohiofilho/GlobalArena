using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Presentation-only orthographic tactical camera. These coordinates are in
/// the CURRENT projected tactical chart, not canonical physical tile positions.
/// </summary>
public sealed record TacticalCameraBookmark(
    ulong StrategicFocus, int FrameRings, bool Equidistant, bool CoverViewport,
    double CenterX, double CenterZ, double Zoom, double YawDegrees);

public sealed record TacticalViewSidecar(
    int Version, string CanonicalSaveSha256Hex, TacticalCameraBookmark Camera);

/// <summary>
/// Companion to the physical V9–V13 save. Never touches the physical payload,
/// simulation revision, economy, political ownership or canonical hash.
/// Stale, malformed and mismatched companions are ignored, not recovered.
/// </summary>
public static class TacticalViewSidecarCodec
{
    public const int CurrentVersion = 1;
    private const int MaximumBytes = 16_384;

    public static string Create(string canonicalSave, TacticalCameraBookmark camera)
    {
        ArgumentNullException.ThrowIfNull(canonicalSave);
        Validate(camera);
        return JsonSerializer.Serialize(new TacticalViewSidecar(
            CurrentVersion, Digest(canonicalSave), camera));
    }

    public static bool TryRestore(string canonicalSave, string? sidecar,
        out TacticalCameraBookmark? camera)
    {
        ArgumentNullException.ThrowIfNull(canonicalSave);
        camera = null;
        if (string.IsNullOrEmpty(sidecar) ||
            Encoding.UTF8.GetByteCount(sidecar) > MaximumBytes)
            return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<TacticalViewSidecar>(sidecar);
            if (parsed is null || parsed.Version != CurrentVersion ||
                !string.Equals(parsed.CanonicalSaveSha256Hex,
                    Digest(canonicalSave), StringComparison.Ordinal))
                return false;
            Validate(parsed.Camera);
            camera = parsed.Camera;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException
            or OverflowException)
        {
            return false;
        }
    }

    private static string Digest(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static void Validate(TacticalCameraBookmark? camera)
    {
        if (camera is null ||
            camera.StrategicFocus == 0UL ||
            camera.FrameRings is not (1 or 2) ||
            !double.IsFinite(camera.CenterX) ||
            !double.IsFinite(camera.CenterZ) ||
            !double.IsFinite(camera.Zoom) ||
            !double.IsFinite(camera.YawDegrees) ||
            Math.Abs(camera.CenterX) > 100.0 ||
            Math.Abs(camera.CenterZ) > 100.0 ||
            camera.Zoom < 0.0001 || camera.Zoom > 100.0 ||
            camera.YawDegrees < 0.0 || camera.YawDegrees >= 360.0)
            throw new ArgumentException("Invalid tactical camera bookmark.");
    }
}
