# First playable polish pass — 2026-09-05

## Art direction reset

2026-09-06: The user relaxed the exact-replica requirement to "the best that we can do".
Current deliverable: `ArtSource/Characters/HeroSculpt/HeroRefined.blend`, built from the
existing study using `Tools/refine_hero.py`. Changes focus on coherent construction:
one connected jacket, continuous collar and boots, shirt/scarf connection, cloth
shading, localized facial edits, softer beard edges and projected cape embroidery.
`refined-full.png`, `refined-portrait.png`, `refined-back.png` are actual Cycles renders.
This remains an unapproved stylized study; production rigging, texture baking and
runtime optimization are not complete. No game assets were overwritten.

Latest user requirement: an **exact replica of the concept-reference.png character**.
The full HeroTailored study is also rejected as nowhere close. Do not reinterpret the
design or describe this study as an approved basis. Preserve the reference's face,
hair, proportions, coat silhouette, fabric layering, ornate metalwork and material
finish. The available scripted Blender workflow has not demonstrated the ability to
meet that fidelity. No image-to-3D tool is currently connected; such a tool would only
be a candidate starting point, not a guarantee of an exact reconstruction.

Continued at the user's request with an emphasis on reusing internet resources and
reducing trial-and-error. `ArtSource/Characters/HeroSculpt/HeroTailored.blend` now contains
a full clothed character study built on the official CC0 realistic male body: long coat,
scarf, shoulder cape, strand hair, clockwork accessories and sword. Render review led to
corrections for hair/scalp fit, collar/shoulder coverage, garment overlap and boots.
This remains an unapproved design study, not a finished production character. The face,
costume construction and material finish still fall short of the concept reference.
No Unity player asset was replaced by this pass.

Latest feedback: the user also rejected the LastSecond study as nowhere close. Stop
iterating those procedural character forms. `ArtSource/Characters/HeroSculpt` now holds
an explicitly attributed anatomical head foundation from Blender's official CC0 library.
It is imported source anatomy, not an original completed hero. The agent has acknowledged
that the current procedural workflow has not delivered the requested concept-level quality.
Do not claim that asset preparation or additional polygons close that gap.

The user rejected both previous character passes as insufficient for a lead character.
Do not treat the Keeper as the approved visual direction. The current exploration is
`ArtSource/Characters/LastSecond`: a visible-faced duelist with tailored clothing,
clockwork gauntlet and signature blade. This folder contains both an explicitly labeled
generated concept reference and an actual Blender design study. The Blender model has
not reached the reference's fidelity and is not a finished or game-ready replacement.
Resolve the hero's quality and identity before deriving enemies and environments from it.

## Enemies — readable attack telegraphs

The last piece of the benchmark-combat-room milestone. Enemies already had a Windup state that
swelled their emission and scale, which told you *who* was attacking and roughly *when*. It never
told you **where** — and in a game whose whole premise is stopping to read a frozen room, where is
the question that matters. A brute threatened a three-metre wedge you could not see; an archer
threatened a line across the room that did not exist until the arrow did.

`EnemyTelegraph` now paints the threatened ground during a windup: a wedge for a swing, a narrow
lane for a shot, each fading in from a hint to a clear warning as the blow charges.

- **It draws what actually hurts.** The melee strike damages at `attackRange + 0.6` while the
  slash visual was drawn at `attackRange` — so the visual already understated the real reach by
  60cm. That overreach is now a named constant, `StrikeOverreach`, shared by the damage check,
  the slash and the telegraph, so the three cannot drift. A telegraph that understates its reach
  kills the player in a spot the game drew as safe.
- **It stops at walls.** Every spoke raycasts, so a wedge is cut short by cover and an archer's
  lane ends at the stone. The review capture shows this better than it was staged: the melee
  wedge is severed in two by a wall while the archer's lane threads through the doorway between
  them.
- **It is painted ground, nothing more.** No collider, no physics, no decisions. It is parented
  to the VFX root rather than to the enemy and fed world-space vertices, because the enemy's own
  transform is both rotated by facing and scaled by the windup pulse — either would warp geometry
  parented under it.
