# assets/sprites — the Track A ↔ Track B contract

The app loads everything in this folder **by name**. Filenames must match exactly
(plan §2.7). All files: same canvas size, RGBA PNG.

```
idle_neutral_base.png       # full character, pupils HIDDEN
idle_neutral_pupils.png     # ONLY pupils, transparent elsewhere (overlay)
anim_blink_000.png, anim_blink_001.png, ...
anim_bounce_000.png, ...
anim_lookaround_000.png, ...
anim_sit_000.png, ...
react_click_000.png, ...
react_drag_000.png, ...
react_sleep_000.png, ...
```

- Sequences: `{state}_{frame:000}.png`, zero-padded, starting at `000`.
  `AssetLoader.BuildSpriteFrames()` groups these into animations automatically.
- The two `idle_neutral_*` files are NOT numbered, so they're treated as the
  static base + pupil overlay (not an animation).

## Track A deliverable note (fill this in)

- Canvas size: `512 x 512`
- Playback fps: `12`
- Eye-socket centers (px, neutral pose): left `( , )`, right `( , )`
  → set these in `EyeTracker` exports (`LeftEyeCenter`, `RightEyeCenter`).

## Placeholders (so Track B builds before real renders exist)

Run from `DesktopPet/`:

```bash
python3 tools/make_placeholders.py
```

This writes flat-colored stand-ins for the base, pupils, and a couple of short
sequences. If you don't have Python/Pillow, the app also self-heals: any missing
sprite renders as a translucent magenta square (see `AssetLoader`), so the shell
still runs — you just won't see the character until real assets land.
