using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    Swing,
    Hit,
    EnemyDie,
    ArrowFire,
    ArrowHit,
    Pickup,
    Descend,
    PlayerHurt,
    Dash,
    PlayerDie,
}

/// <summary>
/// Every sound in this game is generated from maths at startup — there is not a single
/// audio file in the project.
///
/// The payoff isn't just "no assets to download": because we own the AudioSources, world
/// sounds get their pitch scaled by <see cref="WorldTime"/>. Freeze time and an arrow
/// firing across the room drops into a low growl; move, and the whole mix snaps back up.
/// That one line does more for the feel of the mechanic than any visual effect.
/// </summary>
public class Sound : MonoBehaviour
{
    private const int Rate = 44100;

    private static Sound _instance;
    private static readonly System.Random Rng = new System.Random(1337);

    private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
    private AudioSource[] _pool;
    private AudioSource _ambient;
    private int _next;

    private void Awake()
    {
        _instance = this;
        BuildClips();
        BuildSources();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // The drone is part of the world, so it slows with it.
        if (_ambient != null)
            _ambient.pitch = Mathf.Lerp(0.45f, 1f, WorldTime.Flow);
    }

    /// <param name="worldPitched">
    /// True for things the world does (arrows, enemies) — these slow down with time.
    /// False for the player's own actions, which always happen at full speed.
    /// </param>
    public static void Play(Sfx sfx, float volume = 1f, bool worldPitched = false)
    {
        if (_instance != null) _instance.PlayInternal(sfx, volume, worldPitched);
    }

    private void PlayInternal(Sfx sfx, float volume, bool worldPitched)
    {
        if (!_clips.TryGetValue(sfx, out AudioClip clip) || clip == null) return;

        AudioSource src = _pool[_next];
        _next = (_next + 1) % _pool.Length;

        src.pitch = worldPitched
            ? Mathf.Lerp(0.4f, 1f, WorldTime.Flow) * Random.Range(0.96f, 1.04f)
            : Random.Range(0.96f, 1.04f); // slight variation so repeats don't sound robotic

        src.PlayOneShot(clip, volume);
    }

    private void BuildSources()
    {
        _pool = new AudioSource[12];
        for (int i = 0; i < _pool.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            _pool[i] = src;
        }

        _ambient = gameObject.AddComponent<AudioSource>();
        _ambient.clip = BuildDrone();
        _ambient.loop = true;
        _ambient.volume = 0.16f;
        _ambient.spatialBlend = 0f;
        _ambient.Play();
    }

    private void BuildClips()
    {
        _clips[Sfx.Swing]      = BuildWhoosh("swing", 0.20f, 0.03f, 0.34f, 0.45f);
        _clips[Sfx.Dash]       = BuildWhoosh("dash", 0.30f, 0.015f, 0.22f, 0.6f);
        _clips[Sfx.Hit]        = BuildHit();
        _clips[Sfx.EnemyDie]   = BuildEnemyDie();
        _clips[Sfx.ArrowFire]  = BuildSweep("arrowFire", 0.10f, 1500f, 700f, 0.28f);
        _clips[Sfx.ArrowHit]   = BuildSweep("arrowHit", 0.09f, 900f, 300f, 0.32f);
        _clips[Sfx.Pickup]     = BuildPickup();
        _clips[Sfx.Descend]    = BuildDescend();
        _clips[Sfx.PlayerHurt] = BuildHurt();
        _clips[Sfx.PlayerDie]  = BuildPlayerDie();
    }

    // ---- synthesis --------------------------------------------------------

    private static AudioClip Finish(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

    /// <summary>Noise pushed through a one-pole lowpass that opens then closes — reads as "air".</summary>
    private static AudioClip BuildWhoosh(string name, float dur, float cutoffMin, float cutoffMax, float gain)
    {
        int n = (int)(Rate * dur);
        var d = new float[n];
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float cutoff = Mathf.Lerp(cutoffMin, cutoffMax, Mathf.Sin(u * Mathf.PI));
            lp += (Noise() - lp) * cutoff;

            float env = Mathf.Sin(u * Mathf.PI);
            d[i] = lp * env * env * gain;
        }
        return Finish(name, d);
    }

