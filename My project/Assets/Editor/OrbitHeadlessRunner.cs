using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless entry point for the Orbit Snake checks. Mirrors RiftHeadlessRunner:
//
//   "<Unity>" -batchmode -nographics -projectPath "<project>" -executeMethod OrbitHeadlessRunner.Run -logFile <path>
//
// Exit codes: 0 = all checks passed, 1 = a check threw, 2 = Play Mode never entered (compile error).
public static class OrbitHeadlessRunner
{
    const double TimeoutSeconds=90; static double deadline;
    [MenuItem("Orbit/Run headless verification (CI)")]
    public static void Run()
    {
        OrbitSceneBuilder.Ensure();
        deadline=EditorApplication.timeSinceStartup+TimeoutSeconds;
        EditorSceneManager.OpenScene(OrbitSceneBuilder.ScenePath);
        EditorApplication.playModeStateChanged+=OnStateChanged; EditorApplication.update+=WatchForTimeout; EditorApplication.isPlaying=true;
    }
    static void WatchForTimeout()
    {
        if(EditorApplication.isPlaying||EditorApplication.timeSinceStartup<deadline)return;
        EditorApplication.update-=WatchForTimeout; EditorApplication.playModeStateChanged-=OnStateChanged;
        Debug.LogError("[CI] TIMEOUT waiting to enter Play Mode"); EditorApplication.Exit(2);
    }
    static void OnStateChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        EditorApplication.playModeStateChanged-=OnStateChanged; EditorApplication.update-=WatchForTimeout; EditorApplication.delayCall+=Execute;
    }
    static void Execute()
    {
        int exit=0;
        try{ Debug.Log(OrbitVerification.Run()); Debug.Log("[CI] OrbitVerification PASSED"); }
        catch(Exception e){ Debug.LogError("[CI] OrbitVerification FAILED: "+e.Message); exit=1; }
        EditorApplication.isPlaying=false; EditorApplication.Exit(exit);
    }
}
