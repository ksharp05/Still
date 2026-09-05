using UnityEngine;

/// <summary>
/// A guttering light bracketed to the wall beside a doorway.
///
/// This is the one piece of dressing that earns its own GameObject: everything else in the
/// environment kit is static and gets welded into the merged meshes, but a brazier needs an
/// Update and a real Light, and a doorway you can see burning from down a corridor is worth
/// the cost. The budget is deliberately small — see <see cref="MaxPerFloor"/>.
///
/// It hangs at head height with no collider at all. Nothing an actor can walk into should be
/// bracketed to a wall they have to squeeze past.
/// </summary>
public class Brazier : MonoBehaviour
{
    /// <summary>Room lights already run to a dozen a floor, plus one per pickup. Stay modest.</summary>
    public const int MaxPerFloor = 6;

    /// <summary>Bowl centre. Clears a 1.8 m actor, so the prop never needs a collider.</summary>
    public const float MountHeight = 2.05f;

    private Light _light;
    private Transform _bowl;
    private Vector3 _bowlScale;
    private float _phase;
    private float _offset;

    public static Brazier Spawn(Vector3 wallFace, Vector3 intoRoom, Transform parent)
    {
        var go = new GameObject("Brazier");
        go.transform.SetParent(parent, true);
        go.transform.position = wallFace + intoRoom * 0.28f + Vector3.up * MountHeight;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.42f, 0.2f, 0.42f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = Palette.SconceMat;
        Destroy(visual.GetComponent<Collider>()); // decoration only — nothing may snag on it

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 7.5f;
        light.shadows = LightShadows.None;

        // Cold blue, never warm. Palette.Ranged is orange, and a warm flame at eye height
        // would read at a glance as an archer standing in the doorway.
        light.color = Palette.Sconce;

        var brazier = go.AddComponent<Brazier>();
        brazier._light = light;
        brazier._bowl = visual.transform;
        brazier._bowlScale = visual.transform.localScale;
        brazier._offset = Random.Range(0f, Mathf.PI * 2f); // desync so a row does not pulse in lockstep
        go.AddComponent<VfxEmitter>().Effect = VfxEmitter.Kind.Brazier;
        return brazier;
    }

    private void Update()
    {
        // World time, not real time: a brazier is scenery and should slow when the world does.
        // Stairs deliberately take the opposite choice, because the exit must always call you
        // forward even while you stand still.
        float dt = WorldTime.DeltaTime;
        if (dt <= 0f) return;

        _phase += dt * 5.3f;
        float flicker = 0.5f + 0.5f * Mathf.Sin(_phase) * Mathf.Sin(_phase * 2.37f + _offset);

        // Dimmer while time is frozen, so standing still visibly cools the room as well as
        // slowing it.
        float cool = Mathf.Lerp(0.62f, 1f, WorldTime.Flow);

        _light.intensity = Mathf.Lerp(0.55f, 1.15f, flicker) * cool;
        _bowl.localScale = _bowlScale * (1f + flicker * 0.06f);
    }
}
