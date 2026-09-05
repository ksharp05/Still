# The Keeper — STILL player character

Original stylized armored wanderer, created in Blender 5.2. Replaces the first block-shaped Wanderer in Unity.

- `Keeper.blend`: editable individual armor plates, folded cloak, named pose rig and studio setup.
- `hero.png`, `back.png`, `game-angle.png`: Blender renders, not gameplay screenshots.
- `asset-report.json`: export geometry and rig statistics.
- `../../../Tools/create_keeper.py`: reproducible authoring and export script.
- `../../../Assets/_Project/Resources/Characters/Wanderer.fbx`: runtime asset, retaining the existing Unity asset GUID.

Design: ivory shell armor over navy cloth, an asymmetrical layered pauldron, recessed cyan eyes, pointed cheek guards, split cloak and a clock motif on the back. Height approximately 1.92 m; 5,596 triangles, four material groups and 16 bones.

The rig uses rigid weights appropriate to separate armor pieces. It is a posing foundation, not a completed animation set. Hands are stylized closed gloves; there is no facial rig. Cloak panels currently follow the spine without cloth simulation. Walk, dash, attack and death clips remain future work.

Regenerate from the project root using Blender in background mode with `--python Tools/create_keeper.py`. The earlier `create_characters.py` now preserves this player asset while regenerating the original enemies.
