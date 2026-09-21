using System.Collections.Generic;
using UnityEngine;

public partial class AerialCombatPrototype
{
    GUIStyle small,normal,title,right;
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
        stylesReady=true;
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
            Text(new Rect(1027,y,170,22),(i+1)+". "+p.callsign,small);
            Text(new Rect(1188,y,54,22),p.score.ToString(),right);GUI.color=Color.white;
        }

        Box(new Rect(20,Height-108,885,88),new Color(.01f,.025f,.045f,.82f));
        Text(new Rect(36,Height-96,180,26),Mathf.RoundToInt(player.Speed*3.6f)+"  km/h",normal);
        Text(new Rect(36,Height-64,180,24),"ALT "+Mathf.RoundToInt(player.Altitude)+" m",small);
        Text(new Rect(208,Height-96,130,26),"THR "+Mathf.RoundToInt(player.throttle*100)+"%",normal);
        Box(new Rect(210,Height-55,110,5),new Color(.2f,.25f,.3f));
        Box(new Rect(210,Height-55,110*player.fuel,5),cyan);
        Text(new Rect(353,Height-96,140,26),"HULL "+Mathf.CeilToInt(player.health),normal);
        Box(new Rect(355,Height-55,110,5),new Color(.25f,.14f,.1f));
        Box(new Rect(355,Height-55,110*player.health/100,5),orange);
        Text(new Rect(495,Height-96,180,26),"CARGO "+player.cargo,normal);
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
            if(damageFlash>0)Box(new Rect(0,0,Width,Height),new Color(1,.03f,.01f,damageFlash*.4f));
        }
        else
        {
            Box(new Rect(0,0,Width,Height),new Color(.02f,.03f,.06f,.35f));
            Text(new Rect(525,310,330,48),"REDEPLOYING  "+Mathf.CeilToInt(player.respawn),title);
        }
        if(elapsed<18 || paused)
            Text(new Rect(30,110,975,30),"Mouse: pitch / bank   C: center   WASD: pitch / bank   QE: rudder   Shift / Ctrl: throttle   Space: boost   LMB: cannon   RMB: seeker",small);
        if(paused)Text(new Rect(500,340,320,44),"PAUSED  /  ESC",title);
    }
}
