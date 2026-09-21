using UnityEngine;

// Greybox art, camera and audio. Flat materials, one generated sphere, primitives for everything that moves. The one
// piece of feedback the greybox needs is junk colour: green when the snake's heading matches it (a catch), red when it
// does not (a strike), so the core rule is legible before a single contact happens.
public partial class OrbitSnake
{
    Material planetMat, hullMat, segMat, junkCatch, junkStrike, wreckMat, fallMat, glowMat;
    AudioSource audioSrc; AudioClip[] pings=new AudioClip[4]; AudioClip[] chimes=new AudioClip[5];
    Vector3 camPos, camUp;

    Material Mat(Color c,bool unlit){ var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); m.SetColor("_BaseColor",c); m.color=c; if(!unlit){m.SetFloat("_Metallic",.2f);m.SetFloat("_Smoothness",.35f);} owned.Add(m); return m; }
    GameObject Shape(string name,Transform parent,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
    { var g=GameObject.CreatePrimitive(type); g.name=name; g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=scale; Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial=mat; return g; }

    void BuildArt()
    {
        planetMat=Mat(new Color(.2f,.26f,.34f),false); hullMat=Mat(new Color(.95f,.97f,1f),false); segMat=Mat(new Color(.55f,.8f,1f),false);
        junkCatch=Mat(new Color(.25f,1.2f,.45f),true); junkStrike=Mat(new Color(1.3f,.25f,.2f),true); wreckMat=Mat(new Color(1.2f,.7f,.2f),true);
        fallMat=Mat(new Color(1.4f,.6f,.15f),true); glowMat=Mat(new Color(.4f,1.1f,1.4f),true);
        var planet=new GameObject("Planet"); planet.transform.SetParent(world,false);
        planet.AddComponent<MeshFilter>().sharedMesh=Sphere(PlanetRadius,64,32); planet.AddComponent<MeshRenderer>().sharedMaterial=planetMat;
        // Latitude/longitude grid every 15 degrees: seen from straight above, it is the only thing that shows speed.
        var gridMat=Mat(new Color(.3f,.38f,.48f),true);
        for(int i=0;i<23;i++){ var ring=new GameObject("Grid").AddComponent<LineRenderer>(); ring.transform.SetParent(world,false); ring.useWorldSpace=true; ring.loop=true; ring.positionCount=128; ring.widthMultiplier=.7f; ring.sharedMaterial=gridMat;
            bool lon=i<12; var q=lon?Quaternion.AngleAxis(i*15,Vector3.up)*Quaternion.AngleAxis(90,Vector3.right):Quaternion.identity; float lat=(i-12)*15-75; float r=lon?PlanetRadius+.5f:Mathf.Cos(lat*Mathf.Deg2Rad)*(PlanetRadius+.5f); float y=lon?0:Mathf.Sin(lat*Mathf.Deg2Rad)*(PlanetRadius+.5f);
            for(int k=0;k<128;k++){ float a=k*Mathf.PI*2/128; ring.SetPosition(k,q*new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r)); } }
        // Star field: 500 unlit specks on a far sphere, fixed seed so the screenshot runs compare.
        var stars=new GameObject("Stars"); stars.transform.SetParent(world,false); var starMat=Mat(new Color(.7f,.75f,.85f),true); var sr=new System.Random(3);
        for(int i=0;i<500;i++){ float z=(float)sr.NextDouble()*2-1,a=(float)sr.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt(1-z*z); Shape("Star",stars.transform,PrimitiveType.Cube,new Vector3(r*Mathf.Cos(a),z,r*Mathf.Sin(a))*1500,Vector3.one*(2+(float)sr.NextDouble()*3),starMat); }
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world); sun.type=LightType.Directional; sun.intensity=2f; sun.color=new Color(1,.95f,.85f); sun.transform.rotation=Quaternion.Euler(35,-40,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.22f,.25f,.32f); RenderSettings.skybox=null; RenderSettings.fog=false;
    }

    Mesh Sphere(float r,int lon,int lat)
    {
        var v=new Vector3[(lon+1)*(lat+1)]; var n=new Vector3[v.Length]; var t=new int[lon*lat*6]; int i=0;
        for(int y=0;y<=lat;y++)for(int x=0;x<=lon;x++){ float a=x*Mathf.PI*2/lon,b=y*Mathf.PI/lat; n[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a)); v[i]=n[i]*r; i++; }
        i=0; for(int y=0;y<lat;y++)for(int x=0;x<lon;x++){ int a=y*(lon+1)+x; t[i++]=a;t[i++]=a+lon+1;t[i++]=a+1; t[i++]=a+1;t[i++]=a+lon+1;t[i++]=a+lon+2; }
        var m=new Mesh{name="Planet",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=v,normals=n,triangles=t}; m.RecalculateBounds(); owned.Add(m); return m;
    }

    public void BuildShipArt(OrbitShip s)
    {
        // Everything is 1.5x for the 120 u top-down camera; the nose glow and fins make the heading readable from above.
        Shape("Hull",s.transform,PrimitiveType.Capsule,Vector3.zero,new Vector3(3.2f,2.4f,3.2f),hullMat).transform.localRotation=Quaternion.Euler(90,0,0);
        Shape("Nose",s.transform,PrimitiveType.Sphere,new Vector3(0,0,2.8f),Vector3.one*2.2f,glowMat);
        Shape("Fin L",s.transform,PrimitiveType.Cube,new Vector3(-2.4f,0,-1),new Vector3(2.8f,.3f,1.8f),hullMat);
        Shape("Fin R",s.transform,PrimitiveType.Cube,new Vector3(2.4f,0,-1),new Vector3(2.8f,.3f,1.8f),hullMat);
    }
    public Transform BuildSegmentArt(OrbitShip s,int index)
    {
        var g=Shape("Segment "+index,world,PrimitiveType.Cube,Vector3.zero,new Vector3(3.6f,3.6f,4.8f),segMat);
        Shape("Band",g.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(1.1f,1.1f,.3f),glowMat); return g.transform;
    }
    public void BuildJunkArt(OrbitJunk j)
    {
        var body=Shape("Body",j.transform,PrimitiveType.Cube,Vector3.zero,j.wreck?new Vector3(3.6f,3.6f,4.8f):new Vector3(5f,3f,4.5f),j.wreck?wreckMat:junkStrike); j.body=body.GetComponent<Renderer>();
        if(!j.wreck)Shape("Panel",j.transform,PrimitiveType.Cube,new Vector3(0,0,3.6f),new Vector3(2,.3f,6),junkStrike);
    }
    public void BuildFallingArt(OrbitFalling f){ Shape("Ember",f.transform,PrimitiveType.Sphere,Vector3.zero,Vector3.one*2.4f,fallMat); }

    // Junk within reach ahead is tinted by the rule that will decide the contact: green if the heading matches, red if not.
    public void TintJunk()
    {
        foreach(var j in junk){ if(!j.body)continue; bool near=j.shell==level&&(j.Position-ship.Position).magnitude<150; Material m=j.wreck?wreckMat:(near&&Vector3.Angle(ship.tangent,j.Direction)<CatchAngle?junkCatch:junkStrike); if(j.body.sharedMaterial!=m)j.body.sharedMaterial=m; }
    }

    void BuildCamera()
    {
        cam=new GameObject("Orbit camera").AddComponent<Camera>(); cam.transform.SetParent(world); cam.tag="MainCamera";
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.01f,.012f,.03f); cam.fieldOfView=62; cam.nearClipPlane=1; cam.farClipPlane=2000;
        cam.gameObject.AddComponent<AudioListener>();
    }
    // Top-down, rigid: the camera sits CamHeight above the head looking straight down the normal, no lag, no shake, so
    // the ship is pinned to the screen centre and only the world moves. Screen-up is a tangent vector parallel-transported
    // along the path (re-projected onto each new tangent plane, never rotated about the normal): turning spins the ship
    // on screen, not the world. The chase camera and its flicking made the user seasick; strikes flash the HUD instead.
    const float CamHeight=85f;
    public void SnapCamera(){ camUp=ship.tangent; ApplyCamera(); }
    void UpdateCamera(float dt){ ApplyCamera(); TintJunk(); }
    void ApplyCamera()
    {
        camUp=(camUp-ship.normal*Vector3.Dot(camUp,ship.normal)).normalized; if(camUp.sqrMagnitude<.5f)camUp=ship.tangent;
        camPos=ship.Position+ship.normal*CamHeight; cam.transform.position=camPos; cam.transform.rotation=Quaternion.LookRotation(-ship.normal,camUp);
    }

    AudioClip Sound(string name,float length,float start,float end,float noise)
    {
        int count=(int)(22050*length); var samples=new float[count]; var random=new System.Random(44); float phase=0;
        for(int i=0;i<count;i++){ float t=(float)i/count; phase+=Mathf.Lerp(start,end,t)*Mathf.PI*2/22050; samples[i]=(Mathf.Sin(phase)*(1-noise)+((float)random.NextDouble()*2-1)*noise)*Mathf.Pow(1-t,2)*.5f; }
        var clip=AudioClip.Create(name,count,1,22050,false); clip.SetData(samples,0); owned.Add(clip); return clip;
    }
    void BuildAudio()
    {
        audioSrc=gameObject.AddComponent<AudioSource>(); audioSrc.spatialBlend=0;
        pings[0]=Sound("strike",.35f,160,50,.6f); pings[1]=Sound("catch",.18f,520,880,.05f); pings[2]=Sound("shot",.12f,900,400,.3f); pings[3]=Sound("win",1.2f,440,880,0);
        for(int i=0;i<chimes.Length;i++)chimes[i]=Sound("chime"+i,.6f,330*(1+i*.25f),660*(1+i*.25f),0);
    }
    public void Ping(int kind){ if(audioSrc&&Application.isPlaying)audioSrc.PlayOneShot(pings[Mathf.Clamp(kind,0,3)],.7f); }
    public void Chime(int lvl){ if(audioSrc&&Application.isPlaying)audioSrc.PlayOneShot(chimes[Mathf.Clamp(lvl,0,chimes.Length-1)],.8f); }
}
