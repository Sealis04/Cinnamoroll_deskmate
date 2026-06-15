# Desktop Sprite-Pet — Working Plan

A Windows desktop companion ("pet") rendered as a 2D sprite out of Goo Engine, with a small Godot 4 + C# app that displays it, handles interactions, and plays idle animations.

This document is the build spec. It is split into two tracks that can be developed **in parallel**:

- **Track A — Asset render-out (done by me, the user, in Goo Engine/Blender).** Produces the sprite frames.
- **Track B — The app (built by Claude Code).** Can be built immediately against placeholder sprites that follow the naming convention below, then the real Goo Engine renders are dropped in.

---

## 1. Architecture decision (and why)

**Path: 2D sprite, pre-rendered from Goo Engine.**

The Goo Engine anime aesthetic lives in its custom Eevee NPR shader nodes. Those do **not** survive a 3D export (GLB/glTF flattens them to standard PBR), so a real-time 3D pet would lose the look. By rendering the character to image sequences *inside* Goo Engine, the NPR shading, outlines, and cel banding bake into the PNGs — preserving the aesthetic 1:1. The app then displays flat sprites.

**Trade-off accepted:** no true any-angle 3D head tracking. This is fine because cursor tracking here is **eyes-only**, which is implemented as a separate pupil sprite layer offset toward the cursor (smooth, continuous — see §4.3). All other interactions are unaffected by going 2D.

### Tech stack
- **Engine/app:** Godot 4.x with **C# (.NET)**.
  - **Pinned version: Godot 4.3 (.NET / Mono build), .NET 8 SDK.** Pin this and don't float it — the mouse-passthrough and status-indicator APIs (§4.2, §4.9) have shifted across 4.x point releases, so a pinned version keeps behaviour reproducible. See `DesktopPet/README.md` for the exact toolchain.
  - 2D sprite display + animation playback are native.
  - Transparent / borderless / always-on-top window is supported via window flags.
  - Native mouse **passthrough polygon** support handles click-through cleanly (no manual Win32 toggling needed).
  - C# allows **P/Invoke into `user32.dll`** for the Windows-specific features (edge sitting, foreground-app detection).
- **Alternative considered:** native C# WPF. Cleaner Win32 interop but more manual sprite/animation handling. Stick with Godot unless the Win32 features become the bottleneck.

---

## 2. Track A — Goo Engine character pipeline & render-out

This section runs in production order: **model (§2.1) → rig (§2.2) → animate
(§2.3) → render rules (§2.4) → two-layer split (§2.5) → states (§2.6) → naming
contract (§2.7) → deliverable (§2.8)**. Sections §2.4–2.8 are the *interface*
Track B depends on; §2.1–2.3 are the *production work* that feeds them.

### 2.1 Modeling the rig-ready mesh

Goal: a chibi character whose **silhouette reads at 512²**, whose **eyes are
split into separable geometry** (the hard requirement that makes §2.5 possible),
and whose proportions stay registered to the locked orthographic camera.

**Reference & proportions**
- Gather **front + side** reference and model to an orthographic front view so the
  mesh lines up with the eventual locked render camera.
- Chibi build: head ≈ 45–55% of total height, rounded body, short stubby limbs,
  long floppy ears. Exaggerate — it's a tiny on-screen pet, subtle detail is lost.

**Topology (pre-rendered, so be generous)**
- This is **never realtime**, so spend polygons on a smooth silhouette. All-quad
  base mesh + a **Subdivision Surface** modifier is fine.
- Keep clean **edge loops around the eyes and mouth** so blink/expression shape
  keys and the jaw bone deform cleanly.
- Build order: head → body → ears → muzzle/snout → arms → legs → tail.

**Eyes — the part that gates the two-layer split (§2.5)**
- Model the **iris/pupil as its own geometry, separate from the eye-white/socket**.
  Recommended anime approach: a slightly recessed **eye-white surface** plus a
  **flat iris/pupil "decal" plane** that sits just in front of it and faces the
  camera. Flat iris planes are trivial to render-isolate and keep the pupil on a
  consistent plane for clean compositing.