    /// <summary>A sine sweeping between two frequencies, with an exponential decay.</summary>
    private static AudioClip BuildSweep(string name, float dur, float fromHz, float toHz, float gain)
    {
        int n = (int)(Rate * dur);
        var d = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = Mathf.Lerp(fromHz, toHz, u * u);
            phase += 2f * Mathf.PI * f / Rate;

            float env = Mathf.Exp(-u * 6f);
            d[i] = Mathf.Sin(phase) * env * gain;
        }
        return Finish(name, d);
    }

    /// <summary>Low body thump plus a bright transient click — the classic "impact" recipe.</summary>
    private static AudioClip BuildHit()
    {
        float dur = 0.24f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = Mathf.Lerp(210f, 65f, Mathf.Sqrt(u)); // pitch drop = weight
            phase += 2f * Mathf.PI * f / Rate;

            float body = Mathf.Sin(phase) * Mathf.Exp(-u * 9f) * 0.55f;
            float click = Noise() * Mathf.Exp(-u * 55f) * 0.4f;
            d[i] = body + click;
        }
        return Finish("hit", d);
    }

    private static AudioClip BuildEnemyDie()
    {
        float dur = 0.42f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float phase = 0f, lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = Mathf.Lerp(320f, 45f, u);
            phase += 2f * Mathf.PI * f / Rate;

            lp += (Noise() - lp) * 0.25f;
            float env = Mathf.Exp(-u * 5f);
            d[i] = (Mathf.Sin(phase) * 0.4f + lp * 0.45f) * env;
        }
        return Finish("enemyDie", d);
    }

    /// <summary>Rising two-note bell. Bright, short, unmistakably "good thing happened".</summary>
    private static AudioClip BuildPickup()
    {
        float dur = 0.30f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float p1 = 0f, p2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = u < 0.4f ? 784f : 1175f; // G5 then D6
            p1 += 2f * Mathf.PI * f / Rate;
            p2 += 2f * Mathf.PI * f * 2f / Rate;

            float env = Mathf.Exp(-u * 7f);
            d[i] = (Mathf.Sin(p1) * 0.5f + Mathf.Sin(p2) * 0.18f) * env * 0.5f;
        }
        return Finish("pickup", d);
    }

    /// <summary>A rising major triad — the reward for finding the stairs.</summary>
    private static AudioClip BuildDescend()
    {
        float dur = 0.85f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float[] freqs = { 261.6f, 329.6f, 392.0f, 523.3f };
        var phases = new float[freqs.Length];

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float sum = 0f;

            for (int k = 0; k < freqs.Length; k++)
            {
                // Each note enters a beat after the last.
                float start = k * 0.16f;
                if (u < start) continue;

                phases[k] += 2f * Mathf.PI * freqs[k] / Rate;
                sum += Mathf.Sin(phases[k]) * Mathf.Exp(-(u - start) * 3.2f);
            }
            d[i] = sum * 0.22f * Mathf.Min(1f, (1f - u) * 6f);
        }
        return Finish("descend", d);
    }

    /// <summary>Harsh detuned buzz — deliberately unpleasant.</summary>
    private static AudioClip BuildHurt()
    {
        float dur = 0.26f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float p1 = 0f, p2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            p1 += 2f * Mathf.PI * 132f / Rate;
            p2 += 2f * Mathf.PI * 139f / Rate; // slight detune = beating = grating

            float square = Mathf.Sign(Mathf.Sin(p1)) * 0.3f + Mathf.Sign(Mathf.Sin(p2)) * 0.3f;
            d[i] = square * Mathf.Exp(-u * 8f) * 0.5f;
        }
        return Finish("hurt", d);
    }

    private static AudioClip BuildPlayerDie()
    {
        float dur = 1.6f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float phase = 0f, lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float f = Mathf.Lerp(220f, 28f, Mathf.Sqrt(u)); // the long slide down
            phase += 2f * Mathf.PI * f / Rate;

            lp += (Noise() - lp) * 0.06f;
            float env = Mathf.Exp(-u * 2.2f);
            d[i] = (Mathf.Sin(phase) * 0.5f + lp * 0.35f) * env;
        }
        return Finish("playerDie", d);
    }

    /// <summary>
    /// Looping ambient drone. Frequencies are exact multiples of 1/duration so the loop
    /// point lands on a zero crossing for every partial — no click on wrap.
    /// </summary>
    private static AudioClip BuildDrone()
    {
        float dur = 4f;
        int n = (int)(Rate * dur);
        var d = new float[n];
        float[] freqs = { 55f, 82.5f, 110f, 164.5f };

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;

            for (int k = 0; k < freqs.Length; k++)
            {
                // Slow amplitude drift per partial keeps it from sounding static.
                float lfo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (0.25f * (k + 1)) * t);
                sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * lfo / freqs.Length;
            }
            d[i] = sum * 0.6f;
        }
        return Finish("drone", d);
    }
}

