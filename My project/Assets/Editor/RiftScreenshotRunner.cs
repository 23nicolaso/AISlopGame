using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless screenshot entry point: enters Play Mode, stages five fixed camera setups, renders the
// chase camera into a RenderTexture and writes PNGs, then exits with a machine-readable code.
//
//   "<Unity>" -batchmode -projectPath "<project>" -executeMethod RiftScreenshotRunner.Run -logFile <path>
//
// No -nographics: Camera.Render() needs a real GPU context. No -quit: this script owns process
// lifetime like RiftHeadlessRunner does. Output directory is $RIFT_SHOT_DIR or <repo>/docs/screenshots.
//
// Every frame is checked for mean luminance and contrast before it is accepted, so a broken shader,
// a camera left inside the planet or a missing light fails the run instead of producing a dark PNG
// nobody looks at. IMGUI (OnGUI) does not go through Camera.Render(), so the HUD is absent here.
//
// Exit codes: 0 = all frames rendered and passed the sanity check. 1 = a frame failed or a stage
// threw. 2 = never entered Play Mode inside the timeout.
public static class RiftScreenshotRunner
{
    const double TimeoutSeconds = 120;
    const int Width = 1920, Height = 1080;
    // Let a couple of seconds of real simulation run first so trails, particles and gate rings have state to show.
    const int WarmupFrames = 120;
    static double deadline;
    static int framesInPlay;
    static string outDir;

    [MenuItem("Rift/Render verification screenshots (CI)")]
    public static void Run()
    {
        outDir = Environment.GetEnvironmentVariable("RIFT_SHOT_DIR");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/screenshots"));
        Directory.CreateDirectory(outDir);
        Debug.Log("[SHOT] output directory: " + outDir);
        deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.playModeStateChanged += OnStateChanged;
        EditorApplication.update += WatchForTimeout;
        EditorApplication.isPlaying = true;
    }

