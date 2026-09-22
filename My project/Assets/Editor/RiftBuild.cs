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
        // The cloud shell is a transparent Lit material made at runtime. Variant stripping only keeps what some material
        // asset uses, and with just an opaque Lit anchor the WebGL build dropped _SURFACE_TYPE_TRANSPARENT: the clouds
        // came out as an opaque white ball around the planet. This anchor carries the transparent keywords.
        const string transparentPath = "Assets/Resources/RiftShaders/UniversalRenderPipeline_Lit_Transparent.mat";
        if (!AssetDatabase.LoadAssetAtPath<Material>(transparentPath))
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10);
            m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            AssetDatabase.CreateAsset(m, transparentPath);
            Debug.Log("[BUILD] created shader anchor " + transparentPath);
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
    // Same recipe as unity-flappybird's BuildGame.BuildWebGL, which is verified on itch.io: explicit WebGL 2, Gzip with the
    // decompression fallback (itch serves .gz without a Content-Encoding header), the project's Itch template that fills
    // the iframe and lets Unity match the canvas size. Edit-mode checks run first and the log ends with
    // BUILD_AND_TESTS_PASSED, which scripts/build-webgl.sh and the publish workflow grep for. productName is only
    // borrowed for the page title and put back afterwards.
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Debug.Log("PASS: " + name); }
    [MenuItem("Orbit/Build WebGL player")]
    public static void OrbitWebGL()
    {
        EnsureShaderAssets(); OrbitSceneBuilder.Ensure();
        string product = PlayerSettings.productName; bool bg = PlayerSettings.runInBackground;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:Itch"; PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.productName = "ORBIT SNAKE"; PlayerSettings.runInBackground = true;
        var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        Check(playerSettings.FindProperty("activeInputHandler").intValue == 1, "only the new Input System is enabled");
        // This project assigns URP per quality level (the URP template leaves the graphics-settings default empty), so
        // each level must resolve to a URP asset either on its own or through the default.
        for (int i = 0; i < QualitySettings.names.Length; i++)
            Check((QualitySettings.GetRenderPipelineAssetAt(i) ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, "URP quality level: " + QualitySettings.names[i]);
        Check(PlayerSettings.WebGL.decompressionFallback, "WebGL works without custom HTTP compression headers");
        Check(File.Exists("Assets/WebGLTemplates/Itch/index.html"), "responsive WebGL template exists");
        Check(File.Exists(OrbitSceneBuilder.ScenePath), "Orbit Snake scene exists");
        Check(File.Exists("Assets/Resources/RiftShaders/UniversalRenderPipeline_Lit.mat"), "runtime shaders are anchored for the player build");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/ORBIT-web"));
        Directory.CreateDirectory(output);
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { OrbitSceneBuilder.ScenePath }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None });
            Debug.Log("[BUILD] " + report.summary.result + " -> " + output + " (" + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors)");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed");
            Debug.Log("BUILD_AND_TESTS_PASSED");
        }
        finally { PlayerSettings.productName = product; PlayerSettings.runInBackground = bg; AssetDatabase.SaveAssets(); }
    }
}
