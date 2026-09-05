using UnityEngine;

/// <summary>
/// Every material in the game, built in code — there are no art assets to import.
///
/// The art direction follows the mechanic: the world itself is cold, dark and nearly
/// colourless, and everything that MOVES is hot and emissive. So when time is frozen
/// the screen is almost monochrome, and when you move it lights up. Bloom does the
/// rest of the work for us.
///
/// Materials are created once and reused across floors and restarts.
/// </summary>
public static class Palette
{
    // Cold world.
    public static readonly Color Wall      = new Color(0.20f, 0.25f, 0.32f);
    public static readonly Color WallTop   = new Color(0.29f, 0.36f, 0.44f);
    public static readonly Color Floor     = new Color(0.19f, 0.24f, 0.29f);

    // Architecture kit. Rooms are paved and slightly warmer than the raw corridor rock,
    // the border trim rings every walkable edge, and the cornice catches the room lights
    // along the top of every wall. Together they tell you at a glance whether you are
    // standing in a room or a passage.
    public static readonly Color FloorRoom = new Color(0.225f, 0.268f, 0.318f);
    public static readonly Color FloorTrim = new Color(0.145f, 0.183f, 0.232f);
    public static readonly Color Cornice   = new Color(0.355f, 0.430f, 0.520f);
    public static readonly Color Pillar    = new Color(0.265f, 0.320f, 0.390f);
    public static readonly Color Sconce    = new Color(0.330f, 0.620f, 0.980f);

    /// <summary>Threshold paving under a doorway — the brightest stone in the kit, because
    /// from a top-down camera a bright stripe across an opening is the cheapest possible
    /// "this is a door".</summary>
    public static readonly Color Threshold = new Color(0.300f, 0.365f, 0.440f);

    /// <summary>Broken flagstones scattered on the paving. Darker than the floor it sits on,
    /// so it reads as debris rather than as something you could stand behind.</summary>
    public static readonly Color Debris    = new Color(0.172f, 0.208f, 0.252f);

    // Hot actors.
    public static readonly Color Player    = new Color(0.909f, 0.956f, 1f);
    public static readonly Color PlayerGlow= new Color(0.298f, 0.784f, 1f);
    public static readonly Color Melee     = new Color(1f, 0.231f, 0.188f);
    public static readonly Color Ranged    = new Color(1f, 0.584f, 0f);
    public static readonly Color Arrow     = new Color(1f, 0.901f, 0.427f);
    public static readonly Color Shard     = new Color(0.301f, 0.815f, 0.882f);
    public static readonly Color Heart     = new Color(1f, 0.361f, 0.541f);
    public static readonly Color Exit      = new Color(0.223f, 1f, 0.541f);
    public static readonly Color Blade     = new Color(0.792f, 0.937f, 1f);

    private static Material _floorRoom, _floorTrim, _cornice, _pillar, _sconce, _threshold, _debris;
    private static Material _floor, _wall, _wallTop, _player, _melee, _ranged, _arrow, _shard, _heart, _exit, _blade;

    public static Material FloorMat   { get { if (_floor == null) _floor = Stone(Floor, 0.22f, 0f, ProceduralTexture.Paving, PavingMetres, 1.1f); return _floor; } }
    public static Material WallMat    { get { if (_wall == null) _wall = Stone(Wall, 0.30f, 0f, ProceduralTexture.Rock, RockMetres, 1.4f); return _wall; } }
    public static Material WallTopMat { get { if (_wallTop == null) _wallTop = Stone(WallTop, 0.40f, 0.1f, ProceduralTexture.Rock, RockMetres, 0.8f); return _wallTop; } }

    /// <summary>Paving inside rooms. Corridors keep the plainer <see cref="FloorMat"/>.</summary>
    public static Material FloorRoomMat { get { if (_floorRoom == null) _floorRoom = Stone(FloorRoom, 0.28f, 0.05f, ProceduralTexture.Paving, PavingMetres, 1.1f); return _floorRoom; } }
    public static Material FloorTrimMat { get { if (_floorTrim == null) _floorTrim = Stone(FloorTrim, 0.16f, 0f, ProceduralTexture.Paving, PavingMetres, 1.1f); return _floorTrim; } }
    public static Material CorniceMat   { get { if (_cornice == null) _cornice = Stone(Cornice, 0.45f, 0.15f, ProceduralTexture.Rock, RockMetres, 0.8f); return _cornice; } }
    public static Material ThresholdMat { get { if (_threshold == null) _threshold = Stone(Threshold, 0.42f, 0.08f, ProceduralTexture.Paving, PavingMetres, 0.5f); return _threshold; } }
    public static Material DebrisMat    { get { if (_debris == null) _debris = Stone(Debris, 0.14f, 0f, ProceduralTexture.Rock, RockMetres, 0.7f); return _debris; } }
    public static Material PillarMat    { get { if (_pillar == null) _pillar = Stone(Pillar, 0.38f, 0.1f, ProceduralTexture.Rock, RockMetres, 1.4f); return _pillar; } }

