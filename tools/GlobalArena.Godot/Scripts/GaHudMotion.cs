using Godot;

namespace GlobalArena.Godot;

/// <summary>Presentation only: animate tint, never layout or input hitboxes.
/// Native pressed/focus/disabled styles remain authoritative. No extra
/// command handlers, no animation on idle, no delayed action dispatch.</summary>
internal static class GaHudMotion
{
    private const string ActionMeta = "ga_hud_motion";
    private static bool Reduced => System.Environment.GetEnvironmentVariable("GA_NEXT_REDUCED_HUD_MOTION") == "1";
    internal static bool IsAttached(Button button) => button.HasMeta(ActionMeta);

    internal static void AttachAction(Button button)
    {
        if (IsAttached(button)) return;
        button.SetMeta(ActionMeta, true);
        Tween? running = null;
        void Tint(float value, double seconds, Tween.TransitionType curve, Tween.EaseType ease)
        {
            if (running?.IsValid() == true) running.Kill();
            if (Reduced || !button.IsInsideTree() || button.Disabled)
            {
                button.SelfModulate = Colors.White;
                return;
            }
            running = button.CreateTween();
            running.TweenProperty(button, "self_modulate", new Color(value, value, value, 1f), seconds)
                .SetTrans(curve).SetEase(ease);
        }
        button.MouseEntered += () => Tint(GaHudVisualTokens.HoverTint, GaHudVisualTokens.HoverSeconds, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        button.MouseExited += () => Tint(1f, GaHudVisualTokens.ExitSeconds, Tween.TransitionType.Quad, Tween.EaseType.In);
        button.ButtonDown += () => Tint(GaHudVisualTokens.PressTint, GaHudVisualTokens.PressSeconds, Tween.TransitionType.Quad, Tween.EaseType.Out);
        button.ButtonUp += () => Tint(1f, GaHudVisualTokens.ReleaseSeconds, Tween.TransitionType.Cubic, Tween.EaseType.Out);
    }

    internal static void SetDisclosure(Control content, bool expanded)
    {
        // Visibility is immediate and still controlled by the caller.
        // The soft reveal has no effect on layout, queues or pointer targeting.
        content.Modulate = Colors.White;
        if (!expanded || Reduced || !content.IsInsideTree()) return;
        content.Modulate = new Color(1f, 1f, 1f, GaHudVisualTokens.RevealAlpha);
        content.CreateTween().TweenProperty(content, "modulate", Colors.White, GaHudVisualTokens.RevealSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
