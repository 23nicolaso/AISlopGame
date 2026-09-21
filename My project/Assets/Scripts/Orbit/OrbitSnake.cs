using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ORBIT SNAKE — a snake on the shell of a planet. No orbital mechanics: the snake's whole state is a unit normal (where it
// is) and a unit tangent (where it points); moving is rotating the normal, turning is rotating the tangent about the normal.
// Junk rides great circles of the same shell at a fixed angular rate. Contact is a catch when the headings match and a
// strike otherwise. Everything runs through Step(dt) so the Editor checks can drive it at a fixed dt.
public partial class OrbitSnake : MonoBehaviour
{
    public static OrbitSnake I;
    public const float PlanetRadius=300f;
    // Shell altitudes by level. Each climb is into a belt that was never swept.
    public static readonly float[] ShellAltitude={40,72,108,150,200};
    public static readonly string[] ShellNames={"LOW ORBIT","MID ORBIT","HIGH ORBIT","DEEP FIELD","ESCAPE"};
    // Segments Space needs before it will eject the train and lift the ship one shell.
    public static readonly int[] EjectQuota={4,6,8,10};
    // Junk per shell in the greybox; the field is denser the higher you climb.
    public static readonly int[] JunkCount={22,32,44,58,0};
    // 60 u/s at 150 deg/s is a 23 u turning circle (144 u round): a train past ~20 segments can be bitten by its own head,
    // which is the classic snake stake and the reason the eject quota climbs.
    public const float Speed=60f, TurnRate=150f, JunkSpeedMin=.55f, JunkSpeedMax=.9f;
    // A contact counts as a catch when the snake's heading is within this many degrees of the junk's direction of travel.
    public const float CatchAngle=45f, ContactRadius=6f, BiteRadius=4.2f, SegmentSpacing=7f, StrikeGrace=1.5f;
    public const int MaxSegments=30;

    public bool paused, ended, won;
    public float elapsed; public int level, score, caught, strikes; public string endReason="";
    public OrbitShip ship;
    public readonly List<OrbitJunk> junk=new();
    public readonly List<OrbitFalling> falling=new();
    public readonly List<Object> owned=new();
    public Transform world; public Camera cam;
    public string toast=""; public float toastTimer; public readonly List<string> feed=new();
    public float ejectPulse, strikePulse, catchPulse; public int lastEjected;
    System.Random rng;