- **It freezes with the world.** The windup ticks on `WorldTime.DeltaTime`, so standing still
  leaves the warning hanging in the air at whatever charge it had reached, which is exactly the
  thing the player stopped in order to read.

Nine assertions cover the honesty properties specifically: the wedge reaches the radius it was
given, never claims more ground than the attack covers, is clipped by a wall rather than reaching
through it, builds from faint to clear rather than appearing at full strength, carries no
collider, and clears when the attack resolves. `Builds/Review/telegraph.png` renders both kinds.

Validation now stands at 203 gameplay checks and 25 VFX checks, all passing.

**Not done.** Alpha, colour and the archer lane's 14m length are first guesses that want playing —
a room with four archers may read as clutter, and the honest fix then is fewer archers or shorter
lanes, not a dimmer warning. The milestone's remaining question is unchanged: validate a benchmark
combat room before expanding content across the dungeon.

## Particle effects — finishing the incoming VFX work

A particle system arrived from work done elsewhere: `GameVfx` (shared bounded banks, a pooled
slash-ribbon mesh, three separate clocks), `VfxEmitter`, `VfxTuning`, an authoring menu and a
dedicated `VfxValidation` harness. The design is sound and the call sites were already wired —
24 of them across combat, dashing, pickups, the stairs, braziers, projectiles and death. What it
was not was finished.

**The project did not compile.** `VfxAuthoring.Demo()` referenced a `VfxShowcase` type that was
never written. Added it: a runtime component that plays every effect in turn around the player,
one every 1.15s on unscaled time, then removes itself so the menu item can just be run again. It
lives in the runtime assembly because an editor script can reference a runtime type but not the
reverse, and it warns if you run it outside a live run, where every clock but Death is stopped and
the demo would emit correctly while appearing to do nothing.

**The tuning asset did not exist.** `VfxValidation` asserts it does, and `GameVfx` silently falls
back to a throwaway instance without it — so the shipped game had working defaults that nothing
could edit. Created at `Assets/_Project/Resources/VfxTuning.asset`.

**A defect in the incoming harness.** `VfxValidation` case 8 asserted before its `CanRestart`
retry guard, so the assertion re-ran on every retry: 25 real checks produced a 166-line report.
Guard hoisted above the assertion, matching how `PolishValidation` case 4 already does it.

**Nothing rendered a pixel.** The incoming harness proves banks, clocks, pooling and budgets
behave, but never looks at the screen — an effect that emits correctly and draws nothing would
pass every check. Two additions close that:

- `CheckSlashRenders` asserts a slash leaves an *enabled* renderer with real extent, the right
  material, and non-zero vertex alpha. A count of active arcs stays true for an arc that is
  invisible; these are the three things that make it actually visible.
- A staged `vfx` capture. Getting it right needed two timing facts: a particle emitted this frame
  has not been simulated and renders as nothing, so emission and capture must be a step apart;
  and the world clock runs at 0.035 while the player stands still, so world-clock effects hang in
  the air and are the right ones to photograph.

**The editor capture cannot show this system properly**, and that is worth recording rather than
working around. `Capture` disables post-processing for determinism, and the effects are authored
against bloom — a slash peaks at 0.10 vertex alpha, which is close to invisible unbloomed. So
`AutoCapture` gained an effects shot, and `Builds/Review/player-vfx.png` is a frame from the real
player showing the slash ribbon, the deflect ring, the death glow and the muzzle and shard points
as they actually look.

Validation now stands at 194 gameplay checks (`Logs/polish-validation.txt`) and 25 VFX checks
(`Logs/vfx-checks.txt`), all passing, with the Windows build rebuilt and verified by rendering.

Two notes for later. `Palette.Particle()` is now dead — `GameVfx` owns particle materials and
nothing calls it. And the gameplay check count varies between 194 and 196 by design: two pillar
assertions are conditional on the randomly-seeded floor actually containing a colonnade.

## Combat — shards, upgrades, and a walk

Three gaps closed. All three were things the game said about itself but could not do.

### Moving calmly is now expressible

