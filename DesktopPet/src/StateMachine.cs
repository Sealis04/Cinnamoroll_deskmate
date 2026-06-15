using Godot;
using System;

namespace DesktopPet;

/// <summary>
/// The pet's top-level states. Only NEUTRAL runs live eye tracking; everything
/// else plays a baked sequence (see plan §4.3).
/// </summary>
public enum PetState
{
    Neutral,   // resting; eye tracking active
    IdleAnim,  // playing a random idle clip
    Drag,      // being dragged by the cursor
    React,     // one-shot reaction (e.g. click)
    Sleep,     // inactivity / "quiet app"; loops react_sleep
}

/// <summary>
/// Minimal state machine. Holds the current state and raises a signal on change
/// so EyeTracker (pause/resume), TrayMenu, etc. can react without polling.
/// </summary>
public partial class StateMachine : Node
{
    [Signal]
    public delegate void StateChangedEventHandler(int from, int to);

    public PetState Current { get; private set; } = PetState.Neutral;

    /// <summary>True while a state that bakes its own eyes is active.</summary>
    public bool IsAnimating =>
        Current is PetState.IdleAnim or PetState.Drag or PetState.React or PetState.Sleep;

    public void TransitionTo(PetState next)
    {
        if (next == Current)
            return;

        PetState prev = Current;
        Current = next;
        GD.Print($"[StateMachine] {prev} -> {next}");
        EmitSignal(SignalName.StateChanged, (int)prev, (int)next);
    }

    /// <summary>Convenience: only transition if we are currently resting.</summary>
    public bool TryTransitionFromNeutral(PetState next)
    {
        if (Current != PetState.Neutral)
            return false;
        TransitionTo(next);
        return true;
    }
}
