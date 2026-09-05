using System.Collections;
using UnityEngine;

/// <summary>
/// A single melee weapon, swung in an arc.
///
/// Melee-only is a design choice, not a shortcut: the whole fantasy of this game is
/// walking calmly through a hail of frozen arrows to reach the archer that fired them.
/// Giving the player a gun would delete that.
///
/// The blade does reach further than its length, though: a swing that catches a suspended
/// arrow returns it, aimed at whoever is nearest. That is the answer to the archer standoff
/// and it is deliberately not a gun — the player never generates a projectile, they only
/// send back one already in the air. It also means a room full of frozen arrows is something
/// you can clear rather than only route around.
///
/// The swing forces time to full speed for its duration, so an attack from a dead stop
/// still lands with weight instead of oozing out in slow motion. That is the real price of
/// a deflection: returning one arrow starts every other arrow in the room moving again.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Swing")]
    [SerializeField] private float range = 3.1f;
    [SerializeField] private float arcDegrees = 120f;
    [SerializeField] private int damage = 2;
    [SerializeField] private float swingDuration = 0.26f;
    [SerializeField] private float cooldown = 0.32f;

    [Tooltip("How far into the swing the damage check happens, 0..1.")]
    [SerializeField] private float hitPoint01 = 0.4f;

    [Header("Knockback")]
    [SerializeField] private float knockback = 5.5f;

    [Header("Deflection")]
    [Tooltip("Arrows are small and fast; the catch radius is more generous than the blade.")]
    [SerializeField] private float deflectRange = 3.9f;

    [Tooltip("Wider than the damage arc, so a deflection feels like a save rather than a trick shot.")]
    [SerializeField] private float deflectArcDegrees = 200f;

    public bool IsSwinging { get; private set; }
    public event System.Action Swung;
    public event System.Action<Vector3> Connected;

    /// <summary>
    /// Arrows returned since combat state was last reset — i.e. per floor, since GameManager
    /// resets combat on descent and on restart. Read by validation.
    /// </summary>
    public int Deflections { get; private set; }

    private PlayerController _player;
    private Transform _bladePivot;
    private float _readyAt;

    // Authored values, captured before anything can modify them. Upgrades recompute the live
    // stats from these every time rather than adjusting them in place — the player object is
    // never destroyed, not even across a restart, so an incremental edit would leak into the
    // next run with nothing to undo it.
    private int _baseDamage;
    private float _baseRange;
    private float _baseDeflectRange;

    private readonly Collider[] _hits = new Collider[24];
    private readonly System.Collections.Generic.HashSet<Health> _damaged = new System.Collections.Generic.HashSet<Health>();

    private void Awake()
    {
        _baseDamage = damage;
        _baseRange = range;
        _baseDeflectRange = deflectRange;

        _player = GetComponent<PlayerController>();
        _bladePivot = transform.Find("BladePivot");
        if (_bladePivot != null) _bladePivot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.CanPlay) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (Input.GetMouseButtonDown(0) && !IsSwinging && Time.time >= _readyAt)
            StartCoroutine(Swing());
    }

    /// <summary>
    /// Recompute the tunable stats from the authored values plus upgrade bonuses. Passing zeroes
    /// restores exactly what was authored, which is how an upgrade reset works.
    /// </summary>
    public void ApplyTuning(int bonusDamage, float bonusRange)
    {
        damage = _baseDamage + bonusDamage;
        range = _baseRange + bonusRange;
        deflectRange = _baseDeflectRange + bonusRange;
    }

    public int Damage => damage;
    public float Range => range;
    public float DeflectRange => deflectRange;

    public void ResetCombat()
    {
        StopAllCoroutines();
        IsSwinging = false;
        _readyAt = 0f;
        Deflections = 0;
        if (_bladePivot != null)
        {
            foreach (var trail in _bladePivot.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
            _bladePivot.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => ResetCombat();

    private IEnumerator Swing()
    {
        IsSwinging = true;
        _readyAt = Time.time + cooldown;

        if (TimeDirector.Instance != null) TimeDirector.Instance.Impulse(swingDuration);
        if (_bladePivot != null) _bladePivot.gameObject.SetActive(true);

        Swung?.Invoke();
        GameVfx.Slash(transform.position, _player.AimDirection, range, arcDegrees);
        Sound.Play(Sfx.Swing);

        float half = arcDegrees * 0.5f;
        float elapsed = 0f;
        bool damageDone = false;

        while (elapsed < swingDuration)
        {
            // Scaled delta on purpose: hit-stop should freeze the blade mid-arc too.
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingDuration);

            // Ease-out so the blade snaps forward then settles — a linear sweep reads as robotic.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (_bladePivot != null)
                _bladePivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-half, half, eased), 0f);

            if (!damageDone && t >= hitPoint01)
            {
                damageDone = true;
                ResolveHits();
                ResolveDeflections();
            }
            yield return null;
        }

        if (_bladePivot != null) _bladePivot.gameObject.SetActive(false);
        IsSwinging = false;
    }

    private void ResolveHits()
    {
        Vector3 origin = transform.position;
        Vector3 forward = _player != null ? _player.AimDirection : transform.forward;

        // Collide (not Ignore) because enemy hitboxes are triggers — see ActorFactory.
        int count = Physics.OverlapSphereNonAlloc(origin, range, _hits, Layers.EnemyMask,
                                                  QueryTriggerInteraction.Collide);
        bool anyHit = false;
        _damaged.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col == null) continue;

            Vector3 toTarget = col.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            // Cone check — the arc has a back, so you can't hit what's behind you.
            if (Vector3.Angle(forward, toTarget.normalized) > arcDegrees * 0.5f) continue;
            if (Physics.Linecast(origin + Vector3.up * 0.7f,
                col.transform.position + Vector3.up * 0.7f, Layers.WallMask, QueryTriggerInteraction.Ignore)) continue;

            var health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;
            if (!_damaged.Add(health)) continue;

            Vector3 hitPoint = col.ClosestPoint(origin);
            if (!health.TakeDamage(damage, hitPoint)) continue;

            anyHit = true;
            Connected?.Invoke(hitPoint);

            var enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null) enemy.ApplyKnockback(toTarget.normalized * knockback);
        }

        if (anyHit)
        {
            Sound.Play(Sfx.Hit);
            Juice.HitStop(0.055f);
            Juice.Shake(0.35f);
        }
    }

    /// <summary>
    /// Catch any arrow inside the swing and send it back.
    ///
    /// The catch window is deliberately more forgiving than the damage cone — wider arc and
    /// a little more reach. An arrow is a 12 cm object that may be crossing the screen, and a
    /// deflection that only worked on a frame-perfect read would be a trick shot rather than
    /// the defensive answer this is meant to be.
    ///
    /// Arrows already travelling for the player are skipped, so a swing cannot volley its own
    /// returned arrow back and forth.
    /// </summary>
    private void ResolveDeflections()
    {
        Vector3 origin = transform.position;
        Vector3 forward = _player != null ? _player.AimDirection : transform.forward;
        Vector3 eye = origin + Vector3.up * 0.7f;

        // Iterate backwards: Deflect can destroy an arrow outright when it impacts immediately,
        // and the list is maintained by OnDisable.
        for (int i = Projectile.Active.Count - 1; i >= 0; i--)
        {
            Projectile arrow = Projectile.Active[i];
            if (arrow == null || arrow.Deflected) continue;

            Vector3 to = arrow.transform.position - origin;
            to.y = 0f;
            if (to.sqrMagnitude > deflectRange * deflectRange) continue;
            if (to.sqrMagnitude > 0.0001f &&
                Vector3.Angle(forward, to.normalized) > deflectArcDegrees * 0.5f) continue;

            // Same wall rule as melee: you cannot parry through stone.
            if (Physics.Linecast(eye, arrow.transform.position, Layers.WallMask,
                                 QueryTriggerInteraction.Ignore)) continue;

            Vector3 deflectionPoint = arrow.transform.position;
            arrow.Deflect(forward);
            Deflections++;

            Sound.Play(Sfx.Hit, 0.7f);
            GameVfx.Emit(VfxKind.Deflect, deflectionPoint, forward);
            Juice.HitStop(0.04f);
            Juice.Shake(0.25f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, range);

        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, deflectRange);
    }
}