`PlayerController.MoveIntent01` is the magnitude of the input vector, and `TimeDirector` turns it
into world speed through `Lerp(Frozen, 1, intent)` — a continuum. But keyboard input came from
`GetAxisRaw`, which is strictly -1/0/+1, so the keyboard could only ever hand it the endpoints.
The game is named for walking calmly through frozen arrows and the keyboard could not walk.

Holding Shift now scales the input vector by `walkFactor` (0.30). That is the whole change,
because `UpdateWalk` targets `input * moveSpeed` — one multiply slows the player and the world in
lockstep. Walking moves at 2.7 m/s with the world at 32%, so arrows crawl while you cross a room.

Shift used to be an undocumented alias for dash; it has been removed from dash, which stays on
Space, or holding Shift to walk would have fired a dash every time. Worth noting a gamepad already
produced fractional input, so analog walk always worked on a stick — this only brings the keyboard
to parity.

### Shards buy something

Choosing the stairs now opens **the threshold**, a new `RunState.Outfitting`: spend what you found,
then ENTER to descend or ESC to go back and find more. `SetState` already freezes the world for any
non-Playing state and `CanPlay` already excludes it, so movement, pickups and the stairs prompt
fall silent with no extra work.

Four lines, bought with keys 1-4, at rising cost: EDGE (+1 swing damage, x3), REACH (+0.45 m swing
and deflect, x3), VITALITY (+1 max health, x4), MOMENTUM (-0.04 s dash cooldown, x3). Neither swing
duration nor dash duration is purchasable on purpose — both feed `TimeDirector.Impulse`, so
shortening either would quietly change how long the world runs at full speed after every attack: an
edit to the core mechanic disguised as a stat.

Three ceilings come straight from the code and the caps respect all of them. The HUD allocates
exactly twelve health pips, so VITALITY stops at +4. `DashReady01` divides by the dash cooldown, so
MOMENTUM can never approach zero without putting NaN into a HUD localScale. And the pause card is
shown for every non-Playing state, so it had to be told explicitly not to stack behind the
threshold.

### The player can get stronger

Enemy health rises about 11% per floor while every player stat was a compile-time constant with no
runtime setter anywhere in the project. `PlayerUpgrades` is now the only thing that writes them.

Stats are **derived, never incremented**: `Apply()` recomputes each affected value from the
components' authored defaults plus current levels, so a reset is just "levels to zero, recompute".
That is not stylistic. The player GameObject is created once and never destroyed — it survives
every descent *and* every restart — so a stat nudged in place would persist into the next run with
nothing anywhere to undo it. `Health.SetMax` was added alongside, because the existing `Configure`
sets current to full, which would make every +1 max health also a free full heal.

### Validation

188 checks, all passing. The one that earns its keep is the reset: buy every line to its cap,
restart, and assert swing damage, range, dash cooldown and max health are all back at their
authored values. Also covered: purchases refused without shards change no stat; a purchase deducts
exactly its cost; an upgraded swing takes measurably more health off a real enemy rather than only
changing a field; the threshold opens without descending, backs out without descending, and
descends exactly one floor on confirm; and the pause card does not stack behind it.
`Builds/Review/threshold.png` renders the screen.

Two ordering notes for whoever edits the harness next. `CheckThreshold` and `CheckUpgrades` run
last because they descend and restart, which rebuilds the floor and invalidates anything that
inspected the previous one — an earlier ordering tripped the brazier collider assertion, because
`Brazier.Spawn` uses deferred `Destroy` and the collider still exists on the frame the floor is
built. And a `Capture` of the threshold needs `HUD.Update` pumped by reflection first, since the
card is toggled there and assertions run inside a single frame.

### Not done

The harness cannot press keys, so the walk key and the 1-4 purchase keys are verified only at the
level of the maths and the state machine beneath them; the actual bindings want playing. And the
whole of the upgrade set is a balance change a green harness cannot evaluate — four lines against
enemy scaling, with caps and costs that are first guesses.

## Combat — arrow deflection

