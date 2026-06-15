# Cinnamoroll DeskMate — Track B app (technical setup)

Godot 4 + C# desktop sprite-pet. This is the **Track B** scaffold from
[`../cinnamoroll-desktop-pet-plan.md`](../cinnamoroll-desktop-pet-plan.md). It is
structured so it can be built end-to-end against placeholder sprites and have the
real Goo Engine renders dropped in later (Track A).

## Toolchain (pinned)

| Thing | Version |
|---|---|
| Godot | **4.3**, **.NET / Mono build** (not the standard build) |
| .NET SDK | **8.0** |
| Target framework | `net8.0` |
| OS target | Windows 10/11 (P/Invoke into `user32.dll` for foreground-app + edge sitting) |

Pin these. The mouse-passthrough (`DisplayServer.WindowSetMousePassthrough`) and
status-indicator (`DisplayServer.CreateStatusIndicator`) APIs have shifted across
4.x point releases; a floating version makes the click-through and tray behaviour
non-reproducible.

## First run

1. Install the **.NET 8 SDK** and the **Godot 4.3 .NET** editor.
2. Generate placeholder sprites so the app has something to show:
   ```bash
   python3 tools/make_placeholders.py
   ```
   (See [`assets/sprites/README.md`](assets/sprites/README.md). Requires Pillow,
   or use the documented fallback.)
3. Open `project.godot` in the Godot .NET editor. It will create the C# solution
   on first open. Build (hammer icon) then run (F5).

The window starts transparent, borderless, always-on-top, and shows
`idle_neutral_base.png`. **Quit via the tray icon** (the window has no taskbar
entry by design — see `src/TrayMenu.cs`).

## Compile-check (no Godot editor needed)

A Godot 4 C# project compiles from the `Godot.NET.Sdk` + `GodotSharp` NuGet
packages alone, so you can verify the C# without installing the editor:

```bash
dotnet build DesktopPet.csproj                      # compile-check (0 warnings target)
dotnet format DesktopPet.csproj --verify-no-changes # formatting/style lint
```

This is what the SessionStart hook (`.claude/hooks/session-start.sh`) sets up for
Claude Code on the web: it installs the .NET 8 SDK and restores packages so these
commands run in a fresh web session. Note this checks that the code **compiles** —
actually **running** the pet still needs the Godot 4.3 .NET editor on a desktop.

## Build order

Follow §5 of the plan. Milestones 1–6 work on placeholders. Component → milestone
map:

| Milestone | Files |
|---|---|
| 1. Window shell + tray/quit | `PetWindow.cs`, `TrayMenu.cs` |
| 2. Click-through (verify early) | `PetWindow.cs` |
| 3. Eye tracking | `EyeTracker.cs` |
| 4. Drag & drop | `DragHandler.cs` |
| 5. Idle state machine + sleep | `IdleScheduler.cs`, `StateMachine.cs` |
| 6. Click reaction | `StateMachine.cs` |
| 7. Active-app reaction | `AppReactions.cs`, `Win32Interop.cs` |
| 8. Settings window | `TrayMenu.cs`, `SettingsConfig.cs` |
| 9. Edge sitting (optional) | `EdgeSitter.cs`, `Win32Interop.cs` |

## Coordinate-space rule (read this before touching positioning)

For any cursor↔window math, read **both** sides from `DisplayServer` in physical
pixels (`MouseGetPosition()`, `WindowGetPosition()`) and use the delta. Never mix
Godot logical input coordinates with OS window coordinates — that mismatch is the
usual cause of off-aim pupils and drag drift on scaled/multi-monitor setups. Go
through the helpers in `PetWindow` (`GetCursorInWindowSpace`,
`ClampToVisibleScreen`).

## Status of the C# files

The `src/*.cs` files are a **compilable skeleton**: window/tray/state-machine
wiring is implemented; per-feature bodies are stubbed with `// TODO(milestone N)`
markers and the exact API calls to use, so each milestone is a fill-in rather
than a blank page.
