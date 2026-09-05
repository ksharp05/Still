using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carves a floor: scatter non-overlapping rooms, then connect each new room to the
/// nearest already-connected one with an L-shaped corridor. That "connect to nearest"
/// rule is what stops the map sprawling into long silly detours — you get a compact,
/// loopy layout that reads well from a top-down camera.
///
/// Deeper floors get bigger and denser. Everything is driven by a seed so a run can
/// be replayed exactly if we ever want that.
/// </summary>
public static class DungeonGenerator
{
    private const int CorridorRadius = 1; // 1 -> 3 tiles wide, enough room to fight in

    public static DungeonMap Generate(int floorNumber, int seed)
    {
        var rng = new System.Random(seed);

        // Floors grow slowly with depth, then stop so they never become a slog.
        int size = Mathf.Min(46 + floorNumber * 4, 78);
        int roomTarget = Mathf.Min(5 + floorNumber, 11);

        var map = new DungeonMap(size, size);
        PlaceRooms(map, rng, roomTarget);

        // A floor with one room is a failed generation — retry with a nudged seed.
        if (map.Rooms.Count < 2)
            return Generate(floorNumber, seed + 7919);

        ConnectRooms(map, rng);
        ChooseStartAndExit(map);
        PlacePillars(map);
        PlaceRubble(map, new System.Random(DungeonMap.DressSeed(seed, floorNumber)));
        FindDoorways(map); // last: it reads the final tile state, pillars and rubble included
        return map;
    }

    private static void PlaceRooms(DungeonMap map, System.Random rng, int roomTarget)
    {
        const int maxAttempts = 240;
        const int margin = 2; // keep rooms off the outer edge so walls always have somewhere to sit

        for (int attempt = 0; attempt < maxAttempts && map.Rooms.Count < roomTarget; attempt++)
        {
            int w = rng.Next(7, 13);
            int h = rng.Next(7, 13);
            int x = rng.Next(margin, map.Width - w - margin);
            int y = rng.Next(margin, map.Height - h - margin);

            var candidate = new RectInt(x, y, w, h);

            // Pad by 2 tiles so rooms never share a wall — corridors should be visible.
            var padded = new RectInt(x - 2, y - 2, w + 4, h + 4);
            bool overlaps = false;
            foreach (var existing in map.Rooms)
            {
                if (padded.Overlaps(existing)) { overlaps = true; break; }
            }
            if (overlaps) continue;

            int index = map.Rooms.Count;
            map.Rooms.Add(candidate);
            CarveRect(map, candidate, index);
        }
    }

    /// <summary>
    /// Connect every room into one network. We walk the rooms in order and link each to
    /// the nearest room already in the network, which keeps corridors short.
    /// </summary>
    private static void ConnectRooms(DungeonMap map, System.Random rng)
    {
        var connected = new List<int> { 0 };
        var pending = new List<int>();
        for (int i = 1; i < map.Rooms.Count; i++) pending.Add(i);

        while (pending.Count > 0)
        {
            int bestPending = 0, bestConnected = 0;
            float bestDist = float.MaxValue;

            foreach (int p in pending)
            {
                Vector2Int pc = DungeonMap.Center(map.Rooms[p]);
                foreach (int c in connected)
                {
                    float d = (pc - DungeonMap.Center(map.Rooms[c])).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestPending = p;
                        bestConnected = c;
                    }
                }
            }

            CarveCorridor(map,
                DungeonMap.Center(map.Rooms[bestConnected]),
                DungeonMap.Center(map.Rooms[bestPending]),
                rng.Next(2) == 0);

            connected.Add(bestPending);
            pending.Remove(bestPending);
        }

