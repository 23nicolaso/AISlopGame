using System.Collections.Generic;
using UnityEngine;

// IMGUI HUD on a 1280x720 virtual canvas with no words. The score is the only text and it is digits. Everything else
// is shape: five concentric arcs for the shell, a row of pips for the train against the quota, glyphs for the skills
// held, an enter-shaped glyph on the end screen, two bars for pause. OrbitVerification asserts this file draws exactly
// one label, so a word cannot creep back in without a check failing.
// Numbers move the way a health bar should: the score counts up to its value and swells as it goes, points fly off the
// spot they were earned as rising digits, lost pips fall out of the row instead of vanishing, a new pip pops.
public partial class OrbitSnake
{
    const float Width=1280,Height=720; GUIStyle digits,small,pop;
    static readonly Color Ink=new Color(1,1,1,.9f), Dim=new Color(1,1,1,.18f), Lit=new Color(.55f,.85f,1), Ready=new Color(.4f,1,.6f);
    // Tweened score, its swell on a change, pips lost to the last strike, the eject/win white flash, the last skill taken.
    public float shownScore, scorePop, ejectFlash; public int lostPips; public Skill lastSkill; int prevScore;
    // Rising digits in the world: where, how much, colour, size, age.
    public struct Pop{ public Vector3 at; public int value; public Color colour; public float size, t; }
    public readonly List<Pop> pops=new();
    public void Popup(Vector3 at,int value,Color colour,float size){ pops.Add(new Pop{at=at,value=value,colour=colour,size=size}); if(pops.Count>24)pops.RemoveAt(0); }
    void ResetHud(){ shownScore=0; scorePop=0; ejectFlash=0; lostPips=0; prevScore=0; pops.Clear(); }
    // On the unscaled clock from Update (and from the checks): the score display chases the score, faster the further
    // behind it is, so a big eject rolls up over a second and a catch lands almost at once.
    public void TickHud(float dt)
    {
        if(score!=prevScore){ scorePop=1; prevScore=score; }
        shownScore=Mathf.MoveTowards(shownScore,score,dt*Mathf.Max(90,Mathf.Abs(score-shownScore)*4));
        scorePop=Mathf.Max(0,scorePop-dt*2.5f); ejectFlash=Mathf.Max(0,ejectFlash-dt);
        for(int i=pops.Count-1;i>=0;i--){ var p=pops[i]; p.t+=dt; if(p.t>1f)pops.RemoveAt(i); else pops[i]=p; }
    }
    void Styles(){ if(digits!=null)return; digits=new GUIStyle(GUI.skin.label){fontSize=44,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperRight}; digits.normal.textColor=Ink; small=new GUIStyle(digits){fontSize=20}; small.normal.textColor=new Color(1,1,1,.45f); pop=new GUIStyle(digits){alignment=TextAnchor.MiddleCenter}; }
    static void Box(Rect r,Color c){ var old=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
    // A rotated bar: centre, length along the angle, thickness across it.
    static void Bar(Vector2 c,float len,float thick,float deg,Color col){ var old=GUI.matrix; GUI.matrix=old*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.Euler(0,0,deg),Vector3.one); Box(new Rect(-len*.5f,-thick*.5f,len,thick),col); GUI.matrix=old; }
    // An arc of short bars, `sweep` degrees from `start`, clockwise on screen.
    static void Arc(Vector2 c,float r,float start,float sweep,float thick,Color col){ int n=Mathf.Max(6,(int)(sweep/8)); float step=sweep/n; for(int i=0;i<n;i++){ float a=(start+(i+.5f)*step)*Mathf.Deg2Rad; Bar(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,r*step*Mathf.Deg2Rad+1.5f,thick,start+(i+.5f)*step+90,col); } }
    static float Pulse(float hz) => .5f+.5f*Mathf.Sin(Time.unscaledTime*hz*Mathf.PI*2);
    // The one and only label in the HUD: an integer. OrbitVerification counts GUI.Label calls in this file and expects one.
    void Digits(Rect r,int v,GUIStyle style=null){ var keep=digits; if(style!=null)digits=style; GUI.Label(r,v.ToString(),digits); digits=keep; }

