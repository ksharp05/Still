using UnityEngine;

/// <summary>Small ambient sources share the central banks and obey distance/time budgets.</summary>
public class VfxEmitter : MonoBehaviour
{
    public enum Kind { Brazier, Exit, Shard, Heart }
    public Kind Effect;
    private float _timer, _phase;
    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.CanPlay) return;
        float dt = Effect == Kind.Exit ? Time.deltaTime : WorldTime.DeltaTime;
        _timer -= dt; _phase += dt*3;
        if (_timer > 0) return;
        _timer = Effect == Kind.Brazier ? .16f : Effect == Kind.Exit ? (HUD.ReducedEffects ? .4f : .14f) : .7f;
        GameVfx.Ambient(transform.position,Effect,_phase);
    }
}
