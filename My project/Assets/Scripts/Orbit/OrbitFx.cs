using UnityEngine;

// Wordless feedback in the world: the eject-ready ring around the head, the strike ring, the tail flash on a catch, the
// break-up on death, the skill icons on offer and their pods. Nothing here writes a word; the HUD draws numbers only.
public partial class OrbitSnake
{
    LineRenderer readyRing, strikeRing, nearRing; float strikeRingT=1, nearRingT=1; Material ringMat, strikeMat, nearMat, shotMat; Material[] skillMats;
    // Seconds since the last catch: drives the new segment's pop-in and the ripple that runs from the tail to the head.
    float catchAge=99;
    static readonly Vector3 SegScale=new Vector3(3.6f,3.2f,4.6f);
    // Easing (the lecture's tween curves): back-out overshoots and settles, cubic-out decelerates.
    public static float EaseOutBack(float t){ t=Mathf.Clamp01(t); const float c1=1.70158f,c3=c1+1; return 1+c3*Mathf.Pow(t-1,3)+c1*Mathf.Pow(t-1,2); }
    public static float EaseOutCubic(float t){ t=Mathf.Clamp01(t); return 1-Mathf.Pow(1-t,3); }

    void ResetFx()
    {
        if(!readyRing){ readyRing=Circle("Ready ring",ringMat=Mat(new Color(.4f,1.2f,.6f),true),9,.6f); strikeRing=Circle("Strike ring",strikeMat=Mat(new Color(1.4f,.3f,.2f),true),6,.9f); nearRing=Circle("Near-miss ring",nearMat=Mat(new Color(1.2f,1.2f,1.3f),true),7,.5f); }
        readyRing.enabled=false; strikeRing.enabled=false; nearRing.enabled=false; strikeRingT=1; nearRingT=1; catchAge=99;
        if(skillMats==null){ skillMats=new Material[SkillCount]; for(int i=0;i<SkillCount;i++)skillMats[i]=Mat(SkillColors[i],true); shotMat=Mat(new Color(1.3f,1.1f,.4f),true); }
    }
    LineRenderer Circle(string name,Material m,float r,float width)
    {
        var l=new GameObject(name).AddComponent<LineRenderer>(); l.transform.SetParent(world,false); l.useWorldSpace=false; l.loop=true; l.positionCount=48; l.widthMultiplier=width; l.sharedMaterial=m;
        for(int i=0;i<48;i++){ float a=i*Mathf.PI*2/48; l.SetPosition(i,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r)); } return l;
    }
    public void StrikeRing(){ strikeRingT=0; }

    // Per-frame: rings follow the head in its tangent frame; the ready ring breathes, the strike ring expands and fades.
    // Then the animation-principles pass, all of it cosmetic: the hull banks into a turn with lag and the bank runs down
    // the train a beat late (follow-through), the hull squashes on impacts and stretches on a launch and eases back, a
    // caught segment pops in with overshoot and sends a ripple up the train, the bands chase head-ward while Space is
    // live (anticipation), and a dead train tumbles apart.
    void TickFx(float dt)
    {
        if(!readyRing||!ship)return;
        var frame=Quaternion.LookRotation(ship.tangent,ship.normal);
        readyRing.enabled=EjectReady&&!paused; readyRing.transform.SetPositionAndRotation(ship.Position+ship.normal*.5f,frame); float b=1+.12f*Mathf.Sin(Time.unscaledTime*6); readyRing.transform.localScale=new Vector3(b,1,b);
        if(strikeRingT<1){ strikeRingT=Mathf.Min(1,strikeRingT+dt*2); strikeRing.enabled=true; strikeRing.transform.SetPositionAndRotation(ship.Position+ship.normal*.6f,frame); float s=1+strikeRingT*4; strikeRing.transform.localScale=new Vector3(s,1,s); strikeRing.widthMultiplier=.9f*(1-strikeRingT); }
        else strikeRing.enabled=false;
        if(nearRingT<1){ nearRingT=Mathf.Min(1,nearRingT+dt*3); nearRing.enabled=true; nearRing.transform.SetPositionAndRotation(ship.Position+ship.normal*.7f,frame); float s=1+EaseOutCubic(nearRingT)*2.2f; nearRing.transform.localScale=new Vector3(s,1,s); nearRing.widthMultiplier=.5f*(1-nearRingT); }
        else nearRing.enabled=false;
        catchAge+=dt; float k9=1-Mathf.Exp(-dt*9), k7=1-Mathf.Exp(-dt*7);
        // Hull: bank (positive roll about forward is right wing down, so a right turn banks right), then squash back to one.
        if(!ship.dead){ ship.bank=Mathf.Lerp(ship.bank,ship.turn*36f,k9); ship.transform.rotation=frame*Quaternion.Euler(0,0,ship.bank); }
        ship.squash=Vector3.Lerp(ship.squash,Vector3.one,k7); ship.transform.localScale=ship.squash;
        // Exhaust: wider plumes while turning (secondary action), shorter while braking.
        if(shipTrails!=null)foreach(var tr in shipTrails)if(tr){ tr.startWidth=.9f*(1+.6f*Mathf.Abs(ship.turn)); tr.time=ship.brake?.12f:.22f; }
        // Side thrusters: a puff off the outer fin while the stick is over half way, so a hard turn is visibly worked for.
        if(Mathf.Abs(ship.turn)>.5f&&!ship.dead&&!paused)Puff(ship.Position-ship.Right*Mathf.Sign(ship.turn)*2.6f-ship.tangent*1.2f,-ship.Right*Mathf.Sign(ship.turn));
        int n=ship.segments.Count; bool ready=EjectReady&&!paused; float wave=Time.unscaledTime*1.6f;
        for(int i=0;i<n;i++)
        {
            var seg=ship.segments[i];
            if(ship.dead){ seg.position+=(seg.forward*5+seg.up*4)*dt; seg.Rotate(dt*120*(i%2==0?1:-1),dt*60,0,Space.Self); continue; }
            // Bank follows the segment ahead: a turn is a wave that travels down the train.
            float ahead=i==0?ship.bank:ship.segBank[i-1]; ship.segBank[i]=Mathf.Lerp(ship.segBank[i],ahead,1-Mathf.Exp(-dt*10));
            var sn=ship.TrailNormal((i+1)*SegmentSpacing,out var sd); seg.rotation=Quaternion.LookRotation(sd,sn)*Quaternion.Euler(0,0,ship.segBank[i]);
            // Scale: the newest pops in with overshoot; the rest bulge in turn as the catch ripples toward the head.
            float t=catchAge-(n-1-i)*.035f, s=1;
            if(i==n-1&&catchAge<.35f)s=EaseOutBack(catchAge/.35f); else if(t>0&&t<.25f)s=1+.28f*Mathf.Sin(t/.25f*Mathf.PI);
            seg.localScale=SegScale*s;
            // Bands: the newest flares white on a catch; while Space is live a white pulse chases from the tail to the head.
            var band=seg.GetChild(0).GetComponent<Renderer>(); if(band){ bool lit=(i==n-1&&ship.tailFlash>0)||(ready&&Mathf.Repeat(wave+i*.07f,1)<.12f); band.sharedMaterial=lit?hullMat:glowMat; }
        }
        // Armour: the hull itself goes bright while the charge is held.
        var hull=ship.transform.Find("Hull"); if(hull)hull.GetComponent<Renderer>().sharedMaterial=armour>0?skillMats[(int)Skill.Armour]:hullMat;
        // Winning: confetti keeps falling in front of the climbing camera for the whole pull-away.
        if(won&&endTimer<2.5f&&cam)Confetti(cam.transform.position-ship.normal*40+Random.insideUnitSphere*12,3);
    }

    // Death: the hull scatters as falling embers and the art goes dark. The HUD dims over the next second on its own.
    void BreakUp()
    {
        for(int i=0;i<8;i++){ var f=new GameObject("Hull fragment").AddComponent<OrbitFalling>(); f.transform.SetParent(world); f.Init(ship.Position+Random.insideUnitSphere*3,i*.05f); BuildFallingArt(f); falling.Add(f); }
        foreach(Transform c in ship.transform)c.gameObject.SetActive(false);
    }

    public void BuildShotArt(OrbitJunk j)
    {
        foreach(Transform c in j.transform)Destroy(c.gameObject);
        var body=Shape("Shot",j.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(2.2f,2.2f,6f),shotMat); j.body=null;
    }

    // Skill icons: a disc base and a glyph in the pod's tangent plane, seen from straight above. Shapes, not letters.
    public void BuildPodArt(SkillPod p)
    {
        var m=skillMats[(int)p.skill]; var dim=Mat(SkillColors[(int)p.skill]*.35f,true);
        var disc=Shape("Disc",p.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(11,.15f,11),dim);
        Glyph(p.skill,p.transform,m,1f);
    }
    // Builds a glyph out of cubes in the local XZ plane at height 1.2. Shared by the pods; the HUD draws the same shapes in 2D.
    void Glyph(Skill s,Transform parent,Material m,float k)
    {
        void Bar(float x,float z,float w,float l,float rot=0){ var g=Shape("Glyph",parent,PrimitiveType.Cube,new Vector3(x*k,1.2f,z*k),new Vector3(w*k,.8f,l*k),m); g.transform.localRotation=Quaternion.Euler(0,rot,0); }
        switch(s)
        {
            case Skill.Magnet: Bar(-2.2f,.6f,1.4f,4.2f); Bar(2.2f,.6f,1.4f,4.2f); Bar(0,-2f,5.8f,1.6f); break;
            case Skill.Armour: Shape("Plate",parent,PrimitiveType.Cylinder,new Vector3(0,1.2f,0),new Vector3(6.4f*k,.4f,6.4f*k),m); Shape("Boss",parent,PrimitiveType.Cylinder,new Vector3(0,1.7f,0),new Vector3(2.4f*k,.4f,2.4f*k),hullMat); break;
            case Skill.Whip: Bar(0,-1,1.2f,6.5f); Bar(-1.5f,2.2f,1.1f,3.4f,45); Bar(1.5f,2.2f,1.1f,3.4f,-45); break;
            case Skill.Brake: Bar(-1.8f,0,1.6f,6.5f); Bar(1.8f,0,1.6f,6.5f); break;
            case Skill.Phase: Bar(-1.6f,-1.2f,3f,3f); Bar(1.6f,1.2f,3f,3f); break;
            case Skill.Compound: Bar(-2.4f,-1.5f,1.6f,2f); Bar(0,-.5f,1.6f,4f); Bar(2.4f,.5f,1.6f,6f); break;
        }
    }
}
