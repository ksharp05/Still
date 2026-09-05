using UnityEngine;

/// <summary>
/// The ground area an enemy is about to hit, drawn while it winds up.
///
/// The existing telegraph — an emissive swell and a scale pulse on the enemy itself — answers
/// *who* is attacking and roughly *when*. It never answered *where*, and in a game whose whole
/// premise is stopping to read a frozen room, where is the question that matters. A melee brute
/// threatens a three-metre wedge you cannot see; an archer threatens a line across the room that
/// is completely invisible until the arrow exists.
///
/// So this paints the threatened ground: a wedge for a swing, a narrow lane for a shot. Both are
/// clipped against walls per spoke, because a telegraph that reached through stone would teach
/// the player to distrust it — and a telegraph the player does not trust is worse than none.
///
/// Purely presentational: no collider, no physics, and it never decides anything. The numbers it
/// draws come from the same constants the strike itself uses.
/// </summary>
public class EnemyTelegraph : MonoBehaviour
{
    private const int Segments = 28;

    /// <summary>Sunk just above the floor so it reads as painted on the ground, not floating.</summary>
    private const float GroundOffset = 0.05f;

    /// <summary>Peak alpha at full charge. Deliberately restrained: this covers a lot of screen.</summary>
    private const float PeakAlpha = 0.34f;

    private Mesh _mesh;
    private MeshRenderer _renderer;
    private Vector3[] _vertices;
    private Color[] _colors;

    public bool Visible => _renderer != null && _renderer.enabled;

    /// <summary>The drawn surface, so validation can measure what the player is actually shown.</summary>
    public Renderer Surface => _renderer;

    private void Awake()
    {
        var host = GameVfx.Instance;
        if (host == null) { enabled = false; return; }

        // Parented to the VFX root rather than to the enemy, and fed world-space vertices. The
        // enemy's own transform is rotated by facing AND scaled by the windup pulse, either of
        // which would warp geometry parented under it.
        var go = new GameObject("Attack telegraph", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(host.transform, false);

        _mesh = new Mesh { name = "Telegraph wedge" };
        _mesh.MarkDynamic();

        // A fan: one centre vertex, then a rim.
        _vertices = new Vector3[Segments + 2];
        _colors = new Color[Segments + 2];

        var uv = new Vector2[Segments + 2];
        var triangles = new int[Segments * 3];
        for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0.5f, 0.5f); // flat shape mask; vertex colour does the shaping
        for (int i = 0; i < Segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        _mesh.vertices = _vertices;
        _mesh.uv = uv;
        _mesh.triangles = triangles;

        go.GetComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = go.GetComponent<MeshRenderer>();
        _renderer.sharedMaterial = host.Ribbon;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        Hide();
    }

    private void OnDestroy()
    {
        if (_renderer != null) Destroy(_renderer.gameObject);
        if (_mesh != null) Destroy(_mesh);
    }

    private void OnDisable() => Hide();

    /// <summary>
    /// Paint the threatened ground for this frame of the windup.
    ///
    /// <paramref name="charge01"/> runs 0 to 1 across the windup, and drives alpha on a curve so
    /// the warning starts as a hint and only becomes loud as the blow is about to land.
    /// </summary>
    public void Show(Vector3 origin, Vector3 forward, float radius, float degrees, Color color, float charge01)
    {
        if (_renderer == null) return;

        origin.y += GroundOffset;
        float facing = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        float alpha = PeakAlpha * Mathf.Pow(Mathf.Clamp01(charge01), 1.6f);
        if (HUD.ReducedEffects) alpha *= 0.5f;

        _vertices[0] = origin;
        _colors[0] = new Color(color.r, color.g, color.b, alpha * 0.25f);

        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float a = (facing - degrees * 0.5f + degrees * t) * Mathf.Deg2Rad;
            var spoke = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

            // Stop at the first wall, so the warning never claims ground it cannot reach.
            float reach = radius;
            if (Physics.Raycast(origin, spoke, out RaycastHit hit, radius, Layers.WallMask,
                                QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(0f, hit.distance - 0.05f);

            _vertices[i + 1] = origin + spoke * reach;

            // Fade to nothing at the angular edges so the wedge has a soft boundary rather than
            // a hard fan that reads as UI.
            float edge = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            _colors[i + 1] = new Color(color.r, color.g, color.b, alpha * Mathf.Max(0.15f, edge));
        }

        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
        _mesh.RecalculateBounds();
        _renderer.enabled = true;
    }

    public void Hide()
    {
        if (_renderer != null) _renderer.enabled = false;
    }
}
