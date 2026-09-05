using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Batch-mode smoke checks for run flow, authored models and restart regressions.</summary>
[InitializeOnLoad]
public static class PolishValidation
{
    static PolishValidation()
    {
        if (!SessionState.GetBool("Still.Validation", false)) return;
        _deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update += Tick;
    }
    private static int _step, _frames;
    private static double _deadline;
    private static string _report = "";
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Still.unity");
        SessionState.SetBool("Still.Validation", true);
        _deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _report += "PASS " + message + "\n";
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Play-mode validation timed out");
            if (!EditorApplication.isPlaying || GameManager.Instance == null) return;
            if (++_frames < 12) return;
            _frames = 0;
            var gm = GameManager.Instance;
            var player = GameManager.PlayerTransform;
            var combat = player.GetComponent<PlayerCombat>();
            switch (_step++)
            {
                case 0:
                    Check(gm.State == GameManager.RunState.Title && Time.timeScale == 0f, "Title screen pauses simulation");
                    Check(player.Find("CharacterArt") != null, "Blender player model loaded");
                    Check(player.Find("CharacterArt").GetComponentsInChildren<Renderer>().Length <= 4, "Player uses at most four material renderers");
                    var skins = player.Find("CharacterArt").GetComponentsInChildren<SkinnedMeshRenderer>();
                    Check(skins.Length == 4 && skins[0].bones.Length > 0, "Keeper imports four skinned meshes with bones");
                    Transform facing = null;
                    foreach (var t in player.GetComponentsInChildren<Transform>()) if (t.name == "FacingMarker") facing = t;
                    Check(facing != null && player.InverseTransformPoint(facing.position).z > 0, "Keeper faces the gameplay forward direction");
                    foreach (string name in new[] { "Wanderer", "Sentinel", "Archer" })
                        Check(Resources.Load<GameObject>("Characters/" + name) != null, name + " FBX imported");
                    Capture("title");
                    gm.StartRun();
                    break;
                case 1:
                    Check(gm.CanPlay && !Juice.TimeLocked, "Beginning a run enables gameplay");
                    Capture("gameplay");
                    Juice.HitStop(.01f);
                    gm.Pause();
                    break;
                case 2:
                    Check(gm.State == GameManager.RunState.Paused && Time.timeScale == 0f, "Hit-stop cannot release pause");
                    Capture("pause");
                    gm.Resume();
                    break;
                case 3:
                    Check(gm.CanPlay, "Resume restores gameplay");
                    var routine = (System.Collections.IEnumerator)typeof(PlayerCombat).GetMethod("Swing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(combat, null);
                    combat.StartCoroutine(routine);
                    Check(combat.IsSwinging, "Swing active before simulated death");
                    GameManager.PlayerHealth.TakeDamage(999, player.position);
                    Check(gm.IsGameOver && !combat.IsSwinging, "Death during swing clears combat state");
                    Capture("death");
                    break;
                case 4:
                    if (!gm.CanRestart) { _step--; break; }
                    gm.StartRun();
                    break;
                case 5:
                    Check(!combat.IsSwinging && !player.GetComponent<PlayerController>().IsDashing && !GameManager.PlayerHealth.Invulnerable, "Restart resets combat, dash and invulnerability");
                    Check(GameManager.PlayerHealth.Current == GameManager.PlayerHealth.Max, "Restart restores health");
                    var previous = GameManager.FloorRoot.gameObject;
                    gm.Descend();
                    Check(gm.Floor == 2 && !previous.activeSelf, "Descent deactivates outgoing floor immediately");
                    gm.Pause();
                    break;
                case 6:
                    Check(Time.timeScale == 0 && gm.Floor == 2, "Second floor remains paused");
                    gm.Resume();
                    break;
                case 7:
                    CheckCombatAndPickups(gm);
                    CheckDeflection();
                    CheckWalk();
                    CheckEnvironment();

                    // Emitted here and photographed next step: a particle emitted this frame has
                    // not been simulated yet and renders as nothing at all.
                    StageVfx();
                    break;
                case 8:
                    CheckSlashRenders();
                    Capture("vfx");
                    RestoreVfx();

                    // Last, deliberately: these descend and restart, rebuilding the floor and
                    // invalidating everything the steps above inspected.
                    CheckThreshold(gm);
                    CheckUpgrades(gm);
                    Finish(0);
                    break;
            }
        }
        catch (Exception e) { _report += "FAIL " + e + "\n"; Finish(1); }
    }
    /// <summary>
    /// The architecture kit. Two things can go wrong with it and both are invisible in a
    /// screenshot: the merged meshes silently losing a submesh, and a pillar sealing a
    /// doorway so the exit becomes unreachable. Check both.
    /// </summary>
    private static void CheckEnvironment()
    {
        var dungeon = GameManager.FloorRoot.Find("Dungeon");
        Check(dungeon != null, "Floor root holds a built dungeon");

        foreach (string part in new[] { "Floor", "Walls", "Cornice" })
            Check(dungeon.Find(part) != null, "Environment kit builds " + part);

        var floor = dungeon.Find("Floor").GetComponent<MeshRenderer>();
        Check(floor.sharedMaterials.Length == 5,
              "Paving separates corridor, room, trim, threshold and debris");

        var cornice = dungeon.Find("Cornice");
        Check(cornice.GetComponent<MeshRenderer>() != null &&
              cornice.GetComponentsInChildren<Collider>().Length == 0,
              "Overhanging cornice and doorway lintels are visual only and never block an actor");

        var walls = dungeon.Find("Walls");
        Check(walls.gameObject.layer == Layers.Wall, "Walls stay on the Wall layer for line of sight");

        var pillars = dungeon.Find("Pillars");
        if (pillars != null)
        {
            Check(pillars.gameObject.layer == Layers.Wall, "Pillars provide real cover on the Wall layer");
            Check(pillars.GetComponent<MeshCollider>() != null, "Pillars are solid");
        }

        // Generate a spread of floors and flood-fill each: pillars must never cut the exit off.
        bool sawColonnade = false, sawFramable = false, sawRubble = false;
        for (int f = 1; f <= 12; f++)
        {
            DungeonMap map = DungeonGenerator.Generate(f, 20260905 + f * 31);
            sawColonnade |= map.Pillars.Count > 0;
            sawRubble |= map.Rubble.Count > 0;
            Check(map.Rubble.Count <= 4, "Floor " + f + " keeps fallen masonry inside its budget");
            Check(Reachable(map), "Floor " + f + " keeps the stairs reachable from the spawn");
            sawFramable |= CheckDoorwayData(map, f);
        }
        Check(sawColonnade, "Larger rooms actually receive colonnades");
        Check(sawRubble, "Larger rooms actually receive fallen masonry");
        Check(sawFramable, "Openings wide enough to frame actually occur");

        CheckDoorwayGeometry();
        CheckDressing(dungeon);
        CheckTexturing(dungeon);

        CaptureColonnade(dungeon);
        CaptureDoorway();
    }

    /// <summary>
    /// The frame has to be provably passable, not plausibly passable. Geometric arithmetic
    /// catches a bad constant; sweeping the built colliders catches everything else.
    /// </summary>
    private static void CheckDoorwayGeometry()
    {
        // Widest thing that must fit is the enemy hitbox, radius 0.45 (ActorFactory).
        const float widest = 0.45f;
        Check(DungeonBuilder.MinDoorwayClearance > widest * 2f + 0.2f,
              "Doorway jambs leave clearance for the widest actor");

        var map = GameManager.CurrentMap;
        Check(map != null, "A live floor layout is available to probe");

        int framed = 0;
        bool clear = true;
        foreach (Doorway d in map.Doorways)
        {
            if (!d.Framable) continue;
            framed++;

            Vector3 centre = DungeonBuilder.TileToWorld(d.Start)
                           + new Vector3(d.Along.x, 0f, d.Along.y)
                             * (DungeonBuilder.TileSize * (d.Width - 1) * 0.5f)
                           + Vector3.up * 0.9f;

            // A capsule the size of the widest actor, standing in the opening.
            clear &= !Physics.CheckCapsule(centre - Vector3.up * 0.45f, centre + Vector3.up * 0.45f,
                                           widest, Layers.WallMask, QueryTriggerInteraction.Ignore);
        }

        Check(framed > 0, "The live floor has framed doorways to probe");
        Check(clear, "An actor-sized capsule fits through every framed doorway on the live floor");
    }

    /// <summary>
    /// The generated stone. Most of what can go wrong here fails silently — a missing
    /// keyword, an inverted colour space, a texture regenerated every floor — so each one
    /// gets an assertion rather than an eyeball.
    /// </summary>
    private static void CheckTexturing(Transform dungeon)
    {
        Check(ProceduralTexture.IsBuilt, "Stone textures are generated");
        Check(ProceduralTexture.GenerationMilliseconds < 900f,
              "Texture generation stays inside its budget (" +
              Mathf.RoundToInt(ProceduralTexture.GenerationMilliseconds) + " ms)");

        ProceduralTexture.Surface paving = ProceduralTexture.Paving, rock = ProceduralTexture.Rock;

        // A field that cannot tile shows a hard seam grid every repeat across a 124 m floor.
        Check(paving.SeamError < 0.001f && rock.SeamError < 0.001f,
              "Generated stone tiles without a seam");

        // The single cheapest way to catch an inverted linear/sRGB argument. The format name
        // carries the colour space, which avoids depending on where the format helper lives.
        Check(paving.Albedo.graphicsFormat.ToString().Contains("SRGB") &&
              !paving.Normal.graphicsFormat.ToString().Contains("SRGB") &&
              rock.Albedo.graphicsFormat.ToString().Contains("SRGB") &&
              !rock.Normal.graphicsFormat.ToString().Contains("SRGB"),
              "Albedo is sRGB and the normal map is linear");

        Check(paving.AlbedoMean > 0.7f && paving.AlbedoMean <= 1f &&
              rock.AlbedoMean > 0.7f && rock.AlbedoMean <= 1f,
              "Albedo means stay in range, so the palette is not dragged dark");

        var scenery = new[]
        {
            Palette.FloorMat, Palette.FloorRoomMat, Palette.FloorTrimMat, Palette.ThresholdMat,
            Palette.DebrisMat, Palette.WallMat, Palette.WallTopMat, Palette.CorniceMat, Palette.PillarMat,
        };

        bool bumped = true, smooth = true, cold = true;
        foreach (Material m in scenery)
        {
            // THE silent-failure guard: a bump map without its keyword does nothing at all.
            bumped &= m.GetTexture("_BumpMap") == null || m.IsKeywordEnabled("_NORMALMAP");
            smooth &= m.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            // Scenery must never glow: that is the one thing reserved for what moves.
            cold &= !m.IsKeywordEnabled("_EMISSION");
        }
        Check(bumped, "Every scenery bump map has _NORMALMAP enabled");
        Check(smooth, "Scenery smoothness is sourced from the albedo alpha");
        Check(cold && ProceduralTexture.MaxSmoothnessScale <= 1f,
              "Textured scenery can never bloom as a pickup");

        // Every dungeon mesh must carry tangents, or _NORMALMAP renders NaN normals.
        bool tangents = true;
        foreach (string part in new[] { "Floor", "Walls", "Cornice", "Pillars" })
        {
            Transform t = dungeon.Find(part);
            if (t == null) continue;
            Mesh mesh = t.GetComponent<MeshFilter>().sharedMesh;
            tangents &= mesh.HasVertexAttribute(VertexAttribute.Tangent) &&
                        mesh.tangents.Length == mesh.vertexCount;
        }
        Check(tangents, "Every dungeon mesh carries tangents for the normal map");

        // World-projected UVs, proved directly rather than inferred from ranges.
        //
        // For an upward-facing face the projection is (x, z) in world metres, so every such
        // vertex must satisfy uv == (position.x, position.z) exactly. That is layout
        // independent, unlike comparing UV ranges against mesh bounds: the floor mesh also
        // carries debris box side faces, which project (z, y) and (x, y), so the u range is a
        // union with no closed form. An earlier range-based check passed only on floors that
        // happened to be wider than deep, and the run seed is random (GameManager.cs:110).
        Mesh floorMesh = dungeon.Find("Floor").GetComponent<MeshFilter>().sharedMesh;
        Vector3[] verts = floorMesh.vertices;
        Vector3[] norms = floorMesh.normals;
        Vector2[] uv = floorMesh.uv;

        int upFaces = 0;
        bool projected = true;
        for (int i = 0; i < verts.Length; i++)
        {
            if (norms[i].y <= 0.5f) continue;
            upFaces++;
            projected &= Mathf.Abs(uv[i].x - verts[i].x) < 0.001f
                      && Mathf.Abs(uv[i].y - verts[i].z) < 0.001f;
        }

        Check(upFaces > 100, "The paving mesh has upward faces to check");
        Check(projected, "Paving UVs are projected from world position, not per-quad");

        // World projection only works while the geometry sits at the origin.
        Check(dungeon.position == Vector3.zero && dungeon.lossyScale == Vector3.one,
              "Dungeon geometry stays at the world origin");
    }

    /// <summary>
    /// The honesty rule: nothing may sit in the band where it would look like cover without
    /// being cover. Debris stays under the line-of-sight ray; masonry blocks it outright.
    /// </summary>
    private static void CheckDressing(Transform dungeon)
    {
        var floorMesh = dungeon.Find("Floor").GetComponent<MeshFilter>().sharedMesh;
        Check(floorMesh.bounds.max.y < DungeonBuilder.DebrisCeiling,
              "Nothing in the paving mesh rises into the line-of-sight band");
        Check(dungeon.Find("Floor").gameObject.layer != Layers.Wall,
              "Paving is never on the Wall layer, so debris can never read as cover");

        var map = GameManager.CurrentMap;
        bool blocks = true;
        foreach (Vector2Int t in map.Rubble)
        {
            // Trace at exactly the height EnemyAI uses, across the block.
            Vector3 c = DungeonBuilder.TileToWorld(t) + Vector3.up * 0.5f;
            blocks &= Physics.Linecast(c - Vector3.right * DungeonBuilder.TileSize,
                                       c + Vector3.right * DungeonBuilder.TileSize,
                                       Layers.WallMask, QueryTriggerInteraction.Ignore);
        }
        Check(blocks, "Fallen masonry blocks sight at the height enemies actually trace");

        var braziers = GameManager.FloorRoot.GetComponentsInChildren<Brazier>();
        Check(braziers.Length > 0 && braziers.Length <= Brazier.MaxPerFloor,
              "Braziers appear and stay inside their per-floor budget");

        bool safe = true;
        foreach (Brazier b in braziers)
            safe &= b.GetComponentInChildren<Collider>() == null
                 && b.transform.position.y > 1.9f;
        Check(safe, "Braziers hang above head height and never collide with an actor");

        // Dressing must not perturb the streams enemies and loot are drawn from.
        Check(DungeonMap.DressSeed(12345, 5) != (12345 ^ (5 * 7919 + 104729)),
              "Dressing draws from a stream independent of enemy and loot placement");
    }

    /// <summary>Park the player in the widest framed doorway and render it.</summary>
    private static void CaptureDoorway()
    {
        var map = GameManager.CurrentMap;
        if (map == null) return;

        Doorway best = default;
        int bestWidth = 0;
        foreach (Doorway d in map.Doorways)
            if (d.Framable && d.Width > bestWidth) { bestWidth = d.Width; best = d; }
        if (bestWidth == 0) return;

        Vector3 centre = DungeonBuilder.TileToWorld(best.Start)
                       + new Vector3(best.Along.x, 0f, best.Along.y)
                         * (DungeonBuilder.TileSize * (best.Width - 1) * 0.5f);

        var player = GameManager.PlayerTransform;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.position = new Vector3(centre.x, player.position.y, centre.z);
        controller.enabled = true;
        Physics.SyncTransforms();

        CameraRig.Instance.SetTarget(player);
        Capture("doorway");
    }

    /// <summary>
    /// Park the player beside a column and render it. The default gameplay capture is the
    /// spawn room, which never receives a colonnade, so without this the most substantial
    /// piece of the environment kit is the one piece no review render ever shows.
    /// </summary>
    private static void CaptureColonnade(Transform dungeon)
    {
        if (dungeon.Find("Pillars") == null) return;

        var live = GameManager.CurrentMap;
        if (live == null || live.Pillars.Count == 0) return;

        // Stand in the middle of the colonnade, not on the column itself.
        Vector2Int tile = live.Pillars[live.Pillars.Count / 2];
        Vector3 center = DungeonBuilder.TileToWorld(tile.x + 1, tile.y + 1);

        var player = GameManager.PlayerTransform;
        var controller = player.GetComponent<CharacterController>();

        // The controller owns the transform, so it has to be off while we teleport.
        controller.enabled = false;
        player.position = new Vector3(center.x, player.position.y, center.z);
        controller.enabled = true;
        Physics.SyncTransforms();

        CameraRig.Instance.SetTarget(player);
        Capture("colonnade");
    }

    /// <summary>
    /// Doorway detection is derived data: it must describe the floor without altering it.
    /// These run over generated maps rather than built geometry, which is the only way to
    /// cover a spread of floors cheaply.
    /// </summary>
    /// <returns>True when this floor contained at least one framable opening.</returns>
    private static bool CheckDoorwayData(DungeonMap map, int floor)
    {
        bool framable = false, walkable = true, marked = true, distinct = true, sized = true;
        var seen = new System.Collections.Generic.HashSet<Vector2Int>();

        foreach (Doorway d in map.Doorways)
        {
            framable |= d.Framable;
            sized &= d.Width >= 1 && (!d.Framable || d.Width <= Doorway.MaxFramedWidth);

            for (int k = 0; k < d.Width; k++)
            {
                Vector2Int t = d.TileAt(k);

                // The whole safety argument rests on this: detection marks tiles, never carves them.
                walkable &= map.IsFloor(t.x, t.y);
                marked &= map.IsDoorwayTile(t.x, t.y);

                // A tile claimed by two doorways would be dressed and lit twice.
                distinct &= seen.Add(t);
            }
        }

        bool entered = true;
        for (int i = 0; i < map.Rooms.Count; i++)
            entered &= map.Doorways.Exists(d => d.Room == i);

        // One assertion per property per floor: per-tile checks would bury the report.
        Check(walkable, "Floor " + floor + " doorway tiles stay walkable");
        Check(marked, "Floor " + floor + " doorway tiles are marked");
        Check(distinct, "Floor " + floor + " doorways do not overlap");
        Check(sized, "Floor " + floor + " never frames an opening wider than a portal");
        Check(entered, "Floor " + floor + " gives every room a detected entrance");

        return framable;
    }

    /// <summary>Flood fill over walkable tiles from the start room to the exit room.</summary>
    private static bool Reachable(DungeonMap map)
    {
        Vector2Int start = DungeonMap.Center(map.StartRoom);
        Vector2Int goal = DungeonMap.Center(map.ExitRoom);
        if (!map.IsFloor(start.x, start.y) || !map.IsFloor(goal.x, goal.y)) return false;

        var seen = new bool[map.Width, map.Height];
        var queue = new System.Collections.Generic.Queue<Vector2Int>();
        queue.Enqueue(start);
        seen[start.x, start.y] = true;

        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            if (t == goal) return true;
            foreach (Vector2Int d in steps)
            {
                Vector2Int n = t + d;
                if (!map.IsFloor(n.x, n.y) || seen[n.x, n.y]) continue;
                seen[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return false;
    }

    /// <summary>
    /// The walk modifier. The harness cannot hold a key down, so this asserts the invariant the
    /// feature rests on rather than the input path: the walk factor must slow the player enough
    /// that the world stays genuinely slow, because MoveIntent01 IS the scaled input magnitude
    /// and TimeDirector turns that straight into world speed.
    /// </summary>
    private static void CheckWalk()
    {
        var player = GameManager.PlayerTransform.GetComponent<PlayerController>();
        float factor = player.WalkFactor;

        Check(factor > 0.1f && factor < 0.6f,
              "The walk factor is a real slow-down without being unusable (" + factor.ToString("F2") + ")");

        // This is the number that matters: TimeDirector does Lerp(Frozen, 1, intent).
        float worldSpeed = Mathf.Lerp(WorldTime.Frozen, 1f, factor);
        Check(worldSpeed < 0.5f,
              "Walking keeps the world below half speed (" + worldSpeed.ToString("F2") + ")");

        // The whole point: the keyboard can now reach the middle of the intent range that
        // TimeDirector was always built to accept but GetAxisRaw could never produce.
        Check(factor > WorldTime.Frozen && factor < 1f,
              "Walking reaches intent between frozen and full, which GetAxisRaw alone cannot");
    }

    /// <summary>
    /// Stage a spread of effects for a photograph, one step before the capture.
    ///
    /// The VFX harness proves the banks, clocks and pooling behave; it never looks at a pixel.
    /// This is the counterpart — the only thing that would catch a shape emitting correctly and
    /// rendering as nothing, or in the wrong colour.
    ///
    /// Two timing facts shape it. A particle emitted this frame has not been simulated yet, so
    /// emission and capture must be a step apart. And the world clock runs at 0.035 while the
    /// player stands still, so world-clock effects hang in the air almost indefinitely — which
    /// makes them the right ones to photograph, where player-clock bursts would be long gone.
    /// </summary>
    private static void StageVfx()
    {
        var v = GameVfx.Instance;
        var player = GameManager.PlayerTransform;
        if (v == null || player == null) return;

        v.Clear();

        // Slashes are authored at 0.22s and would expire between the two steps.
        _slashLifetime = v.Tuning.SlashLifetime;
        v.Tuning.SlashLifetime = 2f;

        Vector3 at = player.position + Vector3.up * 0.7f;
        GameVfx.Emit(VfxKind.EnemyDeath, at + Vector3.forward * 2.6f, tint: Palette.Melee);
        GameVfx.Emit(VfxKind.Hit, at + Vector3.right * 2.6f, Vector3.right);
        GameVfx.Emit(VfxKind.Muzzle, at + Vector3.left * 2.6f, Vector3.left);
        GameVfx.Emit(VfxKind.WallHit, at + Vector3.back * 2.6f, Vector3.back);
        GameVfx.Emit(VfxKind.Charge, at + (Vector3.forward + Vector3.right) * 1.9f, tint: Palette.Ranged);

        GameVfx.Slash(player.position, Vector3.forward, 3.1f, 120f);
        GameVfx.Slash(player.position + Vector3.right * 3.4f, Vector3.right, 2.4f, 110f, hostile: true);
    }

    /// <summary>
    /// A slash must actually reach the screen, not merely exist.
    ///
    /// The VFX harness counts active arcs, which stays true for an arc that renders as nothing —
    /// a disabled renderer, a degenerate mesh, or vertex colours left at zero alpha would all
    /// pass a count. This checks the three things that make it visible.
    /// </summary>
    private static void CheckSlashRenders()
    {
        var v = GameVfx.Instance;
        Check(v != null, "The particle service is live");

        // Fresh, so its age cannot have run out between validation steps.
        GameVfx.Slash(GameManager.PlayerTransform.position, Vector3.forward, 3.1f, 120f);
        Check(v.ActiveArcs > 0, "A slash occupies a pooled arc");

        MeshRenderer drawn = null;
        foreach (MeshRenderer r in v.GetComponentsInChildren<MeshRenderer>())
            if (r.enabled && r.name == "Pooled slash") { drawn = r; break; }

        Check(drawn != null, "A slash leaves an enabled renderer");
        Check(drawn.bounds.size.magnitude > 0.5f, "The slash ribbon has real extent rather than a collapsed mesh");
        Check(drawn.sharedMaterial != null && drawn.sharedMaterial.HasProperty("_Shape"),
              "The slash uses the STILL particle material");

        Mesh mesh = drawn.GetComponent<MeshFilter>().sharedMesh;
        float peak = 0f;
        foreach (Color c in mesh.colors) peak = Mathf.Max(peak, c.a);
        Check(peak > 0.01f, "The slash ribbon carries visible vertex alpha (" + peak.ToString("F3") + ")");
    }

    private static float _slashLifetime;

    private static void RestoreVfx()
    {
        if (GameVfx.Instance != null) GameVfx.Instance.Tuning.SlashLifetime = _slashLifetime;
    }

    /// <summary>
    /// The threshold state.
    ///
    /// The trap it guards against is a UI one: the pause card is shown for every state that is
    /// not Playing, so without an explicit exclusion it would stack behind the threshold card.
    /// </summary>
    private static void CheckThreshold(GameManager gm)
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<HUD>();
        var pump = typeof(HUD).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(hud != null && pump != null, "The HUD can be driven for a threshold check");

        Transform root = hud.GetComponentInChildren<Canvas>().transform;
        Transform card = root.Find("Threshold");
        Transform menu = root.Find("Menu");
        Check(card != null && menu != null, "The threshold and pause cards both exist");

        int floor = gm.Floor;

        gm.BeginDescent();
        Check(gm.State == GameManager.RunState.Outfitting, "Choosing the stairs opens the threshold");
        Check(gm.Floor == floor, "Opening the threshold does not descend on its own");
        Check(!gm.CanPlay && Time.timeScale == 0f, "The threshold stops the world");

        pump.Invoke(hud, null);
        Check(card.gameObject.activeSelf, "The threshold card is shown");
        Check(!menu.gameObject.activeSelf, "The pause card does not stack behind the threshold");
        Capture("threshold");

        gm.CancelDescent();
        Check(gm.State == GameManager.RunState.Playing, "Backing out of the threshold resumes play");
        Check(gm.Floor == floor, "Backing out does not descend");

        pump.Invoke(hud, null);
        Check(!card.gameObject.activeSelf, "The threshold card is hidden again after backing out");

        gm.BeginDescent();
        gm.Descend();
        Check(gm.State == GameManager.RunState.Playing, "Confirming the threshold returns to play");
        Check(gm.Floor == floor + 1, "Confirming the threshold descends exactly one floor");
    }

    /// <summary>
    /// Shard spending and the upgrade lines.
    ///
    /// The assertion that earns its keep here is the reset one. The player GameObject is created
    /// once and never destroyed — it survives every descent and every restart — so a stat nudged
    /// in place would leak into the next run with nothing to undo it. Upgrades derive their
    /// values from the authored defaults every time precisely so that cannot happen, and this
    /// proves it rather than trusting it.
    /// </summary>
    private static void CheckUpgrades(GameManager gm)
    {
        var player = GameManager.PlayerTransform;
        var combat = player.GetComponent<PlayerCombat>();
        var controller = player.GetComponent<PlayerController>();
        var upgrades = GameManager.Upgrades;

        Check(upgrades != null, "The player carries an upgrade sheet");
        Check(PlayerUpgrades.Lines.Length == PlayerUpgrades.LineCount,
              "Every upgrade line has a key on the threshold screen");

        int baseDamage = combat.Damage;
        float baseRange = combat.Range;
        float baseCooldown = controller.DashCooldown;
        int baseMax = GameManager.PlayerHealth.Max;

        // Purchases are refused without shards, and refusing costs nothing.
        var spent = typeof(GameManager).GetMethod("TrySpendShards");
        while (gm.Shards > 0) gm.TrySpendShards(1);
        Check(!upgrades.TryPurchase(UpgradeKind.Edge), "An upgrade cannot be bought without shards");
        Check(combat.Damage == baseDamage, "A refused purchase changes no stat");

        // Fund it, then buy.
        int cost = upgrades.CostOf(UpgradeKind.Edge);
        for (int i = 0; i < cost; i++) gm.AddShard();

        Check(upgrades.TryPurchase(UpgradeKind.Edge), "An affordable upgrade is bought");
        Check(gm.Shards == 0, "A purchase deducts exactly its cost");
        Check(upgrades.LevelOf(UpgradeKind.Edge) == 1, "A purchase raises the level");
        Check(combat.Damage == baseDamage + 1, "EDGE actually increases swing damage");

        // The stat has to bite in real combat, not just in a field.
        var dummy = new GameObject("Upgrade probe") { layer = Layers.Enemy };
        dummy.transform.position = player.position + player.GetComponent<PlayerController>().AimDirection * 2f;
        var hp = dummy.AddComponent<Health>();
        hp.Configure(20);
        var box = dummy.AddComponent<SphereCollider>();
        box.isTrigger = true;
        box.center = Vector3.up * 0.7f;
        Physics.SyncTransforms();

        typeof(PlayerCombat).GetMethod("ResolveHits", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(combat, null);
        Check(hp.Current == 20 - (baseDamage + 1), "An upgraded swing takes more health off an enemy");
        UnityEngine.Object.DestroyImmediate(dummy);

        // Buy the rest of the lines to their caps and check the ceilings hold.
        foreach (PlayerUpgrades.Line line in PlayerUpgrades.Lines)
        {
            while (!upgrades.IsMaxed(line.Kind))
            {
                for (int i = 0; i < upgrades.CostOf(line.Kind); i++) gm.AddShard();
                if (!upgrades.TryPurchase(line.Kind)) break;
            }
            Check(upgrades.IsMaxed(line.Kind), line.Name + " can be taken to its cap");
        }

        Check(GameManager.PlayerHealth.Max <= 12,
              "Fully upgraded health stays inside the HUD's twelve pips");
        Check(controller.DashCooldown > 0.1f,
              "Fully upgraded dash cooldown never approaches zero, which would put NaN in the HUD");
        Check(controller.DashCooldown < baseCooldown, "MOMENTUM actually shortens the dash cooldown");
        Check(combat.Range > baseRange && combat.DeflectRange > baseRange,
              "REACH extends both the swing and the deflect");

        // THE assertion: a new run must start unupgraded, despite the player object surviving.
        gm.StartRun();
        Check(upgrades.LevelOf(UpgradeKind.Edge) == 0, "A new run starts with no upgrades");
        Check(combat.Damage == baseDamage, "Swing damage returns to its authored value on a new run");
        Check(Mathf.Abs(combat.Range - baseRange) < 0.001f, "Swing range returns to its authored value");
        Check(Mathf.Abs(controller.DashCooldown - baseCooldown) < 0.001f,
              "Dash cooldown returns to its authored value");
        Check(GameManager.PlayerHealth.Max == baseMax, "Max health returns to its authored value");
        Check(gm.Shards == 0, "A new run starts with no shards");
    }

    /// <summary>
    /// Arrow deflection. The dangerous failures here are all silent: a returned arrow that
    /// can still hurt the player, a swing that volleys its own arrow forever, or a parry that
    /// reaches through a wall. Each gets an assertion.
    /// </summary>
    private static void CheckDeflection()
    {
        var player = GameManager.PlayerTransform;
        var combat = player.GetComponent<PlayerCombat>();
        Vector3 aim = player.GetComponent<PlayerController>().AimDirection;
        var resolve = typeof(PlayerCombat).GetMethod("ResolveDeflections", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(resolve != null, "Deflection resolution is reachable for validation");

        int before = Projectile.Active.Count;

        // An arrow suspended right in front of the player, flying at them.
        var arrow = Projectile.Spawn(player.position + aim * 2f + Vector3.up * 0.7f, -aim, 10f, 1);
        Check(Projectile.Active.Contains(arrow), "Arrows publish themselves to the live registry");
        Check(!arrow.Deflected, "A freshly fired arrow belongs to the enemy");

        resolve.Invoke(combat, null);
        Check(arrow != null && arrow.Deflected, "A swing returns an arrow that is in front of the player");
        Check(combat.Deflections > 0, "Deflections are counted");
        Check(Vector3.Dot(arrow.Direction, aim) > 0f,
              "A returned arrow travels away from the player, not into them");

        // Deflecting again must not re-arm it: that would be an infinite volley.
        int countAfter = combat.Deflections;
        resolve.Invoke(combat, null);
        Check(combat.Deflections == countAfter, "An already-returned arrow is never deflected twice");

        UnityEngine.Object.DestroyImmediate(arrow.gameObject);

        // Behind the player, outside even the generous catch arc.
        var behind = Projectile.Spawn(player.position - aim * 6f + Vector3.up * 0.7f, aim, 10f, 1);
        resolve.Invoke(combat, null);
        Check(!behind.Deflected, "An arrow well out of reach is not deflected");
        UnityEngine.Object.DestroyImmediate(behind.gameObject);

        // A wall between player and arrow must block the parry, exactly like melee.
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.layer = Layers.Wall;
        wall.transform.position = player.position + aim * 1f + Vector3.up * 0.7f;
        wall.transform.rotation = Quaternion.LookRotation(aim);
        wall.transform.localScale = new Vector3(4f, 4f, 0.15f);
        Physics.SyncTransforms();

        var walled = Projectile.Spawn(player.position + aim * 2f + Vector3.up * 0.7f, -aim, 10f, 1);
        resolve.Invoke(combat, null);
        Check(!walled.Deflected, "A wall blocks deflection, just as it blocks melee");

        UnityEngine.Object.DestroyImmediate(walled.gameObject);
        UnityEngine.Object.DestroyImmediate(wall);
        Check(Projectile.Active.Count == before, "The arrow registry drains when arrows are destroyed");

        Check(Projectile.DeflectedDamage >= 3,
              "A returned arrow is worth the swing that returned it");

        CaptureDeflection(combat, resolve);
    }

    /// <summary>
    /// Stage a volley converging on the player, return it, and render the frame. A returned
    /// arrow is supposed to be unmistakably the player's, and only a picture shows whether
    /// the repaint and the new headings actually read at gameplay distance.
    /// </summary>
    private static void CaptureDeflection(PlayerCombat combat, MethodInfo resolve)
    {
        var player = GameManager.PlayerTransform;
        var staged = new System.Collections.Generic.List<GameObject>();

        // A fan of arrows a moment from landing, plus targets for them to be sent back at.
        for (int i = -2; i <= 2; i++)
        {
            float angle = i * 26f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 at = player.position - dir * 2.6f + Vector3.up * 0.7f;

            staged.Add(Projectile.Spawn(at, dir, 12f, 1).gameObject);
            staged.Add(ActorFactory.CreateEnemy(EnemyKind.Ranged,
                player.position - dir * 11f, 3, GameManager.FloorRoot));
        }

        Physics.SyncTransforms();
        resolve.Invoke(combat, null);
        Capture("deflection");

        foreach (GameObject go in staged)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }

    private static void CheckCombatAndPickups(GameManager gm)
    {
        var player = GameManager.PlayerTransform;
        var combat = player.GetComponent<PlayerCombat>();
        Vector3 direction = player.GetComponent<PlayerController>().AimDirection;
        var target = new GameObject("Validation enemy");
        target.layer = Layers.Enemy;
        target.transform.position = player.position + direction * 2;
        var hp = target.AddComponent<Health>(); hp.Configure(6);
        var sphere = target.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.center = Vector3.up * .7f;
        var capsule = target.AddComponent<CapsuleCollider>(); capsule.isTrigger = true; capsule.center = Vector3.up * .7f;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.layer = Layers.Wall;
        wall.transform.position = player.position + direction + Vector3.up * .7f;
        wall.transform.rotation = Quaternion.LookRotation(direction);
        wall.transform.localScale = new Vector3(2, 2, .15f);
        Physics.SyncTransforms();
        var resolve = typeof(PlayerCombat).GetMethod("ResolveHits", BindingFlags.NonPublic | BindingFlags.Instance);
        resolve.Invoke(combat, null);
        Check(hp.Current == 6, "Wall blocks player melee damage");
        wall.SetActive(false); Physics.SyncTransforms();
        resolve.Invoke(combat, null);
        Check(hp.Current == 4, "Multiple enemy colliders receive only one hit per swing");
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(wall);
        int shards = gm.Shards;
        var pickup = Pickup.Spawn(player.position, PickupKind.Shard, GameManager.FloorRoot);
        var collect = typeof(Pickup).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance);
        var collider = player.GetComponent<CharacterController>();
        collect.Invoke(pickup, new object[] { collider });
        collect.Invoke(pickup, new object[] { collider });
        Check(gm.Shards == shards + 1, "Duplicate pickup callbacks grant only one shard");
        var heart = Pickup.Spawn(player.position, PickupKind.Heart, GameManager.FloorRoot);
        collect.Invoke(heart, new object[] { collider });
        Check(!(bool)typeof(Pickup).GetField("_consumed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(heart), "Heart remains available at full health");
    }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        var data = camera.GetUniversalAdditionalCameraData();
        // Deterministic layout render. Actual in-game UI is overlay and bypasses post FX.
        bool post = data.renderPostProcessing; data.renderPostProcessing = false;
        var layers = new System.Collections.Generic.Dictionary<GameObject, int>();
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
        { layers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = 5; }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        int mask = camera.cullingMask;
        camera.cullingMask |= 1 << 5;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
            new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Builds/Review");
        File.WriteAllBytes("Builds/Review/" + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.cullingMask = mask;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        foreach (var pair in layers) pair.Key.layer = pair.Value;
        data.renderPostProcessing = post;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool("Still.Validation", false);
        File.WriteAllText("Logs/polish-validation.txt", _report);
        Debug.Log(_report);
        EditorApplication.Exit(code);
    }
}

