using Godot;

namespace DesktopPet;

/// <summary>
/// Moves the pupil sprite toward the cursor, clamped to an ellipse matching the
/// eye socket. Active only in NEUTRAL (plan §4.3). Eye-socket centers and radii
/// come from Track A's deliverable (plan §2.5).
/// </summary>
public partial class EyeTracker : Node
{
    /// <summary>Max pupil travel from socket center, in canvas pixels.</summary>
    [Export] public float MaxOffsetPx = 6f;

    /// <summary>Socket centers in canvas-local pixels (set from Track A coords).</summary>
    [Export] public Vector2 LeftEyeCenter = new(210, 230);
    [Export] public Vector2 RightEyeCenter = new(302, 230);

    /// <summary>Ellipse half-extents the pupil is clamped within.</summary>
    [Export] public Vector2 SocketRadius = new(8, 6);

    /// <summary>Smoothing factor (0..1 per frame); higher = snappier.</summary>
    [Export] public float Smoothing = 0.25f;

    private Sprite2D _pupils = null!;
    private StateMachine _state = null!;
    private Vector2 _restOffset;
    private Vector2 _currentOffset;

    public void Setup(Sprite2D pupils, StateMachine state)
    {
        _pupils = pupils;
        _state = state;
        _restOffset = pupils.Offset;
        _currentOffset = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        if (_pupils is null || _state is null)
            return;

        // Pause tracking during any baked animation; ease pupils back to rest.
        Vector2 target = _state.Current == PetState.Neutral ? ComputeOffset() : Vector2.Zero;

        _currentOffset = _currentOffset.Lerp(target, Smoothing);
        _pupils.Offset = _restOffset + _currentOffset;
    }

    private Vector2 ComputeOffset()
    {
        // Cursor in window/canvas space (physical px == canvas px at scale 1).
        // Both terms read from DisplayServer so the spaces match (plan §4.10).
        Vector2 cursor = PetWindow.GetCursorInWindowSpace();
        Vector2 eyeMid = (LeftEyeCenter + RightEyeCenter) * 0.5f;

        Vector2 dir = cursor - eyeMid;
        if (dir.LengthSquared() < 0.001f)
            return Vector2.Zero;

        // Scale toward cursor, then clamp into the socket ellipse.
        Vector2 raw = dir.Normalized() * MaxOffsetPx;
        return ClampToEllipse(raw, SocketRadius);
    }

    private static Vector2 ClampToEllipse(Vector2 v, Vector2 r)
    {
        if (r.X <= 0 || r.Y <= 0)
            return Vector2.Zero;
        float norm = (v.X * v.X) / (r.X * r.X) + (v.Y * v.Y) / (r.Y * r.Y);
        return norm <= 1f ? v : v / Mathf.Sqrt(norm);
    }
}