    // The skill glyphs, same shapes as the pods, drawn in a square.
    public static void Glyph2D(Skill s,Rect r,Color col)
    {
        float u=r.width/10; Vector2 c=r.center;
        switch(s)
        {
            case Skill.Magnet: Box(new Rect(c.x-3.2f*u,c.y-3*u,1.6f*u,4.6f*u),col); Box(new Rect(c.x+1.6f*u,c.y-3*u,1.6f*u,4.6f*u),col); Box(new Rect(c.x-3.2f*u,c.y+1.6f*u,6.4f*u,1.6f*u),col); break;
            case Skill.Armour: Arc(c,3.2f*u,0,360,1.4f*u,col); Box(new Rect(c.x-1.1f*u,c.y-1.1f*u,2.2f*u,2.2f*u),col); break;
            case Skill.Whip: Bar(c+new Vector2(0,1.2f*u),6*u,1.2f*u,90,col); Bar(c+new Vector2(-1.4f*u,-1.8f*u),3.4f*u,1.1f*u,45,col); Bar(c+new Vector2(1.4f*u,-1.8f*u),3.4f*u,1.1f*u,-45,col); break;
            case Skill.Brake: Box(new Rect(c.x-2.8f*u,c.y-3.2f*u,1.6f*u,6.4f*u),col); Box(new Rect(c.x+1.2f*u,c.y-3.2f*u,1.6f*u,6.4f*u),col); break;
            case Skill.Phase: Box(new Rect(c.x-3.2f*u,c.y-.4f*u,3*u,3*u),col); Box(new Rect(c.x+.2f*u,c.y-3.2f*u,3*u,3*u),col); break;
            case Skill.Compound: Box(new Rect(c.x-3.4f*u,c.y+1*u,1.6f*u,2*u),col); Box(new Rect(c.x-.8f*u,c.y-1*u,1.6f*u,4*u),col); Box(new Rect(c.x+1.8f*u,c.y-3*u,1.6f*u,6*u),col); break;
        }
    }
    // The enter key as a shape: a box with a bent arrow. The only prompt the end screen has.
    static void EnterGlyph(Vector2 c,float s,Color col){ Bar(c+new Vector2(s*.35f,-s*.25f),s*.5f,s*.12f,90,col); Bar(c+new Vector2(0,0),s*.7f,s*.12f,0,col); Bar(c+new Vector2(-s*.28f,-s*.12f),s*.34f,s*.12f,45,col); Bar(c+new Vector2(-s*.28f,s*.12f),s*.34f,s*.12f,-45,col); }

