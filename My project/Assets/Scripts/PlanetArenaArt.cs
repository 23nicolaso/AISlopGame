using System.Collections.Generic;
using UnityEngine;

public partial class AerialCombatPrototype
{
Material Material(Color color, bool unlit)
    {
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor",color); m.color = color;
        if (!unlit) { m.SetFloat("_Metallic",.45f); m.SetFloat("_Smoothness",.45f); }
        owned.Add(m); return m;
    }

    // Additive glow that survives a lit sky. _Fade is the material's own opacity, so a per-renderer MaterialPropertyBlock
    // is free to overwrite _BaseColor without touching it. Falls back to plain Unlit if the shader ever fails to import.
    Material Additive(Color color,float fade)
    {
        var shader=Shader.Find("Rift/Additive");
        var m=new Material(shader?shader:Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor",shader?color:color*fade);
        if(shader)m.SetFloat("_Fade",fade);
        owned.Add(m); return m;
    }

public GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        g.transform.SetParent(parent,false); g.transform.localPosition = pos; g.transform.localScale = scale;
        Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

void Hull(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        Vector3[] v = {
            new Vector3(0,0,3.8f),
            new Vector3(-.85f,.35f,.3f),new Vector3(.85f,.35f,.3f),
            new Vector3(.85f,-.3f,.3f),new Vector3(-.85f,-.3f,.3f),
            new Vector3(-.55f,.25f,-2),new Vector3(.55f,.25f,-2),
            new Vector3(.55f,-.25f,-2),new Vector3(-.55f,-.25f,-2)
        };
        int[] faces = {0,2,1,0,3,2,0,4,3,0,1,4,1,2,6,1,6,5,2,3,7,2,7,6,3,4,8,3,8,7,4,1,5,4,5,8,5,6,7,5,7,8};
        MeshPart(parent,name,pos,scale,v,faces,mat);
    }

void MeshPart(Transform parent,string name,Vector3 pos,Vector3 scale,Vector3[] vertices,int[] triangles,Material mat)
    {
        // Unshared vertices make the panel edges catch the light.
        var flat = new Vector3[triangles.Length]; var indices = new int[triangles.Length];
        for(int i=0;i<indices.Length;i++) { flat[i]=vertices[triangles[i]]; indices[i]=i; }
        var mesh = new Mesh { name=name, vertices=flat, triangles=indices }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
        var g = new GameObject(name); g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=scale;
        g.AddComponent<MeshFilter>().sharedMesh=mesh; g.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }

Transform BuildShip(Transform parent,bool enemy)
    {
        var art = new GameObject(enemy ? "Scythe drone" : "Kestrel / swept delta").transform; art.SetParent(parent,false);
        Hull(art,"Armored fuselage",Vector3.zero,new Vector3(enemy ? 1.3f:1,1,enemy?.75f:1),enemy?dark:alloy);
        Hull(art,"Obsidian canopy",new Vector3(0,.35f,.5f),new Vector3(.45f,.75f,.4f),dark);
        for(int side=-1;side<=1;side+=2)
        {
            Vector3[] wing = {new Vector3(side*.45f,0,1.8f),new Vector3(side*(enemy?3.5f:3.8f),-.15f,-1.8f),new Vector3(side*.7f,0,-1.5f),new Vector3(side*.7f,-.25f,-1.2f)};
            MeshPart(art,"Swept wing",Vector3.zero,Vector3.one,wing,new[]{0,1,2,2,1,0,0,3,1,1,3,2,2,3,0},enemy?dark:alloy);
            Hull(art,"Engine nacelle",new Vector3(side*1.2f,-.1f,-.8f),new Vector3(.45f,.7f,.65f),dark);
            Shape("Engine aperture",art,PrimitiveType.Sphere,new Vector3(side*1.2f,-.1f,-2.1f),new Vector3(.43f,.32f,.12f),enemy?red:teal);
            var plume=Shape("Exhaust",art,PrimitiveType.Sphere,new Vector3(side*1.2f,-.1f,-2.8f),new Vector3(.22f,.18f,1.5f),enemy?red:teal);
            var trail=plume.AddComponent<TrailRenderer>(); trail.sharedMaterial=enemy?red:teal; trail.time=.35f; trail.startWidth=.16f; trail.endWidth=0; trail.minVertexDistance=.3f;
            Shape("Wing identification",art,PrimitiveType.Cube,new Vector3(side*2,.04f,-.7f),new Vector3(.7f,.05f,.16f),enemy?red:gold);
            var fin=Shape("Canted stabilizer",art,PrimitiveType.Cube,new Vector3(side*.65f,.6f,-1.2f),new Vector3(.1f,1,.8f),enemy?dark:alloy);
            fin.transform.localRotation=Quaternion.Euler(0,0,side*-25);
        }
        return art;
    }

AudioClip Sound(string name,float length,float start,float end,float noise)
    {
        int count=(int)(22050*length); var samples=new float[count]; var random=new System.Random(44); float phase=0;
        for(int i=0;i<count;i++) { float t=(float)i/count; phase+=Mathf.Lerp(start,end,t)*Mathf.PI*2/22050; samples[i]=(Mathf.Sin(phase)*(1-noise)+((float)random.NextDouble()*2-1)*noise)*Mathf.Pow(1-t,2)*.5f; }
        var clip=AudioClip.Create(name,count,1,22050,false); clip.SetData(samples,0); owned.Add(clip); return clip;
    }

    public void Burst(Vector3 pos,int count,float size,bool explosion)
    {
        for(int i=0;i<count;i++)
        {
            var g=Shape("Hot debris",world,PrimitiveType.Cube,pos,Vector3.one*Random.Range(.08f,.3f)*size,explosion?gold:teal);
            var f=g.AddComponent<ArenaDebris>(); f.velocity=Random.onUnitSphere*Random.Range(3,15)*size; f.life=explosion?1:.25f;
        }
    }

    LineRenderer Ring(Transform parent,float radius,Material material,float width,int count=96)
    {
        var r=new GameObject("Navigation ring").AddComponent<LineRenderer>();
        r.transform.SetParent(parent,false);r.useWorldSpace=false;
        r.sharedMaterial=material;r.loop=true;r.widthMultiplier=width;r.positionCount=count;
        for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;r.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}
        return r;
    }