        // One extra loop back makes the floor feel like a place rather than a tree of dead ends.
        if (map.Rooms.Count >= 4)
        {
            int a = rng.Next(map.Rooms.Count);
            int b = rng.Next(map.Rooms.Count);
            if (a != b)
                CarveCorridor(map, DungeonMap.Center(map.Rooms[a]), DungeonMap.Center(map.Rooms[b]), rng.Next(2) == 0);
        }
    }

    /// <summary>Start in one room, put the stairs in whichever room is furthest away.</summary>
    private static void ChooseStartAndExit(DungeonMap map)
    {
        map.StartRoomIndex = 0;
        Vector2Int start = DungeonMap.Center(map.StartRoom);

        int furthest = 0;
        float best = -1f;
        for (int i = 1; i < map.Rooms.Count; i++)
        {
            float d = (DungeonMap.Center(map.Rooms[i]) - start).sqrMagnitude;
            if (d > best) { best = d; furthest = i; }
        }
        map.ExitRoomIndex = furthest;
    }

    /// <summary>
    /// Tiles nothing solid may ever occupy: the player's spawn and the stairs down, plus the
    /// ring around each. Blocking the stairs tile does not merely look wrong — it makes the
    /// floor unfinishable, and a flood fill reports it as an unreachable exit, which is a
    /// confusing way to find out you dropped a rock on the staircase.
    /// </summary>
    private static bool IsReserved(DungeonMap map, int x, int y)
    {
        Vector2Int spawn = DungeonMap.Center(map.StartRoom);
        Vector2Int stairs = DungeonMap.Center(map.ExitRoom);

        return (Mathf.Abs(x - spawn.x) <= 1 && Mathf.Abs(y - spawn.y) <= 1)
            || (Mathf.Abs(x - stairs.x) <= 1 && Mathf.Abs(y - stairs.y) <= 1);
    }

    /// <summary>
    /// Scatter a few collapsed blocks of wall through the larger rooms.
    ///
    /// Rubble is written as <see cref="Tile.Pillar"/> rather than as decoration, and that is
    /// the whole point: it is solid, so it blocks sight and movement, and because it is a
    /// solid tile the existing reachability flood fill covers it for free. Knee-high scenery
    /// that looked solid but did not block an archer would be a lie to the player, so the
    /// only rubble that exists is rubble tall enough to mean it.
    /// </summary>
    private static void PlaceRubble(DungeonMap map, System.Random rng)
    {
        const int budget = 4;

        for (int i = 0; i < map.Rooms.Count && map.Rubble.Count < budget; i++)
        {
            if (i == map.StartRoomIndex) continue; // never clutter the room the player wakes in

            RectInt room = map.Rooms[i];
            if (room.width < 9 || room.height < 9) continue;
            if (rng.NextDouble() < 0.35) continue;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                int x = rng.Next(room.xMin + 2, room.xMax - 2);
                int y = rng.Next(room.yMin + 2, room.yMax - 2);
                if (TryPlaceRubble(map, x, y)) break;
            }
        }
    }

    /// <summary>
    /// Same guard as a pillar — floor on all four sides — plus spacing against anything else
    /// solid, so a block never pairs up with a column to pinch a room in two.
    /// </summary>
    private static bool TryPlaceRubble(DungeonMap map, int x, int y)
    {
        if (!map.IsFloor(x, y) || IsReserved(map, x, y)) return false;
        if (!map.IsFloor(x + 1, y) || !map.IsFloor(x - 1, y) ||
            !map.IsFloor(x, y + 1) || !map.IsFloor(x, y - 1)) return false;

        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
            if (!map.IsFloor(x + dx, y + dy) && map.HasGround(x + dx, y + dy)) return false;

        map.Tiles[x, y] = Tile.Pillar;
        map.Rubble.Add(new Vector2Int(x, y));
        return true;
    }

    /// <summary>
    /// Find every opening where a corridor punched through a room's wall line.
    ///
    /// A room's wall line is the single ring of cells one step outside its rect. Where a
    /// corridor came through, those cells are floor. Every maximal run of such cells is one
    /// doorway — and taking maximal runs is exactly what makes two corridors arriving side
    /// by side read as one wide portal instead of two lintels a tile apart.
    /// </summary>
    private static void FindDoorways(DungeonMap map)
    {
        for (int i = 0; i < map.Rooms.Count; i++)
        {
            RectInt r = map.Rooms[i];
            ScanWallLine(map, i, new Vector2Int(r.xMin, r.yMin - 1), Vector2Int.right, r.width, DoorSide.South, Vector2Int.down);
            ScanWallLine(map, i, new Vector2Int(r.xMin, r.yMax), Vector2Int.right, r.width, DoorSide.North, Vector2Int.up);
            ScanWallLine(map, i, new Vector2Int(r.xMin - 1, r.yMin), Vector2Int.up, r.height, DoorSide.West, Vector2Int.left);
            ScanWallLine(map, i, new Vector2Int(r.xMax, r.yMin), Vector2Int.up, r.height, DoorSide.East, Vector2Int.right);
        }
    }

    private static void ScanWallLine(DungeonMap map, int room, Vector2Int origin, Vector2Int along,
                                     int length, DoorSide side, Vector2Int outward)
    {
        int run = 0;

        // Deliberately <= length: a run ending flush with the room corner still gets closed out.
        for (int i = 0; i <= length; i++)
        {
            Vector2Int t = origin + along * i;
            if (i < length && IsOpening(map, t, outward)) { run++; continue; }
            if (run == 0) continue;

            Vector2Int start = origin + along * (i - run);
            var door = new Doorway
            {
                Room = room,
                Side = side,
                Start = start,
                Width = run,
                Along = along,
                Outward = outward,
                AnchorLow = IsSolid(map, start - along),
                AnchorHigh = IsSolid(map, origin + along * i),
            };

            map.Doorways.Add(door);
            for (int k = 0; k < run; k++)
            {
                Vector2Int d = door.TileAt(k);
                map.DoorTile[d.x, d.y] = true;
            }
            run = 0;
        }
    }

    /// <summary>
    /// A wall-line cell counts as an opening only when the passage continues away from the
    /// room as well as into it. Without the outward test a corridor that dead-ends flush
    /// against the wall would be dressed as a portal leading into solid rock.
    /// </summary>
    private static bool IsOpening(DungeonMap map, Vector2Int t, Vector2Int outward) =>
        map.IsFloor(t.x, t.y) &&
        map.IsFloor(t.x + outward.x, t.y + outward.y) &&
        map.IsFloor(t.x - outward.x, t.y - outward.y);

    private static bool IsSolid(DungeonMap map, Vector2Int t) =>
        map.InBounds(t.x, t.y) && map.Tiles[t.x, t.y] == Tile.Wall;

    /// <summary>
    /// Symmetrical colonnades inside the larger rooms.
    ///
    /// Pillars are solid tiles, so they double as cover: archers lose line of sight behind
    /// them, which gives the fight somewhere to be fought. They are inset two tiles from the
    /// room corners, and only in rooms big enough that four of them still leave a clear
    /// centre to fight in — a cramped room with columns is just annoying.
    /// </summary>
    private static void PlacePillars(DungeonMap map)
    {
        for (int i = 0; i < map.Rooms.Count; i++)
        {
            if (i == map.StartRoomIndex) continue; // never box the player in at spawn

            RectInt room = map.Rooms[i];
            if (room.width < 10 || room.height < 10) continue;

            const int inset = 2;
            TryPlacePillar(map, room.xMin + inset, room.yMin + inset);
            TryPlacePillar(map, room.xMax - 1 - inset, room.yMin + inset);
            TryPlacePillar(map, room.xMin + inset, room.yMax - 1 - inset);
            TryPlacePillar(map, room.xMax - 1 - inset, room.yMax - 1 - inset);
        }
    }

    /// <summary>
    /// Only convert a tile that is floor on all four sides. That guarantee is what keeps a
    /// pillar from ever sealing a doorway or pinching a corridor closed.
    /// </summary>
    private static void TryPlacePillar(DungeonMap map, int x, int y)
    {
        if (!map.IsFloor(x, y) || IsReserved(map, x, y)) return;
        if (!map.IsFloor(x + 1, y) || !map.IsFloor(x - 1, y) ||
            !map.IsFloor(x, y + 1) || !map.IsFloor(x, y - 1)) return;

        map.Tiles[x, y] = Tile.Pillar;
        map.Pillars.Add(new Vector2Int(x, y));
    }

    private static void CarveRect(DungeonMap map, RectInt rect, int roomIndex)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            if (!map.InBounds(x, y)) continue;
            map.Tiles[x, y] = Tile.Floor;
            map.Region[x, y] = roomIndex;
        }
    }

    /// <summary>L-shaped corridor. <paramref name="horizontalFirst"/> just varies the elbow.</summary>
    private static void CarveCorridor(DungeonMap map, Vector2Int from, Vector2Int to, bool horizontalFirst)
    {
        if (horizontalFirst)
        {
            CarveHorizontal(map, from.x, to.x, from.y);
            CarveVertical(map, from.y, to.y, to.x);
        }
        else
        {
            CarveVertical(map, from.y, to.y, from.x);
            CarveHorizontal(map, from.x, to.x, to.y);
        }
    }

    private static void CarveHorizontal(DungeonMap map, int x0, int x1, int y)
    {
        for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
        for (int dy = -CorridorRadius; dy <= CorridorRadius; dy++)
            CarveCorridorTile(map, x, y + dy);
    }

    private static void CarveVertical(DungeonMap map, int y0, int y1, int x)
    {
        for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
        for (int dx = -CorridorRadius; dx <= CorridorRadius; dx++)
            CarveCorridorTile(map, x + dx, y);
    }

    /// <summary>
    /// Carve one corridor tile. A corridor that runs through a room must not relabel that
    /// room's floor, or the room would lose its paving where the passage crosses it.
    /// </summary>
    private static void CarveCorridorTile(DungeonMap map, int x, int y)
    {
        if (!map.InBounds(x, y)) return;
        if (map.Region[x, y] == DungeonMap.Solid) map.Region[x, y] = DungeonMap.Corridor;
        map.Tiles[x, y] = Tile.Floor;
    }
}
