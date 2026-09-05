# STILL

A top-down roguelike where **time only moves when you do.**

Stand still and the world freezes — arrows hang in the air, a brute stops mid-lunge, the
colour drains out of the screen. Move, and it all resumes at full speed. Every fight is a
puzzle you solve standing still and then execute in one burst of motion.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move (this is also what advances time) |
| `Mouse` | Aim |
| `Left Click` | Swing |
| `Space` / `Left Shift` | Dash — invulnerable, and forces time to full speed |
| `Enter` or Begin button | Start from the title screen |
| `Escape` | Pause / resume |
| `E` near the green exit | Descend |
| `R` or restart button | Restart after death |

## The one rule that shapes everything

The player runs on `Time.deltaTime`. Everything else runs on `WorldTime.DeltaTime`, which is
`Time.deltaTime * WorldTime.Scale`, and that scale is driven by how much you're moving.

This is why the game doesn't use `Time.timeScale` for the effect — that would slow *you*
down too, and the whole feel depends on being quick in a world that is stuck.

`Time.timeScale` is still used, but only for hit-stop: a few frames where genuinely
everything freezes, which is what makes a sword connect feel solid.

## How it's built

One `Bootstrap` component in an empty scene builds the game at runtime. Character visuals
now load Blender-authored FBX assets from `Resources/Characters`, with procedural shapes
as a fallback. Editable sources and a lineup render live in `ArtSource/Characters` at the
project root. Audio remains synthesized at runtime.

The first polish pass adds title/pause/death screens, settings, deliberate descent,
dash feedback and reliable run resets. See the root `POLISH_PROGRESS.md` for validation
and remaining work. Build a Windows review player using **STILL > Build Windows Review**.

- **Meshes** — dungeon floors and walls are welded into two meshes, so a 70×70 floor is two
  draw calls instead of thousands of objects.
- **Materials** — built in code (`Palette`). Cold, near-monochrome world; hot emissive actors.
- **Audio** — every sound is synthesized from maths at startup (`Sound`). World sounds have
  their pitch scaled by `WorldTime`, so the mix growls when you stop and snaps back when you move.
- **Post-processing** — `TimeVisuals` drives saturation, vignette, chromatic aberration and
  bloom straight off `WorldTime.Flow`. The mechanic is visible without a word of explanation.

## Layout

```
Scripts/
  Core/      WorldTime, TimeDirector, GameManager, Bootstrap, Juice, Sound,
             Palette, Layers, CameraRig, TimeVisuals, ActorFactory, Health, GameEvents
  Dungeon/   DungeonMap, DungeonGenerator, DungeonBuilder, FloorPopulator
  Player/    PlayerController, PlayerCombat
  Enemies/   EnemyAI, Projectile
  Items/     Pickup, Stairs
  UI/        HUD
Editor/
  ProjectSetup.cs   Creates layers, enables legacy input, builds the scene
```

## Things worth tuning first

- `WorldTime.Frozen` (0.035) — how dead "stopped" feels. Try 0.0 for a hard freeze.
- `TimeDirector.spinUp` / `windDown` — how eagerly time responds. The asymmetry is deliberate.
- `TimeVisuals` frozen-vs-moving values — the entire art direction lives in those ten numbers.
- `FloorPopulator.archerChance` — archers are what make the mechanic matter.

## Note on builds

The game runs from the editor with no setup. If you make a standalone build, add
`Sprites/Default` to *Project Settings → Graphics → Always Included Shaders*, since the
trail and particle materials look it up by name at runtime and it can otherwise be stripped.
