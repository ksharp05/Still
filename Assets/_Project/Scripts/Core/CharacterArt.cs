using UnityEngine;

/// <summary>Loads the Blender-authored models and assigns native URP materials.</summary>
public static class CharacterArt
{
    private static Material _armor, _edge, _cloth;
    private static readonly System.Collections.Generic.Dictionary<Color, Material> Accents = new System.Collections.Generic.Dictionary<Color, Material>();
    public static bool Attach(Transform actor, string asset, Color accent)
    {
        var source = Resources.Load<GameObject>("Characters/" + asset);
        if (source == null) return false;
        var visual = Object.Instantiate(source, actor, false);
        visual.name = "CharacterArt";
        if (_armor == null) _armor = Palette.Lit(new Color(.12f, .18f, .23f), .45f, .65f);
        if (_edge == null) _edge = Palette.Lit(new Color(.5f, .61f, .66f), .55f, .55f);
        if (_cloth == null) _cloth = Palette.Lit(new Color(.025f, .045f, .065f), .1f, 0f);
        if (!Accents.TryGetValue(accent, out var glow) || glow == null)
        {
            glow = Palette.Lit(accent, .45f, .3f, accent, 1.8f);
            Accents[accent] = glow;
        }
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i] != null ? materials[i].name : "Armor";
                materials[i] = name.StartsWith("Core") ? glow : name.StartsWith("Edge") ? _edge : name.StartsWith("Cloth") ? _cloth : _armor;
            }
            renderer.sharedMaterials = materials;
        }
        // Explicit marker supports models with lights on both their front and back.
        Transform facing = null;
        foreach (var t in visual.GetComponentsInChildren<Transform>())
            if (t.name == "FacingMarker") { facing = t; break; }
        if (facing != null)
        {
            if (actor.InverseTransformPoint(facing.position).z < 0)
                visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
        else
        {
            foreach (var t in visual.GetComponentsInChildren<Renderer>())
                if (t.name.StartsWith("Core") && actor.InverseTransformPoint(t.bounds.center).z < 0)
                { visual.transform.localRotation = Quaternion.Euler(0, 180, 0); break; }
        }
        visual.AddComponent<CharacterMotion>();
        return true;
    }
}

/// <summary>Subtle movement feedback using actual displacement and the actor's own clock.</summary>
public class CharacterMotion : MonoBehaviour
{
    private Vector3 _last;
    private float _phase;
    private PlayerController _player;
    private void Start() { _last = transform.parent.position; _player = GetComponentInParent<PlayerController>(); }
    private void LateUpdate()
    {
        float dt = _player != null ? Time.deltaTime : WorldTime.DeltaTime;
        Vector3 now = transform.parent.position;
        float distance = new Vector2(now.x - _last.x, now.z - _last.z).magnitude;
        _last = now;
        if (dt <= 0f) return;
        _phase += distance * 7f;
        float bob = distance > .001f ? Mathf.Abs(Mathf.Sin(_phase)) * .045f : 0f;
        transform.localPosition = new Vector3(0, bob, 0);
    }
}