- Put **every pupil/iris object in a dedicated `Pupils` Collection** so a single
  visibility toggle drives both render passes in §2.5 (Pupils hidden = base layer;
  only Pupils visible = pupil layer).

**Materials, outlines, collections**
- Author the **Goo Engine NPR / cel shader** per surface now (this is the look that
  must bake into the PNGs). Add outlines via Goo's line-art / inverted-hull.
- Keep the **pupil material independent** of the eye-white so they can be lit/keyed
  separately if needed.
- Organize Collections: `Body`, `Face`, `Pupils` (and `Hidden_Helpers` for empties).

**Scale & origin**
- Set the character **origin at a single consistent point** (e.g. base of the body)
  and place it on the world origin under the locked camera, so the **rest pose
  registers exactly** to where the base/pupil layers are composited (§2.5, §2.4).

### 2.2 Rigging

Keep it minimal — a chibi pet needs expressiveness in the **face and squash**, not
a full biped IK rig.

**Armature (body)**
- `root`/COG → `spine` (1–2 bones) → `head`. Stubby `arm.L/R`, `leg.L/R`
  (FK is fine at this scale; IK on legs optional). `ear.L/R` as 2–3-bone chains for
  **follow-through floppiness**, plus a `tail` bone or short chain.
- Add a **body squash-&-stretch control** (a scale bone on the spine/COG) so the
  bounce can deform freely — pre-rendered means exaggeration is free.

**Face rig (highest priority — this is what sells the pet)**
- **Blink:** a `blink` **shape key** (0→1 closes the eyelids). Used by `anim_blink`
  and as a beat inside other clips.
- **Mouth/expression:** shape keys (`smile`, `surprise`, `mouth_open`) and/or a
  simple `jaw` bone.
- **Eye darting (animated clips only):** an `eye_target` **empty** with a
  **Track-To / Look-At constraint** on the iris planes. Note this drives the eyes
  **only in baked animations** (e.g. `anim_lookaround`); in NEUTRAL the pupils are
  moved **in-app**, not in the render, so the neutral render keeps eyes centered.

**Rig hygiene (registration-critical)**
- The armature **rest pose must equal the `idle_neutral` pose exactly** — the base
  layer is rendered from this rest, so any drift breaks pupil-overlay alignment.
- Add **custom bone shapes** for usability; **weight-paint and test** deformation,
  paying attention to the ears, tail, and the squash on the body.

### 2.3 Animation authoring

**Global rules**
- Author the Blender scene at the chosen **12 fps** (matches §2.4).
- Apply the **12 principles** where they pay off: anticipation, squash/stretch,
  ease in/out, overshoot, and **follow-through on ears & tail**.
- **Transition contract with Track B:** the app cuts NEUTRAL → clip → NEUTRAL with
  no blending. So **one-shot clips start and end on (or very near) the neutral
  pose**, and **looping clips are seamless** (frame `000` ≈ last frame). This is
  what makes the in-app state switches read smoothly.
- One **Action per state** (use the Action editor / NLA); keep them all in one
  master `.blend` so the rig is shared.

**Per-clip breakdown** (frame counts are targets at 12 fps; tune to taste)

| Clip | ~Frames | Loop? | Beats |
|---|---|---|---|
| `idle_neutral` | 1 (static) | — | The rest pose. Rendered as base + pupils (§2.5), not animated. |
| `anim_blink` | 3–5 | no | open → half → closed → half → open. Quick. Can also be an overlay. |
| `anim_bounce` | 10–14 | no (return to neutral) | squash anticipation → hop + overshoot → settle; ears trail. |
| `anim_lookaround` | 18–30 | no | head turns L/R, eyes dart via `eye_target`, small stretch, return. |
| `anim_sit` | 8–16 | no | settle down into a held sit pose. |
| `react_click` | 6–10 | no | anticipation dip → happy/surprised pop → settle. |
| `react_drag` | 8–12 | **yes** | held-by-scruff dangle; gentle pendulum sway; first ≈ last. |
| `react_sleep` | 12–24 | **yes** | slow breathing rise/fall (+ optional Zzz); first ≈ last. |

