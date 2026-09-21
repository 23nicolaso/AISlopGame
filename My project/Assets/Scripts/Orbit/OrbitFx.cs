using UnityEngine;

// Wordless feedback in the world: the eject-ready ring around the head, the strike ring, the tail flash on a catch, the
// break-up on death, the skill icons on offer and their pods. Nothing here writes a word; the HUD draws numbers only.
public partial class OrbitSnake
{
    LineRenderer readyRing, strikeRing; float strikeRingT=1; Material ringMat, strikeMat, shotMat; Material[] skillMats;

    void ResetFx()
    {
        if(!readyRing){ readyRing=Circle("Ready ring",ringMat=Mat(new Color(.4f,1.2f,.6f),true),9,.6f); strikeRing=Circle("Strike ring",strikeMat=Mat(new Color(1.4f,.3f,.2f),true),6,.9f); }
        readyRing.enabled=false; strikeRing.enabled=false; strikeRingT=1;
        if(skillMats==null){ skillMats=new Material[SkillCount]; for(int i=0;i<SkillCount;i++)skillMats[i]=Mat(SkillColors[i],true); shotMat=Mat(new Color(1.3f,1.1f,.4f),true); }
    }
    LineRenderer Circle(string name,Material m,float r,float width)
    {
        var l=new GameObject(name).AddComponent<LineRenderer>(); l.transform.SetParent(world,false); l.useWorldSpace=false; l.loop=true; l.positionCount=48; l.widthMultiplier=width; l.sharedMaterial=m;
        for(int i=0;i<48;i++){ float a=i*Mathf.PI*2/48; l.SetPosition(i,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r)); } return l;
    }
    public void StrikeRing(){ strikeRingT=0; }

    // Per-frame: rings follow the head in its tangent frame; the ready ring breathes, the strike ring expands and fades.
    void TickFx(float dt)
    {
        if(!readyRing||!ship)return;
        var frame=Quaternion.LookRotation(ship.tangent,ship.normal);
        readyRing.enabled=EjectReady&&!paused; readyRing.transform.SetPositionAndRotation(ship.Position+ship.normal*.5f,frame); float b=1+.12f*Mathf.Sin(Time.unscaledTime*6); readyRing.transform.localScale=new Vector3(b,1,b);
        if(strikeRingT<1){ strikeRingT=Mathf.Min(1,strikeRingT+dt*2); strikeRing.enabled=true; strikeRing.transform.SetPositionAndRotation(ship.Position+ship.normal*.6f,frame); float s=1+strikeRingT*4; strikeRing.transform.localScale=new Vector3(s,1,s); strikeRing.widthMultiplier=.9f*(1-strikeRingT); }
        else strikeRing.enabled=false;
        // Catch: the newest segment's band flares white for .3 s.
        if(ship.segments.Count>0){ var band=ship.segments[ship.segments.Count-1].GetChild(0).GetComponent<Renderer>(); if(band)band.sharedMaterial=ship.tailFlash>0?hullMat:glowMat; }
        // Armour: the hull itself goes bright while the charge is held.
        var hull=ship.transform.Find("Hull"); if(hull)hull.GetComponent<Renderer>().sharedMaterial=armour>0?skillMats[(int)Skill.Armour]:hullMat;
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
