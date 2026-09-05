using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An arrow. Builds its own visuals, so there is no prefab to wire up.
///
/// Movement is raycast-driven rather than physics-driven: at very low world speeds a
/// Rigidbody would jitter or tunnel, and we need arrows to hang in the air perfectly
/// still for as long as the player stands there. A ray per step is also exact — no
/// tunnelling through walls at high speed either.
///
/// The trail is left on real time on purpose. When frozen it retracts to nothing and you
/// get a crisp arrow suspended mid-air; when time flows it stretches into a streak. That
/// contrast is free and it sells the whole mechanic.
///
/// An arrow can change sides. <see cref="Deflect"/> hands it to the player: it turns to face
/// a new target, switches which layers it can hit, and repaints itself in the blade colour.
/// That is the one way the player gets reach in a melee-only game, and it costs no new
/// weapon — the ammunition was already in the air, fired by the archer it comes back to.
/// </summary>
public class Projectile : MonoBehaviour
{
    private const float Radius = 0.12f;

    /// <summary>Damage a returned arrow carries. Enemies have 2-7 HP, so this is worth the swing.</summary>
    public const int DeflectedDamage = 3;

    /// <summary>A returned arrow flies harder than it arrived.</summary>
    private const float DeflectSpeedScale = 1.35f;

    /// <summary>How far a deflection looks for something to aim at.</summary>
    private const float SeekRange = 22f;

    /// <summary>
    /// Every arrow currently in flight.
    ///
    /// Arrows carry no collider at all — they raycast themselves — so there is nothing for
    /// an overlap query to find. Rather than give them one, which would put them on a physics
    /// layer and risk disturbing the line-of-sight and cover rules everything else depends on,
    /// they simply publish themselves here for the sword to look through.
    /// </summary>
    public static readonly List<Projectile> Active = new List<Projectile>();

    /// <summary>True once the player has returned this arrow; it then hunts enemies instead.</summary>
    public bool Deflected { get; private set; }

    public Vector3 Direction => _direction;

    private Vector3 _direction;
    private float _speed;
    private int _damage;
    private float _life = 9f;

    public static Projectile Spawn(Vector3 position, Vector3 direction, float speed, int damage)
    {
        var go = new GameObject("Arrow");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Parent to the floor so arrows in flight are cleaned up on descent rather than
        // following you down the stairs.
        if (GameManager.FloorRoot != null) go.transform.SetParent(GameManager.FloorRoot, true);

        // Body: a thin stretched cube reads as an arrow at this camera distance.
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.09f, 0.09f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = Palette.ArrowMat;
        Destroy(body.GetComponent<Collider>()); // we do our own raycasting

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0f;
        trail.sharedMaterial = GameVfx.Instance != null ? GameVfx.Instance.EnemyTrail : Palette.Glow(Palette.Arrow, 2.5f);
        trail.numCapVertices = 3;
        trail.minVertexDistance = 0.06f;

        var projectile = go.AddComponent<Projectile>();
        projectile._body = body.GetComponent<Renderer>();
        projectile._direction = direction.normalized;
        projectile._speed = speed;
        projectile._damage = damage;
        GameVfx.Emit(VfxKind.Muzzle, position, direction);
        return projectile;
    }

    private Renderer _body;

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    /// <summary>
    /// Send this arrow back out under the player's ownership.
    ///
    /// It aims at whatever enemy it can actually reach — an arrow that pinged off in a
    /// cosmetically satisfying direction and hit nothing would teach the player that
    /// deflection does not work. With nothing in reach it simply reverses, which still
    /// clears the air and can still catch whoever fired it.
    /// </summary>
    public void Deflect(Vector3 fallbackDirection)
    {
        Deflected = true;
        _damage = DeflectedDamage;
        _speed *= DeflectSpeedScale;
        _life = Mathf.Max(_life, 4f); // never expire in the player's hand a frame after the parry

        Vector3 aim = FindTarget(out Transform target) ? (target.position + Vector3.up * 0.7f - transform.position) : -_direction;
        if (aim.sqrMagnitude < 0.0001f) aim = fallbackDirection;

        aim.y = 0f;
        _direction = aim.normalized;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

        // Repaint so a returned arrow is unmistakably the player's.
        if (_body != null) _body.sharedMaterial = Palette.BladeMat;
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) { trail.sharedMaterial = GameVfx.Instance != null ? GameVfx.Instance.PlayerTrail : Palette.Glow(Palette.Blade, 3f); trail.Clear(); }
    }

    /// <summary>Nearest living enemy with clear line of sight from this arrow.</summary>
    private bool FindTarget(out Transform target)
    {
        target = null;
        float best = float.MaxValue;

        var hits = Physics.OverlapSphere(transform.position, SeekRange, Layers.EnemyMask,
                                         QueryTriggerInteraction.Collide);
        foreach (Collider col in hits)
        {
            var health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead) continue;

            Vector3 to = col.transform.position + Vector3.up * 0.7f - transform.position;
            if (to.sqrMagnitude >= best) continue;

            // Do not aim through a wall; the arrow would only thud into it.
            if (Physics.Linecast(transform.position, col.transform.position + Vector3.up * 0.7f,
                                 Layers.WallMask, QueryTriggerInteraction.Ignore)) continue;

            best = to.sqrMagnitude;
            target = col.transform;
        }
        return target != null;
    }

    private void Update()
    {
        float dt = WorldTime.DeltaTime;
        if (dt <= 0f) return;

        _life -= dt;
        if (_life <= 0f) { Destroy(gameObject); return; }

        float step = _speed * dt;

        // Look ahead by exactly this frame's travel, so nothing is ever skipped over.
        // A deflected arrow swaps which side it can hurt — it will not turn on the player.
        int mask = Layers.WallMask | (Deflected ? Layers.EnemyMask : Layers.PlayerMask);
        // Enemy hitboxes are triggers, so a returned arrow has to be allowed to see them.
        if (Physics.SphereCast(transform.position, Radius, _direction, out RaycastHit hit, step, mask,
                               Deflected ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore))
        {
            Impact(hit.point, hit.collider);
            return;
        }

        transform.position += _direction * step;
    }

    private void Impact(Vector3 point, Collider other)
    {
        var health = other != null ? other.GetComponentInParent<Health>() : null;

        if (health != null && !health.IsDead && health.TakeDamage(_damage, point))
        {
            if (Deflected)
            {
                Sound.Play(Sfx.Hit);
                Juice.HitStop(0.05f);
                Juice.Shake(0.3f);
                // Health's damage callback already creates the impact effect.

                var enemy = other.GetComponentInParent<EnemyAI>();
                if (enemy != null) enemy.ApplyKnockback(_direction * 4f);
            }
            else
            {
                Sound.Play(Sfx.PlayerHurt);
                Juice.Shake(0.8f);
                Juice.HitStop(0.05f);
                // Player damage feedback is emitted once by GameManager.
            }
        }
        else
        {
            Sound.Play(Sfx.ArrowHit, 0.5f, worldPitched: true);
            GameVfx.Emit(VfxKind.WallHit, point, _direction);
        }

        Destroy(gameObject);
    }
}

