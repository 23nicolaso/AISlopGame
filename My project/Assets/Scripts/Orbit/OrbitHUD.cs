using UnityEngine;

// IMGUI HUD on a 1280x720 virtual canvas. Greybox: the shell you are on, the train against the eject quota, the score,
// one toast line, and the end/pause overlays. The catch/strike rule is taught by the junk colours, not by text.
public partial class OrbitSnake
{
    const float Width=1280,Height=720; GUIStyle big,mid,small,mono;
    void Styles(){ if(big!=null)return; big=new GUIStyle(GUI.skin.label){fontSize=40,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter}; mid=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold}; small=new GUIStyle(GUI.skin.label){fontSize=15}; mono=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter}; foreach(var s in new[]{big,mid,small,mono})s.normal.textColor=Color.white; }
    static void Box(Rect r,Color c){ var old=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }

    void OnGUI()
    {
        if(!ship)return; Styles();
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/Width,Screen.height/Height,1));
        // Strike flash and catch tick.
        if(strikePulse>0)Box(new Rect(0,0,Width,Height),new Color(1,.15f,.1f,strikePulse*.35f));
        if(catchPulse>0)Box(new Rect(0,0,Width,Height),new Color(.3f,1,.5f,catchPulse*.12f));
        // Shell and level, top left.
        Box(new Rect(24,20,300,64),new Color(0,0,0,.45f));
        GUI.Label(new Rect(36,24,280,30),ShellNames[level],mid); GUI.Label(new Rect(36,54,280,24),"shell "+(level+1)+" of "+ShellAltitude.Length+"   alt "+ShellAltitude[Mathf.Clamp(level,0,ShellAltitude.Length-1)]+" km",small);
        // Score, top right.
        Box(new Rect(Width-284,20,260,64),new Color(0,0,0,.45f));
        GUI.Label(new Rect(Width-272,24,240,30),"SCORE  "+score,mid); GUI.Label(new Rect(Width-272,54,240,24),"caught "+caught+"   strikes "+strikes+"   "+elapsed.ToString("0")+" s",small);
        // Train against quota, bottom centre. Fills green when Space is live.
        int quota=level<EjectQuota.Length?EjectQuota[level]:0; int n=ship.segments.Count; bool ready=quota>0&&n>=quota;
        Box(new Rect(Width*.5f-220,Height-92,440,64),new Color(0,0,0,.5f));
        float fill=quota>0?Mathf.Clamp01(n/(float)quota):1; Box(new Rect(Width*.5f-208,Height-58,416,10),new Color(1,1,1,.12f)); Box(new Rect(Width*.5f-208,Height-58,416*fill,10),ready?new Color(.35f,1,.5f):new Color(.55f,.8f,1));
        GUI.Label(new Rect(Width*.5f-208,Height-90,416,30),ready?"TRAIN "+n+"  —  SPACE to eject and climb  (+"+(n*n*5+50*(level+1))+")":"TRAIN "+n+" / "+quota,mono);
        // Toast and feed.
        if(toastTimer>0){ Box(new Rect(Width*.5f-400,110,800,40),new Color(0,0,0,Mathf.Min(.6f,toastTimer))); GUI.color=new Color(1,1,1,Mathf.Min(1,toastTimer)); GUI.Label(new Rect(Width*.5f-390,112,780,36),toast,mono); GUI.color=Color.white; }
        for(int i=0;i<feed.Count;i++){ GUI.color=new Color(1,1,1,.35f+.1f*i); GUI.Label(new Rect(24,Height-40-(feed.Count-1-i)*20,700,20),feed[i],small); } GUI.color=Color.white;
        if(paused){ Box(new Rect(0,0,Width,Height),new Color(0,0,0,.55f)); GUI.Label(new Rect(0,Height*.5f-40,Width,60),"PAUSED",big); GUI.Label(new Rect(0,Height*.5f+20,Width,30),"Esc to resume",mono); }
        if(ended){ Box(new Rect(0,0,Width,Height),new Color(0,0,0,.6f)); GUI.Label(new Rect(0,Height*.5f-80,Width,60),won?"ESCAPED":"LOST",big); GUI.Label(new Rect(0,Height*.5f-10,Width,30),endReason,mono); GUI.Label(new Rect(0,Height*.5f+30,Width,30),"score "+score+"   caught "+caught+"   reached "+ShellNames[Mathf.Clamp(level,0,ShellNames.Length-1)],mono); GUI.Label(new Rect(0,Height*.5f+70,Width,30),"Enter to run again",mono); }
    }
}
