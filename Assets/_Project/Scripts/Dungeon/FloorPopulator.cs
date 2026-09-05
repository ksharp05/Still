using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fills a generated floor with enemies, loot, lights and the stairs down.
///
/// Difficulty comes from two dials: how many enemies per room, and how many of them are
/// archers. Archers are what make the time mechanic matter, so their share climbs with
/// depth — early floors are a brawl, later floors are a bullet-hell you solve standing still.
/// </summary>
public static class FloorPopulator
{
    public static Vector3 Populate(DungeonMap map, int floor, int seed, Transform parent)
    {
        // Offset from the layout seed so loot placement isn't correlated with room shapes.
        var rng = new System.Random(seed ^ (floor * 7919 + 104729));

        Vector3 playerSpawn = DungeonBuilder.TileToWorld(DungeonMap.Center(map.StartRoom));

        for (int i = 0; i < map.Rooms.Count; i++)
        {
            RectInt room = map.Rooms[i];
            AddRoomLight(room, parent, RoleOf(map, i));

            if (i == map.StartRoomIndex) continue;

            SpawnEnemies(map, room, floor, parent, rng);
            SpawnLoot(map, room, floor, parent, rng);
        }

        // Its own stream: pulling brazier draws from rng would shift every enemy and shard.
        SpawnBraziers(map, parent, new System.Random(DungeonMap.DressSeed(seed, floor)));

        Stairs.Spawn(DungeonBuilder.TileToWorld(DungeonMap.Center(map.ExitRoom)), parent);
        return playerSpawn;
    }

    private static void SpawnEnemies(DungeonMap map, RectInt room, int floor, Transform parent, System.Random rng)
    {
        int count = Mathf.Clamp(1 + floor / 2 + rng.Next(0, 2), 1, 6);

        // Archers go from rare to roughly half the pack by the deep floors.
        float archerChance = Mathf.Clamp01(0.15f + floor * 0.06f);

        for (int i = 0; i < count; i++)
        {
            if (!TryFindSpot(map, room, rng, out Vector3 pos)) continue;

            EnemyKind kind = rng.NextDouble() < archerChance ? EnemyKind.Ranged : EnemyKind.Melee;
            ActorFactory.CreateEnemy(kind, pos, floor, parent);
        }
    }

    private static void SpawnLoot(DungeonMap map, RectInt room, int floor, Transform parent, System.Random rng)
    {
        int shards = rng.Next(1, 4);
        for (int i = 0; i < shards; i++)
        {
            if (TryFindSpot(map, room, rng, out Vector3 pos))
                Pickup.Spawn(pos, PickupKind.Shard, parent);
        }

        // Roughly one heart every couple of rooms, so healing is a reason to explore.
        if (rng.NextDouble() < 0.3 && TryFindSpot(map, room, rng, out Vector3 heartPos))
            Pickup.Spawn(heartPos, PickupKind.Heart, parent);
    }

    /// <summary>
    /// Bracket braziers to framed doorways, exit room first.
    ///
    /// Doing the exit first is the point: its room light is already thrown further than the
    /// others so its glow reaches down a corridor, and lighting its doorways as well means
    /// the way out announces itself before you can see into the room.
    /// </summary>
    private static void SpawnBraziers(DungeonMap map, Transform parent, System.Random rng)
    {
        var framed = new List<Doorway>();
        foreach (Doorway d in map.Doorways)
            if (d.Framable) framed.Add(d);

        // Exit doorways to the front, the rest shuffled so it is not always the same corners.
        for (int i = framed.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (framed[i], framed[j]) = (framed[j], framed[i]);
        }
        framed.Sort((a, b) =>
            (b.Room == map.ExitRoomIndex ? 1 : 0) - (a.Room == map.ExitRoomIndex ? 1 : 0));

        int placed = 0;
        foreach (Doorway d in framed)
        {
            if (placed >= Brazier.MaxPerFloor) break;

            // Mount on the flanking wall block, facing into the opening.
            Vector2Int flank = d.Start - d.Along;
            Vector3 face = DungeonBuilder.TileToWorld(flank);
            var into = new Vector3(d.Along.x, 0f, d.Along.y);

            Brazier.Spawn(face, into, parent);
            placed++;
        }
    }

    /// <summary>What a room is for. Only lighting cares, and lighting is how the player reads it.</summary>
    private enum RoomRole { Ordinary, Start, Exit }

    private static RoomRole RoleOf(DungeonMap map, int index)
    {
        if (index == map.StartRoomIndex) return RoomRole.Start;
        if (index == map.ExitRoomIndex) return RoomRole.Exit;
        return RoomRole.Ordinary;
    }

    /// <summary>
    /// One point light per room, tinted by what the room is.
    ///
    /// The exit burns the same green as the stairs themselves, and it is thrown further
    /// than an ordinary room light on purpose: that spill reaching you down a corridor is
    /// the only wayfinding the floor has, and it saves the player wandering a solved level.
    /// </summary>
    private static void AddRoomLight(RectInt room, Transform parent, RoomRole role)
    {
        var go = new GameObject("RoomLight");
        go.transform.SetParent(parent, false);
        go.transform.position = DungeonBuilder.TileToWorld(DungeonMap.Center(room)) + Vector3.up * 3.4f;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        // Reach the corners of the room without spilling far down the corridors.
        float reach = Mathf.Max(room.width, room.height) * DungeonBuilder.TileSize;
        light.range = reach * (role == RoomRole.Exit ? 1.55f : 1.15f);
        light.shadows = LightShadows.None; // dozens of shadowed points would tank the frame rate

        switch (role)
        {
            case RoomRole.Exit:
                light.intensity = 2.1f;
                light.color = Color.Lerp(Palette.Exit, new Color(0.62f, 0.72f, 1f), 0.45f);
                break;
            case RoomRole.Start:
                // Slightly warmer and brighter: the first thing you see should feel safe.
                light.intensity = 1.8f;
                light.color = new Color(0.74f, 0.79f, 1f);
                break;
            default:
                light.intensity = 1.5f;
                light.color = new Color(0.62f, 0.72f, 1f);
                break;
        }
    }

    /// <summary>Pick a random walkable tile inside the room, inset so nothing spawns in a wall.</summary>
    private static bool TryFindSpot(DungeonMap map, RectInt room, System.Random rng, out Vector3 position)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int x = rng.Next(room.xMin + 1, room.xMax - 1);
            int y = rng.Next(room.yMin + 1, room.yMax - 1);

            if (map.IsFloor(x, y))
            {
                position = DungeonBuilder.TileToWorld(x, y);
                return true;
            }
        }

        position = Vector3.zero;
        return false;
    }
}
