using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Turns a <see cref="DungeonMap"/> into actual geometry.
///
/// Everything is welded into a handful of merged meshes rather than built as thousands of
/// GameObjects. A 78x78 floor would otherwise be several thousand renderers — this way it
/// is four, and the frame rate stays flat, which matters more than it sounds: a steady
/// framerate IS polish.
///
/// The architecture kit lives here too. Three passes give the raw carved layout the read
/// of built space:
///
///  * Paving      — room floors, corridor floors and the border trim that rings every
///                  walkable edge each get their own submesh, so a glance tells you
///                  whether you are in a chamber or a passage.
///  * Cornice     — a proud band along the top of every exposed wall face, plus its
///                  underside. It catches the room lights and gives the walls a
///                  silhouette instead of a flat slab. Visual only, no collider, so it can
///                  overhang the play space without ever snagging an actor.
///  * Colonnades  — the pillars the generator placed. Solid, collidable cover with a
///                  plinth, shaft, capital and an emissive sconce bowl.
///
/// Only wall faces that actually touch carved ground are generated. Nobody ever sees the
/// inside of the rock, so we do not pay for it.
/// </summary>
public static class DungeonBuilder
{
    public const float TileSize = 1.6f;
    public const float WallHeight = 3.2f;

    /// <summary>Height the cornice band starts at. Well clear of anything that walks under it.</summary>
    private const float CorniceY = 2.45f;

    /// <summary>How far the cornice overhangs the wall face.</summary>
    private const float CorniceOut = 0.12f;

    /// <summary>Border trim sits a hair above the paving so it never z-fights with it.</summary>
    private const float TrimLift = 0.012f;

    // Doorway frame. A jamb stands JambOut proud of the flanking wall into the opening and
    // is buried JambBury behind it, so its back face is never coplanar with the wall.
    private const float JambOut = 0.18f;
    private const float JambBury = 0.06f;
    private const float JambDepth = 1.00f;   // across the passage, inside the 1.6 wall row
    private const float LintelDepth = 0.62f;
    private const float CapAlong = 0.36f;
    private const float CapDepth = 1.12f;

    /// <summary>
    /// Narrowest walkable gap a framed doorway can leave: the smallest framable opening is
    /// two tiles, less a jamb at each end. The widest thing that must pass is the enemy
    /// hitbox at radius 0.45, i.e. 0.90 across — validation asserts the margin.
    /// </summary>
    public static float MinDoorwayClearance => 2f * TileSize - 2f * JambOut;

    // Dressing. The line-of-sight ray runs at 0.5, so scenery is either well under it or
    // well over it — never in between, where a prop would look like cover without being it.
    private const float MasonryHeight = 1.35f;   // solid, on the Wall layer: honest cover
    private const float DebrisTop = 0.082f;      // walked over; below both step offsets
    private const float SconceY = 2.00f;         // clears a 1.8 m actor's head

    /// <summary>Tallest anything in the paving mesh may reach. Asserted by validation.</summary>
    public const float DebrisCeiling = 0.10f;

    public static Vector3 TileToWorld(int x, int y) =>
        new Vector3((x + 0.5f) * TileSize, 0f, (y + 0.5f) * TileSize);

    public static Vector3 TileToWorld(Vector2Int t) => TileToWorld(t.x, t.y);

    public static GameObject Build(DungeonMap map, Transform parent)
    {
        var root = new GameObject("Dungeon");
        if (parent != null) root.transform.SetParent(parent, false);

        BuildFloor(map, root.transform);
        BuildWalls(map, root.transform);
        BuildCornice(map, root.transform);
        BuildPillars(map, root.transform);
        return root;
    }

    /// <summary>
    /// Ground plane. Submesh 0 corridors, 1 room paving, 2 border trim, 3 doorway threshold.
    /// Pillar tiles are paved too — the column stands on the floor, it does not replace it.
    ///
    /// The threshold band costs no extra geometry at all: a doorway tile is always an edge
    /// tile, so it was already being drawn as dark trim. This only promotes it to a bright
    /// stripe, which from an 18 m camera is the most legible cue the kit has.
    /// </summary>
    private static void BuildFloor(DungeonMap map, Transform parent)
    {
        var mb = new MeshBuilder(5);

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (!map.HasGround(x, y)) continue;

            float x0 = x * TileSize, x1 = x0 + TileSize;
            float z0 = y * TileSize, z1 = z0 + TileSize;

            bool door = map.IsDoorwayTile(x, y);
            bool trim = map.IsEdgeFloor(x, y);
            int submesh = door ? 3 : trim ? 2 : (map.Region[x, y] >= 0 ? 1 : 0);
            float h = (door || trim) ? TrimLift : 0f;

            mb.AddQuad(
                new Vector3(x0, h, z0), new Vector3(x1, h, z0),
                new Vector3(x0, h, z1), new Vector3(x1, h, z1),
                Vector3.up, submesh);

            if (!door) AddDebris(mb, map, x, y, h);
        }

