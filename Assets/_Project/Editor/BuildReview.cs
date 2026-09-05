using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildReview
{
    [MenuItem("STILL/Build Windows Review")]
    public static void Windows()
    {
        Directory.CreateDirectory("Builds/Windows");
        PlayerSettings.productName = "STILL";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/_Project/Scenes/Still.unity" },
            locationPathName = "Builds/Windows/STILL.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        File.WriteAllText("Logs/windows-build.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
