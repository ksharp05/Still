# STILL

A top-down action roguelike where **the world only moves when you do**.

Stand still and time nearly stops — arrows hang in the air, an archer's draw freezes
mid-pull, and you can read the whole room. Move, and it all resumes at once. You are always
running at full speed; everything else multiplies its delta by a world clock driven by your
own movement. That asymmetry is the game.

> Stop to think. Move to commit.

## Opening the project

Unity **6000.3.8f1**, Universal Render Pipeline. Open `Assets/_Project/Scenes/Still.unity`
and press Play.

The scene contains exactly one GameObject with `Bootstrap` on it. Everything else — camera,
lighting, post-processing, HUD, the dungeon, the characters, every material, every texture,
every sound — is built in code at startup. There are no prefabs to wire up and no scene state
to keep in sync.

This repository uses **Git LFS** for binaries. Run `git lfs install` once before cloning, or
`git lfs pull` afterwards, or the `.blend` and `.fbx` files will arrive as text pointers.

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move — and drive the world clock |
| `Shift` | Walk slowly: 30% speed, and the world runs at 30% with you |
| Mouse | Aim |
| Click | Swing. Catches arrows in the arc and returns them |
| `Space` | Dash (invulnerable) |
| `E` | The threshold — spend shards, then descend |
| `Esc` | Pause |

## How it is built

Almost nothing is an asset. `Palette` builds every material at runtime, `ProceduralTexture`
generates the stone from a periodic height field, `Sound` synthesises every effect from maths,
and `DungeonGenerator` / `DungeonBuilder` carve and weld each floor into a handful of merged
meshes. The only imported art is three Blender-authored FBX characters.

```
Assets/_Project/Scripts/
  Core/      Bootstrap, GameManager, WorldTime, TimeDirector, Palette,
             ProceduralTexture, GameVfx, Health, Sound, CameraRig
  Dungeon/   DungeonGenerator, DungeonBuilder, DungeonMap, FloorPopulator, Brazier
  Player/    PlayerController, PlayerCombat, PlayerUpgrades
  Enemies/   EnemyAI, Projectile
  Items/     Pickup, Stairs
  UI/        HUD
```

A few design rules the code holds itself to, documented at their call sites:

- **The player never reads the world clock for their own timing.** Player movement and combat
  use `Time.deltaTime`; everything else uses `WorldTime.DeltaTime`.
- **Melee only.** The player never generates a projectile. Reach against archers comes from
  deflecting *their* arrows back — the ammunition was already in the air.
- **Nothing sits between 0.10 m and 1.20 m.** Enemy line of sight traces at 0.5 m, so scenery
  is either clearly below it (decoration) or clearly above it (real cover). Nothing may look
  like cover without being cover.
- **Only moving things glow.** Scenery smoothness is capped so it cannot bloom its way into
  looking collectable.

## Verification

Two headless play-mode harnesses. Run them from the project root, and **without `-quit`** —
it fires before play mode finishes.

```bash
Unity.exe -batchmode -projectPath . -executeMethod PolishValidation.Run -logFile Logs/validation.log
```

Gameplay, environment and progression — currently 194 assertions to `Logs/polish-validation.txt`,
plus review renders to `Builds/Review/`.

```bash
Unity.exe -batchmode -projectPath . -executeMethod VfxValidation.Run -logFile Logs/vfx.log
```

Particle banks, clocks, pooling and budgets — 25 assertions to `Logs/vfx-checks.txt`.

```bash
Unity.exe -batchmode -buildTarget Win64 -projectPath . -executeMethod BuildReview.Windows -logFile Logs/build.log
```

Windows player to `Builds/Windows/`. Launch it with `-autoshot <dir>` and it screenshots itself
and quits — which is how the shipped build gets verified by rendering rather than only by
starting, since post-processing is disabled in the editor captures for determinism.

## Status

A vertical slice under active development, not a finished game. `POLISH_PLAN.md` holds the
direction; `POLISH_PROGRESS.md` records each pass, what was verified, and — deliberately — what
is still missing. Balance in particular is unvalidated: a green harness says the systems behave,
not that the game is fun.
