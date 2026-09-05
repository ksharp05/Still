using UnityEngine;

/// <summary>
/// The way down. Stepping on it immediately generates the next floor.
///
/// You can leave at any time, even with the floor full of enemies — that's the run's
/// central gamble. Shards are the score, and they're lying around in rooms you'd have
/// to fight through. Descending early is safe and poor; clearing out is rich and risky.
/// </summary>
public class Stairs : MonoBehaviour
{
    public static bool NearExit { get; private set; }
    private void OnDisable() { NearExit = false; }
    private Transform _ring;
    private Light _light;
    private float _phase;
    private bool _used;

    public static Stairs Spawn(Vector3 position, Transform parent)
    {
        var go = new GameObject("Stairs");
        go.transform.position = position;
        if (parent != null) go.transform.SetParent(parent, true);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        pad.transform.SetParent(go.transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        pad.transform.localScale = new Vector3(2.2f, 0.06f, 2.2f);
        pad.GetComponent<Renderer>().sharedMaterial = Palette.ExitMat;
        Destroy(pad.GetComponent<Collider>());

        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.3f;
        trigger.center = new Vector3(0f, 0.5f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 11f;
        light.intensity = 3f;
        light.color = Palette.Exit;

        var stairs = go.AddComponent<Stairs>();
        stairs._ring = pad.transform;
        stairs._light = light;
        go.AddComponent<VfxEmitter>().Effect = VfxEmitter.Kind.Exit;
        return stairs;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        NearExit = !_used && gm != null && gm.CanPlay && GameManager.PlayerTransform != null &&
            Vector3.Distance(GameManager.PlayerTransform.position, transform.position) < 2.3f;
        if (NearExit && Input.GetKeyDown(KeyCode.E))
        {
            _used = true;
            NearExit = false;
            Sound.Play(Sfx.Descend, 0.85f);
            gm.BeginDescent(); // the threshold screen confirms the actual descent
            return;
        }
        // Real time, unlike everything else in the world — the exit should always be
        // visibly alive, so it reads as the one thing calling you forward.
        _phase += Time.deltaTime * 2.2f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_phase);

        if (_light != null) _light.intensity = Mathf.Lerp(2.2f, 4.4f, pulse);
        if (_ring != null)
        {
            float s = Mathf.Lerp(2.1f, 2.45f, pulse);
            _ring.localScale = new Vector3(s, 0.06f, s);
        }
    }

}

