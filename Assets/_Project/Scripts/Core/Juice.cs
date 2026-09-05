using System.Collections;
using UnityEngine;

/// <summary>Hit-stop and camera impulse. Particle presentation is owned by GameVfx.</summary>
public class Juice : MonoBehaviour
{
    private static Juice _instance;
    [SerializeField] private float hitStopScale = .03f;
    private Coroutine _hitStopRoutine;
    public static bool TimeLocked { get; set; }
    private void Awake() => _instance = this;
    private void OnDestroy() { if (_instance == this) _instance = null; }
    public static void Shake(float strength) { if (CameraRig.Instance != null) CameraRig.Instance.Shake(strength); }
    public static void Sparks(Vector3 position, Color color, int count = 10, float speed = 6f)
        => GameVfx.LegacyBurst(position, color, count, speed);
    public static void HitStop(float seconds)
    {
        if (_instance == null || TimeLocked) return;
        if (_instance._hitStopRoutine != null) _instance.StopCoroutine(_instance._hitStopRoutine);
        _instance._hitStopRoutine = _instance.StartCoroutine(_instance.HitStopRoutine(seconds));
    }
    private IEnumerator HitStopRoutine(float seconds)
    {
        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(seconds);
        if (!TimeLocked) Time.timeScale = 1f;
        _hitStopRoutine = null;
    }
}