    public static float ShellRadius(int level) => PlanetRadius+ShellAltitude[Mathf.Clamp(level,0,ShellAltitude.Length-1)];

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
        BuildArt(); BuildAudio(); BuildCamera();
        Restart(7);
    }

    public void Restart(int seed)
    {
        rng=new System.Random(seed);
        foreach(var j in junk)Destroy(j.gameObject); junk.Clear();
        foreach(var f in falling)Destroy(f.gameObject); falling.Clear();
        if(ship)Destroy(ship.gameObject);
        elapsed=0; level=0; score=0; caught=0; strikes=0; ended=false; won=false; paused=false; endReason=""; feed.Clear(); toast=""; toastTimer=0; ejectPulse=strikePulse=catchPulse=0; lastEjected=0;
        ship=new GameObject("Snake").AddComponent<OrbitShip>(); ship.transform.SetParent(world);
        ship.Init(Vector3.up,Vector3.forward,ShellRadius(0)); BuildShipArt(ship);
        SeedShell(0);
        Toast("LOW ORBIT — A/D to turn. Come up behind junk to catch it; hit it head-on and you lose segments.");
        SnapCamera();
    }

    // Junk on random great circles of a shell. Half runs the other way round its circle so the same crossing is a catch
    // from one side and a strike from the other.
    public void SeedShell(int shell)
    {
        for(int i=0;i<JunkCount[Mathf.Clamp(shell,0,JunkCount.Length-1)];i++)
        {
            Vector3 axis=RandomUnit(); float phase=(float)rng.NextDouble()*Mathf.PI*2;
            float speed=Mathf.Lerp(JunkSpeedMin,JunkSpeedMax,(float)rng.NextDouble())*Speed*(i%2==0?1:-1);
            SpawnJunk(shell,axis,phase,speed/ShellRadius(shell),false);
        }
    }
    Vector3 RandomUnit(){ float z=(float)rng.NextDouble()*2-1,a=(float)rng.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt(1-z*z); return new Vector3(r*Mathf.Cos(a),z,r*Mathf.Sin(a)); }

    public OrbitJunk SpawnJunk(int shell,Vector3 axis,float phase,float rate,bool wreck)
    {
        var j=new GameObject(wreck?"Severed segment":"Junk").AddComponent<OrbitJunk>(); j.transform.SetParent(world);
        j.Init(shell,axis,phase,rate,wreck); BuildJunkArt(j); junk.Add(j); return j;
    }
    // Junk placed at a point moving in a direction: the great circle through the point along that tangent.
    public OrbitJunk SpawnJunkAt(int shell,Vector3 normal,Vector3 dir,float speed,bool wreck)
    {
        dir=(dir-normal*Vector3.Dot(dir,normal)).normalized; if(dir.sqrMagnitude<1e-6f)dir=Vector3.Cross(normal,Vector3.right).normalized;
        Vector3 axis=Vector3.Cross(normal,dir).normalized; var j=SpawnJunk(shell,axis,0,speed/ShellRadius(shell),wreck); j.SetBasis(normal,dir); return j;
    }

    public void Toast(string s){toast=s;toastTimer=4f;feed.Add(s);if(feed.Count>6)feed.RemoveAt(0);}

    void FixedUpdate(){ Advance(Time.fixedDeltaTime); }
    // The pause gate. Step() itself is unconditional so the checks can drive it while the game is paused.
    public void Advance(float dt){ if(paused||ended)return; Step(dt); }

    // One deterministic world step: the snake moves, junk advances, then contacts are resolved against this step's poses.
    public void Step(float dt)
    {
        elapsed+=dt; ship.Simulate(dt);
        foreach(var j in junk)j.Tick(dt);
        for(int i=falling.Count-1;i>=0;i--){ falling[i].Tick(dt); if(falling[i].done){Destroy(falling[i].gameObject);falling.RemoveAt(i);} }
        if(ship.grace>0)ship.grace-=dt;
        for(int i=junk.Count-1;i>=0;i--)
        {
            var j=junk[i]; if(j.shell!=level||j.age<1)continue;
            if((j.Position-ship.Position).magnitude>ContactRadius)continue;
            float angle=Vector3.Angle(ship.tangent,j.Direction);
            if(angle<CatchAngle){ Catch(j); junk.RemoveAt(i); }
            else if(ship.grace<=0){ Strike(j); junk.RemoveAt(i); if(ended)return; }
        }
        ship.CheckSelfBite();
        if(toastTimer>0)toastTimer-=dt; ejectPulse=Mathf.Max(0,ejectPulse-dt); strikePulse=Mathf.Max(0,strikePulse-dt); catchPulse=Mathf.Max(0,catchPulse-dt);
    }

    void Catch(OrbitJunk j)
    {
        caught++; score+=10+5*level; catchPulse=.4f;
        if(ship.segments.Count<MaxSegments)ship.AddSegment(); Destroy(j.gameObject); Ping(1);
    }

    // A strike costs the two hindmost segments; with nothing left to shed the hull goes and the run ends.
    void Strike(OrbitJunk j)
    {
        Destroy(j.gameObject); strikes++; strikePulse=.6f; ship.shake=1;
        if(ship.segments.Count==0){ End(false,"Struck by "+(j.wreck?"your own wreckage":"debris")+" with nothing left to shed"); return; }
        ship.Shed(2); ship.grace=StrikeGrace; Toast("STRIKE — two segments lost"); Ping(0);
    }

    public void End(bool win,string why){ if(ended)return; ended=true; won=win; endReason=why; ship.dead=!win; Toast(why); Ping(win?3:0); }

    // Space: the whole train is thrown down at the planet and burns; the recoil lifts the ship one shell. Score scales with
    // the square of the train so holding on past the quota is worth something, and the tail you were risking is the stake.
    public bool Eject()
    {
        if(ended||level>=EjectQuota.Length||ship.segments.Count<EjectQuota[level])return false;
        int n=ship.segments.Count; lastEjected=n;
        for(int i=0;i<n;i++){ var f=new GameObject("Falling segment").AddComponent<OrbitFalling>(); f.transform.SetParent(world); f.Init(ship.segments[i].position,i*.06f); BuildFallingArt(f); falling.Add(f); }
        ship.Shed(n); score+=n*n*5+50*(level+1); ejectPulse=1.2f;
        level++; ship.Lift(ShellRadius(level)); if(junk.FindAll(x=>x.shell==level).Count==0)SeedShell(level);
        Toast("EJECTED "+n+" — burning up below. Climbing to "+ShellNames[level]); Chime(level);
        if(level>=ShellAltitude.Length-1)End(true,"ESCAPE — you cleared the field. Score "+score);
        return true;
    }

    void Update()
    {
        var k=Keyboard.current; if(k==null)return;
        if(k.escapeKey.wasPressedThisFrame&&!ended)paused=!paused;
        if(ended&&k.enterKey.wasPressedThisFrame)Restart((int)(Time.realtimeSinceStartup*1000)&0xffff);
        if(paused||ended){ship.turn=0;return;}
        float turn=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
        var m=Mouse.current; if(m!=null&&Cursor.lockState==CursorLockMode.Locked)turn+=m.delta.ReadValue().x*.02f;
        ship.turn=Mathf.Clamp(turn,-1,1);
        if(k.spaceKey.wasPressedThisFrame)Eject();
    }

    void LateUpdate(){ if(!ship)return; UpdateCamera(Time.deltaTime); }
    void OnDestroy(){ foreach(var o in owned)if(o)Destroy(o); if(I==this)I=null; }
}
