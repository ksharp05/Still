using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Every texture in the game, generated in code — there are no image files to import, for
/// the same reason there are no material assets: the whole look is authored in one place.
///
/// Two stone families, built once and shared by every floor and every restart.
///
/// The map that matters here is the NORMAL map, not the albedo. This game tonemaps through
/// ACES, runs contrast 6, and desaturates to −68 the moment the player stands still. Any
/// albedo mottling gets crushed into the same near-black wedge and vanishes exactly when the
/// world is frozen and most needs to read. A normal map instead modulates how a surface
/// answers light, and there is real light direction here — a point light per room, plus a
/// cold directional — so the variation survives desaturation because it IS luminance
/// structure rather than colour. The albedo comes along for free off the same height field
/// and does the one job normals cannot: breaking up flat colour under the ambient term.
/// </summary>
public static class ProceduralTexture
{
    /// <summary>512 across a 6.4 m repeat is about 1.25 cm per texel.</summary>
    public const int Resolution = 512;

    /// <summary>
    /// The albedo's alpha drives smoothness and is capped at 1, so it can only ever dull a
    /// highlight, never sharpen one. That is what makes it arithmetically impossible for
    /// scenery to bloom its way into looking collectable.
    /// </summary>
    public const float MaxSmoothnessScale = 1f;

    private enum Style { Paving, Rock }

    // Fixed, not per-floor. Per-floor seeds would mean new textures and new materials on
    // every staircase — a quarter-second hitch each descent, buying variety nobody asked for.
    private const int PavingSeed = 0x5A17;
    private const int RockSeed = 0x1D0C;

    /// <summary>One generated stone family: two maps cut from a single height field.</summary>
    public readonly struct Surface
    {
        public readonly Texture2D Albedo;   // sRGB,   RGB = value multiplier, A = smoothness scale
        public readonly Texture2D Normal;   // linear, tangent space, A = 255
        public readonly float AlbedoMean;   // linear mean, so Palette can divide it back out
        public readonly float SeamError;    // 0 when the field tiles exactly

        public Surface(Texture2D albedo, Texture2D normal, float albedoMean, float seamError)
        {
            Albedo = albedo;
            Normal = normal;
            AlbedoMean = albedoMean;
            SeamError = seamError;
        }
    }

    /// <summary>
    /// Tuning aid: keeps the CPU copy of each map so the editor can dump them to disk.
    /// Off in a normal run, where the CPU copies are freed as soon as they reach the GPU.
    /// </summary>
    public static bool KeepReadable;

    private static Surface _paving, _rock;
    private static bool _built;

    public static bool IsBuilt => _built;
    public static float GenerationMilliseconds { get; private set; }

    public static Surface Paving { get { Warm(); return _paving; } }
    public static Surface Rock { get { Warm(); return _rock; } }

    /// <summary>
    /// Generate now if it has not happened yet. Bootstrap calls this so the cost lands on
    /// the title screen, where the simulation is already stopped and a hitch is invisible.
    /// </summary>
    public static void Warm()
    {
        if (_built) return;
        _built = true;

        var watch = Stopwatch.StartNew();
        // Contrast is generous because the albedo is a multiplier whose mean gets divided
        // back out in Palette.Stone: a narrow range survives neither gamma encoding nor the
        // bloom, and washes out to a flat sheet exactly where the detail was supposed to be.
        _paving = Build(PavingSeed, Style.Paving, contrast: 0.50f, smoothLo: 0.62f, bump: 2.4f);
        _rock = Build(RockSeed, Style.Rock, contrast: 0.42f, smoothLo: 0.66f, bump: 2.0f);
        GenerationMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
    }

    private static Surface Build(int seed, Style style, float contrast, float smoothLo, float bump)
    {
        int n = Resolution;
        var height = new float[n * n];

        float inv = 1f / n;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            height[y * n + x] = Height(x * inv, y * inv, style, seed);

        Texture2D albedo = BuildAlbedo(height, n, contrast, smoothLo, out float mean);
        Texture2D normal = BuildNormal(height, n, bump);
        return new Surface(albedo, normal, mean, SeamOf(style, seed));
    }

