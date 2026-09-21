using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ORBIT SNAKE — a two-body orbital snake. The planet sits at the origin, every body lives in the XZ plane and the camera
// looks straight down the Y axis, so an orbit is literally the ellipse the prediction line draws. Nothing here uses the
// physics engine: OrbitBody integrates its own state with velocity Verlet and the conic elements come straight from the
// state vector. All simulation goes through Step(dt) so the Editor checks can drive it deterministically.
public partial class OrbitSnake : MonoBehaviour
{
    public static OrbitSnake I;
    // Gravitational parameter. mu=120000 puts a circular orbit at r=140 on a 30 s period, which is the pace a snake needs.
    public const float Mu=120000f, PlanetRadius=100f, AtmosphereTop=112f;
    // Level shells by radius. A level is reached the first time the ship's radius crosses the shell's floor.
    public static readonly float[] ShellFloor={115,170,260,380,520};
    public static readonly string[] LevelNames={"LOW ORBIT","MEDIUM ORBIT","HIGH ORBIT","LUNAR TRANSFER","ESCAPE"};
    public static readonly string[] SkillNames={"PROGRADE / RETROGRADE  W S","RADIAL BURN  A D","TAIL WHIP  Q","TIME WARP  T","ESCAPE BURN"};
    // Segments a boost consumes to lift the apoapsis into the next shell.
    public static readonly int[] BoostQuota={5,7,9,11};
    public const int MaxSegments=24;
    public const float SegmentSpacing=3.6f, CatchRadius=4.5f, DebrisRadius=2.6f, WhipSpeed=16f;
    // Engine: acceleration at empty mass, fuel per second at full throttle. A full tank is 100.
    public const float Thrust=4.2f, FuelBurn=9f, PodFuel=22f, ShipMass=1f, SegmentMass=.16f;

    public bool paused;
    public float elapsed, time;
    public int level, score, caught, warp=1;
    public string deathReason;
    public bool ended, won;
    public SnakeShip ship;
    public readonly List<SupplyPod> pods=new();
    public readonly List<OrbitDebris> debris=new();
    public readonly List<TailShot> shots=new();
    public readonly List<Object> owned=new();
    public Transform world;
    public Camera cam;
    float launchTimer, boostPulse;
    public float toastTimer; public string toast="";
    public readonly List<string> feed=new();
    System.Random rng=new System.Random(7);

    public static Vector3 Gravity(Vector3 p) => -p*(Mu/(p.sqrMagnitude*p.magnitude));
    public static float Radius(Vector3 p) => p.magnitude;
    public static float CircularSpeed(float r) => Mathf.Sqrt(Mu/r);
    public static float Period(float a) => 2*Mathf.PI*Mathf.Sqrt(a*a*a/Mu);
    // Prograde is the direction of motion; for a body at rest it is the counter-clockwise tangent seen from above.
    public static Vector3 Prograde(Vector3 p,Vector3 v) => v.sqrMagnitude>1e-4f?v.normalized:Vector3.Cross(Vector3.up,p.normalized);
    public static int LevelOf(float r){int l=0;for(int i=0;i<ShellFloor.Length;i++)if(r>=ShellFloor[i])l=i;return l;}