    void BuildGate(CaptureGate gate)
    {
        // A large physical torus and three meridians describe a fly-through capture volume.
        var t=gate.transform;
        gate.ring=Ring(t,105,white,2.2f);
        var cross=Ring(t,105,teal,.65f);cross.transform.localRotation=Quaternion.Euler(0,90,0);
        var floor=Ring(t,105,teal,.65f);floor.transform.localRotation=Quaternion.Euler(90,0,0);
        gate.progressRing=Ring(t,88,white,3);gate.progressRing.loop=false;
        Ring(t,112,dark,5);
        // A 400 m beam standing off the top of the ring along the gate's radial up. It is the only thing that says
        // "a refinery is here" from 2 km out, and it wears the owner colour the ring already computes every tick.
        // Neutral cyan is the colour the first Tick would paint it anyway, so the beam is never white for a frame.
        if(!pillarMaterial)pillarMaterial=Additive(new Color(.1f,.7f,1),.4f);
        gate.pillar=Shape("Refinery beacon pillar",t,PrimitiveType.Cube,new Vector3(0,310,0),new Vector3(5,400,5),pillarMaterial).GetComponent<Renderer>();
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI*2/12;
            var p=Shape("Refinery ring segment",t,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*112,Mathf.Sin(a)*112,0),new Vector3(7,15,12),alloy);
            p.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
            Shape("Dock beacon",t,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*105,Mathf.Sin(a)*105,0),Vector3.one*4,teal);
        }
        // In-world directional chevrons point through the aperture.
        for(int i=0;i<4;i++)
        {
            Vector3 p=new Vector3(0,-78,-100+i*23);
            var left=Shape("Approach chevron",t,PrimitiveType.Cube,p+Vector3.left*6,new Vector3(1,1,15),gold);
            left.transform.localRotation=Quaternion.Euler(0,45,0);
            var right=Shape("Approach chevron",t,PrimitiveType.Cube,p+Vector3.right*6,new Vector3(1,1,15),gold);
            right.transform.localRotation=Quaternion.Euler(0,-45,0);
        }
    }

    void BuildCore(SalvageCore core)
    {
        bool blast=core.kind==CoreKind.Volatile,armor=core.kind==CoreKind.Armored;
        // One silhouette read at 300 m: gold reactor = ordinary, violet = unstable, slate slab = reinforced.
        Material accent=blast?violet:armor?white:gold;
        core.art=new GameObject(blast?"Wreck / unstable reactor":armor?"Wreck / reinforced hull":"Wreck / exposed reactor").transform;
        core.art.SetParent(core.transform,false);
        Hull(core.art,"Broken armored hull",Vector3.zero,new Vector3(9,10,7)*(armor?1.2f:1),dark);
        core.weakPoint=Shape("Exposed reactor / shoot",core.art,PrimitiveType.Sphere,new Vector3(0,6,0),Vector3.one*(blast?7:armor?4.5f:6),accent).transform;
        Ring(core.art,armor?13:11,accent,armor?1.4f:.6f);
        for(int i=-1;i<=1;i+=2)
        {
            var panel=Shape("Torn solar array",core.art,PrimitiveType.Cube,new Vector3(i*17,0,-5),new Vector3(18,.7f,12),armor?slate:alloy);
            panel.transform.localRotation=Quaternion.Euler(12,i*15,i*20);
            Shape("Reactor conduit",core.art,PrimitiveType.Cube,new Vector3(i*7,2,0),new Vector3(1,1,14),accent);
        }
        if(armor)
            // Four overlapping belts: the mass is visible, so the cannon discount is legible before anyone reads a number.
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f+Mathf.PI*.25f;
                var belt=Shape("Ablative armor belt",core.art,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*7,1.5f,Mathf.Sin(a)*6),new Vector3(9,13,3.2f),slate);
                belt.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
            }
        if(blast)
        {
            // Split containment cage around a reactor that never got shut down; the vents point where the blast will go.
            for(int i=0;i<3;i++)
            {
                var cage=Ring(core.art,9.5f-i*1.1f,violet,.45f);
                cage.transform.localPosition=new Vector3(0,6,0);
                cage.transform.localRotation=Quaternion.Euler(i*60,i*34,0);
            }
            for(int i=-1;i<=1;i+=2)
            {
                var vent=Shape("Ruptured coolant vent",core.art,PrimitiveType.Cube,new Vector3(i*4.5f,8.5f,0),new Vector3(1.6f,5,1.6f),violet);
                vent.transform.localRotation=Quaternion.Euler(0,0,i*26);
            }
        }
    }

    void BuildPlanet()
    {
        var texture=new Texture2D(1024,512); texture.wrapMode=TextureWrapMode.Repeat;
        for(int y=0;y<512;y++)for(int x=0;x<1024;x++)
        {
            float n=Mathf.PerlinNoise(x*.008f,y*.014f)+.33f*Mathf.PerlinNoise(x*.027f,y*.031f);
            // Five bands (abyss / shelf / strand / lowland / ridge-snow): the old two-band map read as black at low altitude once tonemapping was on.
            Color c=
                n<.60f?Color.Lerp(new Color(.042f,.115f,.225f),new Color(.075f,.255f,.405f),Mathf.InverseLerp(.18f,.60f,n)):
                n<.70f?Color.Lerp(new Color(.085f,.315f,.455f),new Color(.175f,.495f,.525f),Mathf.InverseLerp(.60f,.70f,n)):
                n<.745f?Color.Lerp(new Color(.53f,.48f,.34f),new Color(.34f,.4f,.26f),Mathf.InverseLerp(.70f,.745f,n)):
                n<.95f?Color.Lerp(new Color(.205f,.375f,.245f),new Color(.5f,.47f,.3f),Mathf.InverseLerp(.745f,.95f,n)):
                Color.Lerp(new Color(.66f,.685f,.645f),new Color(.88f,.92f,.95f),Mathf.InverseLerp(.95f,1.2f,n));
            float cloud=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.59f,.8f,Mathf.PerlinNoise(x*.013f+51,y*.022f)));
            texture.SetPixel(x,y,Color.Lerp(c,new Color(.8f,.87f,.93f),cloud*.72f));
        }
        texture.Apply();owned.Add(texture);
        var mat=new Material(Shader.Find("Rift/PlanetSurface"));owned.Add(mat);mat.SetTexture("_BaseMap",texture);
        // Smooth high-resolution sphere prevents the horizon looking polygonal at low altitude.
        int columns=192,rows=96;
        var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];
        var indices=new List<int>();
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
        {
            float lat=(float)y/rows*Mathf.PI,lon=(float)x/columns*Mathf.PI*2;
            int i=y*(columns+1)+x;
            vertices[i]=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon))*PlanetRadius;
            uv[i]=new Vector2((float)x/columns,(float)y/rows);
            if(x<columns && y<rows){int b=i+columns+1;indices.AddRange(new[]{i,i+1,b,i+1,b+1,b});}
        }
        var mesh=new Mesh {name="Nacre sphere",vertices=vertices,uv=uv,triangles=indices.ToArray()};mesh.RecalculateNormals();owned.Add(mesh);
        var planet=new GameObject("Nacre / solid ocean planet");planet.transform.SetParent(world);planet.transform.position=PlanetCenter;
        planet.AddComponent<MeshFilter>().sharedMesh=mesh;planet.AddComponent<MeshRenderer>().sharedMaterial=mat;

        var atmosphereShader=Shader.Find("Rift/Atmosphere");
        if(atmosphereShader)
        {
            var atmosphere=new Material(atmosphereShader);owned.Add(atmosphere);
            atmosphere.SetVector("_PlanetCenter",PlanetCenter);atmosphere.SetFloat("_Radius",PlanetRadius);
            Shape("Blue atmospheric limb",world,PrimitiveType.Sphere,PlanetCenter,Vector3.one*(PlanetRadius+500)*2,atmosphere);
        }
        var stars=new List<Vector3>();var triangles=new List<int>();
        var rng=new System.Random(71);
        for(int i=0;i<1600;i++)
        {
            Vector3 p=new Vector3((float)rng.NextDouble()*2-1,(float)rng.NextDouble()*2-1,(float)rng.NextDouble()*2-1).normalized*8500+PlanetCenter;
            Vector3 right=Vector3.Cross(p-PlanetCenter,Vector3.up).normalized;
            Vector3 up=Vector3.Cross(right,p-PlanetCenter).normalized;
            float s=3+(float)rng.NextDouble()*6;int n=stars.Count;
            stars.Add(p-right*s);stars.Add(p+up*s);stars.Add(p+right*s);
            triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n});
        }
        // The starfield gets its own additive material so its brightness can be a function of altitude instead of a constant.
        starMaterial=Additive(new Color(.78f,.88f,1.1f),0);
        MeshPart(world,"Distant stars",Vector3.zero,Vector3.one,stars.ToArray(),triangles.ToArray(),starMaterial);
        Shape("Moon",world,PrimitiveType.Sphere,new Vector3(-3500,1900,5500),Vector3.one*820,alloy);

        var skyShader=Shader.Find("Rift/Sky");
        if(skyShader)
        {
            var skyMaterial=new Material(skyShader);owned.Add(skyMaterial);
            skyMaterial.SetVector("_SunDir",sunDirection);
            skyMaterial.SetVector("_PlanetCenter",PlanetCenter);skyMaterial.SetFloat("_Radius",PlanetRadius);
            // 6 km radius, drawn Cull Front in the Background queue with no depth write: it can never clip anything, and
            // FollowSky() parks it on the camera every frame so the 14 km far plane is never the thing that ends the world.
            sky=Shape("Sky dome",world,PrimitiveType.Sphere,Vector3.zero,Vector3.one*12000,skyMaterial).transform;
        }
    }

    // Twenty clusters of overlapping flattened spheres instead of twenty-eight single ellipsoids: one sphere at this size
    // reads as a grey egg, six overlapping ones read as a cloud. Deterministic seed, because the screenshot runs compare frames.
    void BuildClouds()
    {
        cloudRoot=new GameObject("Arena / weather").transform;cloudRoot.SetParent(world,false);
        var vapour=new Material(Shader.Find("Universal Render Pipeline/Lit"));owned.Add(vapour);
        // A cloud is a light trap, not a metal panel: no metallic, almost no smoothness, so it only ever shows the sun's side.
        vapour.SetColor("_BaseColor",new Color(.78f,.82f,.88f));vapour.color=new Color(.78f,.82f,.88f);
        vapour.SetFloat("_Metallic",0);vapour.SetFloat("_Smoothness",.06f);
        var rng=new System.Random(1607);int made=0;
        for(int attempt=0;attempt<400 && made<20;attempt++)
        {
            Vector3 at=SurfacePoint((float)rng.NextDouble()*360,(float)rng.NextDouble()*70-20,40+(float)rng.NextDouble()*70);
            bool clear=true;
            // 400 m of clearance from every refinery: a cloud bank parked on a capture ring would hide the whole fight.
            foreach(var gate in gates) if(Vector3.Distance(at,gate.transform.position)<400) clear=false;
            if(!clear) continue;
            made++;
            var cluster=new GameObject("Cloud bank").transform;cluster.SetParent(cloudRoot,false);
            cluster.position=at;cluster.rotation=Quaternion.FromToRotation(Vector3.up,Up(at));
            int puffs=5+rng.Next(3);
            for(int i=0;i<puffs;i++)
            {
                float radius=30+(float)rng.NextDouble()*40;
                Vector3 offset=new Vector3((float)rng.NextDouble()*130-65,(float)rng.NextDouble()*18-9,(float)rng.NextDouble()*95-48);
                Shape("Vapour",cluster,PrimitiveType.Sphere,offset,new Vector3(radius,radius*.4f,radius*.78f),vapour);
            }
        }
    }

    // Thirty props inside 350 m of every refinery. Nothing here has collision, by design: the arena has no physics engine,
    // and the spires are capped at 70 m so the ground furniture always stays under the 155 m salvage decks.
    void BuildScatter()
    {
        scatterRoot=new GameObject("Arena / surface scatter").transform;scatterRoot.SetParent(world,false);
        // Its own rock material: the hull's dark (.025) turns every spire into a black cutout once the sun is low.
        var stone=new Material(Shader.Find("Universal Render Pipeline/Lit"));owned.Add(stone);
        stone.SetColor("_BaseColor",new Color(.21f,.2f,.18f));stone.color=new Color(.21f,.2f,.18f);
        stone.SetFloat("_Metallic",0);stone.SetFloat("_Smoothness",.12f);
        var rng=new System.Random(20260921);
        foreach(var gate in gates)
        {
            Vector3 up=Up(gate.transform.position),ground=PlanetCenter+up*PlanetRadius;
            Vector3 east=Vector3.Cross(up,Vector3.forward).normalized;
            if(east.sqrMagnitude<.5f)east=Vector3.Cross(up,Vector3.right).normalized;
            Vector3 north=Vector3.Cross(east,up);
            for(int i=0;i<30;i++)
            {
                float angle=(float)rng.NextDouble()*Mathf.PI*2,distance=90+(float)rng.NextDouble()*260;
                Vector3 at=PlanetCenter+(ground+(east*Mathf.Cos(angle)+north*Mathf.Sin(angle))*distance-PlanetCenter).normalized*PlanetRadius;
                Vector3 localUp=Up(at);
                Quaternion stand=Quaternion.FromToRotation(Vector3.up,localUp)*Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                if(i%5==4)
                {
                    // One relay mast per five props: a man-made vertical among the rocks, and the only lit thing on the ground.
                    Shape("Relay mast",scatterRoot,PrimitiveType.Cube,at+localUp*30,new Vector3(2,60,2),slate).transform.rotation=stand;
                    Shape("Mast beacon",scatterRoot,PrimitiveType.Sphere,at+localUp*61,Vector3.one*3.4f,i%15==4?gold:i%10==4?red:teal);
                }
                else
                {
                    float height=15+(float)rng.NextDouble()*55,width=6+(float)rng.NextDouble()*12;
                    var rock=Shape("Basalt spire",scatterRoot,PrimitiveType.Cube,at+localUp*height*.5f,new Vector3(width,height,width*.75f),i%3==0?slate:stone);
                    rock.transform.rotation=stand*Quaternion.Euler((float)rng.NextDouble()*10-5,0,(float)rng.NextDouble()*10-5);
                }
            }
        }
    }

    // A ParticleSystem is not the physics engine: this is a render-only speed cue, driven from UpdateChaseCamera's dt.
    void BuildWindStreaks()
    {
        var host=new GameObject("Wind streaks");host.transform.SetParent(cam.transform,false);
        windStreaks=host.AddComponent<ParticleSystem>();
        var main=windStreaks.main;
        main.startLifetime=.45f;main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.25f,.7f);
        main.startColor=new Color(.72f,.84f,1f);main.maxParticles=260;
        // World space: the streak is spawned around the camera and then left behind by it, which is the whole parallax read.
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=true;
        var emission=windStreaks.emission;emission.rateOverTime=0;
        var shape=windStreaks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=26;shape.radiusThickness=.7f;shape.position=new Vector3(0,0,19);
        var velocity=windStreaks.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
        var size=windStreaks.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.35f,1),new Keyframe(1,0)));
        var renderer=host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.04f;renderer.lengthScale=2.4f;renderer.cameraVelocityScale=0;
        renderer.sharedMaterial=Additive(new Color(.62f,.76f,1f),.5f);
        windStreaks.Play();
    }
}
