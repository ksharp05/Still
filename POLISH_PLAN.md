# STILL — integrated game polish plan

Status: implementation started on 2026-09-05. See `POLISH_PROGRESS.md` for the first playable pass and validation results. Manual feel/balance playtesting remains necessary; automated checks do not establish final quality.

## Product direction

Make STILL a readable, atmospheric action roguelike about planning in slowed time and committing to dangerous bursts of movement. Preserve the existing melee-first identity and responsive player movement. Visuals, enemy behavior, audio, and interface should all help the player answer: what is about to happen, what can I do, and why should I explore further?

Proposed first milestone: a complete three-floor run with an introduction, meaningful build choices, escalating encounters, a final encounter, and a satisfying victory or death summary. Treat three floors as a vertical slice to validate pacing, not a commitment to the final game's length. Keep the existing endless descent available as a later mode.

Working art direction: dark architectural ruins with restrained cyan player accents, distinct hostile silhouettes, and warm reward lighting. Evolve the current neon foundation into a coherent place. Avoid filling combat space with decorative glow or detail that hides threats.

## What already exists

| Area | Source-backed baseline | Next need |
| --- | --- | --- |
| Time | Movement-driven world clock; aim influence; attack/dash impulses; hit-stop | Communicate the actual rule and make pauses/state transitions reliable |
| Player | Accelerated movement, mouse aim, arc attack, invulnerable dash | Reliable resets, cooldown feedback, clear hit range and damage response |
| Enemies | Melee and ranged state machines, windups, recovery, stagger | More readable committed attacks and authored encounter combinations |
| Dungeon | Seeded connected rooms, corridors, start and exit rooms | Room roles, landmarks, encounter pacing, spawn separation |
| Run | Health/shards carry between floors; death and restart | Menus, pause, settings, upgrades, completion, persisted records |
| Presentation | Procedural meshes/materials/audio, particles, post effects, camera kick | Consistent shape language, environment identity, audio hierarchy and accessibility |
| Interface | Health pips, floor/shards, time meter, short control hint, death overlay | Onboarding, interaction prompts, build choices and complete run summaries |

## Issues to verify before expanding content

These are source-review findings, not reproduced runtime bugs.

- `PlayerCombat` has no disable/reset handler. Deactivating the player during a swing stops its coroutine without clearing `IsSwinging`; restarting reuses that player. Add explicit combat reset and verify death during every swing phase.
- `PlayerController` retains dash timers, momentum and trails across reuse and teleport. Define a reset path for restart and descent, including invulnerability and camera state.
- `GameManager` rebuilds floors with deferred destruction. Deactivate the outgoing floor before generating its replacement so old colliders and callbacks cannot affect the new floor during that frame.
- Player melee does not check wall occlusion. Enemy melee checks sight when entering windup but does not recheck it when damage lands. Verify both with corners and doorways.
- Health has dash immunity but no post-hit grace period. Evaluate overlapping damage before selecting a short player-only grace period; enemies should still take intentional multi-hit damage.
- Pickups have no consumed guard, and hearts are consumed at full health. Make collection idempotent and preserve unneeded healing.
- Stairs descend immediately on contact. Replace this with a nearby interaction prompt and deliberate confirmation so exploration remains a choice.
- Best depth is session-only; shards currently provide score without build decisions. Establish persistent records and an explicit reward economy.
- Random spawn placement does not reserve occupied tiles. Separate enemies, rewards, and the exit and validate safe entry space.
- The tagline says time only moves with movement, but aim and attacks also advance it and the frozen scale is nonzero. Teach the actual behavior: stopping slows the world; acting advances it. Decide whether a true freeze improves play through testing.

## Implementation sequence

Each phase includes game flow, combat, and presentation. Complete and evaluate a playable result before adding the next layer.

### 1. Establish a dependable, presentable run

- Capture a baseline playthrough and profile representative floors before changing balance.
- Introduce explicit title, playing, paused, transitioning, dead and victory states. Centralize clock/input ownership; hit-stop must never dismiss a menu pause.
- Add title, pause/resume, controls, audio levels, restart flow, and saved best depth/score. Pause on focus loss; require deliberate resume.
- Repair reset, pickup and wall-hit issues above. Prevent the click that dismisses a screen from also attacking or immediately restarting after death.
- Redesign the HUD around health, dash readiness, time state and current objective. Establish reusable type, spacing, color and panel styles.
- Add damage direction feedback, clear dash readiness, deliberate exit interaction and a short floor transition.
- Expose screen shake and flash intensity controls; retain readable threats with effects reduced.

Done when: launch → begin → fight → pause → resume → descend → die → restart works repeatedly, including death mid-swing and mid-dash. No input leaks, stale trails, retained immunity, or unintentional descent. Settings and records survive relaunch.

### 2. Build one representative combat space

