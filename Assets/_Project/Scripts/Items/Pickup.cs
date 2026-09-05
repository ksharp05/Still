using UnityEngine;

public enum PickupKind
{
    /// <summary>Score. The only thing you carry out of a run.</summary>
    Shard,

    /// <summary>Heals one point.</summary>
    Heart,
}

/// <summary>
/// A floating collectible. Builds its own visuals and spins on world time, so when you
/// stand still the whole room — pickups included — goes completely dead.
/// </summary>
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupKind kind = PickupKind.Shard;
    [SerializeField] private float spinSpeed = 110f;
    [SerializeField] private float bobHeight = 0.22f;
    [SerializeField] private float bobSpeed = 2.6f;

    private Transform _visual;
    private float _bobPhase;
    private float _baseY;
    private bool _consumed;

    public static Pickup Spawn(Vector3 position, PickupKind kind, Transform parent)
    {
        var go = new GameObject(kind == PickupKind.Shard ? "Shard" : "Heart");
        go.transform.position = position + Vector3.up * 0.6f;
        if (parent != null) go.transform.SetParent(parent, true);
        go.layer = Layers.Pickup;

        var visual = GameObject.CreatePrimitive(
            kind == PickupKind.Shard ? PrimitiveType.Cube : PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = kind == PickupKind.Shard
            ? new Vector3(0.32f, 0.32f, 0.32f)
            : new Vector3(0.42f, 0.42f, 0.42f);
        visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f); // shards read better on a corner
        visual.GetComponent<Renderer>().sharedMaterial =
            kind == PickupKind.Shard ? Palette.ShardMat : Palette.HeartMat;
        Destroy(visual.GetComponent<Collider>());

        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.85f;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 5f;
        light.intensity = 1.6f;
        light.color = kind == PickupKind.Shard ? Palette.Shard : Palette.Heart;

        var pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup._visual = visual.transform;
        pickup._baseY = go.transform.position.y;
        pickup._bobPhase = Random.Range(0f, Mathf.PI * 2f); // desync so a pile doesn't pulse in lockstep
        go.AddComponent<VfxEmitter>().Effect = kind == PickupKind.Shard ? VfxEmitter.Kind.Shard : VfxEmitter.Kind.Heart;
        return pickup;
    }

    private void Update()
    {
        float dt = WorldTime.DeltaTime;
        if (dt <= 0f) return;

        _bobPhase += bobSpeed * dt;

        if (_visual != null)
            _visual.Rotate(Vector3.up, spinSpeed * dt, Space.World);

        Vector3 p = transform.position;
        p.y = _baseY + Mathf.Sin(_bobPhase) * bobHeight;
        transform.position = p;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed || GameManager.Instance == null || !GameManager.Instance.CanPlay) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (kind == PickupKind.Heart && (GameManager.PlayerHealth == null ||
            GameManager.PlayerHealth.Current >= GameManager.PlayerHealth.Max)) return;
        _consumed = true;

        if (kind == PickupKind.Shard)
        {
            GameManager.Instance?.AddShard();
        }
        else
        {
            GameManager.PlayerHealth?.Heal(1);
        }

        Sound.Play(Sfx.Pickup, 0.7f);
        GameVfx.Emit(kind == PickupKind.Shard ? VfxKind.Shard : VfxKind.Heal, transform.position);
        Destroy(gameObject);
    }
}

