using System;
using UnityEngine;

/// <summary>
/// Decides how fast the world is allowed to run, and writes it into <see cref="WorldTime"/>.
///
/// Movement is the main driver. Swinging the mouse counts for a little too — otherwise
/// standing still to line up a shot feels completely dead. Attacks and dashes fire an
/// <see cref="Impulse"/> that forces full speed briefly, so your own actions always
/// resolve at full pace even from a dead stop.
///
/// The ramp is deliberately asymmetric: time spins up fast so the game feels responsive,
/// and winds down slower so you can read what just happened.
/// </summary>
public class TimeDirector : MonoBehaviour
{
    public static TimeDirector Instance { get; private set; }

    [SerializeField] private float spinUp = 16f;
    [SerializeField] private float windDown = 7f;

    [Tooltip("How much aiming (mouse motion) counts toward advancing time, 0..1.")]
    [SerializeField] private float aimInfluence = 0.3f;

    [Tooltip("Mouse delta magnitude that counts as 'full' aim intent.")]
    [SerializeField] private float aimReference = 6f;

    /// <summary>Supplied by the player: 0 = standing still, 1 = full movement input.</summary>
    private Func<float> _intentSource;

    private float _impulseUntil;

    private void Awake()
    {
        Instance = this;
        WorldTime.ResetToFrozen();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>The player registers here so we can read movement intent without a hard reference.</summary>
    public void SetIntentSource(Func<float> source) => _intentSource = source;
    public void ResetClock() { _impulseUntil = 0f; WorldTime.ResetToFrozen(); }

    /// <summary>Force time to full speed for a moment (used by attacks and dashes).</summary>
    public void Impulse(float seconds)
    {
        _impulseUntil = Mathf.Max(_impulseUntil, Time.unscaledTime + seconds);
    }

    private void Update()
    {
        if (Juice.TimeLocked) return;
        float intent = _intentSource != null ? Mathf.Clamp01(_intentSource()) : 0f;

        // Aiming nudges time forward a little so lining up a shot isn't a total freeze.
        float mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")).magnitude;
        intent = Mathf.Max(intent, Mathf.Clamp01(mouseDelta / aimReference) * aimInfluence);

        if (Time.unscaledTime < _impulseUntil) intent = 1f;

        float target = Mathf.Lerp(WorldTime.Frozen, 1f, intent);
        float rate = target > WorldTime.Scale ? spinUp : windDown;

        // Frame-rate independent exponential smoothing, on UNSCALED delta —
        // this clock must never depend on the clock it is driving.
        float t = 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime);
        WorldTime.Scale = Mathf.Lerp(WorldTime.Scale, target, t);
    }
}
