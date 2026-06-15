using Godot;
using System;

namespace DesktopPet;

/// <summary>
/// Lifecycle UI (plan §4.9). The pet window is borderless, has no taskbar entry,
/// and is click-through — so without this there is NO way to quit or configure
/// it short of Task Manager. Provides a system-tray status indicator and a
/// right-click context menu (also opened by right-clicking the sprite) with:
/// Settings… / Edge sitting (checkable) / Reset position / Quit.
/// </summary>
public partial class TrayMenu : Node
{
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void ResetPositionRequestedEventHandler();
    [Signal] public delegate void EdgeSittingToggledEventHandler(bool enabled);
    [Signal] public delegate void QuitRequestedEventHandler();

    private enum Item { Settings = 0, EdgeSitting = 1, ResetPosition = 2, Quit = 3 }

    private SettingsConfig _settings = null!;
    private PopupMenu _menu = null!;
    private int _statusIndicator = -1;

    public void Setup(SettingsConfig settings)
    {
        _settings = settings;
        BuildMenu();
        CreateStatusIndicator();
    }

    private void BuildMenu()
    {
        _menu = new PopupMenu();
        AddChild(_menu);
        _menu.AddItem("Settings…", (int)Item.Settings);
        _menu.AddCheckItem("Edge sitting (anti-cheat risk)", (int)Item.EdgeSitting);
        _menu.SetItemChecked(_menu.GetItemIndex((int)Item.EdgeSitting), _settings.EdgeSittingEnabled);
        _menu.AddItem("Reset position", (int)Item.ResetPosition);
        _menu.AddSeparator();
        _menu.AddItem("Quit", (int)Item.Quit);
        _menu.IdPressed += OnItemPressed;
    }

    private void CreateStatusIndicator()
    {
        // Godot 4.3+: DisplayServer.CreateStatusIndicator(icon, tooltip, callback).
        // Left-click invokes the callback; we show the menu at the cursor.
        if (!DisplayServer.HasFeature(DisplayServer.Feature.StatusIndicator))
        {
            GD.PushWarning("[Tray] StatusIndicator unavailable on this build; " +
                           "falling back to right-click-on-sprite only. " +
                           "Ensure a global quit path exists (plan §4.9).");
            return;
        }

        Texture2D icon = AssetLoader.LoadOrPlaceholder("idle_neutral_base.png");
        // 4.3 callback signature: (MouseButton button, Vector2I position).
        _statusIndicator = DisplayServer.CreateStatusIndicator(
            icon, "Cinnamoroll DeskMate",
            Callable.From((long _, Vector2I __) => ShowMenuAtCursor()));
    }

    /// <summary>Open the context menu at the current cursor (tray or sprite right-click).</summary>
    public void ShowMenuAtCursor()
    {
        _menu.Position = DisplayServer.MouseGetPosition();
        _menu.Popup();
    }

    private void OnItemPressed(long id)
    {
        switch ((Item)id)
        {
            case Item.Settings:
                EmitSignal(SignalName.SettingsRequested);
                break;
            case Item.EdgeSitting:
                int idx = _menu.GetItemIndex((int)Item.EdgeSitting);
                bool enabled = !_menu.IsItemChecked(idx);
                _menu.SetItemChecked(idx, enabled);
                _settings.EdgeSittingEnabled = enabled;
                _settings.Save();
                EmitSignal(SignalName.EdgeSittingToggled, enabled);
                break;
            case Item.ResetPosition:
                EmitSignal(SignalName.ResetPositionRequested);
                break;
            case Item.Quit:
                EmitSignal(SignalName.QuitRequested);
                break;
        }
    }

    public override void _ExitTree()
    {
        if (_statusIndicator >= 0 &&
            DisplayServer.HasFeature(DisplayServer.Feature.StatusIndicator))
        {
            DisplayServer.DeleteStatusIndicator(_statusIndicator);
        }
    }
}
