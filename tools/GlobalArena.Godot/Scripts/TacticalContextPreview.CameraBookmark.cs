using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Restore the tactical chart and view parameters after canonical V13 world
/// restore. Everything here is local Godot presentation; no game command.
/// </summary>
public partial class TacticalContextPreview
{
    private Button? _savedRingButton;
    private Button? _savedProjectionButton;
    private Button? _savedFramingButton;

    public TacticalCameraBookmark CaptureCameraBookmark()
    {
        if (_camera is null || _activeFocus == 0UL)
            throw new InvalidOperationException("Tactical camera has not been opened.");
        return new TacticalCameraBookmark(
            _activeFocus, _ringCount, _equidistant, _coverViewport,
            _cameraTarget.X, _cameraTarget.Z, _camera.Size,
            Mathf.PosMod(_yawDegrees, 360f));
    }

    /// <summary>
    /// Rebuild the intended projection and THEN reinstate pan/yaw/zoom. Open()
    /// intentionally restores initial framing, so its caller must never call
    /// Open again after successful bookmark recovery.
    /// </summary>
    public bool TryRestoreCameraBookmark(TacticalCameraBookmark bookmark)
    {
        if (_camera is null || _activeFocus == 0UL ||
            _cachedPhysicalMap is null || bookmark is null ||
            bookmark.StrategicFocus == 0UL ||
            bookmark.StrategicFocus > (ulong)_cachedPhysicalMap.CoarseTopology.Cells.Count ||
            bookmark.FrameRings is not (1 or 2) ||
            !double.IsFinite(bookmark.CenterX) ||
            !double.IsFinite(bookmark.CenterZ) ||
            !double.IsFinite(bookmark.Zoom) ||
            !double.IsFinite(bookmark.YawDegrees) ||
            Math.Abs(bookmark.CenterX) > 100 ||
            Math.Abs(bookmark.CenterZ) > 100 ||
            bookmark.Zoom <= 0 || bookmark.Zoom > 100 ||
            bookmark.YawDegrees < 0 || bookmark.YawDegrees >= 360)
            return false;

        var oldRings = _ringCount;
        var oldProjection = _equidistant;
        var oldCover = _coverViewport;
        var oldYaw = _yawDegrees;
        var fallbackFocus = _activeFocus;

        _ringCount = bookmark.FrameRings;
        _equidistant = bookmark.Equidistant;
        _coverViewport = bookmark.CoverViewport;
        try
        {
            Open(bookmark.StrategicFocus);
            var size = (float)bookmark.Zoom;
            // The saved zoom must still be legal for this chart and framing
            // mode. Never silently clamp a user's saved camera.
            var minimum = _coverViewport ? _coverSize * 0.30f : _originalSize * 0.40f;
            var maximum = _coverViewport ? _coverSize : _originalSize * 2.0f;
            if (size < minimum - 0.0001f || size > maximum + 0.0001f)
                throw new InvalidOperationException("Saved zoom is outside the current chart bounds.");

            _cameraTarget = new Vector3(
                (float)bookmark.CenterX, 0f, (float)bookmark.CenterZ);
            _yawDegrees = (float)bookmark.YawDegrees;
            UpdateCameraOrientation();
            _camera.Size = size;
            if (_coverViewport && !ViewportSamplesHaveTerrain(size, 4, 4))
                throw new InvalidOperationException("Saved view no longer fits rendered terrain.");
            _prefetchIntentOrigin = _cameraTarget;
            _lastPrefetchIntentFocus = 0UL;
            if (_savedRingButton is not null)
                _savedRingButton.Text = $"FRAME RINGS: {_ringCount}";
            if (_savedProjectionButton is not null)
                _savedProjectionButton.Text =
                    _equidistant ? "PROJECTION: EQUIDISTANT" : "PROJECTION: DIRECT";
            if (_savedFramingButton is not null)
                _savedFramingButton.Text =
                    _coverViewport ? "FRAME: COVER (EXP)" : "FRAME: FIT";
            if (_information is not null)
                _information.Text = $"TACTICAL | territory {_activeFocus} | G(18,0) | " +
                    $"{(_equidistant ? "EQUIDISTANT" : "DIRECT")} | frame {_ringCount}";
            GD.Print($"GA-VS18-VIEW-RESTORED focus={_activeFocus} target_x={_cameraTarget.X:F4} " +
                $"target_z={_cameraTarget.Z:F4} zoom={_camera.Size:F4} yaw={_yawDegrees:F1} " +
                $"rings={_ringCount} equidistant={_equidistant} cover={_coverViewport}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException
            or OverflowException)
        {
            _ringCount = oldRings;
            _equidistant = oldProjection;
            _coverViewport = oldCover;
            _yawDegrees = oldYaw;
            // Caller reopens default focus after false; restore button labels
            // to the presentation mode actually used for fallback.
            if (_savedRingButton is not null)
                _savedRingButton.Text = $"FRAME RINGS: {_ringCount}";
            if (_savedProjectionButton is not null)
                _savedProjectionButton.Text =
                    _equidistant ? "PROJECTION: EQUIDISTANT" : "PROJECTION: DIRECT";
            if (_savedFramingButton is not null)
                _savedFramingButton.Text =
                    _coverViewport ? "FRAME: COVER (EXP)" : "FRAME: FIT";
            GD.PushWarning("GA-VS18-VIEW-FALLBACK: " + ex.Message);
            // Do not reopen here: the canonical save/load flow controls when
            // it binds a new authoritative geography and decides fallback.
            return false;
        }
    }
}
