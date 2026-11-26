using Godot;

namespace GlobalArena.Godot;

/// <summary>Cached, local directional light and elevation. No screen capture,
/// viewport blur, shader sampling, particle systems or per-frame processing.
/// Panel drawing is behind its native child controls and never receives input.</summary>
internal static class GaHudEffects
{
    private const string SurfaceMeta = "ga_hud_light_plane";
    private static readonly Dictionary<GaHudPlane, ImageTexture> LightCache = new();
    private static readonly Dictionary<string, StyleBoxFlat> ActionStyles = new();

    internal static StyleBoxFlat Surface(GaHudPlane plane, int padding)
    {
        var (fill, border, radius, line, shadow) = plane switch
        {
            GaHudPlane.Atmosphere => (GaHudVisualTokens.Canvas, GaHudVisualTokens.Canvas, GaHudVisualTokens.RadiusSmall, 0, 0),
            GaHudPlane.Structure => (GaHudVisualTokens.Structure, GaHudVisualTokens.Structure, 0, 0, 0),
            GaHudPlane.Portrait => (GaHudVisualTokens.Portrait, GaHudVisualTokens.RecessRim, GaHudVisualTokens.RadiusSmall, GaHudVisualTokens.RimWidth, 0),
            GaHudPlane.Footer => (GaHudVisualTokens.Footer, GaHudVisualTokens.Rim, GaHudVisualTokens.RadiusMedium, GaHudVisualTokens.RimWidth, GaHudVisualTokens.FooterShadow),
            _ => (GaHudVisualTokens.Object, GaHudVisualTokens.Rim, GaHudVisualTokens.RadiusMedium, GaHudVisualTokens.RimWidth, GaHudVisualTokens.ObjectShadow)
        };
        var style = GaVisualFoundation.HudFlat(fill, border, radius, padding, line);
        style.ShadowColor = GaHudVisualTokens.Shadow;
        style.ShadowSize = shadow;
        style.ShadowOffset = new Vector2(0, shadow > 0 ? 2 : 0);
        return style;
    }

    // The same upper-left key light and weaker lower-right reflected light
    // are shared by every material. Signed luminance, not random glow.
    private static ImageTexture Light(GaHudPlane plane)
    {
        if (LightCache.TryGetValue(plane, out var cached)) return cached;
        int w = GaHudVisualTokens.LightTextureWidth, h = GaHudVisualTokens.LightTextureHeight;
        var data = new byte[w * h * 4];
        double strength = plane switch
        {
            GaHudPlane.Atmosphere => 0.72,
            GaHudPlane.Portrait => 0.80,
            GaHudPlane.Footer => 0.85,
            _ => 1.0
        };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double u = x / (double)(w - 1), v = y / (double)(h - 1);
            double key = 0.075 * Math.Exp(-((u - 0.15)*(u - 0.15)*3.5 + (v - 0.04)*(v - 0.04)*5.5));
            double bounce = 0.022 * Math.Exp(-((u - 0.90)*(u - 0.90)*8 + (v - 0.86)*(v - 0.86)*10));
            double directional = 0.018 * (1 - v);
            double occlusion = (plane == GaHudPlane.Portrait ? 0.19 : 0.10) * v * v;
            double edge = 0.065 * Math.Exp(-v * 50);
            double signed = (key + bounce + directional + edge - occlusion) * strength;
            int i = (y * w + x) * 4;
            data[i] = data[i+1] = data[i+2] = signed >= 0 ? (byte)255 : (byte)0;
            data[i+3] = (byte)Math.Round(Math.Clamp(Math.Abs(signed), 0d, 1d) * 255d);
        }
        var texture = ImageTexture.CreateFromImage(Image.CreateFromData(w, h, false, Image.Format.Rgba8, data));
        LightCache.Add(plane, texture);
        return texture;
    }

    internal static void AttachSurface(PanelContainer panel, GaHudPlane plane)
    {
        if (panel.HasMeta(SurfaceMeta)) return;
        var texture = Light(plane);
        panel.SetMeta(SurfaceMeta, (int)plane);
        panel.Draw += () =>
        {
            float inset = GaHudVisualTokens.LightInset;
            var size = panel.Size - new Vector2(inset * 2, inset * 2);
            if (size.X > 0 && size.Y > 0)
                panel.DrawTextureRect(texture, new Rect2(new Vector2(inset, inset), size), false);
        };
        panel.Resized += panel.QueueRedraw;
        panel.QueueRedraw();
    }

    internal static void StyleAction(Button button)
    {
        if (ActionStyles.Count == 0)
        {
            var t = GaHudVisualTokens.ActionPadding;
            var r = GaHudVisualTokens.RadiusMedium;
            var rim = GaHudVisualTokens.RimWidth;
            ActionStyles["normal"] = GaVisualFoundation.HudFlat(GaHudVisualTokens.Action, GaHudVisualTokens.Rim, r, t, rim);
            ActionStyles["normal"].ShadowSize = GaHudVisualTokens.ContactShadow;
            ActionStyles["normal"].ShadowColor = GaHudVisualTokens.Shadow;
            ActionStyles["normal"].ShadowOffset = new Vector2(0, 1);
            ActionStyles["hover"] = GaVisualFoundation.HudFlat(GaHudVisualTokens.Hover, GaHudVisualTokens.Focus, r, t, rim);
            ActionStyles["pressed"] = GaVisualFoundation.HudFlat(GaHudVisualTokens.Pressed, GaHudVisualTokens.Focus, r, t, rim);
            ActionStyles["hover_pressed"] = ActionStyles["pressed"];
            ActionStyles["disabled"] = GaVisualFoundation.HudFlat(GaHudVisualTokens.Disabled, GaHudVisualTokens.RecessRim, r, t, rim);
            ActionStyles["focus"] = GaVisualFoundation.HudFlat(Colors.Transparent, GaHudVisualTokens.Focus, r, 0, rim);
        }
        foreach (var pair in ActionStyles) button.AddThemeStyleboxOverride(pair.Key, pair.Value);
    }

    internal static void ExerciseContract(IReadOnlyList<PanelContainer> portraits, IReadOnlyList<PanelContainer> objects,
        PanelContainer footer, IReadOnlyList<Button> actions)
    {
        if (portraits.Count != 3 || objects.Count != 3 ||
            portraits.Any(p => !p.HasMeta(SurfaceMeta)) || objects.Any(p => !p.HasMeta(SurfaceMeta)) ||
            !footer.HasMeta(SurfaceMeta))
            throw new InvalidOperationException("Material layers were not attached.");
        var image = Light(GaHudPlane.Object).GetImage();
        if (image.GetPixel(0, 0).A <= image.GetPixel(image.GetWidth()/2, image.GetHeight()/2).A ||
            !ReferenceEquals(Light(GaHudPlane.Object), Light(GaHudPlane.Object)))
            throw new InvalidOperationException("Directional light/cache contract failed.");
        foreach (var action in actions)
        {
            if (!GaHudMotion.IsAttached(action) ||
                action.GetThemeStylebox("normal") is not StyleBoxFlat normal ||
                action.GetThemeStylebox("hover") is not StyleBoxFlat hover ||
                action.GetThemeStylebox("pressed") is not StyleBoxFlat pressed ||
                action.GetThemeStylebox("disabled") is not StyleBoxFlat disabled ||
                normal.BgColor == hover.BgColor || hover.BgColor == pressed.BgColor ||
                pressed.BgColor == disabled.BgColor)
                throw new InvalidOperationException("Action material/motion states missing.");
        }
    }
}