**Render-out workflow (authoring → the §2.7 contract)**
- Per Action: set the scene **Frame Start = 0** and end to the clip length, then set
  the output path so Blender writes the contract names directly, e.g.
  output `//render/anim_bounce_###` → `anim_bounce_000.png, _001, …` (3 `#` = the
  zero-padding §2.7 requires). RGBA PNG, transparent film (§2.4).
- **Neutral two-layer pass (§2.5):** render the single rest frame **twice** — once
  with the `Pupils` Collection hidden → `idle_neutral_base.png`; once with **only**
  `Pupils` visible → `idle_neutral_pupils.png`.

**QA before handoff**
- Onion-skin / overlay each sequence against the neutral pose to confirm
  **registration** (no canvas drift); verify loops have **no seam**; confirm the
  **squash/stretch stays inside the 512² canvas**.
- After the final neutral render, read off the **pixel center of each eye** in the
  image editor → these are the `eye-socket center coordinates` in the §2.8
  deliverable and the `LeftEyeCenter` / `RightEyeCenter` exports in `EyeTracker.cs`.

**Suggested source organization (Track A side, outside the app repo)**
```
art/
  cinnamoroll_master.blend     # mesh + rig + all Actions + NPR materials
  render_settings.blend        # or a saved render preset (512², 12fps, transparent)
  refs/                        # front/side reference images
```

### 2.4 Global render rules (apply to EVERY render)
These guarantee the sprites line up when composited and swapped in the app.

- **Fixed canvas:** one resolution for everything, e.g. **512×512 px**. Never change it between renders.
- **Fixed camera:** lock the camera (orthographic preferred), centered on the character. Do **not** move it between states. Character stays registered to the same canvas position across all frames.
- **Transparent background:** Render Properties → Film → **Transparent** ON. Output **PNG with RGBA / alpha**.
- **Consistent lighting:** same light setup across all renders so shading matches frame to frame.
- **Frame rate for sequences:** render animated states at the app's playback rate — **12 fps** is plenty for a pet (24 if you want extra smoothness). Decide once, keep it.

### 2.5 The two-layer split (for eye tracking)
The neutral resting pose is rendered as **two** aligned layers so the app can move the pupils independently:

- **Base layer** — full character and face, but with **pupils/irises hidden** (eye whites/sockets visible, no pupils). Use a Blender **View Layer** or Collection visibility toggle to hide the pupil geometry.
- **Pupil layer** — **only** the pupil/iris geometry visible, everything else hidden, transparent background.

Both rendered from the identical locked camera/canvas so the pupil layer overlays the base perfectly. In the app the pupil layer sits on top and is nudged toward the cursor (clamped to the socket).

> Only the **neutral resting pose** needs the two-layer split. Animated idles (below) bake the eyes in.

### 2.6 Animation states to render
Each animated state is a frame sequence with eyes baked into the frames (live tracking pauses during animations — see §4.3).

| State | Type | Notes |
|---|---|---|
| `idle_neutral` | **2 static PNGs** (base + pupils) | The resting pose with live eye tracking |
| `anim_blink` | short sequence | quick blink; can also be an overlay if preferred |
| `anim_bounce` | sequence | little hop/bob |
| `anim_lookaround` | sequence | glances around, stretches |
| `anim_sit` | sequence | sits/settles |
| `react_click` | sequence | reaction when clicked (surprised/happy) |
| `react_drag` | sequence/loop | dangle pose while being dragged |
| `react_sleep` | sequence/loop | when idle a long time or in a "quiet" app |