The player was never short of a way to fight back: a 3.1 m / 120 degree swing doing 2 damage
with knockback and hit-stop, plus a dash with i-frames. What was missing was any way to engage
with the thing the game is actually about. `PlayerCombat.cs` states the fantasy as "walking
calmly through a hail of frozen arrows to reach the archer that fired them", and arrows were
pure hazard: `Projectile` destroyed its own collider and masked against Wall|Player only, so
nothing the player owned could touch one. A room full of suspended arrows could only be routed
around, and while the player stood still those arrows persisted for minutes.

A swing now catches arrows in its arc and returns them.

- A returned arrow aims at the nearest living enemy it has line of sight to, falling back to a
  straight reversal. Aiming matters: an arrow that pinged off decoratively and hit nothing
  would teach the player that deflection does not work.
- It changes sides — its cast mask flips from Wall|Player to Wall|Enemy, so it can never turn
  back on the player — carries 3 damage against enemy health of 2 to 7, flies 1.35x faster, and
  repaints itself in the blade colour so a returned arrow is unmistakably the player's.
- The catch window is deliberately more forgiving than the damage cone (3.9 m, 200 degrees):
  an arrow is a 12 cm object crossing the screen, and a deflection that needed a frame-perfect
  read would be a trick shot rather than the defensive answer this is meant to be. **This is
  the main tuning knob if it proves too strong.**
- Walls block a parry exactly as they block melee.

This is deliberately not a gun. The player never generates a projectile — they return one that
was already in the air, fired by the archer it goes back to. It is also self-costing: a swing
calls `TimeDirector.Impulse`, so returning one arrow starts every other arrow in the room moving
again. That price was already in the code and this is the first mechanic that makes the player
pay it knowingly.

Arrows carry no collider, so rather than give them one — which would put them on a physics layer
and risk disturbing the line-of-sight and cover rules the whole environment depends on — they
publish themselves to a static `Projectile.Active` registry that the sword reads.

Validation is now 148 checks. The deflection ones cover the silent failures specifically: a
returned arrow travels away from the player rather than into them, an already-returned arrow is
never deflected twice (which would be an infinite volley), a wall blocks the parry, out-of-reach
arrows are left alone, and the registry drains when arrows are destroyed. `Builds/Review/
deflection.png` renders a staged volley being returned.

While adding this, a pre-existing assertion of mine turned out to be wrong rather than the code:
"Paving UVs are world-scaled" compared the UV range against the mesh's x extent, but the floor
mesh also carries debris box side faces that project z into u, so it only passed on floors that
happened to be wider than deep — and the run seed is random. It now proves the property directly:
every upward-facing vertex must satisfy uv == (position.x, position.z).

Not addressed, and still the largest open questions about combat: the player has no way to spend
shards (they are incremented and displayed and nothing else), no stat changes at all across a
run while enemy health more than doubles by floor 12, and `MoveIntent01` is binary because it
comes from `GetAxisRaw` — so "moving calmly", the verb the fantasy is named for, is not
expressible from the keyboard. Deflection also wants playtesting: it is a real buff against the
archer-heavy deep floors that a green harness cannot evaluate.

## Game area — dressing pass

The architecture pass left the area reading as built space but undressed: no threshold where
a corridor met a room, no props, flat untextured colour everywhere, and a packaged build that
predated all of it. That is now closed out.

**One rule governs every prop.** `EnemyAI.HasLineOfSight` traces at y = 0.5 against the Wall
layer, so nothing may sit between 0.10 m and 1.20 m. Below that it is decoration in the floor
mesh on the Default layer, under both character controllers' step offsets. Above it, it is a
real obstacle in the wall mesh on the Wall layer, blocking movement, sight, melee and arrows
alike. Nothing occupies the band in between, because that is exactly where scenery starts
looking like cover without being cover.

- **Doorways.** `DungeonGenerator.FindDoorways` scans the ring of cells one step outside each
  room rect; every maximal run of open cells is one opening. Taking maximal runs is what makes
  two corridors arriving side by side read as a single wide portal rather than two lintels a
  tile apart. A cell only counts if the passage continues outward as well as inward, so a
  corridor dead-ending on a wall is never dressed as a portal to solid rock. Openings are
  framed only when both ends land on rock and the width is 2–5 tiles; a corner entry or a
  corridor running parallel to the wall gets a threshold band and nothing else.
