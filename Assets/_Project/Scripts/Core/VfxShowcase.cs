using System;
using UnityEngine;

/// <summary>
/// Plays every effect in turn so they can be looked at side by side.
///
/// Added at runtime by <c>STILL/VFX/Play effects demo</c> and removes itself after one pass, so
/// the menu item can simply be run again. It lives in the runtime assembly rather than the editor
/// one because an editor script can reference a runtime type but not the reverse.
///
/// Everything here runs on unscaled time: the effects being demonstrated are themselves gated on
/// <see cref="GameVfx.Speed"/>, and a demo driver that stopped whenever they did could never show
/// the frozen-world behaviour that is the point of having three clocks.
/// </summary>
public class VfxShowcase : MonoBehaviour
{
    /// <summary>Seconds each effect is left on screen before the next one.</summary>
    private const float Dwell = 1.15f;

    /// <summary>How far from the player each effect is staged, so they do not overlap.</summary>
    private const float Radius = 2.6f;

    private VfxKind[] _kinds;
    private int _index = -1;
    private float _nextAt;

    private void Start()
    {
        _kinds = (VfxKind[])Enum.GetValues(typeof(VfxKind));

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameManager.RunState.Playing)
        {
            // Worth saying plainly: world and player banks are held at zero speed outside a live
            // run, so the demo would emit correctly and appear to do nothing at all.
            Debug.LogWarning("VFX demo: start a run first — outside Playing, every clock but Death is stopped.");
        }

        Debug.Log($"VFX demo: {_kinds.Length} effects, then two slashes. About {(_kinds.Length + 2) * Dwell:F0}s.");
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAt) return;
        _nextAt = Time.unscaledTime + Dwell;
        _index++;

        Transform player = GameManager.PlayerTransform;
        if (player == null) { Debug.LogWarning("VFX demo: no player."); Destroy(this); return; }

        // Staged in a ring around the player rather than all on top of them, so a burst and the
        // ring underneath it stay legible.
        float angle = _index * 0.9f;
        Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        Vector3 at = player.position + direction * Radius + Vector3.up * 0.7f;

        if (_index < _kinds.Length)
        {
            VfxKind kind = _kinds[_index];
            Debug.Log($"VFX demo [{_index + 1}/{_kinds.Length + 2}]: {kind}");
            GameVfx.Emit(kind, at, direction);
            return;
        }

        switch (_index - _kinds.Length)
        {
            case 0:
                Debug.Log("VFX demo: player slash");
                GameVfx.Slash(player.position, direction, 3.1f, 120f);
                return;
            case 1:
                Debug.Log("VFX demo: hostile slash");
                GameVfx.Slash(player.position, -direction, 2.4f, 110f, hostile: true);
                return;
            default:
                Debug.Log("VFX demo: complete.");
                Destroy(this);
                return;
        }
    }
}