    void OnGUI()
    {
        if(!ship)return; Styles();
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/Width,Screen.height/Height,1));
        // Rising digits at the spot the points were earned, before the HUD so the HUD reads over them.
        if(cam)foreach(var p in pops)
        {
            var sp=cam.WorldToScreenPoint(p.at); if(sp.z<0)continue; float k=p.t; float rise=EaseOutCubic(k)*52; float a=1-Mathf.SmoothStep(.55f,1,k); float grow=1+.35f*(1-EaseOutCubic(Mathf.Min(1,k*4)));
            var st=new GUIStyle(pop){fontSize=Mathf.RoundToInt(p.size*grow)}; st.normal.textColor=new Color(p.colour.r,p.colour.g,p.colour.b,a);
            float x=sp.x*Width/Screen.width, y=(Screen.height-sp.y)*Height/Screen.height-rise-18; Digits(new Rect(x-100,y-30,200,60),p.value,st);
        }
        // Vignettes: red for a strike, green for a catch, white for an armour save, a coloured wash for a skill pick, a
        // white flash for an eject or the escape. Peril breathes red at the four edges in time with the heartbeat.
        if(strikePulse>0)Box(new Rect(0,0,Width,Height),new Color(1,.15f,.1f,strikePulse*.35f));
        if(catchPulse>0)Box(new Rect(0,0,Width,Height),new Color(.3f,1,.5f,catchPulse*.12f));
        if(armourPulse>0)Box(new Rect(0,0,Width,Height),new Color(1,1,1,armourPulse*.3f));
        if(pickPulse>0)Box(new Rect(0,0,Width,Height),new Color(.6f,.8f,1,pickPulse*.15f));
        if(ejectFlash>0)Box(new Rect(0,0,Width,Height),new Color(1,1,1,ejectFlash/.3f*.55f));
        if(peril&&started&&!ended&&!paused){ float beat=Mathf.Max(0,Mathf.Sin(Time.unscaledTime*Mathf.PI*2)); var edge=new Color(1,.2f,.15f,.12f+.2f*beat); float w=22+beat*10; Box(new Rect(0,0,Width,w),edge); Box(new Rect(0,Height-w,Width,w),edge); Box(new Rect(0,0,w,Height),edge); Box(new Rect(Width-w,0,w,Height),edge); }
        // Shell: five concentric arcs top-left, lit up to the current shell; the newest one sweeps in over the climb.
        Vector2 sc=new Vector2(78,78);
        // The current shell's arc drifts from blue to orange as the Kessler clock runs, and blinks when a piece is added.
        Color load=Color.Lerp(Lit,new Color(1,.55f,.2f),KesslerLoad); if(kesslerPulse>0)load=Color.Lerp(load,Color.white,kesslerPulse);
        for(int i=0;i<ShellAltitude.Length;i++){ bool lit=i<=level; Color c=lit?(i==level?(ejectPulse>0?Color.Lerp(Lit,Color.white,Pulse(3)):load):Lit):Dim; float sweep=i==level&&ejectPulse>0?270*EaseOutCubic(1-ejectPulse/1.2f):270; Arc(sc,20+i*9,135,sweep,4,c); }
        // Score, digits only, top right, counting up to its value and swelling as it changes. On the web the itch.io
        // page floats its own buttons over the top-right ~110 px of the embed, so the score drops below them there.
        float st0=Application.platform==RuntimePlatform.WebGLPlayer?110:0;
        digits.fontSize=Mathf.RoundToInt(44+12*scorePop); digits.normal.textColor=Color.Lerp(Ink,new Color(.75f,1,.85f),scorePop);
        Digits(new Rect(Width-330,26+st0-6*scorePop,300,60),Mathf.RoundToInt(shownScore)); digits.fontSize=44; digits.normal.textColor=Ink;
        // Best score, small and dim under the score; gold and breathing when this run beat it.
        if(best>0){ var bs=new GUIStyle(small); if(newBest)bs.normal.textColor=Color.Lerp(new Color(1,.85f,.3f),Color.white,Pulse(1.2f)); Digits(new Rect(Width-330,74+st0,300,30),best,bs); }
        // Train pips against the quota, bottom centre. Filled pips are the segments held; past the quota they turn gold.
        // The newest pip pops in; pips lost to a strike fall out of the row in red.
        int q=Quota; int n=ship.segments.Count; int slots=Mathf.Max(q,n); float pw=22,gap=8; float x0=Width*.5f-(slots*pw+(slots-1)*gap)*.5f;
        for(int i=0;i<slots;i++){ bool filled=i<n; bool extra=i>=q; Color c=!filled?Dim:extra?new Color(1,.85f,.3f):(EjectReady?Color.Lerp(Ready,Color.white,Pulse(1.5f)*.5f):Lit); float s=filled&&i==n-1&&catchPulse>0?1+.5f*(catchPulse/.4f):1; var r=new Rect(x0+i*(pw+gap)+pw*.5f*(1-s),Height-64+pw*.5f*(1-s),pw*s,pw*s); Box(r,c); if(!filled)Box(new Rect(x0+i*(pw+gap)+3,Height-61,pw-6,pw-6),new Color(0,0,0,.35f)); }
        if(lostPips>0&&strikePulse>0){ float k=1-strikePulse/.6f; for(int i=0;i<lostPips;i++){ float x=x0+(n+i)*(pw+gap); Box(new Rect(x,Height-64+EaseOutCubic(k)*40,pw,pw),new Color(1,.3f,.2f,1-k)); } }
        if(EjectReady){ Arc(new Vector2(Width*.5f,Height-53),34+Pulse(1.5f)*4,0,360,2,new Color(.4f,1,.6f,.35f)); }
        // Skills held, bottom left, in their own colours. Armour's glyph dims once the charge is spent; the newest pops.
        int k2=0; for(int i=0;i<SkillCount;i++){ if(!skills[i])continue; Color c=SkillColors[i]; c=new Color(Mathf.Min(1,c.r),Mathf.Min(1,c.g),Mathf.Min(1,c.b),(Skill)i==Skill.Armour&&armour==0?.3f:1); float s=(Skill)i==lastSkill&&pickPulse>0?1+.6f*(pickPulse/.8f):1; Glyph2D((Skill)i,new Rect(30+k2*54+22*(1-s),Height-84+22*(1-s),44*s,44*s),c); k2++; }
        // Start: the shell waits under a dim wash with the enter glyph breathing over the ship. Any key goes.
        if(!started&&!ended){ Box(new Rect(0,0,Width,Height),new Color(0,0,0,.35f)); Arc(new Vector2(Width*.5f,Height*.5f),58+Pulse(.8f)*6,0,360,3,new Color(1,1,1,.5f)); EnterGlyph(new Vector2(Width*.5f,Height*.5f+110),64,new Color(1,1,1,.45f+.45f*Pulse(1))); }
        if(paused){ Box(new Rect(0,0,Width,Height),new Color(0,0,0,.55f)); Box(new Rect(Width*.5f-34,Height*.5f-40,24,80),Ink); Box(new Rect(Width*.5f+10,Height*.5f-40,24,80),Ink); }
        if(ended)
        {
            float t=Mathf.Clamp01(endTimer);
            if(!won)Box(new Rect(0,0,Width,Height),new Color(0,0,0,.6f*t));
            if(won)for(int i=0;i<ShellAltitude.Length;i++)Arc(new Vector2(Width*.5f,Height*.5f-30),40+i*14,0,360,5,Color.Lerp(Lit,Color.white,Pulse(1)));
            else Arc(new Vector2(Width*.5f,Height*.5f-30),60*EaseOutBack(t),0,360,6,new Color(1,.3f,.2f,t));
            Digits(new Rect(Width*.5f-150,Height*.5f-56,300,60),Mathf.RoundToInt(shownScore));
            if(best>0){ var bs=new GUIStyle(small); bs.alignment=TextAnchor.UpperCenter; if(newBest)bs.normal.textColor=Color.Lerp(new Color(1,.85f,.3f),Color.white,Pulse(1.2f)); Digits(new Rect(Width*.5f-150,Height*.5f+4,300,30),best,bs); }
            EnterGlyph(new Vector2(Width*.5f,Height*.5f+90),60,new Color(1,1,1,.4f+.4f*Pulse(1)*t));
        }
    }
}
