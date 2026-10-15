# Visual overhaul: lighting, title cinematic and performance

Update 2026-10-15. Everything runs automatically when `HotelLobby` is played. No scene edits, prefabs or new packages are needed.

## What you see

**First launch (title cinematic).** The game opens from black into slow, letterboxed camera shots of the hotel behind a bouncing *HOTEL TOGETHER* logo:

1. Aerial view over the hotel ("WELCOME TO THE GRAND REOPENING")
2. Dolly across the arrival court ("CHECK IN GUESTS…")
3. Lobby and reception ("RUN THE HOTEL TOGETHER")
4. The same exterior at night with the Zombie Inn grade ("…AND SURVIVE THE NIGHT")

Shots dip to black between cuts and use bokeh depth of field. Your chosen employee stands in the lobby as a full character. Click or press any key to show the employee menu. The cinematic keeps playing behind it with a lighter dim. **LET'S PLAY** swoops the camera into first person. If a wall is in the way, it cuts to just behind your employee first.

**Daytime grade.** The day uses Neutral tonemapping, brighter exposure, higher saturation and contrast, warm highlights, cool shadows, a soft lifted black, bloom with a warm tint and a gentle vignette. The sun is warm, shadows are softer (78% strength) and ambient light is a brighter cartoon sky/ground trilight. SSAO is a little wider for a clay-toy contact shadow.

**Night grade ("Zombie Inn").** At night the grade shifts to violet-blue shadows, stronger saturation, neon-style bloom (threshold 0.72), a darker blue vignette, slight chromatic aberration and fine film grain. It follows the existing dusk/dawn transition of `HotelGuestSimulation`.

**Floating dust motes** drift around the camera and catch the light. They are warm by day, cool and sparse at night, and turned off on Low.

## Performance

| Tier | Render scale (auto-res floor) | MSAA / AA | Shadows | Shadowed lamps | SSAO | Floor reflection |
|---|---|---|---|---|---|---|
| LOW | 80% (60%) | off / FXAA | 22 m, 2 cascades, 1024 | 0 | off | off |
| MEDIUM | 90% (70%) | off / SMAA med | 32 m, 2 cascades, 2048 | 1 | on | 35%, every 2nd frame |
| HIGH | 100% (80%) | 2x / SMAA high | 45 m, 4 cascades, 2048 | 3 | on | 50%, every frame |
| ULTRA | 100% (90%) | 4x / SMAA high | 60 m, 4 cascades, 4096 | 6 | on | 65%, every frame |

- **First-launch tier** comes from GPU memory (≥10 GB Ultra, ≥5 GB High, ≥2.5 GB Medium, otherwise Low). Change it with **Settings → GRAPHICS** (saved).
- **AUTO RES** (on by default) uses the GPU frame time and lowers render scale in 5% steps when the game drops below 60 fps. When there is headroom again it raises the scale back to the tier maximum. FSR 1 upscaling keeps edges sharp.
- **FPS** shows a small FPS / ms / render-scale readout at the top of the screen.
- **Floor reflection** (`HotelGraphicsQuality`): it used to re-render the whole scene with shadows and a 875 m far plane. It now skips shadows, uses a 45 m far plane, uses the tier's resolution and can update every 2nd or 3rd frame.
- **Shadow budget:** only the nearest N shadow-casting point/spot lights keep real-time shadows. They are re-ranked twice a second.
- **Tiny props** (cups, cutlery, labels) stop casting shadows on Low/Medium/High. Characters are never affected.
- **Editor defaults:** `HotelGraphicsSetup` no longer forces 4x MSAA and 4096 shadows every time the scene opens. It sets High-tier defaults and stores FSR on the pipeline asset so builds keep the FSR shader variants.
- **Play mode** never leaves the pipeline asset modified. Every runtime change is restored when Play mode exits.
- **Project settings:** frame timing stats are on (needed for GPU-time measurement). Builds launch borderless full screen at native resolution and the window is resizable.

## Files

New, in `Assets/HotelTogether/Scripts`:
- `HotelVisualDirector.cs`: day/night/cinematic volume profiles, ambient and sun tint, dust motes.
- `HotelPerformanceDirector.cs`: quality tiers, adaptive resolution, shadow-light budget, small-shadow pruning.
- `HotelTitleCinematic.cs`: attract-mode shots, collision-safe camera and the swoop into first person.
- `HotelPlayerTitle.cs` (`HotelPlayer` partial): title logo, captions, letterbox/fade overlay and FPS readout.

Changed:
- `HotelPlayer.cs`: `OnGUI` draws the cinematic overlay and title, and the menu dim is lighter while the cinematic plays.
- `HotelPlayerMenu.cs`: title input, plus a graphics row in Settings (GRAPHICS / AUTO RES / FPS).
- `HotelGuestSimulation.cs`: exposes `NightBlend`.
- `HotelGraphicsQuality.cs`: cheaper reflections and `ApplyTier`.
- `Editor/HotelGraphicsSetup.cs`: new default grade and pipeline defaults.
- `ProjectSettings/ProjectSettings.asset`: frame timing stats, borderless full screen, 1920×1080 default.

## Tuning

- **Camera shots:** select the player in Play mode, find **Hotel Title Cinematic** and edit its `shots` list (from/to camera positions, look targets, FOV, seconds, night 0–1). Default shots are built from `arrivalPoint` and `receptionPoint`. A shot whose camera would be pushed more than 45% toward its subject by walls is skipped automatically.
- **Colours:** edit the values in `HotelVisualDirector.BuildProfiles()` and the day/night colour fields.
- **Tier contents:** edit the `tiers` array in `HotelPerformanceDirector`.

## Validation done

- Every runtime script was compiled with Roslyn (C# 9) against the Unity 6000.6.5f1 engine DLLs and the project's URP 17.6 / Input System assemblies. The editor setup script was compiled separately with `UNITY_EDITOR`. There were no errors.
- Not yet verified in the Unity Editor: shot framing in the actual level, the final colour balance on your display and frame-rate numbers on your machine.