    public struct Conic { public float a,e,apo,peri,energy,period; public Vector3 periDir,side; public bool bound; }
    // Classical elements from a state vector. e comes from the eccentricity vector so a near-circular orbit still has a
    // well-defined periapsis direction; `side` is the in-plane direction of increasing true anomaly.
    public static Conic Elements(Vector3 p,Vector3 v)
    {
        float r=p.magnitude,v2=v.sqrMagnitude; Conic c;
        c.energy=v2*.5f-Mu/r; c.a=-Mu/(2*c.energy);
        Vector3 ev=((v2-Mu/r)*p-Vector3.Dot(p,v)*v)/Mu; c.e=ev.magnitude;
        c.periDir=c.e>1e-4f?ev/c.e:p/r;
        Vector3 h=Vector3.Cross(p,v); Vector3 n=h.sqrMagnitude>1e-6f?h.normalized:Vector3.up;
        c.side=Vector3.Cross(n,c.periDir);
        c.bound=c.e<1&&c.energy<0; c.apo=c.bound?c.a*(1+c.e):float.PositiveInfinity; c.peri=c.a*(1-c.e);
        if(!c.bound)c.peri=Mathf.Abs(c.a)*(c.e-1);
        c.period=c.bound?Period(c.a):float.PositiveInfinity; return c;
    }
    public static Vector3 PointOn(Conic c,float theta)
    {
        float p=c.a*(1-c.e*c.e); float r=p/(1+c.e*Mathf.Cos(theta));
        return c.periDir*(r*Mathf.Cos(theta))+c.side*(r*Mathf.Sin(theta));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(SceneManager.GetActiveScene().name!="OrbitSnake")return;
        if(FindAnyObjectByType<OrbitSnake>()==null)new GameObject("ORBIT SNAKE").AddComponent<OrbitSnake>();
    }

