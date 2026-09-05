using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real play-mode lifetime, clock, pooling and event-integration checks.</summary>
[InitializeOnLoad]
public static class VfxValidation
{
    private static int _step, _frames, _normalCount, _originalReduced;
    private static double _deadline;
    private static float _life;
    private static int _children;
    private static string _report = "";
    private static readonly ParticleSystem.Particle[] Buffer = new ParticleSystem.Particle[512];
    static VfxValidation()
    {
        if (!SessionState.GetBool("Still.VfxValidation", false)) return;
        _deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update += Tick;
    }
    public static void Run()
    {
        VfxAuthoring.EnsureTuning();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Still.unity");
        SessionState.SetBool("Still.VfxValidation", true);
        SessionState.SetInt("Still.VfxOriginalReduced", PlayerPrefs.GetInt("Still.ReducedEffects", 0));
        _deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); _report += "PASS " + message + "\n"; }
    private static ParticleSystem Bank(string name) => GameVfx.Instance.transform.Find("VFX " + name).GetComponent<ParticleSystem>();
    private static float Life(string name)
    { var b = Bank(name); return b.GetParticles(Buffer) > 0 ? Buffer[0].remainingLifetime : 0; }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("VFX validation timed out");
            if (!EditorApplication.isPlaying || GameManager.Instance == null) return;
            if (++_frames < 12) return; _frames = 0;
            var v = GameVfx.Instance; var gm = GameManager.Instance; var p = GameManager.PlayerTransform.position;
            switch (_step++)
            {
                case 0:
                    _originalReduced = SessionState.GetInt("Still.VfxOriginalReduced", 0);
                    PlayerPrefs.SetInt("Still.ReducedEffects", 0);
                    Check(v != null && v.enabled, "Particle service initialized");
                    Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("StillVfx")), "Particle shader compiles");
                    Check(Resources.Load<VfxTuning>("VfxTuning") != null, "Editable tuning asset exists");
                    Check(GameVfx.Speed(GameVfx.Clock.World) == 0 && GameVfx.Speed(GameVfx.Clock.Player) == 0, "Title freezes all live gameplay particles");
                    gm.StartRun();
                    TimeDirector.Instance.enabled = false; WorldTime.ResetToFrozen();
                    foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) e.enabled = false;
                    foreach (var e in UnityEngine.Object.FindObjectsByType<VfxEmitter>(FindObjectsSortMode.None)) e.enabled = false;
                    break;
                case 1:
                    Check(Mathf.Abs(v.WorldSimulationSpeed - WorldTime.Frozen) < .001f, "World bank crawls at the frozen-world rate");
                    Check(GameVfx.Speed(GameVfx.Clock.Player) == 1, "Player effects retain full speed in a frozen world");
                    v.Clear();
                    GameVfx.Emit(VfxKind.EnemyDeath, p + Vector3.up);
                    GameVfx.Emit(VfxKind.Deflect, p + Vector3.up);
                    GameVfx.Slash(p, Vector3.forward, 3.1f, 120);
                    gm.Pause();
                    break;
                case 2:
                    Check(v.WorldSimulationSpeed == 0, "Pause sets actual particle simulation speed to zero");
                    _life = Life("World Shard");
                    Check(_life > 0 && v.ActiveArcs == 1, "Particles and slash survive while paused");
                    break;
                case 3:
                    Check(Mathf.Abs(_life - Life("World Shard")) < .0001f, "Pause preserves particle lifetime across frames");
                    Check(v.ActiveArcs == 1, "Pause preserves slash lifetime across frames");
                    gm.Resume();
                    break;
                case 4:
                    Check(Life("World Shard") < _life, "World particles resume aging after unpause");
                    v.Clear(); _children = v.transform.childCount;
                    var rng = UnityEngine.Random.state;
                    float expected = UnityEngine.Random.value; UnityEngine.Random.state = rng;
                    foreach (VfxKind kind in Enum.GetValues(typeof(VfxKind))) GameVfx.Emit(kind, p);
                    Check(UnityEngine.Random.value == expected, "VFX does not perturb Unity gameplay random stream");
                    for (int i = 0; i < 300; i++)
                    { GameVfx.Emit(VfxKind.EnemyDeath, p); GameVfx.Emit(VfxKind.Deflect, p); GameVfx.Slash(p, Vector3.forward, 3, 120); }
                    Check(v.ActiveParticles <= GameVfx.ParticleCapacity, "Burst storm remains within 2,832 live particles");
                    Check(v.ActiveArcs <= GameVfx.ArcCapacity && v.transform.childCount == _children, "300 overlapping slashes reuse fixed object pool");
                    v.Clear();
                    int before = v.EmittedParticles; GameVfx.Emit(VfxKind.Deflect, p); _normalCount = v.EmittedParticles - before;
                    PlayerPrefs.SetInt("Still.ReducedEffects", 1);
                    break;
                case 5:
                    Check(v.ActiveArcs == 0 && Bank("Player Shard").particleCount == 0, "Reduced-effects toggle clears existing intense effects");
                    int start = v.EmittedParticles; GameVfx.Emit(VfxKind.Deflect, p);
                    Check(v.EmittedParticles - start < _normalCount / 2, "Reduced mode more than halves deflection particles");
                    Check(v.PlayerTrail.GetColor("_Tint").a < .5f, "Reduced mode dims shared trails");
                    start = v.EmittedParticles; GameVfx.Ambient(p, VfxEmitter.Kind.Brazier, 0);
                    Check(v.EmittedParticles == start, "Reduced mode suppresses decorative ambient emission");
                    gm.Descend();
                    Check(v.ActiveArcs == 0 && Bank("World Shard").particleCount == 0, "Floor transition removes outgoing effects");
                    Check(Bank("Player Ring").particleCount == 1, "Descent emits a fresh arrival ring on the new floor");
                    PlayerPrefs.SetInt("Still.ReducedEffects", 0);
                    break;
                case 6:
                    v.Clear();
                    GameManager.PlayerHealth.Invulnerable = false;
                    GameManager.PlayerHealth.TakeDamage(999, p);
                    Check(gm.IsGameOver && Bank("Death Shard").particleCount > 0, "Real player death emits particles after the world freezes");
                    break;
                case 7:
                    Check(Bank("Death Shard").main.simulationSpeed == 1 && v.WorldSimulationSpeed == 0, "Death bank advances while world bank stays frozen");
                    _life = Life("Death Ring");
                    Check(_life > 0, "Death ring remains visible during its authored lifetime");
                    break;
                case 8:
                    // Retry guard first: this step waits out the 0.6s post-death restart lockout,
                    // and an assertion above it would re-run on every retry and bury the report.
                    if (!gm.CanRestart) { _step--; break; }
                    Check(Life("Death Ring") < _life, "Death effect actually ages despite Time.timeScale zero");
                    gm.StartRun();
                    Check(Bank("Death Ring").particleCount == 0 && Bank("Death Shard").particleCount == 0, "Restart clears death particles");
                    Finish(0); break;
            }
        }
        catch (Exception e) { _report += "FAIL " + e + "\n"; Finish(1); }
    }
    private static void Finish(int code)
    {
        PlayerPrefs.SetInt("Still.ReducedEffects", _originalReduced);
        EditorApplication.update -= Tick; SessionState.SetBool("Still.VfxValidation", false);
        File.WriteAllText("Logs/vfx-checks.txt", _report); Debug.Log(_report); EditorApplication.Exit(code);
    }
}
