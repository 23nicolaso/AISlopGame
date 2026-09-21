using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless CI entry point: runs both Play Mode verification suites (RiftVerification,
// RiftCombatVerification) without a human clicking a menu item, then exits the batchmode
// process with a machine-readable code. Invoke with:
//
//   "<Unity>" -batchmode -nographics -projectPath "<project>" -executeMethod RiftHeadlessRunner.Run -logFile <path>
//
// Deliberately no -quit on the command line: this script owns the process lifetime and calls
// EditorApplication.Exit() itself once verification finishes or times out, because entering/
// leaving Play Mode is asynchronous (a domain reload) and -quit would race it.
//
// Exit codes: 0 = both suites passed. 1 = at least one suite threw (see the log for which
// Check(...) failed). 2 = never entered Play Mode inside the timeout — almost always a compile
// error blocking Play Mode; run the plain "-batchmode -nographics -quit" compile check first.
public static class RiftHeadlessRunner
{
    const double TimeoutSeconds = 90;
    static double deadline;

    [MenuItem("Rift/Run headless verification (CI)")]
    public static void Run()
    {
        deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.playModeStateChanged += OnStateChanged;
        EditorApplication.update += WatchForTimeout;
        EditorApplication.isPlaying = true;
    }

    static void WatchForTimeout()
    {
        if (EditorApplication.isPlaying || EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= WatchForTimeout;
        EditorApplication.playModeStateChanged -= OnStateChanged;
        Debug.LogError("[CI] TIMEOUT waiting to enter Play Mode (compile error or blocked domain reload?)");
        EditorApplication.Exit(2);
    }

    static void OnStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnStateChanged;
        EditorApplication.update -= WatchForTimeout;
        EditorApplication.delayCall += Execute; // let Boot()'s AfterSceneLoad finish first
    }

    static void Execute()
    {
        int exitCode = 0;
        // RiftVerification.Verify() is a private static method (no [MenuItem] access needed
        // beyond Unity's own reflection) — call it the same way Unity's menu system does,
        // via reflection, so this file never has to modify RiftVerification.cs itself.
        RunPrivateStatic("RiftVerification", "Verify", ref exitCode);
        RunPublic("RiftCombatVerification.Run", () => Debug.Log(RiftCombatVerification.Run()), ref exitCode);
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(exitCode);
    }

    static void RunPrivateStatic(string typeName, string methodName, ref int exitCode)
    {
        try
        {
            var method = Type.GetType(typeName)?.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new Exception("method not found via reflection: " + typeName + "." + methodName);
            method.Invoke(null, null);
            Debug.Log("[CI] " + typeName + "." + methodName + " PASSED");
        }
        catch (TargetInvocationException e)
        {
            Debug.LogError("[CI] " + typeName + "." + methodName + " FAILED: " + (e.InnerException != null ? e.InnerException.Message : e.Message));
            exitCode = 1;
        }
        catch (Exception e)
        {
            Debug.LogError("[CI] " + typeName + "." + methodName + " FAILED: " + e.Message);
            exitCode = 1;
        }
    }

    static void RunPublic(string label, Action action, ref int exitCode)
    {
        try { action(); Debug.Log("[CI] " + label + " PASSED"); }
        catch (Exception e) { Debug.LogError("[CI] " + label + " FAILED: " + e.Message); exitCode = 1; }
    }
}