        var go = new GameObject("Floor");
        go.transform.SetParent(parent, false);
        go.isStatic = true;

        Mesh mesh = mb.Build("FloorMesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { Palette.FloorMat, Palette.FloorRoomMat, Palette.FloorTrimMat,
                    Palette.ThresholdMat, Palette.DebrisMat };

        // The collider uses the same mesh: the 12mm trim lift is far below anything the
        // character controller notices, so one mesh serves both jobs.
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void BuildWalls(DungeonMap map, Transform parent)
    {
        // Submesh 0 = sides, submesh 1 = tops.
        var mb = new MeshBuilder(2);
        const float h = WallHeight;

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (!map.IsWallFacingFloor(x, y)) continue;

            float x0 = x * TileSize, x1 = x0 + TileSize;
            float z0 = y * TileSize, z1 = z0 + TileSize;

            // Top — always visible from our camera angle.
            mb.AddQuad(
                new Vector3(x0, h, z0), new Vector3(x1, h, z0),
                new Vector3(x0, h, z1), new Vector3(x1, h, z1),
                Vector3.up, 1);

            // Sides, only where the neighbouring tile is carved.
            if (map.HasGround(x, y + 1))
                mb.AddQuad(
                    new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1),
                    new Vector3(x1, h, z1), new Vector3(x0, h, z1),
                    Vector3.forward, 0);

