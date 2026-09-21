using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Three staged frames of the Orbit Snake greybox, written to $ORBIT_SHOT_DIR or <repo>/docs/screenshots/orbit:
//
//   "<Unity>" -batchmode -projectPath "<project>" -executeMethod OrbitScreenshotRunner.Run -logFile <path>
//
// No -nographics (Camera.Render needs a GPU). Same mean/std sanity gate as RiftScreenshotRunner. Exit 0/1/2.
public static class OrbitScreenshotRunner
{
    const double TimeoutSeconds=120; const int Width=1920,Height=1080,WarmupFrames=90;
    static double deadline; static int frames; static string outDir;

    [MenuItem("Orbit/Render screenshots (CI)")]
    public static void Run()
    {
        outDir=Environment.GetEnvironmentVariable("ORBIT_SHOT_DIR"); if(string.IsNullOrEmpty(outDir))outDir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/screenshots/orbit"));
        Directory.CreateDirectory(outDir); Debug.Log("[SHOT] output directory: "+outDir);
        OrbitSceneBuilder.Ensure(); deadline=EditorApplication.timeSinceStartup+TimeoutSeconds;
        EditorSceneManager.OpenScene(OrbitSceneBuilder.ScenePath);
        EditorApplication.playModeStateChanged+=OnStateChanged; EditorApplication.update+=WatchForTimeout; EditorApplication.isPlaying=true;
    }
    static void WatchForTimeout(){ if(EditorApplication.timeSinceStartup<deadline)return; EditorApplication.update-=WatchForTimeout; EditorApplication.update-=Warmup; EditorApplication.playModeStateChanged-=OnStateChanged; Debug.LogError("[SHOT] TIMEOUT"); EditorApplication.Exit(2); }
    static void OnStateChanged(PlayModeStateChange state){ if(state!=PlayModeStateChange.EnteredPlayMode)return; EditorApplication.playModeStateChanged-=OnStateChanged; frames=0; EditorApplication.update+=Warmup; }
    static void Warmup()
    {
        if(++frames<WarmupFrames)return; EditorApplication.update-=Warmup; EditorApplication.update-=WatchForTimeout; int exit=0;
        try{ Execute(); } catch(Exception e){ Debug.LogError("[SHOT] FAILED: "+e.Message+"\n"+e.StackTrace); exit=1; }
        EditorApplication.isPlaying=false; EditorApplication.Exit(exit);
    }
    static void Check(bool ok,string msg){ if(!ok)throw new Exception("SCREENSHOT CHECK FAILED: "+msg); }

    static void Execute()
    {
        var g=OrbitSnake.I; Check(g&&g.cam&&g.ship,"orbit snake booted");
        // 1. Launch: what the player sees a couple of seconds in, junk lanes ahead tinted by the catch rule.
        g.Restart(11); for(int i=0;i<100;i++)g.Step(.02f); g.SnapCamera(); g.TintJunk(); Capture("01-launch");
        // 2. Train: twelve segments through a hard turn so the tail draws its arc.
        g.Restart(11); for(int i=0;i<12;i++)g.ship.AddSegment(); g.ship.turn=1; for(int i=0;i<90;i++)g.Step(.02f); g.ship.turn=0; for(int i=0;i<20;i++)g.Step(.02f); g.SnapCamera(); g.TintJunk(); Capture("02-train");
        // 3. Eject: the train falling away below as the ship lifts.
        g.Eject(); for(int i=0;i<30;i++)g.Step(.02f); g.SnapCamera(); g.TintJunk(); Capture("03-eject");
        // 4. Skills on offer: three icons ahead on the new shell, the ship 20 u short of them.
        for(int i=0;i<20;i++)g.Step(.02f); g.SnapCamera(); g.TintJunk(); Capture("04-skills");
        g.Restart(7); Debug.Log("[SHOT] PASS: 4 frames written to "+outDir);
    }

    static void Capture(string name)
    {
        var cam=OrbitSnake.I.cam; var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(Width,Height,TextureFormat.RGB24,false);
        try
        {
            cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,Width,Height),0,0); tex.Apply(); RenderTexture.active=null;
            string path=Path.Combine(outDir,name+".png"); File.WriteAllBytes(path,tex.EncodeToPNG());
            var px=tex.GetPixels32(); double sum=0,sumSq=0; int n=0;
            for(int i=0;i<px.Length;i+=8){ float l=(px[i].r*.299f+px[i].g*.587f+px[i].b*.114f)/255f; sum+=l; sumSq+=l*l; n++; }
            double mean=sum/n,std=Math.Sqrt(Math.Max(0,sumSq/n-mean*mean));
            Debug.Log("[SHOT] "+name+" mean="+mean.ToString("F3")+" std="+std.ToString("F3")+" -> "+path);
            Check(mean>.02&&mean<.97,name+" is not black or blown out (mean "+mean.ToString("F3")+")"); Check(std>.03,name+" has structure (std "+std.ToString("F3")+")");
        }
        finally{ cam.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
