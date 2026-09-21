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
    // Every material in the game is created at runtime from Shader.Find, and a player build strips any shader no asset
    // references — the first standalone build died in Awake with "Value cannot be null. Parameter name: shader". One
    // material asset per shader under Resources/ is the smallest thing that keeps them all in the build.
    static readonly string[] RequiredShaders = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Rift/PlanetSurface", "Rift/Atmosphere", "Rift/Sky", "Rift/Additive" };
    public static void EnsureShaderAssets()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/RiftShaders"));
        foreach (var name in RequiredShaders)
        {
            string path = "Assets/Resources/RiftShaders/" + name.Replace("/", "_").Replace(" ", "") + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
            var shader = Shader.Find(name);
            if (!shader) throw new Exception("[BUILD] shader not found in the Editor: " + name);
            AssetDatabase.CreateAsset(new Material(shader), path);
            Debug.Log("[BUILD] created shader anchor " + path);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Rift/Build macOS player")]
    public static void MacOS()
    {
        EnsureShaderAssets();
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