    // ---------------------------------------------------------------- height field

    /// <summary>
    /// The continuous height field, in normalised 0..1 texture space. Both maps are cut from
    /// this one function, which is what makes them read as a single material: the albedo's
    /// dark patches are exactly the normal map's hollows.
    /// </summary>
    private static float Height(float u, float v, Style style, int seed)
    {
        if (style == Style.Paving)
        {
            // Flagstones, with the grout following the cell edges.
            float cell = Worley(u, v, 6, seed, out float edge);
            float stone = 0.55f + cell * 0.18f;
            float grout = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 7f));

            // A 4x4 groove grid. The repeat is four gameplay tiles wide, so these land on
            // tile boundaries and the floor quietly draws the movement grid for us.
            float grooves = Mathf.Min(Groove(u, 4), Groove(v, 4));

            float grit = Fbm(u, v, 16, 3, 0.5f, seed + 91);
            return Mathf.Clamp01(stone * Mathf.Lerp(0.72f, 1f, grout) * Mathf.Lerp(0.82f, 1f, grooves)
                                 + (grit - 0.5f) * 0.13f);
        }

        // Rock: ridged strata with a soft horizontal coursing, so walls read as laid rather
        // than poured.
        float body = Fbm(u, v, 4, 4, 0.5f, seed);
        float ridge = 1f - Mathf.Abs(Fbm(u * 2f, v * 2f, 8, 2, 0.5f, seed + 17) * 2f - 1f);
        float course = 0.5f + 0.5f * Mathf.Cos(v * Mathf.PI * 2f * 3f);
        return Mathf.Clamp01(body * 0.55f + ridge * 0.3f + course * 0.15f);
    }

    /// <summary>A periodic pulse, dark in a narrow line at each repeat. Used for tile joints.</summary>
    private static float Groove(float t, int repeats)
    {
        float f = t * repeats;
        float d = Mathf.Abs(f - Mathf.Floor(f) - 0.5f) * 2f; // 0 at the joint, 1 mid-stone
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d * 9f));
    }

    /// <summary>
    /// Periodic value-noise fBm. Deliberately not Mathf.PerlinNoise: that cannot be made to
    /// tile, and a texture that does not tile shows a hard seam grid every repeat across a
    /// floor 124 m wide.
    /// </summary>
    private static float Fbm(float u, float v, int period, int octaves, float gain, int seed)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        int p = period;

        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(u, v, p, seed + o * 131) * amp;
            norm += amp;
            amp *= gain;
            p *= 2;
        }
        return sum / norm;
    }

    /// <summary>
    /// Value noise on a lattice of <paramref name="period"/> cells. The lattice index is
    /// wrapped before hashing but not before interpolating, which is the whole trick: the
    /// field is continuous everywhere and identical at 0 and 1.
    /// </summary>
    private static float ValueNoise(float u, float v, int period, int seed)
    {
        float gx = u * period, gy = v * period;
        int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
        float fx = gx - x0, fy = gy - y0;

        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float a = Hash01(x0, y0, period, seed);
        float b = Hash01(x0 + 1, y0, period, seed);
        float c = Hash01(x0, y0 + 1, period, seed);
        float d = Hash01(x0 + 1, y0 + 1, period, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>
    /// Worley F1 over a wrapped feature grid. <paramref name="edge"/> comes back as F2−F1,
    /// which is near zero along cell boundaries — the grout between flagstones.
    /// </summary>
    private static float Worley(float u, float v, int cells, int seed, out float edge)
    {
        float gx = u * cells, gy = v * cells;
        int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);

        float f1 = float.MaxValue, f2 = float.MaxValue;

        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = cx + dx, ny = cy + dy;

            // Unwrapped position, wrapped hash: neighbours across the seam line up exactly.
            float px = nx + Hash01(nx, ny, cells, seed);
            float py = ny + Hash01(nx, ny, cells, seed + 7717);

            float ex = px - gx, ey = py - gy;
            float d = Mathf.Sqrt(ex * ex + ey * ey);

            if (d < f1) { f2 = f1; f1 = d; }
            else if (d < f2) { f2 = d; }
        }

        edge = f2 - f1;
        return Mathf.Clamp01(f1);
    }

    private static float Hash01(int x, int y, int period, int seed)
    {
        int wx = Wrap(x, period), wy = Wrap(y, period);
        unchecked
        {
            int h = seed * 374761393 + wx * 668265263 + wy * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private static int Wrap(int v, int period)
    {
        int m = v % period;
        return m < 0 ? m + period : m;
    }

    /// <summary>
    /// Largest discontinuity across the wrap, sampled from the continuous field. A correctly
    /// periodic field gives exactly zero; anything that cannot tile shows up here rather than
    /// as a seam grid nobody notices until the floor is 78 tiles wide.
    /// </summary>
    private static float SeamOf(Style style, int seed)
    {
        float worst = 0f;
        for (int i = 0; i < 64; i++)
        {
            float t = i / 64f;
            worst = Mathf.Max(worst, Mathf.Abs(Height(0f, t, style, seed) - Height(1f, t, style, seed)));
            worst = Mathf.Max(worst, Mathf.Abs(Height(t, 0f, style, seed) - Height(t, 1f, style, seed)));
        }
        return worst;
    }

    // ---------------------------------------------------------------- maps

    /// <summary>
    /// Greyscale value multiplier in RGB, smoothness scale in alpha.
    ///
    /// The bytes are written verbatim — Unity applies no conversion — so the linear
    /// multiplier has to be gamma-encoded here by hand. Skip that and the shader decodes 0.5
    /// as 0.21, the whole world goes about 2.2x dark, and every tuned palette colour is wrong.
    /// </summary>
    private static Texture2D BuildAlbedo(float[] height, int n, float contrast, float smoothLo,
                                         out float meanLinear)
    {
        var px = new Color32[n * n];
        double sum = 0.0;

        for (int i = 0; i < px.Length; i++)
        {
            float h = height[i];
            float linear = Mathf.Lerp(1f - contrast, 1f, h);
            sum += linear;

            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(linear) * 255f), 0, 255);
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(smoothLo, MaxSmoothnessScale, h) * 255f), 0, 255);
            px[i] = new Color32(g, g, g, a);
        }

        meanLinear = (float)(sum / px.Length);
        return Commit(px, n, linear: false, "ProcAlbedo");
    }

    /// <summary>
    /// Tangent-space normals, Sobel-differenced off the same height field with wrapping so
    /// the map tiles as cleanly as the albedo.
    ///
    /// Alpha must be 255. URP unpacks desktop normal maps through UnpackNormalMapRGorAG,
    /// which multiplies X by W — a zero alpha silently flattens half the map with no error.
    /// </summary>
    private static Texture2D BuildNormal(float[] height, int n, float strength)
    {
        var px = new Color32[n * n];

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float l = height[y * n + Wrap(x - 1, n)];
            float r = height[y * n + Wrap(x + 1, n)];
            float d = height[Wrap(y - 1, n) * n + x];
            float up = height[Wrap(y + 1, n) * n + x];

            var normal = new Vector3((l - r) * strength, (d - up) * strength, 1f).normalized;
            px[y * n + x] = new Color32(Encode(normal.x), Encode(normal.y), Encode(normal.z), 255);
        }

        return Commit(px, n, linear: true, "ProcNormal");
    }

    private static byte Encode(float v) =>
        (byte)Mathf.Clamp(Mathf.RoundToInt(v * 127.5f + 127.5f), 0, 255);

    private static Texture2D Commit(Color32[] px, int n, bool linear, string name)
    {
        // Mips are not optional: without them a floor this size aliases into a shimmer that
        // the film grain then amplifies.
        var t = new Texture2D(n, n, TextureFormat.RGBA32, mipChain: true, linear: linear) { name = name };
        t.wrapMode = TextureWrapMode.Repeat;   // UVs run to well over 100 in world metres
        t.filterMode = FilterMode.Trilinear;   // floors are seen at grazing angles
        t.anisoLevel = 4;
        t.SetPixels32(px);
        t.Apply(updateMipmaps: true, makeNoLongerReadable: !KeepReadable);
        return t;
    }
}
