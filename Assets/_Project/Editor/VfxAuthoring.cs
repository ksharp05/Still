using UnityEditor;
using UnityEngine;

public static class VfxAuthoring
{
    public static VfxTuning EnsureTuning()
    {
        const string path = "Assets/_Project/Resources/VfxTuning.asset";
        var tuning = AssetDatabase.LoadAssetAtPath<VfxTuning>(path);
        if (tuning == null)
        {
            tuning = ScriptableObject.CreateInstance<VfxTuning>();
            AssetDatabase.CreateAsset(tuning, path);
            AssetDatabase.SaveAssets();
        }
        return tuning;
    }

    [MenuItem("STILL/VFX/Select tuning")]
    public static void SelectTuning() => Selection.activeObject = EnsureTuning();

    [MenuItem("STILL/VFX/Play effects demo (during Play mode)")]
    public static void Demo()
    {
        if (!Application.isPlaying || GameManager.Instance == null)
        { Debug.Log("Open Still.unity and enter Play mode, then run the VFX demo."); return; }
        if (Object.FindFirstObjectByType<VfxShowcase>() == null)
            GameManager.Instance.gameObject.AddComponent<VfxShowcase>();
    }
}