- **Framing** is a bright threshold band (free — a doorway tile was already being drawn as
  dark trim), full-height jambs in the wall mesh, and a lintel plus jamb caps in the
  collider-less cornice mesh. The narrowest framed opening leaves 2.84 m walkable against a
  widest actor of 0.90 m. Framing writes no tile data at all, so reachability is unchanged by
  construction.
- **Props.** Fallen masonry at 1.35 m is written as `Tile.Pillar`, so it is honest cover and
  the existing flood fill covers it for free; floor debris at 82 mm is merged into the paving;
  emissive sconce bowls sit in wall faces looking onto rooms; and up to six braziers bracket
  framed doorways, exit room first. The brazier is the only piece with its own GameObject,
  because it needs an `Update` and a real light — it flickers on `WorldTime` and cools as time
  freezes, the opposite choice from `Stairs`, which uses real time on purpose.
- **Surfacing.** `ProceduralTexture` generates two stone families in code — no image files —
  each an albedo and a normal map cut from one periodic height field, about 390 ms once at
  boot. The normal map is the load-bearing one: albedo mottling is crushed by ACES, contrast 6
  and saturation −68, whereas normals modulate the response to light and survive. Smoothness
  rides in the albedo's alpha, capped at 1, so it can only dull a highlight — scenery cannot
  bloom its way into looking collectable. `MeshBuilder` now projects UVs from world position
  in metres and writes tangents; the previous per-quad 0–1 UVs tiled once per 1.6 m tile and
  stretched wall faces 2:1.

Two real bugs were caught by the harness rather than by eye: fallen masonry could land on the
exit room's centre tile and bury the staircase, which now has a reserved-tile guard shared with
pillar placement; and the sconce emission sat above the 0.75 bloom threshold, blowing out to
white slabs that read as pickups.

Validation is `Logs/polish-validation.txt`, 138 checks, all passing — including a capsule sweep
of the widest actor through every framed doorway on the live floor, a line trace across every
masonry block at exactly the height enemies use, an assertion that nothing in the paving mesh
rises into the line-of-sight band, and guards for the silent failures in the texturing path
(`_NORMALMAP` present wherever a bump map is, albedo sRGB against normal linear, exact tiling,
tangents on every mesh, world-scaled UVs).

The Windows build is current and was verified by rendering, not just by starting. Passing
`-autoshot <dir>` makes the player screenshot itself and quit (`AutoCapture`), which is how
`Builds/Review/player-*.png` were produced. That check existed to answer a specific risk: URP
declares `_EMISSION` and `_NORMALMAP` as `shader_feature`, Unity selects those variants by
scanning material assets, and this project deliberately has none — so the keyword-on variants
could have been stripped, silently, in a way no editor test could reproduce. They are not.

Still open on the game area: floor debris uses axis-aligned boxes, so it reads as a scatter of
little rectangles rather than broken stone; there is one texture scale per surface family, with
no large-scale variation between rooms; and the generated normals also feed SSAO, since it
reads DepthNormals, so bump strength effectively applies twice. Fallen masonry is also a
balance change, not only dressing — it hands the player cover against archers, which is what
the time mechanic is about, and it wants playtesting rather than only a green harness.

## Game area — architecture pass

The dungeon was previously flat untextured quads: one floor material, one wall material,
no way to tell a room from a corridor. It now builds as authored space.

- Layout data carries a per-tile region tag (room index, corridor, or solid), so the
  builder can treat chambers and passages differently. `Tile.Pillar` is a third tile type:
  solid and collidable, but with ground under it.
- Paving splits into three submeshes — corridor stone, warmer room paving, and a border
  trim that rings every walkable edge. Still one renderer.
- A cornice band runs along the top of every exposed wall face (soffit, outward face,
  top ledge). It is a separate collider-less object on purpose: it overhangs the play
  space, so nothing should ever be able to collide with it.
- Rooms of 10x10 or larger get a four-column colonnade inset from the corners, built as
  plinth/shaft/capital with an emissive sconce bowl. Columns sit on the Wall layer, so
  they are real cover — archers lose line of sight behind them. A column is only placed on
  a tile that is floor on all four sides, which is what stops one sealing a doorway.