- Create one benchmark room containing a melee enemy, an archer, cover, a reward and an exit before redesigning the whole dungeon.
- Give enemies distinct silhouettes and ground telegraphs matching their actual damage shape. Lock attack direction at a deliberate point so repositioning is understandable and fair.
- Refine attack anticipation, connection and recovery; make sound and animation reflect those phases. Use hit confirmation sparingly and consistently.
- Replace bare room presentation with a small reusable environment kit: floor/wall treatment, door frames, columns, edge details, restrained props and one landmark.
- Test camera framing and foreground wall obstruction. Threats must remain visible through slow time, bloom, damage effects and wide/tall aspect ratios.
- Add a visually distinct heavy enemy with a committed, avoidable attack after the two existing enemies read clearly.

Done when: players can identify enemy roles and predict attacks without relying on color alone; visible telegraphs match hits; the room stays legible while several threats overlap. Validate before multiplying content.

### 3. Make exploration and rewards meaningful

- Move tunable enemy, encounter and upgrade definitions into data assets while preserving runtime construction as a fallback.
- Assign room roles: safe entry, teaching fight, mixed encounter, optional risk/reward, recovery, exit. Use encounter budgets rather than only increasing count and health with depth.
- Add a small pool of mutually interesting run upgrades, offered between floors. Candidate choices: wider melee coverage, a dash-related offensive effect, or a defensive recovery option. Tune concrete values through playtests.
- Recommended initial economy: offer a free build choice on descent and let shards buy an optional heal or upgrade reroll. Track lifetime collected separately from spendable shards so spending does not erase score.
- Show costs, effects and current build clearly. Ensure unaffordable actions do nothing and duplicate acquisition cannot occur.
- Add exploration wayfinding with discovered-room mapping and distinctive exits/landmarks. Do not expose the entire layout before exploration.
- Connect each reward to a visible or audible result so build changes are felt during play.

Done when: players can explain a build choice, exploration offers a meaningful tradeoff, and both early exits and optional fights remain viable. Validate multiple seeds for reachable exits, safe starts and non-overlapping spawns.

### 4. Deliver a complete vertical slice

- Compose three floors with different encounter rhythms and escalating architecture/lighting while reusing the proven kit.
- Add a final encounter testing movement, aim commitment and dash timing with mechanics already taught earlier. Avoid a boss that is merely an inflated health pool.
- Add victory, run duration, collected shards, deepest floor and build recap; provide clear replay and title actions.
- Layer ambient audio and encounter intensity around world time. Keep UI audio independent of the world clock and respect volume settings.
- Tune floor length, healing, upgrade power and difficulty using recorded runs. Proposed pacing target: a 10–15 minute successful slice, subject to testing.

Done when: a new player can learn, make a build, reach a recognizable conclusion and immediately understand why to replay.

### 5. Harden and package

- Profile CPU/GPU, floor generation and allocations in a standalone build. Pool projectiles/effects only where measurement justifies it; review per-pickup light costs.
- Verify runtime-created materials survive shader stripping. Validate the playable scene and build settings.
- Check saved-data defaults, settings bounds, resolution changes, UI navigation and repeat launches.
- Run combat edge-case, seeded-generation and state-transition checks; perform visual QA in the actual player at 1080p, 1440p and ultrawide, with reduced effects.
- Produce a versioned desktop build and a concise controls/readme file. Final platform and hardware performance targets still need selection; desktop keyboard/mouse is the current implementation baseline.

Done when: the packaged build runs without missing materials or exceptions, meets a defined target on the selected hardware, and passes the complete run flow outside the editor.

## Technical boundaries

- Keep `WorldTime` as the world simulation clock and normal scaled delta for the player. Explicit game state controls deliberate pauses; hit-stop is a temporary effect subordinate to that state.
- Extend `GameManager` for run orchestration, but keep settings/persistence, upgrade definitions and UI presentation in separate components.
- Preserve the seeded dungeon pipeline. Add placement reservations and encounter metadata before replacing generation algorithms.
- Reuse `GameEvents` for presentation notifications, while authoritative run state stays in gameplay systems.
- Keep HUD construction modular as screens grow; choose authored prefabs or UI Toolkit only if they materially improve iteration. No framework rewrite is required to start.
- Preserve the melee core. Additional weapons, permanent stat grinds, multiple biomes and a large enemy roster are outside the first slice.

## Validation and decisions

Prioritize a playable review after phases 1 and 2. Those reviews determine whether to adjust time behavior, visual density or combat pacing before investing in progression and a boss.

Automate the consequential invariants: repeated restart resets combat/movement, pause wins over hit-stop, one pickup grants one reward, walls block damage, and generated layouts keep starts/exits reachable. Use human playtests for feel, readability and reward appeal rather than treating passing tests as proof of fun.

Working assumptions requiring future selection: final target platform/hardware, acceptable use of external art/audio, final run length, and whether endless mode is part of the first public build. These do not block the first reliability and benchmark-room passes.

Current work: phase 1 is partially implemented and the Blender character pipeline is working. Next: manual review of the first pass, complete remaining phase-1 feedback and transitions, then build the representative combat room in phase 2.
