using UnityEngine;

/// <summary>
/// Named physics layers, resolved once at load.
///
/// The editor setup pass creates these layers in the TagManager. If for any reason they
/// don't exist, every lookup quietly falls back to Default so the game still runs —
/// line-of-sight just gets less precise. Failing soft beats a NullReference on launch.
/// </summary>
public static class Layers
{
    public const string WallName = "Wall";
    public const string PlayerName = "Player";
    public const string EnemyName = "Enemy";
    public const string PickupName = "Pickup";

    public static readonly int Wall = Resolve(WallName);
    public static readonly int Player = Resolve(PlayerName);
    public static readonly int Enemy = Resolve(EnemyName);
    public static readonly int Pickup = Resolve(PickupName);

    /// <summary>Mask for "does a wall block this line?" checks.</summary>
    public static int WallMask => 1 << Wall;

    /// <summary>Everything an attack can hit.</summary>
    public static int EnemyMask => 1 << Enemy;

    public static int PlayerMask => 1 << Player;

    private static int Resolve(string name)
    {
        int index = LayerMask.NameToLayer(name);
        return index < 0 ? 0 : index; // 0 == Default
    }
}