- Room lights are tinted by role. The exit burns the stairs' green and throws further than
  an ordinary room light; that spill down a corridor is the floor's only wayfinding.
- `GameManager.CurrentMap` exposes the live layout.

Validation (`Logs/polish-validation.txt`, 43 checks, all passing) adds: the kit builds
every part, paving keeps its three materials, the cornice has a renderer and no collider,
walls and pillars stay on the Wall layer, and a flood fill across twelve generated floors
confirms the stairs remain reachable from the spawn with colonnades placed.

Review renders in `Builds/Review`; `colonnade.png` is new and is the only one that shows a
pillared room, since the spawn room never receives columns. These are camera-rendered with
post-processing disabled for deterministic capture, not final player framebuffers.

Not done in this pass, and still open for the game area: no doorway framing where corridors
meet rooms, no floor detail beyond the flat trim ring, no props or rubble, and the wall and
floor materials remain untextured flat colour. The environment reads as architecture now,
but it is not dressed. The packaged Windows build predates this pass.

## Character refinement

The player is now The Keeper: a lean armored wanderer with layered ivory armor, recessed
cyan visor, asymmetrical shoulders and a folded split cloak. Source:
`ArtSource/Characters/Keeper/Keeper.blend`; renders in the same folder. The runtime
`Wanderer.fbx` was replaced while preserving its GUID. It contains 5,596 triangles,
four skinned meshes and a 16-bone pose rig. No locomotion/combat animation clips yet.
Unity verification passed all 21 checks, including skin import and forward orientation
(`Logs/keeper-validation.log`). The previously packaged Windows build predates this
character refinement; the Unity project uses the new model.

## Initial pass

Implemented:

- Explicit title, playing, paused and dead states; pause on focus loss; deliberate keyboard/button start and restart.
- Menus with shared typography and colors, controls, saved master volume, reduced-effects option, persisted best depth and run summary.
- Health, dash-readiness and world-time readouts, onboarding hint and nearby exit interaction prompt.
- Deliberate E-to-descend interaction; outgoing floors deactivate before destruction.
- Reset of combat coroutine flags, dash timers, momentum, invulnerability and trails on reuse/teleport.
- Wall occlusion for player and enemy melee, one damage application per target per swing, duplicate pickup protection and preservation of hearts at full health.
- Blender-authored player, melee and archer models; four material renderers per character, shared runtime URP materials and basic movement bob. (Note: the project imports three FBX characters from `Assets/_Project/Resources/Characters/`. It still has no material, shader or texture assets — those are all generated in code.)
- Brighter environment palette/lighting and less aggressive frozen-time screen effects.

Validation:

- Unity 6000.3.8f1 batch play-mode harness: 19 checks passed, including actual imported FBX resources, title/pause state, pending hit-stop during pause, death during a swing, restart, descent, wall-blocked melee, multi-collider damage and pickup behavior.
- Full report: `Logs/polish-validation.txt`.
- UI/layout review renders: `Builds/Review`. These use camera-rendered UI with post-processing disabled for deterministic capture; they are not exact final player framebuffer captures.
- Blender lineup rendered and inspected: `ArtSource/Characters/lineup.png`.
- Windows review build entry point: `STILL > Build Windows Review`; build outcome is recorded in `Logs/windows-build.txt`.
- Windows review build succeeded with zero errors (about 158 MiB). Launch `Builds/Windows/STILL.exe`; keep its adjacent data/runtime folders together.
- Standalone headless startup completed without logged exceptions with normal local preference access (`Logs/windows-player-normal.log`). This checks startup, not visual rendering or manual controls in the standalone player.

The first phase remains in progress. This is not a finished release. Remaining work includes manual input/focus testing in the player, damage direction feedback, smoother floor transitions, better settings controls and persisted score. Subsequent phases still include authored combat spaces, animated characters, environment detail, upgrades, encounter progression, a final encounter and victory, and performance/accessibility review.

Both halves of that milestone are now in: the architectural kit with its dressing, and readable
attack telegraphs on the enemies. What remains is to validate a benchmark combat room by playing
it — the automated checks establish that the systems behave, not that the encounter is good.
