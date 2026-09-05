using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Makes the built player screenshot itself and quit. Enabled only by passing
/// <c>-autoshot &lt;directory&gt;</c> on the command line, so it is inert in a normal launch.
///
/// This exists because the editor's review renders cannot answer the one question that
/// matters about a shipped build: URP declares _EMISSION, _NORMALMAP and friends as
/// shader_feature, and Unity decides which variants to compile by scanning material assets
/// in the build. This project has none — every material is built in code — so the
/// keyword-on variants may legitimately be stripped, and URP then falls back to the
/// keyword-off variant in silence. The editor compiles variants on demand and therefore
/// can never reproduce it. Only a frame out of the real player can.
///
/// It captures the actual backbuffer via <see cref="ScreenCapture"/>, so what lands on disk
/// includes post-processing — which is the whole point, since bloom on emissive surfaces is
/// exactly what would go missing.
/// </summary>
public class AutoCapture : MonoBehaviour
{
    private const string Flag = "-autoshot";

    /// <summary>Directory passed after the flag, or null when the flag is absent.</summary>
    public static string RequestedDirectory
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == Flag) return args[i + 1];
            return null;
        }
    }

    private string _directory;

    public static void InstallIfRequested(GameObject host)
    {
        string dir = RequestedDirectory;
        if (string.IsNullOrEmpty(dir)) return;
        host.AddComponent<AutoCapture>()._directory = dir;
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(_directory);

        // Let Bootstrap finish and floor one build.
        yield return WaitFrames(90);

        yield return Shot("player-title");

        if (GameManager.Instance != null) GameManager.Instance.StartRun();
        yield return WaitFrames(45);
        yield return Shot("player-frozen");

        // Impulse is how the game itself forces the world back up to speed, so this is the
        // real moving-state look rather than a hand-set clock TimeDirector would overwrite.
        for (int i = 0; i < 60; i++)
        {
            if (TimeDirector.Instance != null) TimeDirector.Instance.Impulse(0.5f);
            yield return null;
        }
        yield return Shot("player-moving");
        yield return ShotEffects();

        Debug.Log("AutoCapture complete: " + _directory);
        Application.Quit(0);
    }

    /// <summary>
    /// Stage the particle effects and photograph them.
    ///
    /// This exists because the editor's review capture disables post-processing for determinism,
    /// and the slash and bursts are authored to be seen through bloom — at their real alpha they
    /// are all but invisible without it. Only a frame out of the actual player shows what the
    /// effects look like.
    /// </summary>
    private IEnumerator ShotEffects()
    {
        Transform player = GameManager.PlayerTransform;
        if (player == null) yield break;

        // Keep the clock running so player-domain effects animate rather than hanging frozen.
        if (TimeDirector.Instance != null) TimeDirector.Instance.Impulse(1.5f);

        Vector3 at = player.position + Vector3.up * 0.7f;
        GameVfx.Emit(VfxKind.EnemyDeath, at + Vector3.forward * 2.6f, tint: Palette.Melee);
        GameVfx.Emit(VfxKind.Deflect, at + Vector3.right * 2.4f, Vector3.right);
        GameVfx.Emit(VfxKind.Shard, player.position + Vector3.left * 2.4f);
        GameVfx.Emit(VfxKind.Muzzle, at + Vector3.back * 2.4f, Vector3.back);
        GameVfx.Slash(player.position, Vector3.forward, 3.1f, 120f);

        // A few frames only: the slash lives 0.22s and the bursts little longer.
        yield return WaitFrames(4);
        yield return Shot("player-vfx");
    }

    private IEnumerator Shot(string name)
    {
        string path = Path.Combine(_directory, name + ".png");
        ScreenCapture.CaptureScreenshot(path);

        // CaptureScreenshot resolves at end of frame and the file write trails it.
        yield return WaitFrames(12);
        Debug.Log("AutoCapture wrote " + path + " exists=" + File.Exists(path));
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