(Start with `idle_neutral`, `anim_blink`, `anim_bounce` to get the loop working; add the rest later.)

### 2.7 Naming convention (the contract between Track A and Track B)
The app loads assets by name, so this must be exact.

```
assets/sprites/
  idle_neutral_base.png
  idle_neutral_pupils.png
  anim_blink_000.png, anim_blink_001.png, ...
  anim_bounce_000.png, anim_bounce_001.png, ...
  anim_lookaround_000.png, ...
  anim_sit_000.png, ...
  react_click_000.png, ...
  react_drag_000.png, ...
  react_sleep_000.png, ...
```

- Sequences: `{state}_{frame:000}.png`, zero-padded, starting at `000`.
- All same canvas size, all RGBA.

### 2.8 Track A deliverable
A populated `assets/sprites/` folder following §2.7, plus a one-line note of the chosen **canvas size**, **fps**, and **eye-socket center coordinates** (pixel x,y of each eye center in the neutral pose — the app needs these to anchor pupil offset).

---

## 3. Track B — App project structure

```
DesktopPet/
  project.godot
  DesktopPet.csproj
  README.md                # toolchain + setup (Track B technical setup)
  src/
    PetController.cs        # top-level node; wires everything, runs the per-frame loop
    StateMachine.cs         # NEUTRAL / IDLE_ANIM / DRAG / REACT / SLEEP states
    PetWindow.cs            # window setup: transparent, borderless, always-on-top, passthrough, DPI helpers
    EyeTracker.cs           # pupil offset toward cursor (NEUTRAL only), DPI/multi-monitor aware
    IdleScheduler.cs        # random timer -> pick & play an idle anim; idle-timeout -> sleep
    DragHandler.cs          # grab/move window, click-vs-drag, drag reaction
    Win32Interop.cs         # P/Invoke wrappers (foreground window, enum windows, taskbar)
    AppReactions.cs         # foreground-app -> reaction mapping (config-driven)
    TrayMenu.cs             # system tray icon + right-click menu: Quit / Settings / toggles  (lifecycle)
    EdgeSitter.cs           # OPTIONAL, build last: perch on window/taskbar edges
    SettingsConfig.cs       # typed loader for config/settings.cfg
  assets/sprites/           # filled by Track A (placeholders until then) + README contract
  config/
    settings.cfg            # idle interval range, app->reaction map, toggles
  scenes/
    Pet.tscn                # Sprite2D (base) + Sprite2D (pupils) + AnimatedSprite2D (anims)
```

---

## 4. Track B — Feature implementation notes

### 4.1 Window shell (`PetWindow.cs`) — **build first**
- Borderless, transparent background, always-on-top, **no taskbar entry**.
- Window sized to the sprite canvas (e.g. 512×512).
- Godot setup: enable per-pixel transparency in project settings (transparent BG + borderless + always-on-top window flags); ensure the viewport clears to transparent.
- Display the static `idle_neutral_base.png` to confirm the shell works before anything else.
- **Window position persistence:** save the last window position to `config/settings.cfg` (`[window] last_pos_x/last_pos_y`) on move/close and restore it on launch, clamped back onto a currently-connected screen so a pet last seen on a now-disconnected monitor still appears.

### 4.2 Click-through (`PetWindow.cs`) — **verify EARLY (promoted to milestone 2)**
- Use Godot 4's **mouse passthrough polygon** (`DisplayServer.WindowSetMousePassthrough`): define the polygon as the character's silhouette (or a simple bounding region for v1). Clicks inside it hit the pet; clicks on transparent areas fall through to the desktop.
- This avoids manual `WS_EX_TRANSPARENT` toggling. **This is the classic sticking point and the API has been flaky across 4.x — prototype it immediately after the window shell** (before eye tracking) so we find out early if the pinned version behaves. If `WindowSetMousePassthrough` misbehaves on the target machine, fall back to a manual `WS_EX_LAYERED | WS_EX_TRANSPARENT` toggle via `Win32Interop` driven by hit-testing the silhouette alpha.
- v1 uses a static bounding polygon. Later, the passthrough region can be refreshed per state (the silhouette differs between neutral and animated poses); keep the refresh call in one place so it can be wired to state changes.

