using Godot;
using System;
using System.Collections.Generic;

namespace DesktopPet;

/// <summary>
/// Two timers (plan §4.4):
///  - random idle: fires every [min,max]s, plays a random idle clip from NEUTRAL.
///  - inactivity: after sleep_after_seconds with no interaction, enters SLEEP.
/// Any interaction must call <see cref="NotifyInteraction"/> to reset the sleep
/// countdown and wake the pet.
/// </summary>
public partial class IdleScheduler : Node
{
    [Signal] public delegate void PlayIdleEventHandler(string clipName);
    [Signal] public delegate void EnterSleepEventHandler();
    [Signal] public delegate void WakeEventHandler();

    private static readonly string[] IdleClips =
    {
        "anim_blink", "anim_bounce", "anim_lookaround", "anim_sit",
    };

    private SettingsConfig _settings = null!;
    private StateMachine _state = null!;
    private readonly RandomNumberGenerator _rng = new();

    private double _idleTimer;
    private double _idleInterval;
    private double _sinceInteraction;
    private bool _asleep;

    public void Setup(SettingsConfig settings, StateMachine state)
    {
        _settings = settings;
        _state = state;
        _rng.Randomize();
        ScheduleNextIdle();
    }

    public override void _Process(double delta)
    {
        if (_state is null)
            return;

        // --- inactivity -> sleep ---
        _sinceInteraction += delta;
        if (!_asleep && _state.Current == PetState.Neutral &&
            _sinceInteraction >= _settings.SleepAfterSeconds)
        {
            _asleep = true;
            EmitSignal(SignalName.EnterSleep);
        }

        // --- random idle (only while resting & awake) ---
        if (_asleep || _state.Current != PetState.Neutral)
            return;

        _idleTimer += delta;
        if (_idleTimer >= _idleInterval)
        {
            ScheduleNextIdle();
            string clip = IdleClips[_rng.RandiRange(0, IdleClips.Length - 1)];
            EmitSignal(SignalName.PlayIdle, clip);
        }
    }

    /// <summary>Call on any user interaction (move/click/drag/app change).</summary>
    public void NotifyInteraction()
    {
        _sinceInteraction = 0;
        if (_asleep)
        {
            _asleep = false;
            EmitSignal(SignalName.Wake);
        }
    }

    private void ScheduleNextIdle()
    {
        _idleTimer = 0;
        _idleInterval = _rng.RandfRange(
            _settings.MinIntervalSeconds, _settings.MaxIntervalSeconds);
    }
}
