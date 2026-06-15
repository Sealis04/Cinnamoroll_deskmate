using Godot;

namespace DesktopPet;

/// <summary>
/// OPTIONAL, build LAST (plan §4.8). Perches the pet on window/taskbar top edges.
/// ⚠️ Enumerating other windows' rects can trip game anti-cheat into false
/// positives — this is gated behind <c>[features] edge_sitting_enabled</c>
/// (default OFF) and the user is warned in the tray menu.
/// </summary>
public partial class EdgeSitter : Node
{
    private SettingsConfig _settings = null!;
    private PetWindow _window = null!;

    public void Setup(SettingsConfig settings, PetWindow window)
    {
        _settings = settings;
        _window = window;
    }

    public void SetEnabled(bool enabled)
    {
        SetProcess(enabled);
        if (!enabled)
            return;
        GD.Print("[EdgeSitter] enabled (anti-cheat risk acknowledged).");
    }

    public override void _Ready()
    {
        SetProcess(_settings?.EdgeSittingEnabled ?? false);
    }

    public override void _Process(double delta)
    {
        // TODO(milestone 9):
        //  - EnumWindows + GetWindowRect (Win32Interop) for visible window tops.
        //  - FindWindow("Shell_TrayWnd") -> taskbar rect for the bottom edge.
        //  - Find the nearest edge under the pet and snap the window onto it.
    }
}
