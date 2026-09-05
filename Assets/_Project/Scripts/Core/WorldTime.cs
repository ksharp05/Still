using UnityEngine;

/// <summary>
/// The heart of STILL.
///
/// The world keeps its own clock, separate from the player's. The player ALWAYS
/// moves in real time; every other moving thing (enemies, arrows, traps) multiplies
/// its delta by <see cref="Scale"/>. Stand still and the world creeps along at
/// <see cref="Frozen"/>; move and it ramps back up to full speed.
///
/// Why not just set Unity's Time.timeScale? Because that would slow the player down
/// too, and the whole feeling of this game is that YOU are quick and the world is stuck.
/// </summary>
public static class WorldTime
{
    /// <summary>World speed while the player is completely still. Not zero on purpose —
    /// a tiny crawl reads as "time is straining forward", where a hard 0 reads as "paused/broken".</summary>
    public const float Frozen = 0.035f;

    /// <summary>Current world time multiplier, roughly Frozen..1. Driven by TimeDirector.</summary>
    public static float Scale { get; internal set; } = Frozen;

    /// <summary>Delta time for anything that belongs to the world rather than the player.</summary>
    public static float DeltaTime => Time.deltaTime * Scale;

    /// <summary>0 when fully frozen, 1 at full speed. Drives visuals and audio pitch.</summary>
    public static float Flow => Mathf.InverseLerp(Frozen, 1f, Scale);

    /// <summary>Called when a run restarts so a fresh run always begins frozen.</summary>
    public static void ResetToFrozen() => Scale = Frozen;
}
