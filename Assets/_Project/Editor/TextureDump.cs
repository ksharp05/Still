using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Writes the generated stone maps to Builds/Review so they can be eyeballed while tuning.</summary>
public static class TextureDump
{
    [MenuItem("STILL/Dump Stone Textures")]
    public static void Run()
    {
        ProceduralTexture.KeepReadable = true;
        ProceduralTexture.Warm();

        Directory.CreateDirectory("Builds/Review");
        Write("paving-albedo", ProceduralTexture.Paving.Albedo);
        Write("paving-normal", ProceduralTexture.Paving.Normal);
        Write("rock-albedo", ProceduralTexture.Rock.Albedo);
        Write("rock-normal", ProceduralTexture.Rock.Normal);

        Debug.Log("dumped stone textures");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void Write(string name, Texture2D t) =>
        File.WriteAllBytes("Builds/Review/tex-" + name + ".png", t.EncodeToPNG());
}
