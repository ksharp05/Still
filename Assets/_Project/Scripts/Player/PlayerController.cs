using UnityEngine;

/// <summary>
/// Movement, aiming and dashing.
///
/// Note every timing here uses <see cref="Time.deltaTime"/>, never WorldTime — the player
/// is the one thing in the game that always runs at full speed. That asymmetry is the
/// entire feel of STILL.
///
/// Acceleration is high but not instant: enough weight that stopping reads as a decision,
/// not enough to feel sluggish. Dash gives i-frames and shoves time to full speed, so a
/// dash out of a frozen arrow storm actually resolves.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 9f;

    [Tooltip("Fraction of full speed while the walk key is held. This is the one number that " +
             "makes 'moving calmly' expressible: it scales the input vector, and because " +
             "MoveIntent01 IS that vector's magnitude, it slows the world by the same amount.")]
    [SerializeField] private float walkFactor = 0.30f;
    [SerializeField] private float acceleration = 95f;
    [SerializeField] private float deceleration = 75f;
    [SerializeField] private float turnSpeed = 24f;
    [SerializeField] private float gravity = -28f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 27f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.5f;

    /// <summary>Where the mouse is pointing, flattened to the ground plane.</summary>
    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    /// <summary>0 when standing still, 1 at full input. TimeDirector reads this.</summary>
    public float MoveIntent01 { get; private set; }

    /// <summary>True while the player is deliberately moving slowly.</summary>
    public bool IsWalking { get; private set; }

    /// <summary>Fraction of full speed a walk moves at, and therefore how fast the world runs.</summary>
    public float WalkFactor => walkFactor;

    public bool IsDashing => _dashTimer > 0f;
    public float DashReady01 => 1f - Mathf.Clamp01((_dashReadyAt - Time.time) / dashCooldown);

    /// <summary>
    /// Recompute tunables from the authored values. A zero bonus restores the original exactly.
    ///
    /// The cooldown is floored well above zero on purpose: <see cref="DashReady01"/> divides by
    /// it, and a zero would put NaN straight into a HUD localScale.
    /// </summary>
    public void ApplyTuning(float dashCooldownReduction)
    {
        dashCooldown = Mathf.Max(0.2f, _baseDashCooldown - dashCooldownReduction);
    }

    public float DashCooldown => dashCooldown;

    public void ResetMotion()
    {
        _velocity = Vector3.zero;
        _verticalSpeed = 0f;
        _dashTimer = 0f;
        _dashReadyAt = 0f;
        MoveIntent01 = 0f;
        IsWalking = false;
        if (_health != null) _health.Invulnerable = false;
        if (_trail != null) { _trail.emitting = false; _trail.Clear(); }
    }

    private void OnDisable() => ResetMotion();
    public event System.Action Dashed;

    /// <summary>Authored dash cooldown, so upgrades can be recomputed rather than accumulated.</summary>
    private float _baseDashCooldown;

    private CharacterController _cc;
    private Health _health;
    private Camera _cam;
    private TrailRenderer _trail;

    private Vector3 _velocity;      // horizontal only
    private float _verticalSpeed;
    private Vector3 _dashDirection;
    private float _dashTimer;
    private float _dashReadyAt;

    private void Awake()
    {
        _baseDashCooldown = dashCooldown;

        _cc = GetComponent<CharacterController>();
        _health = GetComponent<Health>();
        // Look it up by name: the player owns two trails (this one and the blade tip),
        // and GetComponentInChildren would grab whichever happened to be active first.
        Transform dashTrail = transform.Find("DashTrail");
        _trail = dashTrail != null ? dashTrail.GetComponent<TrailRenderer>() : null;
        if (_trail != null) _trail.emitting = false;
    }

    private void Start()
    {
        _cam = Camera.main;
        // Hand our movement intent to the clock without either side holding a reference.
        if (TimeDirector.Instance != null)
            TimeDirector.Instance.SetIntentSource(() => MoveIntent01);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.CanPlay) return;
        ReadAim();

        Vector3 input = ReadMoveInput();

        // Walking scales the input vector rather than the speed, and that is the whole trick:
        // UpdateWalk targets `input * moveSpeed`, and MoveIntent01 is the same vector's
        // magnitude, so one multiply slows the player and the world together. Move at 30% and
        // the world runs at 30% — which is the only way to cross a room full of suspended
        // arrows while they stay suspended.
        IsWalking = !IsDashing && Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.01f;
        if (IsWalking) input *= walkFactor;

        MoveIntent01 = IsDashing ? 1f : Mathf.Clamp01(input.magnitude);

        TryStartDash(input);

        if (IsDashing) UpdateDash();
        else UpdateWalk(input);

        ApplyGravity();
        FaceAim();
    }

    private Vector3 ReadMoveInput()
    {
        // World-aligned on purpose: the camera never rotates, so W is always "up the screen".
        var raw = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        return raw.sqrMagnitude > 1f ? raw.normalized : raw;
    }

    private void ReadAim()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // Intersect the mouse ray with the plane the player stands on.
        var ground = new Plane(Vector3.up, transform.position);
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (ground.Raycast(ray, out float dist))
        {
            Vector3 point = ray.GetPoint(dist);
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) AimDirection = dir.normalized;
        }
    }

    private void TryStartDash(Vector3 input)
    {
        // Space only. LeftShift used to be an undocumented alias for dash, but it now holds a
        // walk — leaving both bound would fire a dash every time the player started walking.
        bool pressed = Input.GetKeyDown(KeyCode.Space);
        if (!pressed || IsDashing || Time.time < _dashReadyAt) return;

        _dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : AimDirection;
        _dashTimer = dashDuration;
        _dashReadyAt = Time.time + dashCooldown;

        if (_health != null) _health.Invulnerable = true;
        if (_trail != null) _trail.emitting = true;

        // A dash should always resolve at full speed, even from a dead stop.
        if (TimeDirector.Instance != null) TimeDirector.Instance.Impulse(dashDuration);

        Dashed?.Invoke();
        GameVfx.Emit(VfxKind.Dash, transform.position, _dashDirection);
    }

    private void UpdateDash()
    {
        _dashTimer -= Time.deltaTime;
        _velocity = _dashDirection * dashSpeed;
        _cc.Move(_velocity * Time.deltaTime);

        if (_dashTimer <= 0f)
        {
            GameVfx.Emit(VfxKind.DashEnd, transform.position, _dashDirection);
            if (_health != null) _health.Invulnerable = false;
            if (_trail != null) _trail.emitting = false;
            // Bleed out of the dash rather than stopping dead.
            _velocity = _dashDirection * moveSpeed;
        }
    }

    private void UpdateWalk(Vector3 input)
    {
        Vector3 target = input * moveSpeed;
        float rate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;

        _velocity = Vector3.MoveTowards(_velocity, target, rate * Time.deltaTime);
        _cc.Move(_velocity * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
        _verticalSpeed += gravity * Time.deltaTime;
        _cc.Move(Vector3.up * (_verticalSpeed * Time.deltaTime));
    }

    private void FaceAim()
    {
        if (AimDirection.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(AimDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }
}