            if (map.HasGround(x, y - 1))
                mb.AddQuad(
                    new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0),
                    new Vector3(x0, h, z0), new Vector3(x1, h, z0),
                    Vector3.back, 0);

            if (map.HasGround(x + 1, y))
                mb.AddQuad(
                    new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1),
                    new Vector3(x1, h, z0), new Vector3(x1, h, z1),
                    Vector3.right, 0);

            if (map.HasGround(x - 1, y))
                mb.AddQuad(
                    new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0),
                    new Vector3(x0, h, z1), new Vector3(x0, h, z0),
                    Vector3.left, 0);
        }

        foreach (Doorway d in map.Doorways)
            if (d.Framable) AddJambs(mb, d);

        foreach (Vector2Int t in map.Rubble)
            AddMasonry(mb, t);

        var go = new GameObject("Walls");
        go.transform.SetParent(parent, false);
        go.isStatic = true;
        go.layer = Layers.Wall;

        Mesh mesh = mb.Build("WallMesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { Palette.WallMat, Palette.WallTopMat };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    /// <summary>
    /// The proud band along the top of every exposed wall face. Deliberately built as its
    /// own collider-less object: it overhangs the walkable space, and geometry that hangs
    /// over the player must never be something the player or a projectile can hit.
    /// </summary>
    private static void BuildCornice(DungeonMap map, Transform parent)
    {
        var mb = new MeshBuilder(2);

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (!map.IsWallFacingFloor(x, y)) continue;

            AddSconces(mb, map, x, y);

            float x0 = x * TileSize, x1 = x0 + TileSize;
            float z0 = y * TileSize, z1 = z0 + TileSize;

            if (map.HasGround(x, y + 1))
                AddCorniceBand(mb, new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), Vector3.forward);
            if (map.HasGround(x, y - 1))
                AddCorniceBand(mb, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), Vector3.back);
            if (map.HasGround(x + 1, y))
                AddCorniceBand(mb, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.right);
            if (map.HasGround(x - 1, y))
                AddCorniceBand(mb, new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0), Vector3.left);
        }

        foreach (Doorway d in map.Doorways)
            if (d.Framable) AddLintel(mb, d);

        var go = new GameObject("Cornice");
        go.transform.SetParent(parent, false);
        go.isStatic = true;

        go.AddComponent<MeshFilter>().sharedMesh = mb.Build("CorniceMesh");
        go.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { Palette.CorniceMat, Palette.SconceMat };
    }

    /// <summary>
    /// One wall face worth of cornice: the underside soffit, the outward face of the
    /// band, and the narrow ledge on top where it meets the wall cap.
    /// </summary>
    private static void AddCorniceBand(MeshBuilder mb, Vector3 left, Vector3 right, Vector3 outward)
    {
        Vector3 o = outward * CorniceOut;
        Vector3 lo = left + o, ro = right + o;
        Vector3 band = Vector3.up * CorniceY, cap = Vector3.up * WallHeight;

        // Soffit — the underside, seen from the room below.
        mb.AddQuad(left + band, right + band, lo + band, ro + band, Vector3.down, 0);

        // Outward face of the band.
        mb.AddQuad(lo + band, ro + band, lo + cap, ro + cap, outward, 0);

        // Ledge on top, closing the gap back to the wall cap.
        mb.AddQuad(lo + cap, ro + cap, left + cap, right + cap, Vector3.up, 0);
    }

    /// <summary>
    /// Broken flagstones on a paving tile. Merged into the floor mesh, so they share its
    /// collider and its Default layer.
    ///
    /// Kept at 82 mm on purpose: four times below the character controllers' step offset, so
    /// they are walked over without a hitch, and six times below the height enemies trace
    /// sight at, so nothing about them can ever suggest cover.
    /// </summary>
    private static void AddDebris(MeshBuilder mb, DungeonMap map, int x, int y, float baseY)
    {
        if (Hash(x, y, 11) % 1000 >= 55) return; // roughly 5.5% of tiles

        Vector3 centre = TileToWorld(x, y);
        int h = Hash(x, y, 29);

        // Nudged off the tile centre so the scatter does not read as a grid.
        centre.x += ((h & 63) / 63f - 0.5f) * TileSize * 0.45f;
        centre.z += (((h >> 6) & 63) / 63f - 0.5f) * TileSize * 0.45f;

        float w = 0.30f + ((h >> 12) & 31) / 31f * 0.32f;
        float d = 0.24f + ((h >> 17) & 31) / 31f * 0.22f;
        float top = DebrisTop - baseY;

        mb.AddBox(centre + Vector3.up * (baseY + top * 0.5f), new Vector3(w, top, d), 4);
    }

    /// <summary>
    /// A collapsed block of wall. Merged into the wall mesh, so it inherits the Wall layer
    /// and blocks movement, sight, melee and arrows alike — which is the only honest way to
    /// put something waist-high in a room an archer is shooting across.
    /// </summary>
    private static void AddMasonry(MeshBuilder walls, Vector2Int tile)
    {
        Vector3 c = TileToWorld(tile);
        int h = Hash(tile.x, tile.y, 71);
        float lean = ((h & 31) / 31f - 0.5f) * 0.12f;

        walls.AddBox(c + Vector3.up * (MasonryHeight * 0.5f),
                     new Vector3(TileSize * 0.78f, MasonryHeight, TileSize * 0.78f), 0);

        // A smaller slab tipped off the top, so the silhouette is not a plain crate.
        walls.AddBox(c + new Vector3(lean, MasonryHeight + 0.14f, -lean),
                     new Vector3(TileSize * 0.52f, 0.28f, TileSize * 0.44f), 0);
    }

    /// <summary>
    /// Emissive bowls set into wall faces that look onto a room, every fourth tile. They go
    /// in the cornice mesh, so like everything else that overhangs the play space they carry
    /// no collider — and at 2.0 they clear a 1.8 m actor's head anyway.
    /// </summary>
    private static void AddSconces(MeshBuilder mb, DungeonMap map, int x, int y)
    {
        if (((x * 7 + y * 13) & 3) != 0) return;

        Vector3 c = TileToWorld(x, y);
        var size = new Vector3(TileSize * 0.22f, 0.16f, TileSize * 0.22f);

        // Only on faces that actually look into a room; corridors stay dark.
        if (map.IsRoomFloor(x, y + 1)) mb.AddBox(c + new Vector3(0f, SconceY, TileSize * 0.5f), size, 1);
        else if (map.IsRoomFloor(x, y - 1)) mb.AddBox(c + new Vector3(0f, SconceY, -TileSize * 0.5f), size, 1);
        else if (map.IsRoomFloor(x + 1, y)) mb.AddBox(c + new Vector3(TileSize * 0.5f, SconceY, 0f), size, 1);
        else if (map.IsRoomFloor(x - 1, y)) mb.AddBox(c + new Vector3(-TileSize * 0.5f, SconceY, 0f), size, 1);
    }

    /// <summary>
    /// Deterministic per-tile hash. Dressing uses this rather than a shared random stream so
    /// that adding or removing one kind of prop can never reshuffle another.
    /// </summary>
    private static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            int h = salt * 73856093 ^ x * 19349663 ^ y * 83492791;
            h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
            return h & 0x7fffffff;
        }
    }

    /// <summary>
    /// Two full-height jambs flanking a doorway, merged into the wall mesh so they inherit
    /// its collider and Wall layer.
    ///
    /// A jamb may be solid where low rubble may not: it runs the full 0 to 3.2, so it blocks
    /// movement, sight, melee and arrows exactly like the wall it grows out of. There is no
    /// gap between what it looks like and what it does.
    /// </summary>
    private static void AddJambs(MeshBuilder walls, Doorway d)
    {
        Vector3 a = new Vector3(d.Along.x, 0f, d.Along.y);
        Vector3 o = new Vector3(d.Outward.x, 0f, d.Outward.y);
        Vector3 centre = TileToWorld(d.Start) + a * (TileSize * (d.Width - 1) * 0.5f);

        Vector3 size = Abs(a) * (JambOut + JambBury) + Vector3.up * WallHeight + Abs(o) * JambDepth;

        // The run's outer edge is at half; the jamb spans [half - JambOut, half + JambBury].
        float half = TileSize * d.Width * 0.5f;
        float offset = half - (JambOut - JambBury) * 0.5f;
        Vector3 lift = Vector3.up * (WallHeight * 0.5f);

        walls.AddBox(centre - a * offset + lift, size, 0);
        walls.AddBox(centre + a * offset + lift, size, 0);
    }

    /// <summary>
    /// The lintel across a doorway and the caps over its jambs — the cornice band continuing
    /// around the frame, which is why they live in the cornice mesh and get no collider.
    ///
    /// Both sit above 2.45 and are shallower than the 1.6 wall row they bridge, so they are
    /// lower and less deep than the wall beside them. Their ground shadow therefore falls
    /// inside the shadow that wall already casts, at any camera pitch: nothing can hide
    /// behind the frame that was not already hidden.
    /// </summary>
    private static void AddLintel(MeshBuilder cornice, Doorway d)
    {
        Vector3 a = new Vector3(d.Along.x, 0f, d.Along.y);
        Vector3 o = new Vector3(d.Outward.x, 0f, d.Outward.y);
        Vector3 centre = TileToWorld(d.Start) + a * (TileSize * (d.Width - 1) * 0.5f);

        float bandHeight = WallHeight - CorniceY;
        Vector3 lift = Vector3.up * (CorniceY + bandHeight * 0.5f);

        // Overruns half a tile onto each flanking block so it visibly lands rather than floats.
        cornice.AddBox(centre + lift,
            Abs(a) * (TileSize * (d.Width + 1)) + Vector3.up * bandHeight + Abs(o) * LintelDepth, 0);

        float offset = TileSize * d.Width * 0.5f - (JambOut - JambBury) * 0.5f;
        Vector3 capSize = Abs(a) * CapAlong + Vector3.up * bandHeight + Abs(o) * CapDepth;

        cornice.AddBox(centre - a * offset + lift, capSize, 0);
        cornice.AddBox(centre + a * offset + lift, capSize, 0);
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    /// <summary>
    /// The colonnades. Submesh 0 is stone, submesh 1 the emissive sconce bowl that sits
    /// on the capital — bloom does the work, so a room full of columns costs no lights.
    /// </summary>
    private static void BuildPillars(DungeonMap map, Transform parent)
    {
        if (map.Pillars.Count == 0) return;

        var mb = new MeshBuilder(2);

        foreach (Vector2Int tile in map.Pillars)
        {
            Vector3 c = TileToWorld(tile);

            // Plinth, shaft, capital: three stacked boxes read as a column from any angle.
            mb.AddBox(c + Vector3.up * 0.11f, new Vector3(TileSize * 0.92f, 0.22f, TileSize * 0.92f), 0);
            mb.AddBox(c + Vector3.up * (WallHeight * 0.5f), new Vector3(TileSize * 0.62f, WallHeight, TileSize * 0.62f), 0);
            mb.AddBox(c + Vector3.up * (WallHeight - 0.16f), new Vector3(TileSize * 0.86f, 0.32f, TileSize * 0.86f), 0);

            // Sconce bowl on top.
            mb.AddBox(c + Vector3.up * (WallHeight + 0.14f), new Vector3(TileSize * 0.34f, 0.2f, TileSize * 0.34f), 1);
        }

        var go = new GameObject("Pillars");
        go.transform.SetParent(parent, false);
        go.isStatic = true;
        go.layer = Layers.Wall; // real cover: archers lose line of sight behind a column

        Mesh mesh = mb.Build("PillarMesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { Palette.PillarMat, Palette.SconceMat };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    /// <summary>
    /// Accumulates quads into vertex/index buffers. Winding is handled by the caller
    /// passing corners as (bottom-left, bottom-right, top-left, top-right) as seen
    /// from the front face — the same order works for floors, tops and every wall side.
    /// </summary>
    private class MeshBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private readonly List<int>[] _tris;

        public MeshBuilder(int submeshCount)
        {
            _tris = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) _tris[i] = new List<int>();
        }

        public void AddQuad(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector3 normal, int submesh)
        {
            int i = _verts.Count;

            _verts.Add(bl); _verts.Add(br); _verts.Add(tl); _verts.Add(tr);
            for (int n = 0; n < 4; n++) _normals.Add(normal);

            // World-projected UVs, in metres. Per-quad 0-1 UVs would repeat the texture once
            // per 1.6 m tile with a seam at every join, and stretch a 1.6 x 3.2 wall face 2:1.
            // Projecting from world position instead makes the stone continuous across the
            // whole merged mesh, and at one consistent scale on floors and walls alike.
            Vector4 tangent = TangentFor(normal);
            _tangents.Add(tangent); _tangents.Add(tangent);
            _tangents.Add(tangent); _tangents.Add(tangent);

            _uvs.Add(Project(bl, normal));
            _uvs.Add(Project(br, normal));
            _uvs.Add(Project(tl, normal));
            _uvs.Add(Project(tr, normal));

            var t = _tris[submesh];
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i + 2); t.Add(i + 3); t.Add(i + 1);
        }

        /// <summary>
        /// World position projected onto the two axes the face does not point along. Every
        /// caller passes an exact axis as the normal, so a dominant-axis test is exact.
        /// </summary>
        private static Vector2 Project(Vector3 p, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f) return new Vector2(p.x, p.z);
            if (Mathf.Abs(normal.x) > 0.5f) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        /// <summary>
        /// Tangent matching <see cref="Project"/>, with w chosen so the bitangent URP derives
        /// as cross(normal, tangent) * w runs along +V.
        ///
        /// This is not optional once a normal map is bound: the _NORMALMAP keyword switches on
        /// the world-space tangent interpolator, and with no tangent stream the GPU supplies
        /// zero, normalize(0) is NaN, and the walls render black.
        /// </summary>
        private static Vector4 TangentFor(Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f)
                return new Vector4(1f, 0f, 0f, normal.y > 0f ? -1f : 1f);
            if (Mathf.Abs(normal.x) > 0.5f)
                return new Vector4(0f, 0f, 1f, normal.x > 0f ? -1f : 1f);
            return new Vector4(1f, 0f, 0f, normal.z > 0f ? 1f : -1f);
        }

        /// <summary>Axis-aligned box around <paramref name="center"/>, six outward-facing quads.</summary>
        public void AddBox(Vector3 center, Vector3 size, int submesh)
        {
            Vector3 h = size * 0.5f;
            float x0 = center.x - h.x, x1 = center.x + h.x;
            float y0 = center.y - h.y, y1 = center.y + h.y;
            float z0 = center.z - h.z, z1 = center.z + h.z;

            AddQuad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), Vector3.back, submesh);
            AddQuad(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward, submesh);
            AddQuad(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), Vector3.right, submesh);
            AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), Vector3.left, submesh);
            AddQuad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), Vector3.up, submesh);
            AddQuad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), Vector3.down, submesh);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            // Dungeons blow past 65k vertices easily; 32-bit indices avoid a silent truncation.
            mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTangents(_tangents);
            mesh.subMeshCount = _tris.Length;
            for (int i = 0; i < _tris.Length; i++) mesh.SetTriangles(_tris[i], i);

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