### 4.3 Eye tracking (`EyeTracker.cs`)
- Two `Sprite2D`s in the neutral pose: **base** (bottom) + **pupils** (top), aligned at load.
- Each frame, **only while in NEUTRAL state**:
  - Get global cursor position, convert to the window's local space.
  - For each eye, compute vector from the eye-socket center (from Track A's coordinates) to the cursor.
  - Normalize and scale to a small max offset (a few px), **clamp to an ellipse** matching the socket so the pupil never leaves the eye.
  - Apply as the pupil sprite's offset.
- **Coordinate space & DPI (see §4.10):** the global cursor and window position must be read in the **same** space. Read both via `DisplayServer` (physical pixels) and subtract, rather than mixing Godot's logical input position with OS window coordinates — that mismatch is the usual cause of "pupils point slightly off" on scaled/multi-monitor setups.
- **Pause tracking** during any animation state (the head/eyes move in those frames, so a fixed offset region won't align). Resume on return to NEUTRAL. Reads perfectly natural: tracks while resting → plays animation → resumes tracking.

### 4.4 Random idle animations (`IdleScheduler.cs` + `StateMachine.cs`)
- Timer fires on a **random interval** (config: e.g. 8–20s).
- On fire (only if currently NEUTRAL): pick a random clip from the idle set, switch to IDLE_ANIM, play the `AnimatedSprite2D` sequence once, then return to NEUTRAL.
- **Idle-timeout → sleep:** a separate inactivity timer (config `[idle] sleep_after_seconds`) tracks time since the last user interaction (move, click, drag, foreground-app change). When it elapses while in NEUTRAL, enter SLEEP and loop `react_sleep`; any interaction wakes the pet back to NEUTRAL. This is the trigger that §2.6's `react_sleep` referenced but the state machine previously didn't drive.
- Pre-rendered, so they look better than real-time would.

### 4.5 Drag & drop (`DragHandler.cs`)
- Left mouse-down **inside the opaque region** → enter DRAG (play `react_drag`), then move the OS window so the pet follows the cursor while held.
- On release → return to NEUTRAL (optional little settle frame), and persist the new window position (§4.1).
- Distinguish **click vs drag**: down+up with negligible movement = click (→ §4.6); movement past a threshold = drag.
- Move the window using **physical-pixel deltas** from `DisplayServer.MouseGetPosition()` so dragging across monitors with different scale factors doesn't drift (§4.10).

### 4.6 Click reaction (`StateMachine.cs`)
- A click (not a drag) → play `react_click` once → return to NEUTRAL.

### 4.7 Active-app reaction (`AppReactions.cs` + `Win32Interop.cs`)
- Poll **`GetForegroundWindow`** → **`GetWindowThreadProcessId`** → resolve **process name**.
- Map process name → reaction in `config/settings.cfg` (e.g. a text editor → `react_sleep`, browser → `anim_lookaround`).
- Only trigger when the foreground app **changes**, and only from NEUTRAL, so it doesn't interrupt constantly.

### 4.8 Window/taskbar edge sitting (`EdgeSitter.cs`) — **OPTIONAL, build LAST**
- `EnumWindows` + `GetWindowRect` to find visible window top edges; `FindWindow("Shell_TrayWnd")` for the taskbar; snap/perch logic onto an edge.
- ⚠️ **Caveat:** reading other windows' positions can trip some game **anti-cheat** systems into false positives (MateEngine disables this feature for exactly this reason). Make it a **toggle in `settings.cfg`, default OFF**, and document the risk to the user in-app.

### 4.9 Lifecycle: tray icon, quit & settings (`TrayMenu.cs`) — **build with the shell**
A borderless, no-taskbar-entry, always-on-top, click-through window has **no built-in way to be closed or configured**. This component provides it:

- **System tray (status) indicator** via `DisplayServer.CreateStatusIndicator` (Godot 4.3+), with a right-click menu: **Settings…**, **Edge sitting** (checkable, reflects/sets the config toggle + its anti-cheat note), **Reset position**, **Quit**.
- **Right-click on the sprite** opens the same context menu (so the tray isn't the only entry point).
- Settings opens a tiny normal (non-passthrough) window bound to `config/settings.cfg`.
- **Fallback:** if `CreateStatusIndicator` is unavailable on the pinned build, fall back to a tray icon via `Win32Interop` (`Shell_NotifyIcon`) or, at minimum, a global quit hotkey — the app must always be quittable without Task Manager.
- This is its own milestone; previously it was missing entirely from the spec.

### 4.10 Multi-monitor & DPI scaling (cross-cutting)
Mixed-DPI, multi-monitor setups are the main source of subtle positioning bugs (pupils aimed slightly wrong, drag drift, pet restoring off-screen). Rules:

- **One coordinate space:** for any cursor↔window math, read **both** sides from `DisplayServer` in **physical pixels** (`DisplayServer.MouseGetPosition()` and `DisplayServer.WindowGetPosition()`) and work with their delta. Never mix Godot logical input coordinates with OS window coordinates.
- **Per-screen scale:** query `DisplayServer.ScreenGetScale()` / `ScreenGetDpi()` for the screen the pet currently sits on; if the canvas should look the same physical size everywhere, scale the root accordingly (or accept fixed-pixel size and document it).
- **Restore safely:** when restoring a saved window position, verify it still lands on a connected screen (`DisplayServer.GetScreenCount()` / `ScreenGetUsableRect()`); otherwise clamp to the primary screen.
- Centralize these in `PetWindow` helpers (`GetCursorInWindowSpace()`, `ClampToVisibleScreen()`) so every other component goes through them.

---

## 5. Build order (milestones)

1. **Window shell + tray/quit** — transparent, borderless, always-on-top, shows the static base sprite; tray icon with **Quit** so the app is controllable from minute one (§4.1, §4.9).
2. **Click-through (verify early)** — passthrough polygon; empty space clicks fall through. Proven on the pinned Godot build before building on top of it (§4.2).
3. **Pupil layer + eye tracking** — pupils follow cursor in NEUTRAL, DPI/multi-monitor aware (§4.3, §4.10).
4. **Drag & drop** — grab and move the window; drag reaction; position persistence.
5. **Random idle state machine** — timed random idle animations, return to NEUTRAL; idle-timeout → SLEEP.
6. **Click reaction.**
7. **Active-app reaction** — config-driven mapping.
8. **Settings window** — flesh out the tray "Settings…" panel over `settings.cfg`.
9. **Edge sitting** (optional, toggle-off-by-default, with anti-cheat note).

Each milestone is independently testable. Milestones 1–6 use **placeholder sprites** (see `assets/sprites/README.md` for how to generate them), so the app can be fully built before Track A renders are final, then real assets dropped in.

---

## 6. Config file (`config/settings.cfg`) — initial keys

```
[idle]
min_interval_seconds = 8
max_interval_seconds = 20
sleep_after_seconds = 120     # inactivity -> SLEEP state (react_sleep loop)

[window]
always_on_top = true
canvas_size = 512
last_pos_x = -1               # -1 = center on first run; updated on move/close
last_pos_y = -1

[features]
edge_sitting_enabled = false  # anti-cheat risk; opt-in only

[app_reactions]
# process_name = reaction_state
Code.exe = react_sleep
chrome.exe = anim_lookaround
```

---

## 7. Notes for personal-use scope
Cinnamoroll is Sanrio IP. This plan is for a **personal, local desktop buddy**. Publishing or distributing the build (e.g. a Steam Workshop upload) would be a different, public-redistribution situation — out of scope here.
