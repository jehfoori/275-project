using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGlBuildMenu
{
    private const string MainScenePath = "Assets/Scenes/MainSimulation.unity";
    private const string OutputDirectory = "Build/WebGL";

    [MenuItem("Build/WebGL/Build Web Demo")]
    public static void BuildWebDemo()
    {
        if (!File.Exists(MainScenePath))
        {
            EditorUtility.DisplayDialog(
                "WebGL Build Failed",
                $"Could not find the main scene at:\n{MainScenePath}",
                "OK");
            return;
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainScenePath, enabled: true)
        };

        BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
        if (previousTarget != BuildTarget.WebGL)
        {
            Debug.Log("Switching active build target to WebGL...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog(
                    "WebGL Build Failed",
                    "Could not switch to the WebGL build target.\n\n"
                    + "Install the WebGL Build Support module for Unity 6000.3.6f1 in Unity Hub, then try again.",
                    "OK");
                return;
            }
        }

        Directory.CreateDirectory(OutputDirectory);

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { MainScenePath },
            locationPathName = OutputDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string fullPath = Path.GetFullPath(OutputDirectory);
            EditorUtility.DisplayDialog(
                "WebGL Build Succeeded",
                $"Output folder:\n{fullPath}\n\n"
                + "Preview locally with:\n"
                + "  ./scripts/serve-webgl.sh\n\n"
                + "Then open http://localhost:8080",
                "OK");
            EditorUtility.RevealInFinder(fullPath);
            return;
        }

        EditorUtility.DisplayDialog(
            "WebGL Build Failed",
            $"Result: {summary.result}\nErrors: {summary.totalErrors}\n\nCheck the Console for details.",
            "OK");
    }

    [MenuItem("Build/WebGL/Open Build Folder")]
    public static void OpenBuildFolder()
    {
        Directory.CreateDirectory(OutputDirectory);
        EditorUtility.RevealInFinder(Path.GetFullPath(OutputDirectory));
    }
}
