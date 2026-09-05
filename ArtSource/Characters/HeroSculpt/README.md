# Anatomical foundation — not a completed hero

**Latest study: `HeroRefined.blend`.** The user asked for the best attainable result
after relaxing the exact-replica requirement. Actual Blender previews are
`refined-full.png`, `refined-portrait.png`, and `refined-back.png`. Reproduce this
refinement with `Tools/refine_hero.py`; it reads the preserved `HeroTailored.blend`.
The refinement replaces disconnected clothing pieces with a continuous jacket and
boots, closes the neckline with a shirt, and improves the facial and material finish.
The character still has no production rig or baked game textures.

The current full-character work is **HeroTailored.blend**, with actual Blender renders
`hero-full.png`, `hero-portrait.png`, and `hero-back.png`. See `WORKFLOW.md` for source
attribution, construction, and remaining production work. `HeroFoundation.blend` below
is preserved as the earlier head-only starting point.

The user rejected the Keeper and LastSecond procedural models. They must not define the
game's approved art direction.

`HeroFoundation.blend` contains the realistic sculpting head from Blender Studio and
community contributors' official CC0 Human Base Meshes v1.4.1 bundle. Its authored anatomy,
topology and multires detail are preserved. It is recentered, given neutral clay materials,
and staged for inspection. `anatomy-foundation.png` is an actual Cycles render of this file.

Source and license record: `../../ThirdParty/HumanBaseMeshes/SOURCE.md`.
Reproducible preparation script: `../../../Tools/prepare_hero_foundation.py`.

This preparation is a change of starting material, not completion of character art. The
hero still requires likeness/design sculpting, convincing hair, authored garments,
texture work, topology and rigging. None of this foundation has replaced the Unity player
or been included in the existing Windows build.
