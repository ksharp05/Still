using UnityEngine;

/// <summary>
/// Builds the player and the enemies entirely in code — no prefabs, nothing to wire.
///
/// Shape carries meaning here, because in a frozen tableau you read the room by silhouette:
/// the player is a bright capsule with a blade, melee enemies are red capsules, and archers
/// are orange diamonds. You should be able to pause and instantly know what's in the room.
/// </summary>
public static class ActorFactory
{
    public static GameObject CreatePlayer(Vector3 position)
    {
        var go = new GameObject("Player");
        go.transform.position = position;
        go.layer = Layers.Player;

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.35f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = Palette.PlayerMat;
        Object.Destroy(body.GetComponent<Collider>()); // the CharacterController is our collider

        // A small snout so facing is unmistakable even when standing still.
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose";
        nose.transform.SetParent(go.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.95f, 0.48f);
        nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.4f);
        nose.GetComponent<Renderer>().sharedMaterial = Palette.BladeMat;
        Object.Destroy(nose.GetComponent<Collider>());

        if (CharacterArt.Attach(go.transform, "Wanderer", Palette.PlayerGlow))
        {
            body.SetActive(false);
            nose.SetActive(false);
        }

        BuildBlade(go.transform);
        BuildDashTrail(go.transform);

        // The lamp lives on its own child. Putting it on the root and then offsetting
        // light.transform would move the PLAYER, since that transform is the player's.
        var lamp = new GameObject("Lamp");
        lamp.transform.SetParent(go.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 14f;
        light.intensity = 2.4f;
        light.color = Palette.PlayerGlow;

        go.AddComponent<Health>().Configure(6);
        go.AddComponent<PlayerController>();
        go.AddComponent<PlayerCombat>();

        // Last: its Awake captures the authored stats above as the values a reset returns to.
        go.AddComponent<PlayerUpgrades>();
        return go;
    }

    private static void BuildBlade(Transform parent)
    {
        var pivot = new GameObject("BladePivot");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Blade";
        blade.transform.SetParent(pivot.transform, false);
        blade.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        blade.transform.localScale = new Vector3(0.1f, 0.1f, 2.6f);
        blade.GetComponent<Renderer>().sharedMaterial = Palette.BladeMat;
        Object.Destroy(blade.GetComponent<Collider>());

        // Trail on the tip turns the swing into a visible arc rather than a popping box.
        var tip = new GameObject("Tip");
        tip.transform.SetParent(pivot.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 2.7f);

        var trail = tip.AddComponent<TrailRenderer>();
        trail.time = 0.16f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0f;
        trail.sharedMaterial = GameVfx.Instance != null ? GameVfx.Instance.PlayerTrail : Palette.Glow(Palette.Blade, 2.4f);
        trail.numCapVertices = 4;
        trail.minVertexDistance = 0.02f;
        trail.autodestruct = false;

        pivot.SetActive(false);
    }

    private static void BuildDashTrail(Transform parent)
    {
        var go = new GameObject("DashTrail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.startWidth = 0.75f;
        trail.endWidth = 0f;
        trail.sharedMaterial = GameVfx.Instance != null ? GameVfx.Instance.PlayerTrail : Palette.Glow(Palette.PlayerGlow, 2.2f);
        trail.numCapVertices = 4;
        trail.minVertexDistance = 0.05f;
        trail.emitting = false;
    }

    public static GameObject CreateEnemy(EnemyKind kind, Vector3 position, int floor, Transform parent)
    {
        var go = new GameObject(kind == EnemyKind.Melee ? "Brute" : "Archer");
        go.transform.position = position;
        go.layer = Layers.Enemy;
        if (parent != null) go.transform.SetParent(parent, true);

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.7f;
        cc.radius = 0.42f;
        cc.center = new Vector3(0f, 0.85f, 0f);

        // The sword finds enemies with an OverlapSphere, and a CharacterController is not
        // returned by overlap queries — so enemies carry an explicit capsule for hit detection.
        // It's a TRIGGER on purpose: a solid collider on the same object would fight the
        // CharacterController and make enemies jitter as they walk.
        var hitbox = go.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;
        hitbox.height = 1.7f;
        hitbox.radius = 0.45f;
        hitbox.center = new Vector3(0f, 0.85f, 0f);

        bool melee = kind == EnemyKind.Melee;
        var visual = GameObject.CreatePrimitive(melee ? PrimitiveType.Capsule : PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, melee ? 0.85f : 0.95f, 0f);
        visual.transform.localRotation = melee ? Quaternion.identity : Quaternion.Euler(0f, 45f, 45f);
        visual.transform.localScale = melee
            ? new Vector3(0.85f, 0.85f, 0.85f)
            : new Vector3(0.75f, 0.75f, 0.75f);
        visual.GetComponent<Renderer>().sharedMaterial = melee ? Palette.MeleeMat : Palette.RangedMat;
        Object.Destroy(visual.GetComponent<Collider>());

        if (CharacterArt.Attach(go.transform, melee ? "Sentinel" : "Archer", melee ? Palette.Melee : Palette.Ranged))
            visual.SetActive(false);

        go.AddComponent<Health>();
        go.AddComponent<EnemyAI>().Configure(kind, floor);
        return go;
    }
}
