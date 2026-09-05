using System;
using UnityEngine;

/// <summary>
/// Shared hit points for anything that can be hurt — the player and every enemy
/// use this same component. Raises events rather than calling into other systems,
/// so the HUD, the death handler and the VFX can all react independently.
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5;

    public int Max => maxHealth;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;

    /// <summary>Set true during a dash so i-frames actually mean something.</summary>
    public bool Invulnerable { get; set; }

    /// <summary>current, max</summary>
    public event Action<int, int> Changed;

    /// <summary>Where the hit landed, so callers can spawn sparks at the right spot.</summary>
    public event Action<Vector3> Damaged;

    public event Action Died;

    private void Awake() => Current = maxHealth;

    /// <summary>Set max HP at spawn time (enemies get their value from EnemyType).</summary>
    public void Configure(int max)
    {
        maxHealth = Mathf.Max(1, max);
        Current = maxHealth;
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>
    /// Change max HP mid-run, carrying the delta into current health.
    ///
    /// Distinct from <see cref="Configure"/>, which sets current to full — correct at spawn,
    /// wrong for a "+1 max health" upgrade, which would otherwise also be a free full heal.
    /// Lowering the max (an upgrade reset) clamps current down instead.
    /// </summary>
    public void SetMax(int max)
    {
        int next = Mathf.Max(1, max);
        int delta = next - maxHealth;

        maxHealth = next;
        Current = delta > 0 ? Current + delta : Mathf.Min(Current, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <returns>True if the hit actually landed (false when dead or invulnerable).</returns>
    public bool TakeDamage(int amount, Vector3 hitPoint)
    {
        if (IsDead || Invulnerable || amount <= 0) return false;

        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(hitPoint);
        GameEvents.RaiseDamage(hitPoint, amount);

        if (Current == 0) Died?.Invoke();
        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }
}
