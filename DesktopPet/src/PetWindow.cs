using Godot;

namespace DesktopPet;

/// <summary>
/// Owns the OS window: transparency, borderless, always-on-top, no taskbar entry,
/// click-through passthrough, and the shared DPI / multi-monitor coordinate
/// helpers every other component must use (plan §4.1, §4.2, §4.10).
/// </summary>
public partial class PetWindow : Node
{
    private SettingsConfig _settings = null!;

    public void Configure(SettingsConfig settings)
    {
        _settings = settings;

        Window win = GetWindow();
        // Most of these are also set in project.godot, but we assert them at
        // runtime so behaviour is reproducible regardless of editor overrides.
        win.Borderless = true;
        win.AlwaysOnTop = settings.AlwaysOnTop;
        win.Transparent = true;
        win.TransparentBg = true;
        win.Unfocusable = true;

        // No taskbar entry. Godot exposes this as a window flag on Windows.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);

        RestorePosition();
    }

    // ---- Click-through (milestone 2, verify EARLY) -------------------------

    /// <summary>
    /// Set the passthrough region. Points are in window-local pixels. Empty array
    /// = whole window passes through; the silhouette/bounding polygon = only the
    /// pet is clickable (plan §4.2).
    /// </summary>
    public void SetPassthrough(Vector2[] regionLocal)
    {
        // TODO(milestone 2): pass the character silhouette. v1 = bounding box.
        // If WindowSetMousePassthrough misbehaves on the pinned 4.3 build, fall
        // back to Win32 WS_EX_TRANSPARENT toggling driven by alpha hit-testing.
        DisplayServer.WindowSetMousePassthrough(regionLocal);
    }

    public void SetWholeWindowClickable()
    {
        // A polygon covering the full canvas = nothing passes through.
        int s = _settings.CanvasSize;
        SetPassthrough(new Vector2[]
        {
            new(0, 0), new(s, 0), new(s, s), new(0, s),
        });
    }

    // ---- Shared coordinate helpers (plan §4.10) ---------------------------

    /// <summary>
    /// Cursor position relative to the window's top-left, in physical pixels.
    /// Reads BOTH cursor and window from DisplayServer so the spaces match.
    /// </summary>
    public static Vector2 GetCursorInWindowSpace()
    {
        Vector2I cursor = DisplayServer.MouseGetPosition();
        Vector2I winPos = DisplayServer.WindowGetPosition();
        return cursor - winPos;
    }

    /// <summary>Clamp a desired top-left so the window stays on a connected screen.</summary>
    public Vector2I ClampToVisibleScreen(Vector2I desired)
    {
        Rect2I usable = DisplayServer.ScreenGetUsableRect(FindScreenForPoint(desired));
        int size = _settings.CanvasSize;
        int x = Mathf.Clamp(desired.X, usable.Position.X, usable.End.X - size);
        int y = Mathf.Clamp(desired.Y, usable.Position.Y, usable.End.Y - size);
        return new Vector2I(x, y);
    }

    /// <summary>Index of the screen whose usable area contains the point, else primary.</summary>
    private static int FindScreenForPoint(Vector2I p)
    {
        for (int i = 0; i < DisplayServer.GetScreenCount(); i++)
        {
            if (DisplayServer.ScreenGetUsableRect(i).HasPoint(p))
                return i;
        }
        return DisplayServer.GetPrimaryScreen();
    }

    // ---- Position persistence (plan §4.1) ---------------------------------

    public void RestorePosition()
    {
        Vector2I pos = _settings.LastPos;
        if (pos.X < 0 || pos.Y < 0)
        {
            // First run: center on the primary screen.
            Rect2I usable = DisplayServer.ScreenGetUsableRect(DisplayServer.GetPrimaryScreen());
            int size = _settings.CanvasSize;
            pos = usable.Position + (usable.Size - new Vector2I(size, size)) / 2;
        }
        DisplayServer.WindowSetPosition(ClampToVisibleScreen(pos));
    }

    public void SavePosition()
    {
        _settings.LastPos = DisplayServer.WindowGetPosition();
        _settings.Save();
    }
}
