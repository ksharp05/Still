# Full character study

`HeroTailored.blend` builds on the official Blender Studio/community CC0 **realistic male
body**, including its facial anatomy and eyes. `HeroFoundation.blend` remains the earlier
head-only import. Attribution and download verification are recorded in
`../../ThirdParty/HumanBaseMeshes/SOURCE.md`.

The full character adds body-derived garment shells, a split long coat, standing collar,
crimson scarf, ivory shoulder cape, swept strand hair, eyebrows, stubble, clockwork clasp
and gauntlet, and a duelling blade. Source anatomy is retained in a hidden collection.
Clothing, face/hair and mechanical parts are separated for further editing.

Rebuild with `Tools/build_hero_sculpt.py` from the Unity project root using Blender 5.2.
The script saves the source before rendering `hero-full.png` and `hero-portrait.png`.
Both images are actual Blender renders. The generated concept in the sibling LastSecond
folder is reference artwork only.

Workflow references consulted:

- [Blender Studio: Design Sculpting](https://studio.blender.org/training/realistic-human-research/design-sculpting/)
  — establish proportions and design from the base mesh. Only the public summary was
  accessible; no claim is made to have watched the subscriber lesson.
- [Blender Studio: Creating Clothing Basemeshes](https://studio.blender.org/training/stylized-character-workflow/5d7f7fc5db37a94301d88ff9/)
  — clothing base construction; public summary only.
- [Official Human Base Meshes](https://www.blender.org/download/demo-files/)
  — reusable anatomical mesh and topology.

This is a character design study, not a production asset. Hair is dense render geometry,
materials are procedural, and garments require further fit/design refinement. No rig,
animation, baked textures, LODs or Unity replacement is supplied by this pass. The user
has not approved this character or the game's art direction.
