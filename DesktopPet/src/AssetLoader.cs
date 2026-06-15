using Godot;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

/// <summary>
/// Loads sprites following the Track A naming contract (plan §2.4) and degrades
/// gracefully when assets are missing: a magenta placeholder texture is returned
/// and a warning logged, so a half-populated assets/ folder never crashes the app.
/// </summary>
public static class AssetLoader
{
    public const string SpriteDir = "res://assets/sprites";

    /// <summary>Load a single texture, or a labelled placeholder if absent.</summary>
    public static Texture2D LoadOrPlaceholder(string fileName, int size = 512)
    {
        string path = $"{SpriteDir}/{fileName}";
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        GD.PushWarning($"[Assets] missing '{fileName}'; using placeholder.");
        return MakePlaceholder(size);
    }

    /// <summary>
    /// Build a <see cref="SpriteFrames"/> by scanning for every
    /// <c>{state}_{NNN}.png</c> sequence in the sprite dir. Returns null if the
    /// directory can't be opened (e.g. exported build with packed resources).
    /// </summary>
    public static SpriteFrames? BuildSpriteFrames(int fps = 12)
    {
        using DirAccess? dir = DirAccess.Open(SpriteDir);
        if (dir is null)
        {
            GD.PushWarning($"[Assets] cannot open {SpriteDir}; no animations loaded.");
            return null;
        }

        // Group "anim_blink_000.png" -> state "anim_blink", frame 0.
        var groups = new Dictionary<string, SortedDictionary<int, string>>();
        foreach (string f in dir.GetFiles())
        {
            // In-editor the dir lists both "x.png" and "x.png.import"; both map to
            // the same state/frame key below, so duplicates are harmless.
            string file = f.EndsWith(".import") ? f[..^".import".Length] : f;
            if (!file.EndsWith(".png"))
                continue;
            string stem = file[..^".png".Length];
            int us = stem.LastIndexOf('_');
            if (us < 0 || !int.TryParse(stem[(us + 1)..], out int frame))
                continue; // not a numbered sequence (e.g. idle_neutral_base)
            string state = stem[..us];
            if (!groups.TryGetValue(state, out var frames))
                groups[state] = frames = new SortedDictionary<int, string>();
            frames[frame] = file;
        }

        var sf = new SpriteFrames();
        sf.RemoveAnimation("default");
        foreach ((string state, var frames) in groups)
        {
            sf.AddAnimation(state);
            sf.SetAnimationSpeed(state, fps);
            foreach (string file in frames.Values)
                sf.AddFrame(state, GD.Load<Texture2D>($"{SpriteDir}/{file}"));
        }

        GD.Print($"[Assets] built {groups.Count} animation(s): {string.Join(", ", groups.Keys)}");
        return sf;
    }

    private static ImageTexture MakePlaceholder(int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(new Color(1, 0, 1, 0.5f)); // translucent magenta = "missing"
        return ImageTexture.CreateFromImage(img);
    }
}
