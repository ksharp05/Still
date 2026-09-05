using System;
using UnityEngine;

/// <summary>
/// A tiny static event bus so systems never need hard references to each other.
/// The HUD doesn't know the player exists; it just listens for "health changed".
///
/// Because these are static, they outlive scene objects — so <see cref="Clear"/>
/// MUST be called when a run restarts, or destroyed listeners from the previous
/// run will still be subscribed and throw MissingReferenceException.
/// </summary>
public static class GameEvents
{
    /// <summary>World position, amount. Raised for any damage, to any target.</summary>
    public static event Action<Vector3, int> DamageDealt;

    /// <summary>World position of the enemy that died.</summary>
    public static event Action<Vector3> EnemyKilled;

    /// <summary>current, max — the player's health specifically.</summary>
    public static event Action<int, int> PlayerHealthChanged;

    /// <summary>The new floor number, 1-based.</summary>
    public static event Action<int> FloorChanged;

    /// <summary>Total shards (this run's score).</summary>
    public static event Action<int> ShardsChanged;

    public static event Action PlayerDied;

    public static void RaiseDamage(Vector3 worldPos, int amount) => DamageDealt?.Invoke(worldPos, amount);
    public static void RaiseEnemyKilled(Vector3 worldPos) => EnemyKilled?.Invoke(worldPos);
    public static void RaisePlayerHealth(int current, int max) => PlayerHealthChanged?.Invoke(current, max);
    public static void RaiseFloor(int floor) => FloorChanged?.Invoke(floor);
    public static void RaiseShards(int total) => ShardsChanged?.Invoke(total);
    public static void RaisePlayerDied() => PlayerDied?.Invoke();

    /// <summary>Drop every subscriber. Call this before rebuilding the world.</summary>
    public static void Clear()
    {
        DamageDealt = null;
        EnemyKilled = null;
        PlayerHealthChanged = null;
        FloorChanged = null;
        ShardsChanged = null;
        PlayerDied = null;
    }
}
