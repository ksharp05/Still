# STILL character source

Created in Blender 5.2 for the first playable polish pass.

The player has since been redesigned as **The Keeper**. Its current editable source and
renders are in `Keeper/`; regenerate it with `Tools/create_keeper.py`. The original lineup
below remains an archive of the first art pass. Unity's `Wanderer.fbx` now uses the Keeper.

- `StillCharacters.blend`: editable lineup scene, materials, lights and camera.
- `lineup.png`: Blender presentation render; this is not an in-game screenshot.
- `../../Tools/create_characters.py`: reproducible model construction, FBX export and preview render.
- `../../Assets/_Project/Resources/Characters`: Unity-ready Wanderer, Sentinel and Archer FBX assets.

Each character has four material groups. Unity assigns shared URP armor, trim, cloth and emissive accent materials through `CharacterArt`. The original runtime primitives remain as a fallback if a model is absent. Source units are metres, with characters approximately 1.9–2.0 m tall.

The Wanderer has a split coat and cyan accents, the Sentinel has broader shoulders, a horned helmet and cleaver, and the Archer carries a bow and quiver with amber accents. The Sentinel replaces the existing melee enemy visually; it does not yet add a new heavy-enemy behavior.

These are initial low-poly models with procedural movement bob, not skinned characters with a finished animation set. Next art work: pose/silhouette iteration from the game camera, rigged locomotion, attack anticipation/recovery, damage and death animation, and environment materials.

Regenerate from the project root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python Tools/create_characters.py
```
