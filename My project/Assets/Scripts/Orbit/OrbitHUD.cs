using UnityEngine;

// IMGUI HUD on a 1280x720 virtual canvas with no words. The score is the only text and it is digits. Everything else
// is shape: five concentric arcs for the shell, a row of pips for the train against the quota, glyphs for the skills
// held, an enter-shaped glyph on the end screen, two bars for pause. OrbitVerification asserts this file draws exactly
// one label, so a word cannot creep back in without a check failing.
public partial class OrbitSnake
{
    const float Width=1280,Height=720; GUIStyle digits;
    static readonly Color Ink=new Color(1,1,1,.9f), Dim=new Color(1,1,1,.18f), Lit=new Color(.55f,.85f,1), Ready=new Color(.4f,1,.6f);
    void Styles(){ if(digits!=null)return; digits=new GUIStyle(GUI.skin.label){fontSize=44,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperRight}; digits.normal.textColor=Ink; }
    static void Box(Rect r,Color c){ var old=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
    // A rotated bar: centre, length along the angle, thickness across it.
    static void Bar(Vector2 c,float len,float thick,float deg,Color col){ var old=GUI.matrix; GUI.matrix=old*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.Euler(0,0,deg),Vector3.one); Box(new Rect(-len*.5f,-thick*.5f,len,thick),col); GUI.matrix=old; }
    // An arc of short bars, `sweep` degrees from `start`, clockwise on screen.
    static void Arc(Vector2 c,float r,float start,float sweep,float thick,Color col){ int n=Mathf.Max(6,(int)(sweep/8)); float step=sweep/n; for(int i=0;i<n;i++){ float a=(start+(i+.5f)*step)*Mathf.Deg2Rad; Bar(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,r*step*Mathf.Deg2Rad+1.5f,thick,start+(i+.5f)*step+90,col); } }
    static float Pulse(float hz) => .5f+.5f*Mathf.Sin(Time.unscaledTime*hz*Mathf.PI*2);
    // The one and only label in the HUD: an integer. OrbitVerification counts GUI.Label calls in this file and expects one.
    void Digits(Rect r,int v){ GUI.Label(r,v.ToString(),digits); }

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
        // Vignettes: red for a strike, green for a catch, white for an armour save, a coloured wash for a skill pick.
        if(strikePulse>0)Box(new Rect(0,0,Width,Height),new Color(1,.15f,.1f,strikePulse*.35f));
        if(catchPulse>0)Box(new Rect(0,0,Width,Height),new Color(.3f,1,.5f,catchPulse*.12f));
        if(armourPulse>0)Box(new Rect(0,0,Width,Height),new Color(1,1,1,armourPulse*.3f));
        if(pickPulse>0)Box(new Rect(0,0,Width,Height),new Color(.6f,.8f,1,pickPulse*.15f));
        // Shell: five concentric arcs top-left, lit up to the current shell; the newest one pulses for a second after a climb.
        Vector2 sc=new Vector2(78,78);
        for(int i=0;i<ShellAltitude.Length;i++){ bool lit=i<=level; Color c=lit?(i==level&&ejectPulse>0?Color.Lerp(Lit,Color.white,Pulse(3)):Lit):Dim; Arc(sc,20+i*9,135,270,4,c); }
        // Score, digits only, top right.
        Digits(new Rect(Width-330,26,300,60),score);
        // Train pips against the quota, bottom centre. Filled pips are the segments held; past the quota they turn gold.
        int q=Quota; int n=ship.segments.Count; int slots=Mathf.Max(q,n); float pw=22,gap=8; float x0=Width*.5f-(slots*pw+(slots-1)*gap)*.5f;
        for(int i=0;i<slots;i++){ bool filled=i<n; bool extra=i>=q; Color c=!filled?Dim:extra?new Color(1,.85f,.3f):(EjectReady?Color.Lerp(Ready,Color.white,Pulse(1.5f)*.5f):Lit); Box(new Rect(x0+i*(pw+gap),Height-64,pw,pw),c); if(!filled)Box(new Rect(x0+i*(pw+gap)+3,Height-61,pw-6,pw-6),new Color(0,0,0,.35f)); }
        if(EjectReady){ Arc(new Vector2(Width*.5f,Height-53),34+Pulse(1.5f)*4,0,360,2,new Color(.4f,1,.6f,.35f)); }
        // Skills held, bottom left, in their own colours. Armour's glyph dims once the charge is spent.
        int k=0; for(int i=0;i<SkillCount;i++){ if(!skills[i])continue; Color c=SkillColors[i]; c=new Color(Mathf.Min(1,c.r),Mathf.Min(1,c.g),Mathf.Min(1,c.b),(Skill)i==Skill.Armour&&armour==0?.3f:1); Glyph2D((Skill)i,new Rect(30+k*54,Height-84,44,44),c); k++; }
        if(paused){ Box(new Rect(0,0,Width,Height),new Color(0,0,0,.55f)); Box(new Rect(Width*.5f-34,Height*.5f-40,24,80),Ink); Box(new Rect(Width*.5f+10,Height*.5f-40,24,80),Ink); }
        if(ended)
        {
            float t=Mathf.Clamp01(endTimer);
            if(!won)Box(new Rect(0,0,Width,Height),new Color(0,0,0,.6f*t));
            if(won)for(int i=0;i<ShellAltitude.Length;i++)Arc(new Vector2(Width*.5f,Height*.5f-30),40+i*14,0,360,5,Color.Lerp(Lit,Color.white,Pulse(1)));
            else Arc(new Vector2(Width*.5f,Height*.5f-30),60,0,360,6,new Color(1,.3f,.2f,t));
            Digits(new Rect(Width*.5f-150,Height*.5f-56,300,60),score);
            EnterGlyph(new Vector2(Width*.5f,Height*.5f+90),60,new Color(1,1,1,.4f+.4f*Pulse(1)*t));
        }
    }
}
