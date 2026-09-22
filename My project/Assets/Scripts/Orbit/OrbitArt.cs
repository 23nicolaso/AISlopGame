using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Art and camera. Everything is generated: a lit planet with a procedural ocean/land/ice/cloud map under a thin grid,
// an atmosphere limb (RIFT's shader, re-centred), a nebula-tinted star field, three junk silhouettes, a swept ship with
// an additive exhaust, particle bursts, and a runtime post-processing volume for bloom and vignette. Junk within reach
// is tinted by the rule that will decide the contact: green if the heading matches, red if not.
public partial class OrbitSnake
{
    Material planetMat, hullMat, segMat, segMatAlt, junkNeutral, junkCatch, junkStrike, wreckMat, fallMat, glowMat, exhaustMat, sparkMat, gridMat, starMat;
    // Layer 3 in TagManager is named Planet: the planet and clouds sit on it so the camera headlight can leave them alone.
    const int PlanetLayer=3;
    Vector3 camPos, camUp; UniversalAdditionalCameraData camData;
    ParticleSystem sparks; Transform cloudLayer;
    public Vector3 sunDir=new Vector3(-.55f,.6f,-.58f).normalized;

    Material Mat(Color c,bool unlit){ var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); m.SetColor("_BaseColor",c); m.color=c; if(!unlit){m.SetFloat("_Metallic",.25f);m.SetFloat("_Smoothness",.4f);} owned.Add(m); return m; }
    Material Additive(Color c,float fade){ var sh=Shader.Find("Rift/Additive"); var m=new Material(sh?sh:Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor",sh?c:c*fade); if(sh)m.SetFloat("_Fade",fade); owned.Add(m); return m; }
    GameObject Shape(string name,Transform parent,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
    { var g=GameObject.CreatePrimitive(type); g.name=name; g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=scale; Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial=mat; return g; }

    void BuildArt()
    {
        // Low smoothness: at .2 the sun's specular on the ocean bloomed into a jagged white star from this height.
        planetMat=Mat(Color.white,false); planetMat.SetFloat("_Metallic",0); planetMat.SetFloat("_Smoothness",.06f); planetMat.SetTexture("_BaseMap",PlanetTexture());
        hullMat=Mat(new Color(.96f,.97f,1f),false); segMat=Mat(new Color(.5f,.78f,1f),false); segMatAlt=Mat(new Color(.36f,.62f,.9f),false);
        // Junk is lit, so the silhouettes keep their shading; the rule colour is a bright base tint, not a flat unlit fill.
        junkNeutral=Mat(new Color(.62f,.66f,.72f),false); junkNeutral.SetFloat("_Metallic",.6f); junkNeutral.SetFloat("_Smoothness",.55f);
        junkCatch=Mat(new Color(.3f,1.2f,.45f),false); junkStrike=Mat(new Color(1.3f,.25f,.2f),false); wreckMat=Mat(new Color(1.25f,.72f,.18f),false);
        fallMat=Mat(new Color(1.5f,.62f,.12f),true); glowMat=Mat(new Color(.45f,1.15f,1.5f),true); exhaustMat=Additive(new Color(.35f,.9f,1.3f),.9f); sparkMat=Additive(Color.white,1);
        gridMat=Mat(new Color(.26f,.32f,.44f),true); starMat=Additive(new Color(.75f,.8f,.95f),1);
        // Planet: a smooth lat-long sphere so the limb is round from 275 u up, lit by the sun so it has a terminator.
        var planet=new GameObject("Planet"); planet.transform.SetParent(world,false); planet.layer=PlanetLayer;
        planet.AddComponent<MeshFilter>().sharedMesh=Sphere(PlanetRadius,128,64); planet.AddComponent<MeshRenderer>().sharedMaterial=planetMat;
        // Cloud layer: the same map's cloud channel on a slightly larger sphere, drifting slowly so the surface reads as alive.
        // Matte clouds: with the default metallic/smoothness the cloud shell's specular bloomed into a white starburst on
        // the ocean (the sun's glint on a sphere 2.5 u above the water), which together with the pole knot read as inside-out.
        var cloudMat=Mat(Color.white,false); cloudMat.SetFloat("_Metallic",0); cloudMat.SetFloat("_Smoothness",0); cloudMat.SetTexture("_BaseMap",CloudTexture()); cloudMat.SetFloat("_Surface",1); cloudMat.SetFloat("_Blend",0); cloudMat.SetOverrideTag("RenderType","Transparent"); cloudMat.renderQueue=3000; cloudMat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); cloudMat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); cloudMat.SetInt("_ZWrite",0); cloudMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); cloudMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        var clouds=new GameObject("Clouds"); clouds.transform.SetParent(world,false); clouds.layer=PlanetLayer; clouds.AddComponent<MeshFilter>().sharedMesh=Sphere(PlanetRadius+2.5f,96,48); clouds.AddComponent<MeshRenderer>().sharedMaterial=cloudMat; cloudLayer=clouds.transform;
        // Grid every 15 degrees, faint: from straight above it is what shows speed over an ocean. Meridians are arcs that
        // stop at ±75° like a globe's, so they never bunch into a knot at the poles (the knot read as a hole in the sphere).
        // Grid lines cast no shadows: the sun has none (below), and nothing else should shade the surface either.
        for(int i=0;i<35;i++){ var ring=new GameObject("Grid").AddComponent<LineRenderer>(); ring.transform.SetParent(world,false); ring.useWorldSpace=true; ring.positionCount=128; ring.widthMultiplier=.3f; ring.sharedMaterial=gridMat; ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; ring.receiveShadows=false;
            bool lon=i<24; ring.loop=!lon; float R=PlanetRadius+3.2f;
            if(lon){ var q=Quaternion.AngleAxis(i*15,Vector3.up); for(int k=0;k<128;k++){ float a=Mathf.Lerp(-75,75,k/127f)*Mathf.Deg2Rad; ring.SetPosition(k,q*new Vector3(Mathf.Cos(a)*R,Mathf.Sin(a)*R,0)); } }
            else { float lat=(i-24)*15-75; float r=Mathf.Cos(lat*Mathf.Deg2Rad)*R, y=Mathf.Sin(lat*Mathf.Deg2Rad)*R; for(int k=0;k<128;k++){ float a=k*Mathf.PI*2/128; ring.SetPosition(k,new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r)); } } }
        // Atmosphere limb: RIFT's shader, re-centred on the origin. Cull Front, so it is seen from inside as a halo on the disc.
        var atmoShader=Shader.Find("Rift/Atmosphere");
        if(atmoShader){ var atmo=new Material(atmoShader); owned.Add(atmo); atmo.SetVector("_PlanetCenter",Vector3.zero); atmo.SetFloat("_Radius",PlanetRadius); Shape("Atmosphere",world,PrimitiveType.Sphere,Vector3.zero,Vector3.one*(PlanetRadius+12)*2,atmo); }
        // Star field: 700 additive specks on a far sphere. (Nebula blobs were tried and read as dark discs from this camera.)
        var stars=new GameObject("Stars"); stars.transform.SetParent(world,false); var sr=new System.Random(3);
        for(int i=0;i<700;i++){ float z=(float)sr.NextDouble()*2-1,a=(float)sr.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt(1-z*z); Shape("Star",stars.transform,PrimitiveType.Cube,new Vector3(r*Mathf.Cos(a),z,r*Mathf.Sin(a))*1500,Vector3.one*(1.5f+(float)sr.NextDouble()*3.5f),starMat); }
        // No shadows: from this height the only shadows that read were the grid lines' (a black starburst where the meridians
        // met at a pole), and they made the planet look concave.
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world); sun.type=LightType.Directional; sun.intensity=2.6f; sun.color=new Color(1,.95f,.86f); sun.transform.rotation=Quaternion.LookRotation(-sunDir); sun.shadows=LightShadows.None;
        // Ambient is high enough that the night side of the planet still shows its continents; the headlight on the camera
        // (BuildCamera) keeps the ship and junk readable there, so the sun only has to draw the terminator.
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.42f,.48f,.62f); RenderSettings.ambientEquatorColor=new Color(.28f,.32f,.44f); RenderSettings.ambientGroundColor=new Color(.16f,.18f,.26f); RenderSettings.skybox=null; RenderSettings.fog=false;
        // Post-processing: a runtime global volume with bloom for the HDR emissives and a soft vignette.
        var volume=new GameObject("Post").AddComponent<Volume>(); volume.transform.SetParent(world,false); volume.isGlobal=true; var profile=ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.profile=profile;
        var bloom=profile.Add<Bloom>(true); bloom.threshold.Override(1f); bloom.intensity.Override(.7f); bloom.scatter.Override(.6f);
        var vig=profile.Add<Vignette>(true); vig.intensity.Override(.28f); vig.smoothness.Override(.5f);
        var tone=profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        // URL debug switches (see OrbitSnake.debug): bisect a platform rendering difference without rebuilding.
        if(debug.Contains("nobloom"))bloom.active=false; if(debug.Contains("notone"))tone.active=false; if(debug.Contains("noclouds"))clouds.SetActive(false);
        if(debug.Contains("nospec")){ planetMat.SetFloat("_Smoothness",0); cloudMat.SetFloat("_Smoothness",0); }
        if(debug.Contains("nocloudspec")){ cloudMat.SetFloat("_Smoothness",0); cloudMat.SetFloat("_Metallic",0); }
        // Sparks: one shared burst system for catches and strikes, coloured per burst.
        var sp=new GameObject("Sparks"); sp.transform.SetParent(world,false); sparks=sp.AddComponent<ParticleSystem>(); var main=sparks.main; main.playOnAwake=false; main.loop=false; main.startLifetime=.5f; main.startSpeed=22; main.startSize=1.1f; main.gravityModifier=0; main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=400;
        var em=sparks.emission; em.enabled=false; var shp=sparks.shape; shp.shapeType=ParticleSystemShapeType.Sphere; shp.radius=.5f; var col=sparks.colorOverLifetime; col.enabled=true; var grad=new Gradient(); grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); col.color=grad;
        var pr=sp.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial=sparkMat; pr.renderMode=ParticleSystemRenderMode.Stretch; pr.lengthScale=3;
    }

    // Surface map: ocean / shelf / land / highland / ice by two-octave noise, latitude ice caps, night-side unaffected (the
    // material is lit). 1024x512, generated once.
    Texture2D PlanetTexture()
    {
        // No mip chain: at the poles the lat-long sphere's sliver triangles sweep u from 0 to 1 in a few pixels, the GPU
        // picks the 1x1 mip (the map's average, dark blue) and the pole renders as a dark starburst. The camera sits 85 u
        // over a 160 u sphere, so the map is magnified, never minified, and mips buy nothing.
        var t=new Texture2D(1024,512,TextureFormat.RGB24,false); t.wrapMode=TextureWrapMode.Repeat; var px=new Color[1024*512];
        for(int y=0;y<512;y++)for(int x=0;x<1024;x++)
        {
            float lat=Mathf.Abs(y/512f-.5f)*2; float n=Mathf.PerlinNoise(x*.006f+7,y*.012f+3)+.4f*Mathf.PerlinNoise(x*.02f,y*.04f)+.15f*Mathf.PerlinNoise(x*.06f,y*.12f);
            Color c=n<.72f?Color.Lerp(new Color(.04f,.12f,.28f),new Color(.08f,.3f,.5f),Mathf.InverseLerp(.3f,.72f,n)):n<.78f?Color.Lerp(new Color(.12f,.38f,.5f),new Color(.55f,.5f,.34f),Mathf.InverseLerp(.72f,.78f,n)):n<1.05f?Color.Lerp(new Color(.24f,.42f,.22f),new Color(.5f,.46f,.3f),Mathf.InverseLerp(.78f,1.05f,n)):Color.Lerp(new Color(.62f,.6f,.55f),new Color(.9f,.92f,.95f),Mathf.InverseLerp(1.05f,1.3f,n));
            float ice=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,.95f,lat+.08f*Mathf.PerlinNoise(x*.03f,y*.03f))); c=Color.Lerp(c,new Color(.92f,.95f,1f),ice);
            px[y*1024+x]=c;
        }
        t.SetPixels(px); t.Apply(false); owned.Add(t); return t;
    }
    Texture2D CloudTexture()
    {
        var t=new Texture2D(512,256,TextureFormat.RGBA32,false); t.wrapMode=TextureWrapMode.Repeat; var px=new Color[512*256];
        for(int y=0;y<256;y++)for(int x=0;x<512;x++){ float c=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.85f,Mathf.PerlinNoise(x*.014f+51,y*.028f+9)+.3f*Mathf.PerlinNoise(x*.05f,y*.1f))); px[y*512+x]=new Color(1,1,1,c*.8f); }
        t.SetPixels(px); t.Apply(false); owned.Add(t); return t;
    }

    Mesh Sphere(float r,int lon,int lat)
    {
        var v=new Vector3[(lon+1)*(lat+1)]; var n=new Vector3[v.Length]; var uv=new Vector2[v.Length]; var t=new int[lon*lat*6]; int i=0;
        for(int y=0;y<=lat;y++)for(int x=0;x<=lon;x++){ float a=x*Mathf.PI*2/lon,b=y*Mathf.PI/lat; n[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a)); v[i]=n[i]*r; uv[i]=new Vector2((float)x/lon,1-(float)y/lat); i++; }
        i=0; for(int y=0;y<lat;y++)for(int x=0;x<lon;x++){ int a=y*(lon+1)+x; t[i++]=a;t[i++]=a+lon+1;t[i++]=a+1; t[i++]=a+1;t[i++]=a+lon+1;t[i++]=a+lon+2; }
        var m=new Mesh{name="Sphere",indexFormat=IndexFormat.UInt32,vertices=v,normals=n,uv=uv,triangles=t}; m.RecalculateBounds(); owned.Add(m); return m;
    }

    public void BuildShipArt(OrbitShip s)
    {
        // Swept hull: a capsule body, a glowing nose, two raked fins, twin engine glows and a short additive exhaust trail.
        Shape("Hull",s.transform,PrimitiveType.Capsule,Vector3.zero,new Vector3(3f,2.2f,3f),hullMat).transform.localRotation=Quaternion.Euler(90,0,0);
        Shape("Canopy",s.transform,PrimitiveType.Sphere,new Vector3(0,1.1f,.6f),new Vector3(1.6f,1,2.4f),Mat(new Color(.08f,.12f,.2f),false));
        Shape("Nose",s.transform,PrimitiveType.Sphere,new Vector3(0,0,2.9f),Vector3.one*2f,glowMat);
        for(int side=-1;side<=1;side+=2)
        {
            var fin=Shape("Fin",s.transform,PrimitiveType.Cube,new Vector3(side*2.6f,0,-1.2f),new Vector3(3.2f,.3f,1.6f),hullMat); fin.transform.localRotation=Quaternion.Euler(0,side*-18,0);
            Shape("Engine",s.transform,PrimitiveType.Sphere,new Vector3(side*1.1f,0,-2.6f),new Vector3(.9f,.9f,1.4f),glowMat);
            var plume=Shape("Exhaust",s.transform,PrimitiveType.Sphere,new Vector3(side*1.1f,0,-3.4f),new Vector3(.5f,.5f,1.2f),exhaustMat);
            var trail=plume.AddComponent<TrailRenderer>(); trail.sharedMaterial=exhaustMat; trail.time=.22f; trail.startWidth=.9f; trail.endWidth=0; trail.minVertexDistance=.4f;
        }
    }
    public Transform BuildSegmentArt(OrbitShip s,int index)
    {
        var g=Shape("Segment "+index,world,PrimitiveType.Cube,Vector3.zero,new Vector3(3.6f,3.2f,4.6f),index%2==0?segMat:segMatAlt);
        Shape("Band",g.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(1.12f,1.12f,.28f),glowMat);
        Shape("Coupling",g.transform,PrimitiveType.Cylinder,new Vector3(0,0,-.62f),new Vector3(.35f,.16f,.35f),hullMat).transform.localRotation=Quaternion.Euler(90,0,0);
        return g.transform;
    }
    // Three junk silhouettes by phase hash: a satellite with two panels, a spent rocket stage, a tumbling plate cluster.
    // Every renderer is listed so the catch/strike tint covers the whole object.
    public void BuildJunkArt(OrbitJunk j)
    {
        var m=j.wreck?wreckMat:junkNeutral; var list=new System.Collections.Generic.List<Renderer>();
        void Part(string n,PrimitiveType t,Vector3 p,Vector3 sc,Vector3 rot){ var g=Shape(n,j.transform,t,p,sc,m); g.transform.localRotation=Quaternion.Euler(rot); list.Add(g.GetComponent<Renderer>()); }
        if(j.wreck){ Part("Body",PrimitiveType.Cube,Vector3.zero,new Vector3(3.6f,3.2f,4.6f),Vector3.zero); Part("Band",PrimitiveType.Cube,Vector3.zero,new Vector3(4f,3.6f,1.2f),Vector3.zero); }
        else switch(Mathf.Abs((int)(j.phase*1000))%3)
        {
            case 0: Part("Bus",PrimitiveType.Cube,Vector3.zero,new Vector3(3f,2.6f,3.4f),Vector3.zero); Part("Panel L",PrimitiveType.Cube,new Vector3(-4.2f,0,0),new Vector3(5,.25f,2.2f),Vector3.zero); Part("Panel R",PrimitiveType.Cube,new Vector3(4.2f,0,0),new Vector3(5,.25f,2.2f),Vector3.zero); Part("Dish",PrimitiveType.Cylinder,new Vector3(0,0,2.4f),new Vector3(2.2f,.2f,2.2f),new Vector3(90,0,0)); break;
            case 1: Part("Stage",PrimitiveType.Capsule,Vector3.zero,new Vector3(2.4f,3.6f,2.4f),new Vector3(90,0,0)); Part("Nozzle",PrimitiveType.Cylinder,new Vector3(0,0,-4.2f),new Vector3(2.6f,.8f,2.6f),new Vector3(90,0,0)); Part("Fin",PrimitiveType.Cube,new Vector3(0,0,-3f),new Vector3(5.4f,.3f,1.6f),Vector3.zero); break;
            default: Part("Plate",PrimitiveType.Cube,Vector3.zero,new Vector3(4.6f,.4f,3.4f),new Vector3(0,0,20)); Part("Plate 2",PrimitiveType.Cube,new Vector3(1.4f,1.2f,-.8f),new Vector3(2.8f,.4f,2.4f),new Vector3(30,40,0)); Part("Strut",PrimitiveType.Cylinder,new Vector3(-1.2f,.6f,1),new Vector3(.5f,2.2f,.5f),new Vector3(20,0,60)); break;
        }
        j.body=list[0]; j.tint=list.ToArray();
    }
    public void BuildFallingArt(OrbitFalling f)
    {
        Shape("Ember",f.transform,PrimitiveType.Sphere,Vector3.zero,Vector3.one*2.4f,fallMat);
        var trail=f.gameObject.AddComponent<TrailRenderer>(); trail.sharedMaterial=Additive(new Color(1.4f,.55f,.1f),.9f); trail.time=.6f; trail.startWidth=1.6f; trail.endWidth=0; trail.minVertexDistance=.5f;
    }
    public void Burst(Vector3 at,Color c,int count)
    {
        if(!sparks)return; var main=sparks.main; main.startColor=c; sparks.transform.position=at; sparks.Emit(count);
    }

    // Junk within reach is tinted by the rule that will decide the contact: green if the heading matches, red if not.
    // Out of reach it is neutral metal, so the colour means "this one, now" rather than painting the whole sky red.
    public void TintJunk()
    {
        foreach(var j in junk){ if(j.tint==null||j.shot)continue; bool near=j.shell==level&&(j.Position-ship.Position).magnitude<150; Material m=j.wreck?wreckMat:!near?junkNeutral:(Vector3.Angle(ship.tangent,j.Direction)<CatchAngleNow?junkCatch:junkStrike); if(j.body&&j.body.sharedMaterial!=m)foreach(var r in j.tint)if(r)r.sharedMaterial=m; }
        if(cloudLayer)cloudLayer.Rotate(0,Time.deltaTime*.4f,0,Space.World);
    }

    void BuildCamera()
    {
        cam=new GameObject("Orbit camera").AddComponent<Camera>(); cam.transform.SetParent(world); cam.tag="MainCamera";
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.008f,.01f,.025f); cam.fieldOfView=62; cam.nearClipPlane=1; cam.farClipPlane=3200; cam.allowHDR=true;
        camData=cam.GetUniversalAdditionalCameraData(); camData.renderPostProcessing=!debug.Contains("nopost"); camData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cam.gameObject.AddComponent<AudioListener>();
        // Headlight: a directional fill riding on the camera, tilted off its axis so cube faces still grade, masked off the
        // planet layer so the terminator stays. Dimmer than the sun, so URP keeps the sun as the main light.
        var head=new GameObject("Headlight").AddComponent<Light>(); head.transform.SetParent(cam.transform,false); head.transform.localRotation=Quaternion.Euler(-28,22,0);
        head.type=LightType.Directional; head.intensity=debug.Contains("nohead")?0:1.4f; head.color=new Color(.85f,.92f,1f); head.cullingMask=~(1<<PlanetLayer);
    }
    // Top-down, rigid: the camera sits above the head looking straight down the normal, no lag, no shake, so the ship is
    // pinned to the screen centre and only the world moves. Screen-up is a tangent vector parallel-transported along the
    // path (re-projected onto each new tangent plane, never rotated about the normal): turning spins the ship on screen,
    // not the world. Height grows with the shell so the planet visibly shrinks as you climb.
    const float CamHeight=85f;
    float CamHeightNow => CamHeight+level*10;
    // Snap is only used after a restart or a staged jump, so it also clears the exhaust trails a teleport would smear.
    public void SnapCamera(){ camUp=ship.tangent; ApplyCamera(); foreach(var t in ship.GetComponentsInChildren<TrailRenderer>())t.Clear(); }
    void UpdateCamera(float dt){ ApplyCamera(); TintJunk(); }
    void ApplyCamera()
    {
        camUp=(camUp-ship.normal*Vector3.Dot(camUp,ship.normal)).normalized; if(camUp.sqrMagnitude<.5f)camUp=ship.tangent;
        // Win: the camera climbs away for three seconds and the planet shrinks to a point. That is the whole victory screen.
        float h=CamHeightNow*(won?1+Mathf.Min(endTimer,3)*3:1);
        camPos=ship.Position+ship.normal*h; cam.transform.position=camPos; cam.transform.rotation=Quaternion.LookRotation(-ship.normal,camUp);
    }
}
