using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-time project configuration: physics layers, input handler, and the playable scene.
///
/// Runs itself once after the scripts are first imported (guarded by an EditorPrefs key)
/// and is also available from the menu as STILL ▸ Set Up Project if you ever need to redo it.
///
/// It deliberately never overwrites an existing scene file — re-running is safe.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Still.unity";
    private const string DonePrefKey = "STILL.SetupComplete.v1";

    private static readonly string[] RequiredLayers = { "Wall", "Player", "Enemy", "Pickup" };

    static ProjectSetup()
    {
        // Defer: the asset database isn't reliably queryable from a static constructor.
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(DonePrefKey, false)) return;
        Run();
    }

    [MenuItem("STILL/Set Up Project")]
    public static void Run()
    {
        bool needsRestart = false;

        needsRestart |= EnsureLayers();
        needsRestart |= EnsureLegacyInputEnabled();
        EnsureScene();

        EditorPrefs.SetBool(DonePrefKey, true);
        AssetDatabase.SaveAssets();

        if (needsRestart)
        {
            Debug.Log("[STILL] Setup complete. Unity needs to restart for the input settings " +
                      "to apply — accept the restart prompt, then press Play.");
        }
        else
        {
            Debug.Log("[STILL] Setup complete. Open Assets/_Project/Scenes/Still.unity and press Play.");
        }
    }

    /// <summary>Adds our named layers to the first free user slots (8..31).</summary>
    private static bool EnsureLayers()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return false;

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null) return false;

        bool changed = false;

        foreach (string wanted in RequiredLayers)
        {
            if (LayerExists(layers, wanted)) continue;

            // User layers start at 8; 0-7 are reserved by Unity.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = wanted;
                changed = true;
                break;
            }
        }

        if (changed)
        {
            tagManager.ApplyModifiedProperties();
            Debug.Log("[STILL] Created physics layers: " + string.Join(", ", RequiredLayers));
        }
        return false; // layers apply immediately, no restart needed
    }

    private static bool LayerExists(SerializedProperty layers, string name)
    {
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return true;
        return false;
    }

    /// <summary>
    /// The game reads input through the classic Input class. If the project was created with
    /// the new Input System only, those calls throw — so switch the handler to "Both".
    /// This one genuinely requires an editor restart to take effect.
    /// </summary>
    private static bool EnsureLegacyInputEnabled()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return false;

        var settings = new SerializedObject(assets[0]);
        SerializedProperty handler = settings.FindProperty("activeInputHandler");
        if (handler == null) return false;

        const int both = 2;
        if (handler.intValue == both) return false;

        handler.intValue = both;
        settings.ApplyModifiedProperties();
        Debug.Log("[STILL] Enabled the legacy Input Manager (set input handling to 'Both').");
        return true;
    }

    /// <summary>Creates the one scene the game needs: a single GameObject with Bootstrap on it.</summary>
    private static void EnsureScene()
    {
        if (File.Exists(ScenePath))
        {
            AddSceneToBuild();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("Bootstrap");
        go.AddComponent<Bootstrap>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        AddSceneToBuild();

        Debug.Log($"[STILL] Created {ScenePath}");
    }

    private static void AddSceneToBuild()
    {
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path == ScenePath) return;

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
    }
}
