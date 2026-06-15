using Godot;

namespace DesktopPet;

/// <summary>
/// Polls the foreground app and, when it CHANGES, maps the process name to a
/// reaction state from config (plan §4.7). Only fires from NEUTRAL so it never
/// interrupts an animation.
/// </summary>
public partial class AppReactions : Node
{
    [Signal] public delegate void ReactionRequestedEventHandler(string reactionState);

    /// <summary>How often to poll the foreground window, seconds.</summary>
    [Export] public double PollIntervalSeconds = 1.0;

    private SettingsConfig _settings = null!;
    private StateMachine _state = null!;
    private double _accum;
    private string? _lastProcess;

    public void Setup(SettingsConfig settings, StateMachine state)
    {
        _settings = settings;
        _state = state;
    }

    public override void _Process(double delta)
    {
        if (_settings is null || !Win32Interop.IsWindows)
            return;

        _accum += delta;
        if (_accum < PollIntervalSeconds)
            return;
        _accum = 0;

        string? proc = Win32Interop.GetForegroundProcessName();
        if (proc is null || proc == _lastProcess)
            return;

        _lastProcess = proc;

        if (_state.Current != PetState.Neutral)
            return; // changed apps but we're busy; don't interrupt.

        // Config keys may be written with or without ".exe" (plan §6 uses
        // "chrome.exe"); GetForegroundProcessName returns no extension.
        string key = proc.ToLowerInvariant();
        if (_settings.AppReactions.TryGetValue(key, out string? reaction) ||
            _settings.AppReactions.TryGetValue(key + ".exe", out reaction))
        {
            EmitSignal(SignalName.ReactionRequested, reaction);
        }
    }
}
