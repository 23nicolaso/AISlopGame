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
        core.art=new GameObject("Wreck / exposed reactor").transform;core.art.SetParent(core.transform,false);
        Hull(core.art,"Broken armored hull",Vector3.zero,new Vector3(9,10,7),dark);
        core.weakPoint=Shape("Exposed golden reactor / shoot",core.art,PrimitiveType.Sphere,new Vector3(0,6,0),Vector3.one*6,gold).transform;
        Ring(core.art,11,gold,.6f);
        for(int i=-1;i<=1;i+=2)
        {
            var panel=Shape("Torn solar array",core.art,PrimitiveType.Cube,new Vector3(i*17,0,-5),new Vector3(18,.7f,12),alloy);
            panel.transform.localRotation=Quaternion.Euler(12,i*15,i*20);
            Shape("Reactor conduit",core.art,PrimitiveType.Cube,new Vector3(i*7,2,0),new Vector3(1,1,14),gold);
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
        MeshPart(world,"Distant stars",Vector3.zero,Vector3.one,stars.ToArray(),triangles.ToArray(),white);
        Shape("Moon",world,PrimitiveType.Sphere,new Vector3(-3500,1900,5500),Vector3.one*820,alloy);
        // Local wisps and sparse aerial debris provide motion references.
        for(int i=0;i<28;i++)
        {
            float lat=i*360f/28;
            var p=SurfacePoint(lat,20*Mathf.Sin(i*2),60);
            var cloud=Shape("Cloud bank",world,PrimitiveType.Sphere,p,new Vector3(100,5,45),Material(new Color(.46f,.58f,.68f),false));
            cloud.transform.rotation=Quaternion.FromToRotation(Vector3.up,Up(p));
        }
    }
}
