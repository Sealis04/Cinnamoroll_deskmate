using Godot;

namespace DesktopPet;

/// <summary>
/// Distinguishes click vs drag and moves the OS window while dragging (plan §4.5).
/// Uses physical-pixel deltas from DisplayServer so dragging across mixed-DPI
/// monitors doesn't drift (plan §4.10).
/// </summary>
public partial class DragHandler : Node
{
    [Signal] public delegate void ClickedEventHandler();
    [Signal] public delegate void DragStartedEventHandler();
    [Signal] public delegate void DragEndedEventHandler();

    /// <summary>Movement (px) past which a press becomes a drag, not a click.</summary>
    [Export] public float DragThresholdPx = 5f;

    private PetWindow _window = null!;
    private bool _pressed;
    private bool _dragging;
    private Vector2I _pressMouse;        // screen-space press point
    private Vector2I _windowAtPress;     // window pos at press

    public void Setup(PetWindow window)
    {
        _window = window;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
                BeginPress();
            else
                EndPress();
        }
        else if (@event is InputEventMouseMotion && _pressed)
        {
            UpdateDrag();
        }
    }

    private void BeginPress()
    {
        _pressed = true;
        _dragging = false;
        _pressMouse = DisplayServer.MouseGetPosition();
        _windowAtPress = DisplayServer.WindowGetPosition();
    }

    private void UpdateDrag()
    {
        Vector2I mouse = DisplayServer.MouseGetPosition();
        Vector2I delta = mouse - _pressMouse;

        if (!_dragging && delta.Length() >= DragThresholdPx)
        {
            _dragging = true;
            EmitSignal(SignalName.DragStarted);
        }

        if (_dragging)
        {
            Vector2I target = _window.ClampToVisibleScreen(_windowAtPress + delta);
            DisplayServer.WindowSetPosition(target);
        }
    }

    private void EndPress()
    {
        if (!_pressed)
            return;
        _pressed = false;

        if (_dragging)
        {
            _dragging = false;
            _window.SavePosition();
            EmitSignal(SignalName.DragEnded);
        }
        else
        {
            EmitSignal(SignalName.Clicked);
        }
    }
}
