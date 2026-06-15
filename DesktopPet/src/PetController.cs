using Godot;

namespace DesktopPet;

/// <summary>
/// Root node of Pet.tscn. Owns the components and wires their signals together.
/// Scene layout (see scenes/Pet.tscn):
///   Pet (Node2D, this)
///     Base   : Sprite2D            -- idle_neutral_base
///     Pupils : Sprite2D            -- idle_neutral_pupils (moved by EyeTracker)
///     Anim   : AnimatedSprite2D    -- baked sequences; hidden in NEUTRAL
/// </summary>
public partial class PetController : Node2D
{
    private Sprite2D _base = null!;
    private Sprite2D _pupils = null!;
    private AnimatedSprite2D _anim = null!;

    private SettingsConfig _settings = null!;
    private readonly StateMachine _state = new();
    private readonly PetWindow _window = new();
    private readonly EyeTracker _eyes = new();
    private readonly IdleScheduler _idle = new();
    private readonly DragHandler _drag = new();
    private readonly AppReactions _apps = new();
    private readonly TrayMenu _tray = new();
    private readonly EdgeSitter _edge = new();

    public override void _Ready()
    {
        _base = GetNode<Sprite2D>("Base");
        _pupils = GetNode<Sprite2D>("Pupils");
        _anim = GetNode<AnimatedSprite2D>("Anim");

        _settings = SettingsConfig.Load();

        // Load assets per the Track A contract, with placeholder fallback.
        _base.Texture = AssetLoader.LoadOrPlaceholder("idle_neutral_base.png", _settings.CanvasSize);
        _pupils.Texture = AssetLoader.LoadOrPlaceholder("idle_neutral_pupils.png", _settings.CanvasSize);
        _anim.SpriteFrames = AssetLoader.BuildSpriteFrames();

        // Register child nodes so their _Process/_Input run.
        foreach (Node n in new Node[] { _state, _window, _eyes, _idle, _drag, _apps, _tray, _edge })
            AddChild(n);

        // Milestone 1: window shell + tray/quit.
        _window.Configure(_settings);
        _tray.Setup(_settings);

        // Milestone 2: click-through. v1 = whole-canvas clickable bounding box;
        // refine to silhouette later. Verify on the pinned build EARLY.
        _window.SetWholeWindowClickable();

        // Milestone 3: eye tracking (NEUTRAL only).
        _eyes.Setup(_pupils, _state);

        // Milestones 4-7: interactions.
        _drag.Setup(_window);
        _idle.Setup(_settings, _state);
        _apps.Setup(_settings, _state);
        _edge.Setup(_settings, _window);

        WireSignals();
        ShowNeutral();
    }

    private void WireSignals()
    {
        // Drag / click
        _drag.DragStarted += () => { _idle.NotifyInteraction(); PlayState(PetState.Drag, "react_drag", loop: true); };
        _drag.DragEnded += ReturnToNeutral;
        _drag.Clicked += () => { _idle.NotifyInteraction(); PlayOneShot(PetState.React, "react_click"); };

        // Idle scheduler
        _idle.PlayIdle += clip => PlayOneShot(PetState.IdleAnim, clip);
        _idle.EnterSleep += () => PlayState(PetState.Sleep, "react_sleep", loop: true);
        _idle.Wake += ReturnToNeutral;

        // Foreground-app reactions
        _apps.ReactionRequested += clip =>
        {
            _idle.NotifyInteraction();
            bool loop = clip is "react_sleep" or "react_drag";
            if (loop)
                PlayState(PetState.Sleep, clip, loop: true);
            else
                PlayOneShot(PetState.IdleAnim, clip);
        };

        // Animation completion -> back to rest (one-shots only).
        _anim.AnimationFinished += () =>
        {
            if (_state.Current is PetState.IdleAnim or PetState.React)
                ReturnToNeutral();
        };

        // Tray / lifecycle
        _tray.QuitRequested += () => { _window.SavePosition(); GetTree().Quit(); };
        _tray.ResetPositionRequested += () => { _settings.LastPos = new Vector2I(-1, -1); _window.RestorePosition(); };
        _tray.EdgeSittingToggled += _edge.SetEnabled;
        _tray.SettingsRequested += OpenSettings;
    }

    // ---- State presentation helpers ---------------------------------------

    private void ShowNeutral()
    {
        _anim.Visible = false;
        _anim.Stop();
        _base.Visible = true;
        _pupils.Visible = true;
    }

    private void ShowAnim(string clip, bool loop)
    {
        _base.Visible = false;
        _pupils.Visible = false;
        _anim.Visible = true;
        if (_anim.SpriteFrames is { } frames && frames.HasAnimation(clip))
        {
            frames.SetAnimationLoop(clip, loop);
            _anim.Play(clip);
        }
        else
        {
            GD.PushWarning($"[Pet] missing animation '{clip}'; staying neutral.");
            ReturnToNeutral();
        }
    }

    private void PlayOneShot(PetState s, string clip)
    {
        if (!_state.TryTransitionFromNeutral(s))
            return;
        ShowAnim(clip, loop: false);
    }

    private void PlayState(PetState s, string clip, bool loop)
    {
        _state.TransitionTo(s);
        ShowAnim(clip, loop);
    }

    private void ReturnToNeutral()
    {
        _state.TransitionTo(PetState.Neutral);
        ShowNeutral();
    }

    private void OpenSettings()
    {
        // TODO(milestone 8): open a small normal (non-passthrough) window bound
        // to settings.cfg. For now, just surface where the file lives.
        GD.Print($"[Pet] Settings requested -> edit {SettingsConfig.Path}");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            _window.SavePosition();
    }
}
