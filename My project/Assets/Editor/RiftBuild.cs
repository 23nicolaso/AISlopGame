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

    // Orbit Snake greybox player: <repo>/Builds/ORBIT.app, its own scene only.
    [MenuItem("Orbit/Build macOS player")]
    public static void OrbitMacOS()
    {
        EnsureShaderAssets(); OrbitSceneBuilder.Ensure();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/ORBIT.app"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { OrbitSceneBuilder.ScenePath }, locationPathName = output, target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
        Debug.Log("[BUILD] " + report.summary.result + " -> " + output + " (" + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors)");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // Orbit Snake WebGL player for itch.io: <repo>/Builds/ORBIT-web/ (index.html at the root, zip the folder as-is).
    // itch.io serves .br/.gz files without a Content-Encoding header, so the build ships uncompressed; the canvas is
    // 1280x720, the HUD's virtual resolution. productName is only borrowed for the page title and put back afterwards.
    [MenuItem("Orbit/Build WebGL player")]
    public static void OrbitWebGL()
    {
        EnsureShaderAssets(); OrbitSceneBuilder.Ensure();
        string product = PlayerSettings.productName; bool bg = PlayerSettings.runInBackground;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.template = "APPLICATION:Default"; PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.productName = "ORBIT SNAKE"; PlayerSettings.runInBackground = true;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/ORBIT-web"));
        Directory.CreateDirectory(output);
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { OrbitSceneBuilder.ScenePath }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None });
            Debug.Log("[BUILD] " + report.summary.result + " -> " + output + " (" + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors)");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        finally { PlayerSettings.productName = product; PlayerSettings.runInBackground = bg; AssetDatabase.SaveAssets(); }
    }
}