    static void WatchForTimeout()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= WatchForTimeout;
        EditorApplication.update -= Warmup;
        EditorApplication.playModeStateChanged -= OnStateChanged;
        Debug.LogError("[SHOT] TIMEOUT (compile error, or Play Mode never reached)");
        EditorApplication.Exit(2);
    }

    static void OnStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnStateChanged;
        framesInPlay = 0;
        EditorApplication.update += Warmup;
    }

    static void Warmup()
    {
        if (++framesInPlay < WarmupFrames) return;
        EditorApplication.update -= Warmup;
        EditorApplication.update -= WatchForTimeout;
        int exitCode = 0;
        try { Execute(); }
        catch (Exception e) { Debug.LogError("[SHOT] FAILED: " + e.Message + "\n" + e.StackTrace); exitCode = 1; }
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(exitCode);
    }

    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("SCREENSHOT CHECK FAILED: " + message);
    }

    static void Place(ArenaPilot pilot, Vector3 position, Quaternion rotation, float speed = 82)
    {
        pilot.transform.SetPositionAndRotation(position, rotation);
        pilot.velocity = rotation * Vector3.forward * speed; pilot.health = 100; pilot.invulnerable = 0;
        // ClearTrails: a TrailRenderer teleported across the map would otherwise draw a line from wherever it was parked.
        pilot.ResetFlight(); pilot.ClearTrails(); pilot.art.gameObject.SetActive(true);
    }

    // Look-at on the sphere: forward toward the target, up along the local radial so the horizon stays level.
    static Quaternion Face(Vector3 from, Vector3 target)
    {
        Vector3 up = AerialCombatPrototype.Up(from);
        Vector3 forward = Vector3.ProjectOnPlane(target - from, up);
        if (forward.sqrMagnitude < 1) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        return Quaternion.LookRotation(forward.normalized, up);
    }

    static void Execute()
    {
        var g = AerialCombatPrototype.I;
        Check(g && g.cam && g.player, "arena booted");
        g.paused = false; g.phase = MatchPhase.Playing; g.phaseTimer = 0;
        var p = g.player;
        // Park every rival far away first; each stage brings back only the aircraft it wants in frame.
        foreach (var other in g.pilots) if (other != p) Place(other, new Vector3(6000 + other.id * 1500, 2500, 0), Quaternion.identity);

        // 1. Launch: exactly what the player sees on frame one, first salvage field dead ahead.
        g.Spawn(p, true); p.controls = Vector3.zero; g.SnapCamera();
        Capture("01-launch");

        // 2. Suborbital: the planet's curvature, atmosphere rim and the night side.
        Place(p, new Vector3(0, 850, 0), Quaternion.Euler(40, 30, 0), 125); g.SnapCamera();
        Capture("02-suborbital");

        // 3. Refinery: gate 1 filling the frame from 110 m out, two rivals sharing the approach.
        var gate = g.gates[0]; Vector3 gateUp = AerialCombatPrototype.Up(gate.transform.position);
        Vector3 gateView = gate.transform.position - gate.transform.forward * 110 + gateUp * 18;
        Place(p, gateView, Face(gateView, gate.transform.position)); g.SnapCamera();
        Place(g.pilots[1], gateView + p.transform.right * 26 + p.transform.forward * 30, p.transform.rotation);
        Place(g.pilots[2], gateView - p.transform.right * 34 + p.transform.forward * 55 + gateUp * 6, p.transform.rotation);
        Capture("03-refinery");

        // 4. Wreck field: the volatile reactor of site 1 (cores[2]) at 70 m with the rest of the field behind it.
        var reactor = g.cores[2]; Vector3 coreUp = AerialCombatPrototype.Up(reactor.transform.position);
        Vector3 fieldView = reactor.transform.position + Quaternion.AngleAxis(35, coreUp) * Vector3.ProjectOnPlane(-Vector3.forward, coreUp).normalized * 70 + coreUp * 14;
        Place(p, fieldView, Face(fieldView, reactor.transform.position)); g.SnapCamera();
        Capture("04-wreckfield");

        // 5. Combat: a rival 60 m ahead and slightly high, tracers in the air, a hit burst on its hull.
        Place(p, new Vector3(0, 300, 0), Quaternion.identity); g.SnapCamera();
        var prey = g.pilots[1];
        // Shoot() refuses a preferred target more than 8 degrees off the nose; 5 m of offset at 60 m is 4.8 degrees.
        Place(prey, p.transform.position + p.transform.forward * 60 + p.transform.up * 3 + p.transform.right * 4, p.transform.rotation * Quaternion.Euler(0, 0, 35));
        Place(g.pilots[3], p.transform.position + p.transform.forward * 140 - p.transform.right * 40 + p.transform.up * 20, p.transform.rotation * Quaternion.Euler(-10, 25, 0));
        p.fireCooldown = 0;
        Check(g.Shoot(p, false, prey.transform), "player cannon fires for the combat frame");
        foreach (var bolt in UnityEngine.Object.FindObjectsByType<ArenaBolt>(FindObjectsSortMode.None)) for (int i = 0; i < 6; i++) bolt.Tick(.02f);
        g.Feedback("medium", prey.transform.position, p);
        // Burst() spawns ArenaDebris cubes, not a ParticleSystem; tick them 0.16 s so the hit reads as a spray rather than a dot.
        foreach (var d in UnityEngine.Object.FindObjectsByType<ArenaDebris>(FindObjectsSortMode.None)) for (int i = 0; i < 8; i++) d.Tick(.02f);
        Capture("05-combat");

        foreach (var bolt in UnityEngine.Object.FindObjectsByType<ArenaBolt>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(bolt.gameObject);
        foreach (var pilot in g.pilots) g.Spawn(pilot);
        Debug.Log("[SHOT] PASS: 5 frames written to " + outDir);
    }

    static void Capture(string name)
    {
        var g = AerialCombatPrototype.I; var cam = g.cam;
        // Art transforms normally follow the pilot in LateUpdate; force them onto the freshly placed poses now.
        foreach (var pilot in g.pilots) pilot.ResetRenderPose();
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tex.Apply(); RenderTexture.active = null;
            string path = Path.Combine(outDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            // Sanity on a 1/8 subsample: a real frame of this game has a mid-dark mean and visible structure.
            var px = tex.GetPixels32(); double sum = 0, sumSq = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 8) { float l = (px[i].r * .299f + px[i].g * .587f + px[i].b * .114f) / 255f; sum += l; sumSq += l * l; n++; }
            double mean = sum / n, std = Math.Sqrt(Math.Max(0, sumSq / n - mean * mean));
            Debug.Log("[SHOT] " + name + " mean=" + mean.ToString("F3") + " std=" + std.ToString("F3") + " -> " + path);
            Check(mean > .02 && mean < .97, name + " is not a black or blown-out frame (mean " + mean.ToString("F3") + ")");
            Check(std > .03, name + " has visible structure (std " + std.ToString("F3") + ")");
        }
        finally
        {
            cam.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
