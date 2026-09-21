using System.Collections.Generic;
using UnityEngine;

public partial class AerialCombatPrototype
{
    GUIStyle small,normal,title,right,banner,clock,count,centered,rightBig,tag,bounty;
    bool stylesReady;
    readonly List<ArenaPilot> standings=new List<ArenaPilot>();
    float standingsRefresh;
    const float Width=1280,Height=720;
    // One row of the event feed. A struct in a plain list: the feed is at most four entries, so nothing here is worth an object.
    public struct ToastRow { public string text; public Color color; public float life,age; }
    public const float ToastLife=3;
    public const int ToastRows=4;
    public readonly List<ToastRow> toasts=new List<ToastRow>();
    // Purely-visual timers, all on unscaled time: number pops, the hitmarker kick, the acquisition snap.
    public float cargoPop,bankPop,hitPop,hitGold,preciseTag,targetLock;
    // The reticle's lock, resolved once per frame in Update because OnGUI runs twice and must never pick a target itself.
    public Transform hudTarget;
    public Vector3 hudTargetVelocity;
    // Nearest live seeker that has the player as its target: -1 when nothing is inbound.
    public float missileRange=-1,missileBeep;
    // Lock tone spacing and the one-shot edge for the confirmation, both driven from LockTick's simulation dt.
    public float lockBeep; bool lockAnnounced;
    public Vector3 missileSource;
    public bool overheated;
    // Comfort settings, persisted in PlayerPrefs and edited from the pause overlay.
    public float shakeScale=.8f;
    public bool reduceFlashing,reduceCameraMotion;
    public int accessRow;
    public void LoadComfort()
    {
        shakeScale=Mathf.Clamp01(PlayerPrefs.GetFloat("rift.shake",.8f));
        reduceFlashing=PlayerPrefs.GetInt("rift.reduceFlashing",0)==1;
        reduceCameraMotion=PlayerPrefs.GetInt("rift.reduceMotion",0)==1;
    }
    public void SaveComfort()
    {
        PlayerPrefs.SetFloat("rift.shake",shakeScale);
        PlayerPrefs.SetInt("rift.reduceFlashing",reduceFlashing?1:0);
        PlayerPrefs.SetInt("rift.reduceMotion",reduceCameraMotion?1:0);
        PlayerPrefs.Save();
    }
    // Newest first, oldest pushed off the bottom: a feed that grows past four rows stops being readable in a dogfight.
    public void Toast(string text,Color c)
    {
        toasts.Insert(0,new ToastRow{text=text,color=c,life=ToastLife,age=0});
        while(toasts.Count>ToastRows)toasts.RemoveAt(toasts.Count-1);
    }
    // Every HUD-only timer in one dt method so the verification harness can advance the feed without a frame.
    public void TickHud(float dt)
    {
        for(int i=toasts.Count-1;i>=0;i--)
        {
            var t=toasts[i];t.life-=dt;t.age+=dt;
            if(t.life<=0)toasts.RemoveAt(i);else toasts[i]=t;
        }
        cargoPop=Mathf.Max(0,cargoPop-dt);bankPop=Mathf.Max(0,bankPop-dt);
        hitPop=Mathf.Max(0,hitPop-dt);hitGold=Mathf.Max(0,hitGold-dt);
        preciseTag=Mathf.Max(0,preciseTag-dt);targetLock=Mathf.Max(0,targetLock-dt);
    }
    // Landed-hit confirmation, player-only: the crosshair kicks, and a precise pass gilds it and prints the multiplier.
    public void Hitmarker(bool precise)
    {
        hitPop=.12f;
        if(precise){hitGold=.4f;preciseTag=.4f;}
    }
    // Runs on the simulation clock from FixedUpdate: the tone has to repeat at a rate, and a rate needs a real dt.
    public void MissileTick(float dt)
    {
        missileRange=-1;
        if(player && player.Alive && MatchActive)
        {
            float best=float.MaxValue;
            foreach(var b in bolts)
            {
                if(!b || !b.seeker || b.target!=player.transform)continue;
                float d=Vector3.Distance(b.transform.position,player.transform.position);
                if(d<best){best=d;missileSource=b.transform.position;}
            }
            if(best<float.MaxValue)missileRange=best;
        }
        if(missileRange<0){missileBeep=0;return;}
        // 1.0 s of spacing at 600 m down to .12 s at 40 m: the ear reads the range without ever reading the number.
        float interval=Mathf.Lerp(.12f,1f,Mathf.Clamp01((missileRange-40)/560));
        missileBeep-=dt;
        if(missileBeep<=0){missileBeep=interval;if(audioSource)audioSource.PlayOneShot(missileTone,.5f);}
    }
    // The acquisition tone: a rising pip every .3 s while the lock builds, one confirmation the frame it completes.
    // Same reasoning as MissileTick — this is a rate, so it runs on the simulation clock from FixedUpdate.
    public void LockTick(float dt)
    {
        if(!player || !player.Alive || !player.lockTarget){lockBeep=0;lockAnnounced=false;return;}
        if(player.lockTimer>=ArenaPilot.LockTime)
        {
            if(!lockAnnounced){lockAnnounced=true;if(audioSource)audioSource.PlayOneShot(lockConfirm,.5f);}
            lockBeep=0;return;
        }
        lockAnnounced=false;
        lockBeep-=dt;
        if(lockBeep<=0){lockBeep=.3f;if(audioSource)audioSource.PlayOneShot(lockTone,.4f);}
    }
    // Scope mapping shared by every radar blip: the aircraft's own frame, 14 m per pixel, pinned to the 69 px rim beyond
    // ~965 m so a far contact still shows its bearing. Public and pure so the harness can check the geometry.
    public Vector2 RadarPoint(Vector3 world)
    {
        Vector3 d=player.transform.InverseTransformDirection(world-player.transform.position);
        return Vector2.ClampMagnitude(new Vector2(d.x,-d.z)/14,69);
    }
    // Back-out ease: 1.25x on the frame the number changes, a shallow undershoot, then home inside .18 s.
    float Pop(float timer){ if(timer<=0)return 1; float t=1-Mathf.Clamp01(timer/.18f); return 1+.25f*(1-t)*Mathf.Cos(t*Mathf.PI*1.5f); }
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
    void HitWedge(float bearing,float alpha){ HitWedge(bearing,alpha,new Color(1,.17f,.09f)); }
    void HitWedge(float bearing,float alpha,Color hue)
    {
        Vector2 c=new Vector2(Width*.5f,Height*.5f);
        Vector2 d=new Vector2(Mathf.Sin(bearing),-Mathf.Cos(bearing)),n=new Vector2(-d.y,d.x);
        Color col=new Color(hue.r,hue.g,hue.b,alpha);
        Vector2 tip=c+d*172,left=c+d*130-n*36,rightEdge=c+d*130+n*36;
        Line(left,tip,col,3);Line(tip,rightEdge,col,3);
        Color inner=new Color(Mathf.Min(1,hue.r+.13f),Mathf.Min(1,hue.g+.13f),Mathf.Min(1,hue.b+.07f),alpha*.5f);
        Line(c+d*118-n*20,c+d*146,inner,2);
        Line(c+d*146,c+d*118+n*20,inner,2);
    }
    // Warnings stack upward from a single slot, so three of them at once never print on top of each other.
    void Warning(ref int slot,string text,Color c)
    {
        float y=Height-170-slot*33;slot++;
        GUI.color=new Color(c.r,c.g,c.b,reduceFlashing?.92f:.6f+.4f*Mathf.Sin(Time.unscaledTime*7));
        Text(new Rect(530,y,300,28),text,normal);GUI.color=Color.white;
    }
    // Event feed under the rankings and clear of the kill banner, which ends at y=331. Four rows of 30 reach y=460,
    // well above the radar at y=527: the right column stays a single column with nothing stacked on anything.
    void ToastFeed()
    {
        for(int i=0;i<toasts.Count;i++)
        {
            var t=toasts[i];
            // Ease-out cubic slide from 268 px off the right edge over .18 s; fade only over the last half second of life.
            float enter=Mathf.Clamp01(t.age/.18f),slide=1-Mathf.Pow(1-enter,3);
            float x=1010+(1-slide)*268,a=Mathf.Clamp01(t.life/.5f),y=340+i*30;
            Box(new Rect(x,y,250,26),new Color(.01f,.025f,.045f,.72f*a));
            Line(new Vector2(x+1,y),new Vector2(x+1,y+26),new Color(t.color.r,t.color.g,t.color.b,a),3);
            GUI.color=new Color(t.color.r,t.color.g,t.color.b,a);
            Text(new Rect(x+13,y+3,232,20),t.text,small);
            GUI.color=Color.white;
        }
    }
    // The one thing the reticle is locked onto gets a full read: brackets, hull state, callsign, range.
    void TargetBox(Transform t,Vector2 bore,Vector3 targetVelocity,bool lead)
    {
        bool visible;Vector2 v=Project(t.position,out visible);
        var foe=t.GetComponent<ArenaPilot>();var wreck=t.GetComponent<SalvageCore>();
        float distance=Vector3.Distance(player.transform.position,t.position);
        Color c=foe?new Color(1,.45f,.18f):wreck && wreck.kind==CoreKind.Volatile?new Color(.78f,.38f,1):wreck && wreck.kind==CoreKind.Armored?new Color(.81f,.87f,.95f):new Color(1,.74f,.18f);
        // A completed seeker lock repaints the whole box: the same brackets, in the colour of a missile about to leave the rail.
        bool locked=player.lockTarget==t && player.lockTimer>=ArenaPilot.LockTime;
        if(locked)c=new Color(1,.2f,.12f);
        if(visible)
        {
            // Sized on range: a 40 m pass frames the whole hull at 55 px, a 600 m speck still reads as a 15 px box.
            float half=Mathf.Clamp(2200/Mathf.Max(40,distance),15,55);
            // Acquisition snap: 1.4x down to 1x over .15 s, so a new lock is felt instead of merely appearing.
            half*=Mathf.Lerp(1,1.4f,Mathf.Clamp01(targetLock/.15f));
            float arm=half*.42f;
            for(int q=0;q<4;q++)
            {
                Vector2 d=new Vector2(q==0||q==3?-1:1,q<2?-1:1);
                Vector2 corner=v+new Vector2(d.x*half,d.y*half);
                Line(corner,corner-new Vector2(d.x*arm,0),c,2);
                Line(corner,corner-new Vector2(0,d.y*arm),c,2);
            }
            float fraction=foe?Mathf.Clamp01(foe.health/100):wreck?Mathf.Clamp01(wreck.health/Mathf.Max(1,wreck.maxHealth)):0;
            float barY=v.y+half+8;
            Box(new Rect(v.x-22,barY,44,4),new Color(.09f,.11f,.14f,.85f));
            Box(new Rect(v.x-22,barY,44*fraction,4),foe?new Color(1,.32f,.13f):c);
            string label=foe?foe.callsign.ToUpper():wreck?(wreck.kind==CoreKind.Volatile?"VOLATILE WRECK":wreck.kind==CoreKind.Armored?"ARMORED WRECK":"SALVAGE WRECK"):"TARGET";
            GUI.color=c;
            Text(new Rect(v.x-90,barY+6,180,18),(locked?"LOCK   ":"")+label+"   "+Mathf.RoundToInt(distance)+" m",centered);
            GUI.color=Color.white;
        }
        if(!lead)return;
        Vector3 ahead=InterceptPoint(player,t.position,targetVelocity,360);
        Vector2 l=Project(ahead,out visible);
        if(visible){Ring2D(l,6,new Color(1,.74f,.18f));Line(bore,l,new Color(1,.75f,.2f,.4f));}
    }
    // The lock reads as a clamp closing: a ring shrinking 40 px to 14 px over the 1.2 s with four chevrons riding it in,
    // then a steady red ring and the word LOCK. Off-frame, it falls back to a chevron on the reticle ring so a lock the
    // player earned and then rolled away from is still something they can find again.
    void LockRing(Transform t,Vector2 bore)
    {
        float progress=Mathf.Clamp01(player.lockTimer/ArenaPilot.LockTime);
        bool locked=progress>=1;
        if(t!=hudTarget)TargetBox(t,bore,Vector3.zero,false);
        bool visible;Vector2 v=Project(t.position,out visible);
        if(!visible)
        {
            Vector3 local=cam.transform.InverseTransformDirection(t.position-player.transform.position);
            if(local.sqrMagnitude>.01f)HitWedge(Mathf.Atan2(local.x,local.z),locked?.9f:.5f,new Color(1,.42f,.14f));
            return;
        }
        float radius=Mathf.Lerp(40,14,progress);
        Color c=locked?new Color(1,.2f,.12f):new Color(1,.62f,.2f);
        float pulse=locked?(reduceFlashing?.9f:.55f+.45f*Mathf.Sin(Time.unscaledTime*9)):.85f;
        Ring2D(v,radius,new Color(c.r,c.g,c.b,pulse));
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f+(locked?Mathf.PI*.25f:progress*2.6f);
            Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            Line(v+d*(radius+4),v+d*(radius+13),new Color(c.r,c.g,c.b,pulse),2);
        }
        if(locked)
        {
            GUI.color=new Color(c.r,c.g,c.b,pulse);
            Text(new Rect(v.x-60,v.y-radius-25,120,20),"LOCK",centered);
            GUI.color=Color.white;
        }
    }
    // Three rows a player can reach from the keyboard mid-match: the only settings that change what the eyes have to take.
    void ComfortPanel()
    {
        float x=470,y=392,w=340,h=140;
        Box(new Rect(x,y,w,h),new Color(.012f,.03f,.055f,.95f));
        Line(new Vector2(x,y),new Vector2(x+w,y),new Color(.16f,.88f,1,.75f),2);
        GUI.color=new Color(.63f,.83f,.9f,.72f);
        Text(new Rect(x+20,y+7,w-40,18),"COMFORT",small);
        GUI.color=Color.white;
        string[] labels={"SHAKE","REDUCE FLASHING","REDUCE CAMERA MOTION"};
        string[] values={Mathf.RoundToInt(shakeScale*100)+"%",reduceFlashing?"ON":"OFF",reduceCameraMotion?"ON":"OFF"};
        for(int i=0;i<3;i++)
        {
            float ry=y+30+i*31;bool on=i==accessRow;
            if(on)Box(new Rect(x+10,ry-2,w-20,27),new Color(.08f,.5f,.6f,.34f));
            GUI.color=on?Color.white:new Color(.63f,.83f,.9f,.78f);
            Text(new Rect(x+20,ry,236,23),(on?"> ":"   ")+labels[i],small);
            Text(new Rect(x+w-124,ry-1,100,23),values[i],right);
            GUI.color=Color.white;
        }
        GUI.color=new Color(.63f,.83f,.9f,.55f);
        Text(new Rect(x,y+h-24,w,18),"UP DOWN select   LEFT RIGHT adjust   1 2 3 jump",centered);
        GUI.color=Color.white;
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
        Box(new Rect(340,92,600,598),new Color(.012f,.03f,.055f,.94f));
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
        // Awards: four things the standings cannot say, in the tag colour so they read as flavour, not as another column.
        for(int i=0;i<awards.Count && i<4;i++)
        {
            GUI.color=i==0?new Color(1,.82f,.4f,.95f):new Color(.63f,.83f,.9f,.9f);
            Text(new Rect(372,538+i*20,536,20),awards[i],small);
        }
        Line(new Vector2(372,622),new Vector2(908,622),new Color(.3f,.6f,.7f,.35f));
        GUI.color=new Color(.9f,.96f,1,.55f+.45f*Mathf.Sin(Time.unscaledTime*3.4f));
        Text(new Rect(340,634,600,26),"PRESS  ENTER  TO  RESTART",banner);
        // Difficulty picker under the restart prompt: arrows on either side, the current tier in the tag colour.
        GUI.color=new Color(.63f,.83f,.9f,.85f);
        Text(new Rect(340,664,600,20),"◀   RIVALS:  "+ArenaPersonality.DifficultyNames[Mathf.Clamp(difficulty,0,2)]+"   ▶      (left / right, next match)",centered);
        GUI.color=Color.white;
    }
    // One line of onboarding under the title block: the current objective, a pop when it advances, gone for good once
    // the loop has closed once on this machine.
    void ObjectiveCard()
    {
        if(tutorialStep>=4 && tutorialPop<=0)return;
        float t=Mathf.Clamp01(1-tutorialPop/(tutorialStep>=4?4f:.5f));
        float scale=tutorialStep>=4?1:Mathf.Lerp(1.25f,1,1-Mathf.Pow(1-Mathf.Clamp01(t*3),3));
        float fade=tutorialStep>=4?Mathf.Clamp01(tutorialPop/.6f):1;
        Matrix4x4 old=GUI.matrix;
        // 340 wide: the first HUD capture from the built player showed the 244 px card wrapping its longest line.
        GUI.matrix=old*Matrix4x4.TRS(new Vector3(190,112,0),Quaternion.identity,new Vector3(scale,scale,1));
        Box(new Rect(-170,-14,340,28),new Color(.01f,.025f,.045f,.8f*fade));
        Line(new Vector2(-170,14),new Vector2(170,14),tutorialStep>=4?new Color(1,.74f,.18f,.8f*fade):new Color(.16f,.88f,1,.6f*fade),2);
        GUI.color=tutorialStep>=4?new Color(1,.88f,.52f,fade):new Color(.63f,.83f,.9f,fade);
        Text(new Rect(-166,-13,336,26),(tutorialStep>=4?"":"OBJECTIVE   ")+Objectives[Mathf.Clamp(tutorialStep,0,4)],small);
        GUI.color=Color.white;GUI.matrix=old;
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
    // Screen position for a world point, pinned to a ring around the reticle when it falls outside the frame.
    Vector2 MarkerPoint(Vector3 location)
    {
        bool visible;Vector2 p=Project(location,out visible);
        if(visible)return p;
        Vector3 relative=cam.transform.InverseTransformPoint(location);
        Vector2 dir=new Vector2(relative.x,-relative.y);
        if(dir.sqrMagnitude<.01f)dir=Vector2.right;
        p=new Vector2(Width*.5f,Height*.5f)+dir.normalized*280;
        p.x=Mathf.Clamp(p.x,35,Width-280);p.y=Mathf.Clamp(p.y,100,Height-130);
        return p;
    }
    // The surge marker deliberately ignores both range and the horizon test every other marker respects: a 30 s
    // window is worthless if the ring only appears once you already happen to be looking at the right continent.
    void SurgeMarker(CaptureGate gate)
    {
        Vector2 p=MarkerPoint(gate.transform.position);
        float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*5.5f);
        Color violetHud=new Color(.76f,.36f,1,.55f+pulse*.45f);
        Ring2D(p,13+pulse*4,violetHud);
        Ring2D(p,7,new Color(.9f,.66f,1,.7f+pulse*.3f));
        // Four closing chevrons: the same read as a countdown clamp tightening on the ring.
        for(int i=0;i<4;i++)
        {
            float a=Time.unscaledTime*1.3f+i*Mathf.PI*.5f;
            Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            Line(p+d*(21+pulse*3),p+d*(28+pulse*3),violetHud,2);
        }
        float x=Mathf.Min(p.x+30,Width-196);
        GUI.color=new Color(.87f,.63f,1,.75f+pulse*.25f);
        Text(new Rect(x,p.y-19,190,18),"OVERCHARGE  x2",small);
        Text(new Rect(x,p.y-1,190,18),Mathf.CeilToInt(gate.overcharge)+"s   "+Mathf.RoundToInt(Vector3.Distance(player.transform.position,gate.transform.position))+" m",small);
        GUI.color=Color.white;
    }
    void NavMarker(Vector3 location,Color c,bool circle)
    {
        if(!VisibleBetween(player.transform.position,location))return;
        Vector2 p=MarkerPoint(location);
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
        // The coast line is also the salvage-doubling line, so the band label is where the player learns the rule.
        GUI.color=player.Altitude>550?new Color(1,.82f,.4f):Color.white;
        Text(new Rect(35,60,220,23),player.Altitude>550?"SUBORBITAL COAST   SALVAGE x2":"ATMOSPHERIC FLIGHT",small);
        GUI.color=Color.white;
        MatchClock();
        BountyStrip();
        ObjectiveCard();

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
            bool marked=p.id==aceId,grudge=p.id==vendettaId;
            GUI.color=grudge?new Color(1,.42f,.3f):marked?new Color(1,.8f,.28f):p.Alive?new Color(.45f,.66f,.76f):new Color(.36f,.4f,.44f);
            Text(new Rect(1139,y+2,46,20),grudge?"VNDT":marked?"ACE":ArenaPersonality.For(p.id).tag,tag);
            GUI.color=p.Alive?Color.white:new Color(.5f,.5f,.5f);
            Text(new Rect(1188,y,54,22),p.score.ToString(),right);GUI.color=Color.white;
        }
        KillBanner();
        ToastFeed();

        Box(new Rect(20,Height-108,885,88),new Color(.01f,.025f,.045f,.82f));
        Text(new Rect(36,Height-96,180,26),Mathf.RoundToInt(player.Speed*3.6f)+"  km/h",normal);
        Text(new Rect(36,Height-64,180,24),"ALT "+Mathf.RoundToInt(player.Altitude)+" m",small);
        Text(new Rect(208,Height-96,130,26),"THR "+Mathf.RoundToInt(player.throttle*100)+"%",normal);
        Box(new Rect(210,Height-55,110,5),new Color(.2f,.25f,.3f));
        Box(new Rect(210,Height-55,110*player.fuel,5),cyan);
        Text(new Rect(353,Height-96,140,26),"HULL "+Mathf.CeilToInt(player.health),normal);
        Box(new Rect(355,Height-55,110,5),new Color(.25f,.14f,.1f));
        Box(new Rect(355,Height-55,110*player.health/100,5),orange);
        // Skin temperature is an ember-red sliver pinned under the hull bar, deliberately nowhere near the cyan
        // weapon-heat bar at the right end of the strip: a full bar here means the airframe is already burning.
        float temp=Mathf.Clamp01(player.hullHeat);bool burning=player.hullHeat>1;
        GUI.color=burning?new Color(1,.55f,.22f):new Color(1,1,1,.7f);
        // Under the HULL number, not beside it: the first built-player capture had "SKIN 0%" printed through "HULL 100".
        Text(new Rect(355,Height-72,110,18),"SKIN "+Mathf.RoundToInt(player.hullHeat*100)+"%",small);
        GUI.color=Color.white;
        Box(new Rect(355,Height-45,110,3),new Color(.22f,.1f,.07f));
        Box(new Rect(355,Height-45,110*temp,3),burning?Color.Lerp(new Color(1,.45f,.12f),new Color(2.4f,1.6f,.9f),.5f+.5f*Mathf.Sin(Time.unscaledTime*9)):Color.Lerp(new Color(.8f,.34f,.1f),new Color(1.7f,.44f,.1f),temp));
        // A full hold is a flight-model penalty, so the readout has to warn before the handling does.
        bool heavy=player.cargo>=60;
        GUI.color=heavy?goldColor:Color.white;
        // Every pickup strikes the number rather than sliding it: a local TRS around the label's own left edge.
        Matrix4x4 beforePop=GUI.matrix;float cargoScale=Pop(cargoPop);
        if(cargoPop>0)GUI.matrix=beforePop*Matrix4x4.TRS(new Vector3(499,Height-84,0),Quaternion.identity,new Vector3(cargoScale,cargoScale,1))*Matrix4x4.Translate(new Vector3(-499,-(Height-84),0));
        Text(new Rect(495,Height-96,180,26),"CARGO "+player.cargo,normal);
        GUI.matrix=beforePop;GUI.color=Color.white;
        if(heavy)
        {
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*6);
            Box(new Rect(592,Height-93,62,20),new Color(1,.74f,.18f,.14f+pulse*.16f));
            GUI.color=new Color(1,.84f,.35f,.6f+pulse*.4f);
            Text(new Rect(598,Height-94,60,20),"HEAVY",small);GUI.color=Color.white;
        }
        // Chain readout sits between CARGO and the seeker strip, with a bar that drains over the 3 s window.
        if(player.chainTimer>0 && player.chainCount>1)
        {
            float k=player.chainTimer/ArenaPilot.ChainWindow;
            GUI.color=new Color(1,.84f,.35f,.6f+.4f*k);
            Text(new Rect(600,Height-70,80,18),"CHAIN  x"+player.ChainMultiplier.ToString("0.##"),small);
            Box(new Rect(602,Height-52,60,3),new Color(.3f,.25f,.12f));Box(new Rect(602,Height-52,60*k,3),new Color(1,.74f,.18f));
            GUI.color=Color.white;
        }
        float bankScale=Pop(bankPop);
        if(bankPop>0)GUI.matrix=beforePop*Matrix4x4.TRS(new Vector3(499,Height-52,0),Quaternion.identity,new Vector3(bankScale,bankScale,1))*Matrix4x4.Translate(new Vector3(-499,-(Height-52),0));
        Text(new Rect(495,Height-64,180,24),"BANKED "+player.score,small);
        GUI.matrix=beforePop;
        // One line carries the whole seeker state machine: cooling down, building a lock, holding one, or empty-handed.
        bool holdingLock=player.lockTarget && player.lockTimer>=ArenaPilot.LockTime;
        GUI.color=holdingLock?new Color(1,.34f,.2f):player.lockTarget?new Color(1,.78f,.32f):Color.white;
        Text(new Rect(681,Height-96,190,26),player.seekerCooldown>0?"SEEKER "+player.seekerCooldown.ToString("0.0")+"s":holdingLock?"LOCKED":player.lockTarget?"LOCKING "+(ArenaPilot.LockTime-player.lockTimer).ToString("0.0")+"s":"SEEKER READY",small);
        GUI.color=Color.white;
        // Above .92 the cannon is locked out, so the bar stops reading as remaining capacity and fills solid red instead.
        bool cooked=player.heat>.92f;
        float heatPulse=reduceFlashing?.5f:.5f+.5f*Mathf.Sin(Time.unscaledTime*12);
        Box(new Rect(685,Height-55,175,5),new Color(.2f,.25f,.3f));
        Box(new Rect(685,Height-55,cooked?175:175*(1-player.heat),5),cooked?Color.Lerp(new Color(1,.25f,.1f),new Color(2.2f,.72f,.3f),heatPulse):cyan);
        if(cooked)
        {
            GUI.color=new Color(1,.44f,.2f,reduceFlashing?.92f:.55f+.45f*heatPulse);
            Text(new Rect(685,Height-96,175,26),"OVERHEAT",right);GUI.color=Color.white;
        }

        // Radar uses the aircraft frame and includes targets behind the camera.
        Vector2 radar=new Vector2(1170,600);Ring2D(radar,73,new Color(.3f,.6f,.7f,.6f));Ring2D(radar,36,new Color(.3f,.6f,.7f,.3f));
        Line(radar+Vector2.up*73,radar+Vector2.down*73,new Color(.3f,.6f,.7f,.3f));
        Line(radar+Vector2.left*73,radar+Vector2.right*73,new Color(.3f,.6f,.7f,.3f));
        // Rings first, under the contacts: a hollow blip in the owner colour, the surge one breathing violet. With the
        // fields 670 m apart the next refinery is always on the scope, which is where a laden pilot looks first.
        foreach(var ring in gates)
        {
            Vector2 blip=radar+RadarPoint(ring.transform.position);
            bool contested=false;int inside=0;foreach(var q in pilots)if(q.Alive && Vector3.Distance(q.transform.position,ring.transform.position)<CaptureGate.Radius)inside++;
            contested=inside>1;
            Color ringColor=contested?new Color(1,.18f,.08f):ring.owner==0?new Color(.1f,1,.8f):ring.owner<0?new Color(.1f,.7f,1):orange;
            if(ring.overcharge>0)ringColor=Color.Lerp(ringColor,new Color(.82f,.44f,1),.5f+.5f*Mathf.Sin(Time.unscaledTime*5.5f));
            Ring2D(blip,ring.overcharge>0?6:4,new Color(ringColor.r,ringColor.g,ringColor.b,.85f));
        }
        foreach(var p in pilots)
        {
            if(p==player || !p.Alive)continue;
            Vector2 point=RadarPoint(p.transform.position);
            bool grudge=p.id==vendettaId;
            Box(new Rect(radar.x+point.x-2,radar.y+point.y-2,4,4),grudge?new Color(1,.3f,.2f):p.id==aceId?goldColor:orange);
            if(grudge)Ring2D(radar+point,6+2*Mathf.Sin(Time.unscaledTime*5),new Color(1,.3f,.2f,.8f));
        }
        Box(new Rect(radar.x-2,radar.y-2,4,4),cyan);
        var gate=NearestGate(player.transform.position);if(gate)NavMarker(gate.transform.position,cyan,true);
        var core=NearestCore(player.transform.position);if(core)NavMarker(core.transform.position,goldColor,false);
        var surge=OverchargedGate();if(surge)SurgeMarker(surge);
        // Everything not locked stays a dim pair of ticks: the full box below is reserved for the one target that matters.
        foreach(var p in pilots)
        {
            if(p==player || !p.Alive || hudTarget==p.transform || Vector3.Distance(p.transform.position,player.transform.position)>800 || !VisibleBetween(player.transform.position,p.transform.position))continue;
            bool visible;Vector2 v=Project(p.transform.position,out visible);if(!visible)continue;
            Line(v+new Vector2(-10,-8),v+new Vector2(-10,8),new Color(orange.r,orange.g,orange.b,.5f));
            Line(v+new Vector2(10,-8),v+new Vector2(10,8),new Color(orange.r,orange.g,orange.b,.5f));
            // The vendetta mark is the one rival the player is allowed to want: a red ring that breathes, and a countdown.
            if(p.id==vendettaId)
            {
                float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*5);
                Ring2D(v,19+pulse*4,new Color(1,.3f,.2f,.5f+pulse*.5f));
                GUI.color=new Color(1,.55f,.4f,.9f);
                Text(new Rect(v.x+14,v.y+8,150,18),"VENDETTA  "+Mathf.CeilToInt(vendettaTimer)+"s",small);
            }
            GUI.color=new Color(1,1,1,.6f);
            Text(new Rect(v.x+14,v.y-10,150,20),p.callsign,small);
            GUI.color=Color.white;
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
            // Locked-out weapons turn the whole reticle red; a landed hit whitens it, and reduced flashing keeps that gentle.
            Color c=cooked?new Color(1,.3f,.13f):hitFlash>0?(reduceFlashing?Color.Lerp(cyan,Color.white,.35f):Color.white):cyan;
            // The ring IS the cone: 17 px cold, 26 px at the overheat gate, so the dispersion the gun has picked up is
            // something the player watches open under sustained fire rather than a number they have to infer from misses.
            float aperture=17+Mathf.Clamp01(player.heat)*9;
            Ring2D(bore,aperture,c);Box(new Rect(bore.x-1,bore.y-1,3,3),c);
            // Four corner ticks kick 6 px outward on a landed hit and ease home over .12 s: the confirmation lives on the crosshair.
            float kick=hitPop>0?6*Mathf.Pow(hitPop/.12f,.6f):0;
            Color tickColor=hitGold>0?new Color(1,.82f,.28f):c;
            for(int q=0;q<4;q++)
            {
                Vector2 d=new Vector2(q==0||q==3?-1:1,q<2?-1:1);
                Line(bore+d*(aperture-6+kick),bore+d*(aperture+3+kick),tickColor,2);
            }
            if(preciseTag>0)
            {
                GUI.color=new Color(1,.85f,.3f,Mathf.Clamp01(preciseTag/.2f));
                Text(new Rect(bore.x+26,bore.y-36,90,20),"x1.75",small);GUI.color=Color.white;
            }
            if(hudTarget)TargetBox(hudTarget,bore,hudTargetVelocity,true);
            if(player.lockTarget)LockRing(player.lockTarget,bore);
            int warning=0;
            if(cooked)Warning(ref warning,"OVERHEAT",new Color(1,.3f,.12f));
            if(player.Speed<45 && player.Altitude<500)Warning(ref warning,"STALL",new Color(1,.85f,.4f));
            if(player.Altitude<40)Warning(ref warning,"PULL UP",new Color(1,.34f,.18f));
            if(player.hullHeat>1)Warning(ref warning,"RE-ENTRY",new Color(1,.46f,.1f));
            // Unbanked cargo is worth nothing at the buzzer; the last 30 s say so where the eye already is.
            if(phase==MatchPhase.Playing && MatchRemaining<30 && player.cargo>0)Warning(ref warning,"BANK BEFORE THE BUZZER",new Color(1,.84f,.35f));
            if(lastAttackAge>0 && lastAttackDirection.sqrMagnitude>.01f)
            {
                Vector3 local=cam.transform.InverseTransformDirection(lastAttackDirection);
                HitWedge(Mathf.Atan2(local.x,local.z),Mathf.Clamp01(lastAttackAge));
            }
            // Inbound seeker: its own hot-magenta chevron so it never reads as the ordinary "you were shot from there" wedge.
            if(missileRange>=0)
            {
                Vector3 local=cam.transform.InverseTransformDirection(missileSource-player.transform.position);
                float flash=reduceFlashing?.92f:.45f+.55f*Mathf.Sin(Time.unscaledTime*11);
                if(local.sqrMagnitude>.01f)HitWedge(Mathf.Atan2(local.x,local.z),flash,new Color(1,.12f,.5f));
                GUI.color=new Color(1,.24f,.52f,flash);
                Text(new Rect(Width*.5f-150,114,300,26),"MISSILE   "+Mathf.RoundToInt(missileRange)+" m",banner);
                GUI.color=Color.white;
            }
            if(damageFlash>0)Box(new Rect(0,0,Width,Height),new Color(1,.03f,.01f,reduceFlashing?.12f:damageFlash*.4f));
        }
        else
        {
            Box(new Rect(0,0,Width,Height),new Color(.02f,.03f,.06f,.35f));
            Text(new Rect(525,310,330,48),"REDEPLOYING  "+Mathf.CeilToInt(player.respawn),title);
        }
        if(elapsed<18 || paused)
            Text(new Rect(30,134,1010,30),"Mouse: pitch / coordinated bank   C: center   WASD: pitch / bank   QE: rudder   Shift / Ctrl: throttle   Space: boost   LMB: cannon   RMB: hold lock, press again to fire",small);
        if(phase==MatchPhase.Countdown)CountdownCard();
        else if(phase==MatchPhase.Ended)ResultsPanel();
        if(paused){Text(new Rect(500,340,320,44),"PAUSED  /  ESC",title);ComfortPanel();}
    }
}
