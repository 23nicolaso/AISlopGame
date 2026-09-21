using System.Collections.Generic;
using UnityEngine;

public partial class AerialCombatPrototype
{
    GUIStyle small,normal,title,right,banner,clock,count,centered,rightBig,tag,bounty;
    bool stylesReady;
    readonly List<ArenaPilot> standings=new List<ArenaPilot>();
    float standingsRefresh;
    const float Width=1280,Height=720;
    void Styles()
    {
        if(stylesReady)return;
        small=new GUIStyle(GUI.skin.label){fontSize=13};small.normal.textColor=new Color(.63f,.83f,.9f);
        normal=new GUIStyle(small){fontSize=18};normal.normal.textColor=Color.white;
        title=new GUIStyle(normal){fontSize=28,fontStyle=FontStyle.Bold};
        right=new GUIStyle(small){alignment=TextAnchor.MiddleRight};
        banner=new GUIStyle(normal){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        clock=new GUIStyle(banner){fontSize=34};
        count=new GUIStyle(banner){fontSize=96};
        centered=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
        rightBig=new GUIStyle(normal){alignment=TextAnchor.MiddleRight};
        tag=new GUIStyle(small){fontSize=10,alignment=TextAnchor.MiddleRight};
        bounty=new GUIStyle(banner){fontSize=17};
        stylesReady=true;
    }
    // Chevron on a ring around the crosshair; the bearing is taken in camera space so it points where the eye looks.
    void HitWedge(float bearing,float alpha)
    {
        Vector2 c=new Vector2(Width*.5f,Height*.5f);
        Vector2 d=new Vector2(Mathf.Sin(bearing),-Mathf.Cos(bearing)),n=new Vector2(-d.y,d.x);
        Color col=new Color(1,.17f,.09f,alpha);
        Vector2 tip=c+d*172,left=c+d*130-n*36,rightEdge=c+d*130+n*36;
        Line(left,tip,col,3);Line(tip,rightEdge,col,3);
        Line(c+d*118-n*20,c+d*146,new Color(1,.3f,.16f,alpha*.5f),2);
        Line(c+d*146,c+d*118+n*20,new Color(1,.3f,.16f,alpha*.5f),2);
    }
    void KillBanner()
    {
        if(bannerTimer<=0)return;
        float t=Mathf.Clamp01(1-bannerTimer/1.5f);
        // Ease-out overshoot: punches in at 1.3x and settles, then fades over the last third of a second.
        float scale=Mathf.Lerp(1.3f,1,1-Mathf.Pow(1-Mathf.Clamp01(t*5),3));
        float fade=Mathf.Clamp01(bannerTimer/.35f);
        Vector2 anchor=new Vector2(1135,312);
        Matrix4x4 old=GUI.matrix;
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(anchor.x,anchor.y,0),Quaternion.identity,new Vector3(scale,scale,1));
        Box(new Rect(-172,-19,344,38),new Color(.02f,.03f,.06f,.55f*fade));
        Line(new Vector2(-172,19),new Vector2(172,19),new Color(1,.74f,.18f,fade*.8f),2);
        GUI.color=new Color(1,.88f,.52f,fade);
        Text(new Rect(-172,-19,344,38),bannerText,banner);
        GUI.color=Color.white;GUI.matrix=old;
    }
    // Top-centre match clock. The final minute goes red and pulses on the second, so the pressure is visible and not only audible.
    void MatchClock()
    {
        float remain=MatchRemaining;
        bool urgent=phase==MatchPhase.Playing && remain<=60;
        float pulse=urgent?Mathf.Clamp01(1-(remain-Mathf.Floor(remain))*3):0;
        Box(new Rect(Width*.5f-96,16,192,56),new Color(.01f,.025f,.045f,.82f));
        Line(new Vector2(Width*.5f-96,72),new Vector2(Width*.5f+96,72),urgent?new Color(1,.26f,.14f,.45f+pulse*.55f):new Color(.16f,.88f,1,.5f),2);
        GUI.color=urgent?Color.Lerp(new Color(1,.3f,.18f),Color.white,pulse*.5f):Color.white;
        Text(new Rect(Width*.5f-96,18,192,38),Mathf.FloorToInt(remain/60)+":"+Mathf.FloorToInt(remain%60).ToString("00"),clock);
        GUI.color=new Color(.63f,.83f,.9f,.7f);
        Text(new Rect(Width*.5f-96,52,192,16),phase==MatchPhase.Ended?"MATCH OVER":"REMAINING",centered);
        GUI.color=Color.white;
    }
    // Sits directly under the match clock, between the title block and the rankings, so it never covers either.
    void BountyStrip()
    {
        if(aceId<0 || aceId>=pilots.Count)return;
        var ace=pilots[aceId];
        float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*4.2f);
        // A crowning punches in wide and settles, so the strip is impossible to miss the moment it is earned.
        float entrance=Mathf.Clamp01(bountyFresh/1.1f),scale=1+entrance*entrance*.35f;
        string label="BOUNTY   "+ace.callsign.ToUpper();
        float w=Mathf.Max(236,label.Length*11+72),h=30;
        Color edge=new Color(1,.74f,.18f,.45f+pulse*.55f);
        Matrix4x4 old=GUI.matrix;
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(Width*.5f,80+h*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
        Rect r=new Rect(-w*.5f,-h*.5f,w,h);
        Box(r,new Color(.13f,.07f,.01f,.5f+pulse*.18f+entrance*.3f));
        Line(new Vector2(r.x,r.y),new Vector2(r.xMax,r.y),edge,2);
        Line(new Vector2(r.x,r.yMax),new Vector2(r.xMax,r.yMax),new Color(1,.74f,.18f,.22f+pulse*.3f),1);
        // Brackets on both ends: the same shape as the target reticle, aimed at a callsign.
        for(int side=-1;side<=1;side+=2)
        {
            float x=side<0?r.x-9:r.xMax+9;
            Line(new Vector2(x-side*7,r.y+3),new Vector2(x,r.y+h*.5f),edge);
            Line(new Vector2(x,r.y+h*.5f),new Vector2(x-side*7,r.yMax-3),edge);
        }
        GUI.color=Color.Lerp(new Color(1,.79f,.32f),Color.white,pulse*.45f);
        Text(r,label,bounty);
        GUI.color=Color.white;GUI.matrix=old;
    }
    void CountdownCard()
    {
        float left=CountdownLength-phaseTimer;
        int n=Mathf.CeilToInt(left);
        // Each numeral owns one second: it pops in at 1.5x, settles, and sheds an expanding ring on the way out.
        float frac=1-Mathf.Clamp01(left-Mathf.Floor(left));
        float scale=Mathf.Lerp(1.5f,1,1-Mathf.Pow(1-Mathf.Clamp01(frac*3.5f),3));
        Color c=n>0?new Color(1,.88f,.52f):new Color(.16f,1,.85f);
        Vector2 anchor=new Vector2(Width*.5f,Height*.5f-30);
        Ring2D(anchor,86+frac*34,new Color(c.r,c.g,c.b,.3f*(1-frac)));
        Matrix4x4 old=GUI.matrix;
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(anchor.x,anchor.y,0),Quaternion.identity,new Vector3(scale,scale,1));
        GUI.color=c;Text(new Rect(-160,-70,320,140),n>0?n.ToString():"GO",count);GUI.color=Color.white;
        GUI.matrix=old;
        GUI.color=new Color(.63f,.83f,.9f,.75f);
        Text(new Rect(Width*.5f-240,Height*.5f+72,480,22),"HOLD FOR LAUNCH   /   WEAPONS COLD",centered);
        GUI.color=Color.white;
    }
    void ResultsPanel()
    {
        Box(new Rect(0,0,Width,Height),new Color(.01f,.02f,.05f,.72f));
        Box(new Rect(340,92,600,516),new Color(.012f,.03f,.055f,.94f));
        Line(new Vector2(340,93),new Vector2(940,93),new Color(1,.74f,.18f,.85f),3);
        Text(new Rect(340,116,600,44),"MATCH COMPLETE",clock);
        if(standings.Count>0)
        {
            GUI.color=new Color(1,.82f,.4f);
            Text(new Rect(340,166,600,22),"WINNER   "+standings[0].callsign+"   /   "+standings[0].score+" BANKED",centered);
        }
        GUI.color=new Color(.63f,.83f,.9f,.6f);
        Text(new Rect(372,206,240,20),"PILOT",small);
        Text(new Rect(600,206,60,20),"KILLS",right);
        Text(new Rect(690,206,60,20),"LOST",right);
        Text(new Rect(790,206,118,20),"BANKED",right);
        GUI.color=Color.white;
        Line(new Vector2(372,230),new Vector2(908,230),new Color(.3f,.6f,.7f,.35f));
        for(int i=0;i<standings.Count;i++)
        {
            var p=standings[i];float y=242+i*34;
            if(p==player)Box(new Rect(356,y-3,568,30),new Color(.08f,.5f,.6f,.3f));
            GUI.color=i==0?new Color(1,.82f,.4f):Color.white;
            Text(new Rect(372,y,240,24),(i+1)+".   "+p.callsign,normal);
            Text(new Rect(600,y+1,60,22),p.kills.ToString(),right);
            Text(new Rect(690,y+1,60,22),p.deaths.ToString(),right);
            Text(new Rect(790,y,118,24),p.score.ToString(),rightBig);
            GUI.color=Color.white;
        }
        Line(new Vector2(372,528),new Vector2(908,528),new Color(.3f,.6f,.7f,.35f));
        GUI.color=new Color(.9f,.96f,1,.55f+.45f*Mathf.Sin(Time.unscaledTime*3.4f));
        Text(new Rect(340,552,600,26),"PRESS  ENTER  TO  RESTART",banner);
        GUI.color=Color.white;
    }
    void Box(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
    void Text(Rect r,string s,GUIStyle style){GUI.Label(r,s,style);}
    void Line(Vector2 a,Vector2 b,Color c,float width=1)
    {
        Matrix4x4 old=GUI.matrix;
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
        Box(new Rect(0,-width*.5f,(b-a).magnitude,width),c);GUI.matrix=old;
    }
    void Ring2D(Vector2 center,float radius,Color c)
    {
        for(int i=0;i<40;i++)
        {
            float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
            Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c);
        }
    }
    Vector2 Project(Vector3 p,out bool visible)
    {
        Vector3 v=cam.WorldToViewportPoint(p);visible=v.z>0 && v.x>0 && v.x<1 && v.y>0 && v.y<1;
        return new Vector2(v.x*Width,(1-v.y)*Height);
    }
    void NavMarker(Vector3 location,Color c,bool circle)
    {
        if(!VisibleBetween(player.transform.position,location))return;
        bool visible;Vector2 p=Project(location,out visible);
        if(!visible)
        {
            Vector3 relative=cam.transform.InverseTransformPoint(location);
            Vector2 dir=new Vector2(relative.x,-relative.y);
            if(dir.sqrMagnitude<.01f)dir=Vector2.right;
            p=new Vector2(Width*.5f,Height*.5f)+dir.normalized*280;
            p.x=Mathf.Clamp(p.x,35,Width-280);p.y=Mathf.Clamp(p.y,100,Height-130);
        }
        if(circle)Ring2D(p,10,c);
        else {Line(p+Vector2.up*8,p+Vector2.right*8,c);Line(p+Vector2.right*8,p+Vector2.down*8,c);Line(p+Vector2.down*8,p+Vector2.left*8,c);Line(p+Vector2.left*8,p+Vector2.up*8,c);}
        if(Vector3.Distance(player.transform.position,location)>150)
            Text(new Rect(p.x+15,p.y-8,100,20),Mathf.RoundToInt(Vector3.Distance(player.transform.position,location))+" m",small);
    }
    void OnGUI()
    {
        if(!player || !cam)return;Styles();
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/Width,Screen.height/Height,1));
        Color cyan=new Color(.16f,.88f,1),goldColor=new Color(1,.74f,.18f),orange=new Color(1,.32f,.13f);
        Box(new Rect(20,20,245,68),new Color(.01f,.025f,.045f,.8f));
        Text(new Rect(34,24,220,36),"R I F T",title);
        Text(new Rect(35,60,220,23),player.Altitude>550?"SUBORBITAL COAST":"ATMOSPHERIC FLIGHT",small);
        MatchClock();
        BountyStrip();

        if(Time.unscaledTime>standingsRefresh || standings.Count==0)
        {
            standingsRefresh=Time.unscaledTime+.5f;standings.Clear();standings.AddRange(pilots);
            standings.Sort((a,b)=>b.score!=a.score?b.score.CompareTo(a.score):b.cargo.CompareTo(a.cargo));
        }
        Box(new Rect(1010,20,250,265),new Color(.01f,.025f,.045f,.85f));
        Text(new Rect(1028,29,220,26),"PILOT RANKINGS",normal);
        for(int i=0;i<standings.Count;i++)
        {
            var p=standings[i];float y=65+i*25;
            if(p==player)Box(new Rect(1018,y-1,232,24),new Color(.08f,.5f,.6f,.35f));
            GUI.color=p.Alive?Color.white:new Color(.5f,.5f,.5f);
            Text(new Rect(1027,y,118,22),(i+1)+". "+p.callsign,small);
            // A four-letter disposition beside every callsign: by the second match the player knows who to hunt and who to avoid.
            bool marked=p.id==aceId;
            GUI.color=marked?new Color(1,.8f,.28f):p.Alive?new Color(.45f,.66f,.76f):new Color(.36f,.4f,.44f);
            Text(new Rect(1139,y+2,46,20),marked?"ACE":ArenaPersonality.For(p.id).tag,tag);
            GUI.color=p.Alive?Color.white:new Color(.5f,.5f,.5f);
            Text(new Rect(1188,y,54,22),p.score.ToString(),right);GUI.color=Color.white;
        }
        KillBanner();

        Box(new Rect(20,Height-108,885,88),new Color(.01f,.025f,.045f,.82f));
        Text(new Rect(36,Height-96,180,26),Mathf.RoundToInt(player.Speed*3.6f)+"  km/h",normal);
        Text(new Rect(36,Height-64,180,24),"ALT "+Mathf.RoundToInt(player.Altitude)+" m",small);
        Text(new Rect(208,Height-96,130,26),"THR "+Mathf.RoundToInt(player.throttle*100)+"%",normal);
        Box(new Rect(210,Height-55,110,5),new Color(.2f,.25f,.3f));
        Box(new Rect(210,Height-55,110*player.fuel,5),cyan);
        Text(new Rect(353,Height-96,140,26),"HULL "+Mathf.CeilToInt(player.health),normal);
        Box(new Rect(355,Height-55,110,5),new Color(.25f,.14f,.1f));
        Box(new Rect(355,Height-55,110*player.health/100,5),orange);
        // A full hold is a flight-model penalty, so the readout has to warn before the handling does.
        bool heavy=player.cargo>=60;
        GUI.color=heavy?goldColor:Color.white;
        Text(new Rect(495,Height-96,180,26),"CARGO "+player.cargo,normal);
        GUI.color=Color.white;
        if(heavy)
        {
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*6);
            Box(new Rect(592,Height-93,62,20),new Color(1,.74f,.18f,.14f+pulse*.16f));
            GUI.color=new Color(1,.84f,.35f,.6f+pulse*.4f);
            Text(new Rect(598,Height-94,60,20),"HEAVY",small);GUI.color=Color.white;
        }
        Text(new Rect(495,Height-64,180,24),"BANKED "+player.score,small);
        Text(new Rect(681,Height-96,190,26),player.seekerCooldown>0?"SEEKER "+player.seekerCooldown.ToString("0.0")+"s":"SEEKER READY",small);
        Box(new Rect(685,Height-55,175,5),new Color(.2f,.25f,.3f));
        Box(new Rect(685,Height-55,175*(1-player.heat),5),cyan);

        // Radar uses the aircraft frame and includes targets behind the camera.
        Vector2 radar=new Vector2(1170,600);Ring2D(radar,73,new Color(.3f,.6f,.7f,.6f));Ring2D(radar,36,new Color(.3f,.6f,.7f,.3f));
        Line(radar+Vector2.up*73,radar+Vector2.down*73,new Color(.3f,.6f,.7f,.3f));
        Line(radar+Vector2.left*73,radar+Vector2.right*73,new Color(.3f,.6f,.7f,.3f));
        foreach(var p in pilots)
        {
            if(p==player || !p.Alive)continue;
            Vector3 d=player.transform.InverseTransformDirection(p.transform.position-player.transform.position);
            Vector2 point=Vector2.ClampMagnitude(new Vector2(d.x,-d.z)/14,69);
            Box(new Rect(radar.x+point.x-2,radar.y+point.y-2,4,4),orange);
        }
        Box(new Rect(radar.x-2,radar.y-2,4,4),cyan);
        var gate=NearestGate(player.transform.position);if(gate)NavMarker(gate.transform.position,cyan,true);
        var core=NearestCore(player.transform.position);if(core)NavMarker(core.transform.position,goldColor,false);
        foreach(var p in pilots)
        {
            if(p==player || !p.Alive || Vector3.Distance(p.transform.position,player.transform.position)>800 || !VisibleBetween(player.transform.position,p.transform.position))continue;
            bool visible;Vector2 v=Project(p.transform.position,out visible);if(!visible)continue;
            Line(v+new Vector2(-10,-8),v+new Vector2(-10,8),orange);
            Line(v+new Vector2(10,-8),v+new Vector2(10,8),orange);
            Text(new Rect(v.x+14,v.y-10,150,20),p.callsign,small);
        }
        if(player.Alive)
        {
            Vector2 stickCenter=new Vector2(Width*.5f,Height*.5f);
            Vector2 stickTip=stickCenter+new Vector2(mouseStick.x,-mouseStick.y)*85;
            if(mouseStick.sqrMagnitude>.005f)
            {
                Line(stickCenter,stickTip,new Color(.65f,.88f,1,.45f));
                Ring2D(stickTip,5,new Color(.8f,.95f,1,.8f));
            }
            bool visible;Vector2 bore=Project(player.transform.position+player.transform.forward*350,out visible);
            Color c=hitFlash>0?Color.white:cyan;
            Ring2D(bore,17,c);Box(new Rect(bore.x-1,bore.y-1,3,3),c);
            Vector3 targetVelocity;Transform target=AimTarget(player,6,out targetVelocity);
            if(target)
            {
                Vector3 lead=InterceptPoint(player,target.position,targetVelocity,360);
                Vector2 t=Project(lead,out visible);if(visible){Ring2D(t,6,goldColor);Line(bore,t,new Color(1,.75f,.2f,.4f));}
            }
            if(player.Speed<45 && player.Altitude<500)Text(new Rect(530,Height-170,300,28),"STALL",normal);
            if(player.Altitude<40)Text(new Rect(530,Height-205,300,28),"PULL UP",normal);
            if(lastAttackAge>0 && lastAttackDirection.sqrMagnitude>.01f)
            {
                Vector3 local=cam.transform.InverseTransformDirection(lastAttackDirection);
                HitWedge(Mathf.Atan2(local.x,local.z),Mathf.Clamp01(lastAttackAge));
            }
            if(damageFlash>0)Box(new Rect(0,0,Width,Height),new Color(1,.03f,.01f,damageFlash*.4f));
        }
        else
        {
            Box(new Rect(0,0,Width,Height),new Color(.02f,.03f,.06f,.35f));
            Text(new Rect(525,310,330,48),"REDEPLOYING  "+Mathf.CeilToInt(player.respawn),title);
        }
        if(elapsed<18 || paused)
            Text(new Rect(30,110,975,30),"Mouse: pitch / bank   C: center   WASD: pitch / bank   QE: rudder   Shift / Ctrl: throttle   Space: boost   LMB: cannon   RMB: seeker",small);
        if(phase==MatchPhase.Countdown)CountdownCard();
        else if(phase==MatchPhase.Ended)ResultsPanel();
        if(paused)Text(new Rect(500,340,320,44),"PAUSED  /  ESC",title);
    }
}