    void Awake()
    {
        if(I&&I!=this){Destroy(gameObject);return;}
        I=this; Application.runInBackground=true;
        foreach(var c in FindObjectsByType<Camera>())c.enabled=false;
        foreach(var l in FindObjectsByType<Light>())l.enabled=false;
        foreach(var a in FindObjectsByType<AudioListener>())a.enabled=false;
        world=new GameObject("Orbit / generated").transform; world.SetParent(transform,false);
        BuildArt(); BuildAudio();
        cam=new GameObject("Orbit camera").AddComponent<Camera>(); cam.transform.SetParent(world);
        cam.tag="MainCamera"; cam.orthographic=true; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.012f,.014f,.03f);
        cam.transform.position=new Vector3(0,600,0); cam.transform.rotation=Quaternion.Euler(90,0,0); cam.nearClipPlane=1; cam.farClipPlane=1400;
        cam.gameObject.AddComponent<AudioListener>();
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world); sun.type=LightType.Directional; sun.intensity=2.2f; sun.color=new Color(1,.93f,.82f);
        sun.transform.rotation=Quaternion.Euler(28,-60,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.16f,.19f,.27f);
        Restart();
    }

    public void Restart()
    {
        foreach(var p in pods)Destroy(p.gameObject); pods.Clear();
        foreach(var d in debris)Destroy(d.gameObject); debris.Clear();
        foreach(var s in shots)Destroy(s.gameObject); shots.Clear();
        if(ship)Destroy(ship.gameObject);
        elapsed=0; time=0; level=0; score=0; caught=0; warp=1; ended=false; won=false; deathReason=""; feed.Clear(); toast=""; toastTimer=0; launchTimer=2; boostPulse=0;
        ship=new GameObject("Snake").AddComponent<SnakeShip>(); ship.transform.SetParent(world);
        float r0=132; ship.Init(new Vector3(r0,0,0),new Vector3(0,0,-CircularSpeed(r0)));
        BuildShipArt(ship);
        SeedBelts();
        Toast("LOW ORBIT — burn prograde (W) to climb, retrograde (S) to fall. Catch the supply pods.");
    }

    // Kessler belts: debris on circular orbits just above each shell floor. Half of each belt runs retrograde so a snake
    // crossing the belt meets some of it at twice orbital speed and cannot simply pace it.
    void SeedBelts()
    {
        for(int shell=1;shell<ShellFloor.Length-1;shell++)
        {
            int count=8+shell*4; float r=ShellFloor[shell]+8;
            for(int i=0;i<count;i++)
            {
                float ang=i*Mathf.PI*2/count+shell*.37f; float rr=r+(float)rng.NextDouble()*10-5;
                Vector3 p=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang))*rr; int dir=i%2==0?1:-1;
                SpawnDebris(p,Vector3.Cross(Vector3.up,p.normalized)*(CircularSpeed(rr)*dir),false);
            }
        }
    }

    public OrbitDebris SpawnDebris(Vector3 p,Vector3 v,bool fromTail)
    {
        var d=new GameObject(fromTail?"Severed segment":"Debris").AddComponent<OrbitDebris>(); d.transform.SetParent(world);
        d.Init(p,v); d.fromTail=fromTail; BuildDebrisArt(d); debris.Add(d); return d;
    }

    // Supply launches. The pad is chosen so the pod's apoapsis arrives just ahead of where the ship will be: the phase
    // angle is the ship's lead over half a transfer period. Even so, half the pods are suborbital and fall back.
    public SupplyPod LaunchPod(float targetRadius,bool insert)
    {
        var c=Elements(ship.pos,ship.vel); float a=(PlanetRadius+targetRadius)*.5f; float flight=Period(a)*.5f;
        // Where the ship will be after the pod's flight, then back off by 180 degrees so the pad is opposite the arrival point.
        float shipAngle=Mathf.Atan2(ship.pos.z,ship.pos.x); float rate=(c.bound?2*Mathf.PI/c.period:2*Mathf.PI/40)*Mathf.Sign(Vector3.Cross(ship.pos,ship.vel).y);
        float arrival=shipAngle+rate*(flight+2.4f); float padAngle=arrival-Mathf.PI*Mathf.Sign(rate);
        Vector3 pad=new Vector3(Mathf.Cos(padAngle),0,Mathf.Sin(padAngle))*PlanetRadius;
        var pod=new GameObject("Supply pod").AddComponent<SupplyPod>(); pod.transform.SetParent(world);
        pod.Init(pad,targetRadius,insert,Mathf.Sign(rate)); BuildPodArt(pod); pods.Add(pod); return pod;
    }

    public void Toast(string s){toast=s;toastTimer=4.5f;feed.Add(s);if(feed.Count>6)feed.RemoveAt(0);}

    void FixedUpdate(){ if(paused||ended)return; for(int i=0;i<warp;i++)Step(Time.fixedDeltaTime); }

    // One deterministic world step. Order matters: the ship moves first so catches and collisions use this step's pose.
    public void Step(float dt)
    {
        elapsed+=dt; time+=dt;
        ship.Simulate(dt);
        float r=ship.pos.magnitude;
        if(r<AtmosphereTop&&!ship.dead){ ship.heat+=dt*(AtmosphereTop-r)*1.6f; if(r<PlanetRadius+2||ship.heat>10)Die("Burned up in the atmosphere"); }
        else ship.heat=Mathf.Max(0,ship.heat-dt*2);
        if(ship.dead)return;
        int reached=LevelOf(r);
        if(reached>level){ level=reached; score+=250*level; Toast(LevelNames[level]+" — "+(level<SkillNames.Length?"unlocked "+SkillNames[level]:"")); Chime(level); if(level>=ShellFloor.Length-1){won=true;ended=true;} }
        launchTimer-=dt;
        if(launchTimer<=0&&pods.Count<4){ launchTimer=5.5f; float target=Mathf.Lerp(ShellFloor[level]+6,ShellFloor[Mathf.Min(level+1,ShellFloor.Length-1)]-8,(float)rng.NextDouble()); LaunchPod(target,rng.NextDouble()<.5); }
        for(int i=pods.Count-1;i>=0;i--)
        {
            var p=pods[i]; p.Tick(dt);
            if(p.pos.magnitude<PlanetRadius+1&&p.phase==SupplyPod.Phase.Coast){ Destroy(p.gameObject); pods.RemoveAt(i); continue; }
            if(p.phase!=SupplyPod.Phase.Ascent&&(p.pos-ship.pos).magnitude<CatchRadius){ Catch(p); pods.RemoveAt(i); }
        }
        for(int i=shots.Count-1;i>=0;i--){ var s=shots[i]; s.Tick(dt); if(s.age>14||s.pos.magnitude<PlanetRadius){Destroy(s.gameObject);shots.RemoveAt(i);} }
        for(int i=debris.Count-1;i>=0;i--)
        {
            var d=debris[i]; d.Tick(dt);
            if(d.pos.magnitude<PlanetRadius+1){Destroy(d.gameObject);debris.RemoveAt(i);continue;}
            bool gone=false;
            for(int j=shots.Count-1;j>=0;j--)if((shots[j].pos-d.pos).magnitude<DebrisRadius+1){Destroy(shots[j].gameObject);shots.RemoveAt(j);gone=true;score+=40;Ping(2);break;}
            if(!gone&&d.age>1.5f&&(d.pos-ship.pos).magnitude<DebrisRadius){ Strike(d); gone=true; }
            if(gone){Destroy(d.gameObject);debris.RemoveAt(i);}
        }
        ship.CheckSelfCollision();
        if(toastTimer>0)toastTimer-=dt; if(boostPulse>0)boostPulse-=dt;
    }

    void Catch(SupplyPod p)
    {
        caught++; score+=100+20*level; ship.fuel=Mathf.Min(100,ship.fuel+PodFuel);
        if(ship.segments.Count<MaxSegments)ship.AddSegment(); Destroy(p.gameObject); Ping(1);
    }

    // A debris strike costs the two hindmost segments; with nothing left to shed it is the hull that goes.
    void Strike(OrbitDebris d)
    {
        if(ship.segments.Count==0){Die("Hull breached by "+(d.fromTail?"your own severed tail":"orbital debris"));return;}
        ship.Shed(2); Toast("DEBRIS STRIKE — lost two segments"); Ping(0); ship.shake=1;
    }

    public void Die(string why){ if(ship.dead)return; ship.dead=true; ended=true; deathReason=why; Toast(why); Ping(0); }

    // Space: eject the quota of segments retrograde as reaction mass and set the tangential speed for an apoapsis
    // in the middle of the next shell. The periapsis stays where it was, so every orbit still dips through the belt below.
    public bool Boost()
    {
        if(level>=BoostQuota.Length||ship.segments.Count<BoostQuota[level]||ship.dead)return false;
        float r=ship.pos.magnitude; float target=(ShellFloor[level+1]+ShellFloor[Mathf.Min(level+2,ShellFloor.Length-1)])*.5f;
        if(level+2>=ShellFloor.Length)target=ShellFloor[level+1]+40;
        float a=(r+target)*.5f; float vt=Mathf.Sqrt(Mu*(2/r-1/a));
        Vector3 up=ship.pos.normalized, side=Prograde(ship.pos,ship.vel); side=(side-up*Vector3.Dot(side,up)).normalized;
        float radial=Vector3.Dot(ship.vel,up);
        for(int i=0;i<BoostQuota[level];i++){ var seg=ship.segments[ship.segments.Count-1]; SpawnDebris(seg.position,ship.vel-side*(WhipSpeed*.5f)*(1+i*.08f),true); ship.RemoveLast(); }
        ship.vel=up*radial+side*vt; ship.RebuildTailFromHead(); boostPulse=1; score+=150; Toast("ORBIT BOOST — apoapsis lifted into "+LevelNames[level+1]); Chime(level+1); return true;
    }

    void Update()
    {
        var k=Keyboard.current; if(k==null)return;
        if(k.escapeKey.wasPressedThisFrame&&!ended)paused=!paused;
        if(ended&&k.enterKey.wasPressedThisFrame)Restart();
        if(paused||ended){ship.throttle=Vector2.zero;return;}
        float pro=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
        float rad=level>=1?(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0):0;
        ship.throttle=new Vector2(pro,rad);
        if(k.spaceKey.wasPressedThisFrame)Boost();
        if(k.qKey.wasPressedThisFrame&&level>=2)ship.Whip();
        if(k.tKey.wasPressedThisFrame&&level>=3)warp=warp==1?4:1;
        UpdateCamera(Time.deltaTime);
    }

    void OnDestroy(){ foreach(var o in owned)if(o)Destroy(o); if(I==this)I=null; }
}