    /// <summary>
    /// Emissive sconce bowls. Pure bloom, no real light, so a colonnade costs nothing.
    ///
    /// Kept deliberately dim and firmly blue. Bloom threshold is 0.75, and anything above it
    /// blows out to white and starts reading as a pickup — the one thing the palette must
    /// never do is make scenery look collectable. 0.45 keeps every channel under that line,
    /// so a sconce lifts off the wall without ever competing with a shard.
    /// </summary>
    public static Material SconceMat    => _sconce    ?? (_sconce    = Lit(Sconce, 0.6f, 0f, Sconce, 0.45f));
    public static Material PlayerMat  => _player  ?? (_player  = Lit(Player, 0.55f, 0f, PlayerGlow, 1.4f));
    public static Material MeleeMat   => _melee   ?? (_melee   = Lit(Melee, 0.45f, 0f, Melee, 1.1f));
    public static Material RangedMat  => _ranged  ?? (_ranged  = Lit(Ranged, 0.45f, 0f, Ranged, 1.1f));
    public static Material ArrowMat   => _arrow   ?? (_arrow   = Lit(Arrow, 0.6f, 0f, Arrow, 3.5f));
    public static Material ShardMat   => _shard   ?? (_shard   = Lit(Shard, 0.7f, 0f, Shard, 3f));
    public static Material HeartMat   => _heart   ?? (_heart   = Lit(Heart, 0.7f, 0f, Heart, 3f));
    public static Material ExitMat    => _exit    ?? (_exit    = Lit(Exit, 0.7f, 0f, Exit, 2.5f));
    public static Material BladeMat   => _blade   ?? (_blade   = Lit(Blade, 0.8f, 0.2f, Blade, 2.2f));

    /// <summary>Paving repeats every four gameplay tiles, so its grooves land on tile joins.</summary>
    private const float PavingMetres = 6.4f;

    /// <summary>Rock repeats every wall height, so the stone meets the cornice exactly.</summary>
    private const float RockMetres = 3.2f;

    /// <summary>
    /// A Lit material dressed with one of the generated stone families.
    ///
    /// Note the base colour is divided by the texture's mean. An 8-bit albedo cannot hold a
    /// multiplier above 1, so its mean necessarily sits below one and would drag the whole
    /// world darker than the palette was tuned for; dividing it back out keeps every colour
    /// where it was chosen.
    /// </summary>
    public static Material Stone(Color baseColor, float smoothness, float metallic,
                                 in ProceduralTexture.Surface surface,
                                 float metresPerRepeat, float bumpScale)
    {
        float mean = surface.AlbedoMean > 0.01f ? surface.AlbedoMean : 1f;
        Material m = Lit(baseColor / mean, smoothness, metallic);

        if (surface.Albedo != null && m.HasProperty("_BaseMap"))
        {
            m.SetTexture("_BaseMap", surface.Albedo);

            // Only _BaseMap has a tiling property in URP Lit: every map samples the UV that
            // _BaseMap_ST produced, so setting a scale on any other map is a silent no-op.
            m.SetTextureScale("_BaseMap", Vector2.one / metresPerRepeat);

            // Smoothness out of the albedo's alpha. The keyword is what does it; the float
            // just keeps the inspector honest.
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetFloat("_SmoothnessTextureChannel", 1f);
        }

        if (surface.Normal != null && m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", surface.Normal);
            m.SetFloat("_BumpScale", bumpScale);

            // Without this keyword URP's SampleNormal returns a hardcoded flat normal and the
            // bump map does nothing at all — no warning, no error, just a flat wall.
            m.EnableKeyword("_NORMALMAP");
        }

        return m;
    }

    /// <summary>
    /// Build a URP Lit material. Falls back to the built-in Standard shader so the
    /// project still runs (just flatter) if URP somehow isn't active.
    /// </summary>
    public static Material Lit(Color baseColor, float smoothness, float metallic,
                               Color? emission = null, float emissionIntensity = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);

        // URP and Standard disagree on property names, so set both where they differ.
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

        if (emission.HasValue && emissionIntensity > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", emission.Value * emissionIntensity);
        }

        return m;
    }

    /// <summary>
    /// Material for trails and flashes.
    ///
    /// "Sprites/Default" is the deliberate choice here: it is alpha-blended and respects
    /// per-vertex colour out of the box, and it renders correctly under URP. The URP Unlit
    /// shader is opaque by default and ignores vertex colour, which would turn every soft
    /// trail into a hard white slab.
    /// </summary>
    public static Material Glow(Color c, float intensity)
    {
        Shader shader = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color");

        var m = new Material(shader);
        Color hot = c * intensity;
        if (m.HasProperty("_Color")) m.SetColor("_Color", hot);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hot);
        return m;
    }

    /// <summary>
    /// Material for the spark particle system. Left white so each particle's own
    /// start colour comes through — that's what lets one system serve every effect.
    /// </summary>
    public static Material Particle()
    {
        Shader shader = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color");

        var m = new Material(shader);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        return m;
    }
}
