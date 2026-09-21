using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Standalone player build, so a match can be flown without opening the Editor:
//
//   "<Unity>" -batchmode -nographics -quit -projectPath "<project>" -executeMethod RiftBuild.MacOS -logFile <path>
//
// Output: <repo>/Builds/RIFT.app (gitignored). The built player understands -rift-screenshot=<png path>: it plays for
// three seconds, captures the full frame INCLUDING the IMGUI HUD (which Camera.Render() in the Editor cannot), and quits.
public static class RiftBuild
{
    [MenuItem("Rift/Build macOS player")]
    public static void MacOS()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/RIFT.app"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[BUILD] " + report.summary.result + " -> " + output + " (" + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors)");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
