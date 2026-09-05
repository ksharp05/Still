using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless WebGL build entry point for the cloud-delivery pipeline.
///
/// Editor-only: this file lives under Assets/Editor so it is stripped from every player
/// build and never ships with the game.
///
/// Invoked by scripts/build.sh as:
///   Unity.exe -quit -batchmode -nographics -projectPath &lt;proj&gt; \
///             -executeMethod CloudBuild.BuildWebGL -outDir &lt;dir&gt; -buildVersion &lt;ver&gt;
/// </summary>
public static class CloudBuild
{
    // Bootstrap assembles the whole game at runtime, so the only scene we need is the
    // near-empty Still scene. SampleScene is Unity's leftover default and is excluded
    // on purpose -- shipping it would make the build boot into an empty room.
    private const string GameScene = "Assets/_Project/Scenes/Still.unity";

    public static void BuildWebGL()
    {
        string outDir  = Arg("-outDir")       ?? "Builds/WebGL";
        string version = Arg("-buildVersion") ?? PlayerSettings.bundleVersion;

        if (!File.Exists(GameScene))
            Fail($"Scene not found: {GameScene}");

        PlayerSettings.bundleVersion = version;
        PlayerSettings.productName   = "Still";

        // Gzip keeps the payload small enough that re-delivering a build over mobile data
        // is cheap. The decompression fallback matters more than it looks: it lets the
        // exact same artifact work on a dumb static host that does not set
        // Content-Encoding, so we can move between R2 / S3 / a LAN server without rebuilding.
        PlayerSettings.WebGL.compressionFormat      = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback  = true;
        PlayerSettings.WebGL.dataCaching            = true;
        PlayerSettings.WebGL.linkerTarget           = WebGLLinkerTarget.Wasm;
        // Stripping exceptions saves payload but turns any managed error into a bare
        // "Uncaught undefined", which is undebuggable. Keep them unless -exceptions none.
        PlayerSettings.WebGL.exceptionSupport = (Arg("-exceptions") == "none")
            ? WebGLExceptionSupport.None
            : WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.template               = "APPLICATION:Default";
        PlayerSettings.runInBackground              = true;
        PlayerSettings.SplashScreen.show            = false;

        // Mobile GPUs in a WebView are the target, not a desktop discrete card.
        PlayerSettings.defaultWebScreenWidth  = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        EditorUserBuildSettings.development = false;

        // STILL builds every material at runtime via Shader.Find, and never references a
        // shader from a scene or a Resources folder. Nothing therefore pulls URP's shaders
        // into the player, Shader.Find returns null, and `new Material(null)` throws before
        // the first frame. This is not WebGL-specific -- a native Android or Windows build
        // fails the same way. Pinning them as always-included is the fix.
        EnsureAlwaysIncludedShaders(new[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
        });

        string abs = Path.GetFullPath(outDir);
        if (Directory.Exists(abs)) Directory.Delete(abs, true);
        Directory.CreateDirectory(abs);

        Log($"Building WebGL v{version} -> {abs}");

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { GameScene },
            locationPathName = abs,
            target           = BuildTarget.WebGL,
            targetGroup      = BuildTargetGroup.WebGL,
            options          = BuildOptions.CompressWithLz4HC,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages.Where(m =>
                             m.type == LogType.Error || m.type == LogType.Exception))
                    Log($"  {step.name}: {msg.content}");

            Fail($"Build {summary.result} with {summary.totalErrors} error(s)");
        }

        Log($"Build OK: {summary.totalSize / (1024f * 1024f):F1} MB in {summary.totalTime.TotalSeconds:F0}s");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Generates an Xcode project for iOS (Unity as a Library).
    ///
    /// The output contains two targets: Unity-iPhone (a runnable sample app) and
    /// UnityFramework.framework (the embeddable library). A host app integrates the
    /// framework; the sample app is only there to prove the export works.
    ///
    /// This runs on Windows -- Unity emits the Xcode project and the IL2CPP-generated C++,
    /// but it must be compiled on a Mac with Xcode.
    /// </summary>
    public static void BuildIOS()
    {
        string outDir  = Arg("-outDir")       ?? "Builds/iOS";
        string version = Arg("-buildVersion") ?? PlayerSettings.bundleVersion;

        if (!File.Exists(GameScene)) Fail($"Scene not found: {GameScene}");

        PlayerSettings.bundleVersion = version;
        PlayerSettings.productName   = "Still";
        PlayerSettings.SetApplicationIdentifier(
            UnityEditor.Build.NamedBuildTarget.iOS, "com.still.game");

        // Same shader pinning as WebGL: Palette resolves everything through Shader.Find at
        // runtime, so without this the player crashes on the first frame here too.
        EnsureAlwaysIncludedShaders(new[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
        });

        PlayerSettings.SetScriptingBackend(
            UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 1); // ARM64
        PlayerSettings.iOS.sdkVersion            = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;
        PlayerSettings.SplashScreen.show         = false;

        string abs = Path.GetFullPath(outDir);
        if (Directory.Exists(abs)) Directory.Delete(abs, true);
        Directory.CreateDirectory(abs);

        Log($"Exporting iOS Xcode project v{version} -> {abs}");

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { GameScene },
            locationPathName = abs,
            target           = BuildTarget.iOS,
            targetGroup      = BuildTargetGroup.iOS,
            options          = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages.Where(m =>
                             m.type == LogType.Error || m.type == LogType.Exception))
                    Log($"  {step.name}: {msg.content}");
            Fail($"iOS export {report.summary.result}");
        }

        Log($"Xcode project OK: {report.summary.totalTime.TotalSeconds:F0}s");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Adds shaders to Graphics Settings' always-included list if they are not already
    /// there, so Shader.Find can resolve them at runtime in a player.
    /// </summary>
    private static void EnsureAlwaysIncludedShaders(string[] names)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
            "ProjectSettings/GraphicsSettings.asset");
        if (asset == null) { Log("could not open GraphicsSettings.asset"); return; }

        var so  = new SerializedObject(asset);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        bool changed = false;

        foreach (string name in names)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) { Log($"  shader missing in editor, skipped: {name}"); continue; }

            bool present = false;
            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    present = true;
                    break;
                }
            }
            if (present) continue;

            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            changed = true;
            Log($"  pinned shader: {name}");
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }

    private static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return (i >= 0 && i < args.Length - 1) ? args[i + 1] : null;
    }

    private static void Log(string m)  => Console.WriteLine($"[CloudBuild] {m}");

    private static void Fail(string m)
    {
        Log($"FAILED: {m}");
        EditorApplication.Exit(1);
    }
}
