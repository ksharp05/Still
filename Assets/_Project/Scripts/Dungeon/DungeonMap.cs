using System.Collections.Generic;
using UnityEngine;

public enum Tile : byte
{
    Wall = 0,
    Floor = 1,
    /// <summary>A structural column standing on a floor tile: solid, but it still has ground under it.</summary>
    Pillar = 2,
}

public enum DoorSide : byte { South = 0, North = 1, West = 2, East = 3 }

/// <summary>
/// One opening where a corridor punched through a room's wall line.
///
/// Purely derived data: finding a doorway never changes what is walkable, which is what
/// lets the builder dress it without any risk to the floor's reachability guarantee.
/// </summary>
public struct Doorway
{
    /// <summary>Widest opening still dressed as a portal. Beyond this it is a corridor
    /// running alongside the room, not a door, and framing it would look like a mistake.</summary>
    public const int MaxFramedWidth = 5;

    public int Room;
    public DoorSide Side;

    /// <summary>First open cell of the run, in scan order.</summary>
    public Vector2Int Start;
    public int Width;

    /// <summary>Unit step along the room's wall line.</summary>
    public Vector2Int Along;

    /// <summary>Unit step from the room out into the passage.</summary>
    public Vector2Int Outward;

    /// <summary>The cell before the run is solid rock, so a jamb has something to stand on.</summary>
    public bool AnchorLow;

    /// <summary>The cell after the run is solid rock.</summary>
    public bool AnchorHigh;

    public Vector2Int TileAt(int i) => Start + Along * i;

    /// <summary>
    /// Both ends land on rock and the opening is door-sized. Only then do jambs and a lintel
    /// have anywhere to land — a corner entry has nothing to carry them, and floating stone
    /// reads worse than a plain gap.
    /// </summary>
    public bool Framable => AnchorLow && AnchorHigh && Width >= 2 && Width <= MaxFramedWidth;
}

/// <summary>
/// Pure layout data — a grid of tiles plus the rooms that were carved into it.
/// Deliberately contains no GameObjects: DungeonBuilder is what turns this into
/// geometry, which keeps generation fast, reproducible and easy to reason about.
///
/// Alongside the tiles we keep a per-tile <see cref="Region"/> tag saying whether a
/// space belongs to a room or to a corridor. The builder uses it to give rooms and
/// corridors different materials, which is most of what makes a floor read as
/// architecture rather than as one continuous cave.
/// </summary>
public class DungeonMap
{
    /// <summary>Region value for tiles that were never carved.</summary>
    public const int Solid = -1;

    /// <summary>Region value for carved tiles that belong to a corridor rather than a room.</summary>
    public const int Corridor = -2;

    public readonly int Width;
    public readonly int Height;
    public readonly Tile[,] Tiles;

    /// <summary><see cref="Solid"/>, <see cref="Corridor"/>, or an index into <see cref="Rooms"/>.</summary>
    public readonly int[,] Region;

    public readonly List<RectInt> Rooms = new List<RectInt>();

    /// <summary>Index into <see cref="Rooms"/> where the player spawns.</summary>
    public int StartRoomIndex;

    /// <summary>Index into <see cref="Rooms"/> holding the stairs down.</summary>
    public int ExitRoomIndex;

    /// <summary>Every pillar tile, so dressing can hang sconces on them without rescanning the grid.</summary>
    public readonly List<Vector2Int> Pillars = new List<Vector2Int>();

    /// <summary>Every opening where a corridor meets a room.</summary>
    public readonly List<Doorway> Doorways = new List<Doorway>();

    /// <summary>Per-tile doorway marker, so the builder can test a tile in O(1) while paving.</summary>
    public readonly bool[,] DoorTile;

    /// <summary>
    /// Fallen masonry. Solid like a pillar, but dressed as a broken block rather than a column.
    /// Kept apart from <see cref="Pillars"/> so the two can be built and lit differently.
    /// </summary>
    public readonly List<Vector2Int> Rubble = new List<Vector2Int>();

    public RectInt StartRoom => Rooms[StartRoomIndex];
    public RectInt ExitRoom => Rooms[ExitRoomIndex];

    public DungeonMap(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new Tile[width, height];
        Region = new int[width, height];
        DoorTile = new bool[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            Region[x, y] = Solid;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Walkable: what actors, spawning and line-of-sight care about.</summary>
    public bool IsFloor(int x, int y) => InBounds(x, y) && Tiles[x, y] == Tile.Floor;

    /// <summary>Anything that was carved, pillars included — i.e. where the ground plane exists.</summary>
    public bool HasGround(int x, int y) => InBounds(x, y) && Tiles[x, y] != Tile.Wall;

    public bool IsPillar(int x, int y) => InBounds(x, y) && Tiles[x, y] == Tile.Pillar;

    public bool IsRoomFloor(int x, int y) => IsFloor(x, y) && Region[x, y] >= 0;

    public bool IsDoorwayTile(int x, int y) => InBounds(x, y) && DoorTile[x, y];

    /// <summary>A floor tile that touches solid rock — where the builder lays the border trim.</summary>
    public bool IsEdgeFloor(int x, int y)
    {
        if (!IsFloor(x, y)) return false;
        return !HasGround(x + 1, y) || !HasGround(x - 1, y)
            || !HasGround(x, y + 1) || !HasGround(x, y - 1);
    }

    /// <summary>True when this solid tile touches carved ground — only those need to be built.</summary>
    public bool IsWallFacingFloor(int x, int y)
    {
        if (!InBounds(x, y) || Tiles[x, y] != Tile.Wall) return false;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            if (HasGround(x + dx, y + dy)) return true;
        }
        return false;
    }

    /// <summary>
    /// Seed stream for scenery.
    ///
    /// Deliberately not FloorPopulator's stream, which is seed ^ (floor * 7919 + 104729):
    /// the two coincide only where 7919f + 104729 == 104729f + 1299709, i.e. f = -12.34, so
    /// they cannot collide on any real floor. Dressing must never shift enemy or loot rolls.
    /// </summary>
    public static int DressSeed(int seed, int floor) => seed ^ (floor * 104729 + 1299709);

    public static Vector2Int Center(RectInt room) =>
        new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);
}
