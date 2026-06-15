using Godot;
using System.Collections.Generic;

namespace DesktopPet;

/// <summary>
/// Typed loader/saver for <c>config/settings.cfg</c> (plan §6). Wraps Godot's
/// <see cref="ConfigFile"/> so the rest of the app reads strongly-typed values.
/// </summary>
public class SettingsConfig
{
    public const string Path = "res://config/settings.cfg";

    private readonly ConfigFile _cfg = new();

    // [idle]
    public int MinIntervalSeconds { get; private set; } = 8;
    public int MaxIntervalSeconds { get; private set; } = 20;
    public int SleepAfterSeconds { get; private set; } = 120;

    // [window]
    public bool AlwaysOnTop { get; private set; } = true;
    public int CanvasSize { get; private set; } = 512;
    public Vector2I LastPos { get; set; } = new(-1, -1);

    // [features]
    public bool EdgeSittingEnabled { get; set; } = false;

    // [app_reactions]  process name (lower-case) -> reaction state name
    public Dictionary<string, string> AppReactions { get; } = new();

    public static SettingsConfig Load()
    {
        var s = new SettingsConfig();
        Error err = s._cfg.Load(Path);
        if (err != Error.Ok)
        {
            GD.PushWarning($"[Settings] could not load {Path} ({err}); using defaults.");
            return s;
        }

        s.MinIntervalSeconds = (int)s._cfg.GetValue("idle", "min_interval_seconds", 8);
        s.MaxIntervalSeconds = (int)s._cfg.GetValue("idle", "max_interval_seconds", 20);
        s.SleepAfterSeconds = (int)s._cfg.GetValue("idle", "sleep_after_seconds", 120);

        s.AlwaysOnTop = (bool)s._cfg.GetValue("window", "always_on_top", true);
        s.CanvasSize = (int)s._cfg.GetValue("window", "canvas_size", 512);
        s.LastPos = new Vector2I(
            (int)s._cfg.GetValue("window", "last_pos_x", -1),
            (int)s._cfg.GetValue("window", "last_pos_y", -1));

        s.EdgeSittingEnabled = (bool)s._cfg.GetValue("features", "edge_sitting_enabled", false);

        if (s._cfg.HasSection("app_reactions"))
        {
            foreach (string key in s._cfg.GetSectionKeys("app_reactions"))
            {
                string reaction = (string)s._cfg.GetValue("app_reactions", key, "");
                if (!string.IsNullOrEmpty(reaction))
                    s.AppReactions[key.ToLowerInvariant()] = reaction;
            }
        }

        return s;
    }

    /// <summary>Persist the runtime-mutable values (window position, toggles).</summary>
    public void Save()
    {
        _cfg.SetValue("window", "last_pos_x", LastPos.X);
        _cfg.SetValue("window", "last_pos_y", LastPos.Y);
        _cfg.SetValue("features", "edge_sitting_enabled", EdgeSittingEnabled);
        Error err = _cfg.Save(Path);
        if (err != Error.Ok)
            GD.PushWarning($"[Settings] could not save {Path} ({err}).");
    }
}
