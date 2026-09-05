using UnityEngine;

public enum EnemyKind
{
    /// <summary>Runs at you and swings. Fast, fragile, dangerous in packs.</summary>
    Melee,

    /// <summary>Keeps its distance and fires arrows. The reason frozen time matters.</summary>
    Ranged,
}

/// <summary>
/// A small state machine: Idle -> Chase -> Windup -> Strike -> Recover.
///
/// Every timer and every step of movement is multiplied by <see cref="WorldTime.DeltaTime"/>.
/// That single decision is what makes the game work — stop moving and an enemy freezes
/// mid-lunge, arrow half-drawn, and stays there until you move again.
///
/// The Windup state exists purely for readability. In frozen time you need to be able to
/// look at a room and understand what is about to happen to you, so every attack telegraphs
/// with a visible swell before it lands.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Idle, Chase, Windup, Recover, Stagger }

    /// <summary>
    /// Extra reach the melee strike actually has beyond <c>attackRange</c>, which is the range it
    /// commits from. Named because three things must agree on it: the damage check, the slash
    /// visual, and the telegraph. A telegraph that drew the smaller number would be a lie — the
    /// player would stand in a spot the game had shown as safe and be hit anyway.
    /// </summary>
    private const float StrikeOverreach = 0.6f;

    /// <summary>Angular width of the melee swing, shared by the strike visual and the telegraph.</summary>
    private const float StrikeArcDegrees = 110f;

    /// <summary>
    /// Angular width of an archer's telegraphed firing lane. Narrow on purpose: an arrow travels
    /// a line, and a wide cone would overstate the threat and make cover look useless.
    /// </summary>
    private const float LaneDegrees = 5f;

    [Header("Identity")]
    [SerializeField] private EnemyKind kind = EnemyKind.Melee;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float aggroRange = 22f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2.4f;
    [SerializeField] private float windupTime = 0.55f;
    [SerializeField] private float recoverTime = 0.5f;
    [SerializeField] private int damage = 1;

    [Header("Ranged")]
    [SerializeField] private float preferredRange = 11f;
    [SerializeField] private float projectileSpeed = 15f;

    private State _state = State.Idle;
    private float _stateTimer;
    private Vector3 _knockback;

    private CharacterController _cc;
    private Health _health;
    private Renderer _renderer;
    private EnemyTelegraph _telegraph;
    private MaterialPropertyBlock _mpb;
    private Color _baseEmission;
    private Vector3 _baseScale;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<Health>();
        _renderer = GetComponentInChildren<Renderer>();
        foreach (var candidate in GetComponentsInChildren<Renderer>())
            if (candidate.name.StartsWith("Core")) { _renderer = candidate; break; }
        _telegraph = gameObject.AddComponent<EnemyTelegraph>();
        _mpb = new MaterialPropertyBlock();
        _baseScale = transform.localScale;

        if (_renderer != null && _renderer.sharedMaterial != null &&
            _renderer.sharedMaterial.HasProperty(EmissionColorId))
            _baseEmission = _renderer.sharedMaterial.GetColor(EmissionColorId);
    }

    private void OnEnable()
    {
        _health.Died += HandleDeath;
        _health.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        _health.Died -= HandleDeath;
        _health.Damaged -= HandleDamaged;
    }

    /// <summary>Stats scale with depth so floor 8 is genuinely nastier than floor 1.</summary>
    public void Configure(EnemyKind enemyKind, int floor)
    {
        kind = enemyKind;
        float scale = 1f + (floor - 1) * 0.11f;

        if (kind == EnemyKind.Melee)
        {
            moveSpeed = 4.2f + floor * 0.16f;
            attackRange = 2.4f;
            windupTime = Mathf.Max(0.3f, 0.55f - floor * 0.02f);
            recoverTime = 0.5f;
            damage = 1;
            _health.Configure(Mathf.RoundToInt(3 * scale));
        }
        else
        {
            moveSpeed = 3.1f;
            attackRange = 14f;
            windupTime = Mathf.Max(0.45f, 0.85f - floor * 0.025f);
            recoverTime = 0.9f;
            damage = 1;
            projectileSpeed = 14f + floor * 0.5f;
            _health.Configure(Mathf.RoundToInt(2 * scale));
        }
    }

    public void ApplyKnockback(Vector3 impulse)
    {
        _knockback += impulse;
        EnterState(State.Stagger, 0.22f);
    }

    private void Update()
    {
        float dt = WorldTime.DeltaTime;
        if (dt <= 0f) return;

        Transform player = GameManager.PlayerTransform;
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 direction = distance > 0.001f ? toPlayer / distance : transform.forward;

        _stateTimer -= dt;
        ApplyKnockbackStep(dt);

        switch (_state)
        {
            case State.Idle:
                if (distance < aggroRange && HasLineOfSight(player)) EnterState(State.Chase, 0f);
                break;

            case State.Chase:
                TickChase(dt, direction, distance, player);
                break;

            case State.Windup:
                FaceDirection(direction, dt);
                PulseTelegraph();
                ShowThreat(direction);
                if (_stateTimer <= 0f) Strike(direction, player, distance);
                break;

            case State.Recover:
                if (_stateTimer <= 0f) EnterState(State.Chase, 0f);
                break;

            case State.Stagger:
                if (_stateTimer <= 0f) EnterState(State.Chase, 0f);
                break;
        }

        ApplyGravity(dt);
    }

    private void TickChase(float dt, Vector3 direction, float distance, Transform player)
    {
        FaceDirection(direction, dt);

        bool inRange = distance <= attackRange;
        bool canSee = HasLineOfSight(player);

        if (kind == EnemyKind.Melee)
        {
            if (inRange && canSee) { EnterState(State.Windup, windupTime); return; }
            Step(direction, moveSpeed, dt);
        }
        else
        {
            // Archers back off when crowded and close in when too far — they want a firing lane.
            if (distance < preferredRange * 0.65f) Step(-direction, moveSpeed, dt);
            else if (distance > preferredRange) Step(direction, moveSpeed, dt);
            else Strafe(direction, dt);

            if (inRange && canSee) EnterState(State.Windup, windupTime);
        }
    }

    private void Strike(Vector3 direction, Transform player, float distance)
    {
        if (kind == EnemyKind.Melee)
        {
            GameVfx.Slash(transform.position, direction, attackRange + StrikeOverreach, StrikeArcDegrees, true);
            if (distance <= attackRange + StrikeOverreach && HasLineOfSight(player) && GameManager.PlayerHealth != null)
            {
                if (GameManager.PlayerHealth.TakeDamage(damage, player.position))
                {
                    Sound.Play(Sfx.PlayerHurt);
                    Juice.Shake(0.8f);
                    Juice.HitStop(0.05f);
                }
            }
            Sound.Play(Sfx.Swing, 0.7f, worldPitched: true);
        }
        else
        {
            Vector3 origin = transform.position + Vector3.up * 0.2f + direction * 0.8f;
            Projectile.Spawn(origin, direction, projectileSpeed, damage);
            Sound.Play(Sfx.ArrowFire, 0.8f, worldPitched: true);
        }

        ClearTelegraph();
        EnterState(State.Recover, recoverTime);
    }

    // ---- movement helpers -------------------------------------------------

    private void Step(Vector3 direction, float speed, float dt)
    {
        Vector3 motion = direction * (speed * dt);
        _cc.Move(motion);

        // Nudge sideways when we scrape a wall, so enemies round corners instead of grinding.
        if ((_cc.collisionFlags & CollisionFlags.Sides) != 0)
        {
            Vector3 slide = Vector3.Cross(Vector3.up, direction);
            _cc.Move(slide * (speed * dt * 0.7f));
        }
    }

    private void Strafe(Vector3 direction, float dt)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        // Deterministic per-enemy direction, so a group fans out instead of clumping.
        float sign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        _cc.Move(side * (sign * moveSpeed * 0.55f * dt));
    }

    private void ApplyKnockbackStep(float dt)
    {
        if (_knockback.sqrMagnitude < 0.01f) return;
        _cc.Move(_knockback * dt);
        _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, 22f * dt);
    }

    private void ApplyGravity(float dt)
    {
        if (!_cc.isGrounded) _cc.Move(Vector3.up * (-18f * dt));
    }

    private void FaceDirection(Vector3 direction, float dt)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * dt));
    }

    private bool HasLineOfSight(Transform player)
    {
        Vector3 from = transform.position + Vector3.up * 0.5f;
        Vector3 to = player.position + Vector3.up * 0.5f;
        return !Physics.Linecast(from, to, Layers.WallMask, QueryTriggerInteraction.Ignore);
    }

    // ---- presentation -----------------------------------------------------

    private void EnterState(State next, float duration)
    {
        if (next == State.Windup)
            GameVfx.Emit(VfxKind.Charge, transform.position + Vector3.up, tint: kind == EnemyKind.Melee ? Palette.Melee : Palette.Ranged);
        _state = next;
        _stateTimer = duration;
        if (next != State.Windup) ClearTelegraph();
    }

    /// <summary>Swell in brightness and size as the attack charges. This is the player's warning.</summary>
    private void PulseTelegraph()
    {
        if (_renderer == null || windupTime <= 0f) return;

        float charge = 1f - Mathf.Clamp01(_stateTimer / windupTime);
        float boost = 1f + charge * charge * 5f;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColorId, _baseEmission * boost);
        _renderer.SetPropertyBlock(_mpb);

        transform.localScale = _baseScale * (1f + charge * 0.16f);
    }

    /// <summary>
    /// Paint the ground this attack is about to cover.
    ///
    /// The radii come from the same constants the strike uses, so what is drawn is exactly what
    /// is dangerous. A brute shows the wedge it will sweep; an archer shows the lane its arrow
    /// will travel, which is the one piece of information a frozen room was never giving you.
    /// </summary>
    private void ShowThreat(Vector3 direction)
    {
        if (_telegraph == null || windupTime <= 0f) return;

        float charge = 1f - Mathf.Clamp01(_stateTimer / windupTime);
        Vector3 origin = transform.position;

        if (kind == EnemyKind.Melee)
            _telegraph.Show(origin, direction, attackRange + StrikeOverreach, StrikeArcDegrees, Palette.Melee, charge);
        else
            _telegraph.Show(origin + Vector3.up * 0.2f, direction, attackRange, LaneDegrees, Palette.Ranged, charge);
    }

    private void ClearTelegraph()
    {
        transform.localScale = _baseScale;
        if (_telegraph != null) _telegraph.Hide();
        if (_renderer == null) return;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColorId, _baseEmission);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void HandleDamaged(Vector3 point)
    {
        Color c = kind == EnemyKind.Melee ? Palette.Melee : Palette.Ranged;
        Vector3 direction = GameManager.PlayerTransform != null ? (transform.position - GameManager.PlayerTransform.position).normalized : Vector3.zero;
        GameVfx.Emit(VfxKind.Hit, point + Vector3.up * .3f, direction, c);
    }

    private void HandleDeath()
    {
        Color c = kind == EnemyKind.Melee ? Palette.Melee : Palette.Ranged;
        GameVfx.Emit(VfxKind.EnemyDeath, transform.position + Vector3.up * .6f, tint: c);
        Juice.Shake(0.5f);
        Sound.Play(Sfx.EnemyDie, 0.9f, worldPitched: true);

        GameEvents.RaiseEnemyKilled(transform.position);
        Destroy(gameObject);
    }
}

