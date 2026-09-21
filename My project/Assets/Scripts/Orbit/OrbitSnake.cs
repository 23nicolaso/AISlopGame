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
    // A small planet: a lap of the first shell is 20 s at 60 u/s, and the horizon curves inside the frame.
    public const float PlanetRadius=160f;
    // Shell altitudes by level. Each climb is into a belt that was never swept.
    public static readonly float[] ShellAltitude={30,55,85,120,160};
    public static readonly string[] ShellNames={"LOW ORBIT","MID ORBIT","HIGH ORBIT","DEEP FIELD","ESCAPE"};
    // Segments Space needs before it will eject the train and lift the ship one shell.
    public static readonly int[] EjectQuota={4,6,8,10};
    // Junk per shell: about one piece per 11 000 u^2 of shell, so at 60 u/s the snake meets something every few seconds.
    public static readonly int[] JunkCount={40,60,80,100,0};
    // 60 u/s at 150 deg/s is a 23 u turning circle (144 u round): a train past ~20 segments can be bitten by its own head,
    // which is the classic snake stake and the reason the eject quota climbs.
    public const float Speed=60f, TurnRate=150f, JunkSpeedMin=.55f, JunkSpeedMax=.9f;
    // A contact counts as a catch when the snake's heading is within this many degrees of the junk's direction of travel.
    public const float CatchAngle=45f, MagnetCatchAngle=70f, ContactRadius=6f, BiteRadius=4.2f, SegmentSpacing=7f, StrikeGrace=1.5f;
    public const int MaxSegments=30;
    // Skills, one picked per shell by flying through its icon. Magnet widens the catch cone; Armour eats one strike; Whip
    // fires the last segment forward (Q); Brake halves speed while S is held; Phase is a sideways hop on a double tap;
    // Compound pays 1.5x on ejection.
    public enum Skill{Magnet,Armour,Whip,Brake,Phase,Compound}
    public const int SkillCount=6; public const float PodRadius=8f, WhipSpeed=1.6f, BrakeFactor=.55f, DashDistance=14f, DashTime=.25f;
    public static readonly Color[] SkillColors={new Color(1.3f,.35f,1.2f),new Color(1.2f,1.2f,1.3f),new Color(1.4f,.7f,.2f),new Color(1.3f,1.2f,.3f),new Color(.3f,1.2f,1.4f),new Color(.4f,1.3f,.5f)};

    public bool paused, ended, won;
    public float elapsed; public int level, score, caught, strikes; public string endReason="";
    public OrbitShip ship;
    public readonly List<OrbitJunk> junk=new();
    public readonly List<OrbitFalling> falling=new();
    public readonly List<SkillPod> skillPods=new();
    public readonly bool[] skills=new bool[SkillCount]; public int armour;
    public readonly List<Object> owned=new();
    public Transform world; public Camera cam;
    public string toast=""; public float toastTimer; public readonly List<string> feed=new();
    public float ejectPulse, strikePulse, catchPulse, armourPulse, pickPulse, endTimer; public int lastEjected;
    System.Random rng;
    // Built player: -orbit-screenshot=<png> captures the full frame with the IMGUI HUD 3 s in (Camera.Render in the
    // Editor cannot see OnGUI), on a scripted weave with a starter train, then quits.
    string screenshotPath; bool shotTaken;

    public static float ShellRadius(int level) => PlanetRadius+ShellAltitude[Mathf.Clamp(level,0,ShellAltitude.Length-1)];
    public float CatchAngleNow => skills[(int)Skill.Magnet]?MagnetCatchAngle:CatchAngle;
    public bool Has(Skill s) => skills[(int)s];
    public int Quota => level<EjectQuota.Length?EjectQuota[level]:0;
    public bool EjectReady => Quota>0&&ship&&ship.segments.Count>=Quota&&!ended;
    public int EjectValue(int n) => Mathf.RoundToInt((n*n*5+50*(level+1))*(Has(Skill.Compound)?1.5f:1));

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
        foreach(var arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("-orbit-screenshot="))screenshotPath=arg.Substring(18);
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
        // Immediate, not deferred: the checks and the screenshot runner restart several times inside one frame.
        foreach(var j in junk)DestroyImmediate(j.gameObject); junk.Clear();
        foreach(var f in falling)DestroyImmediate(f.gameObject); falling.Clear();
        foreach(var p in skillPods)DestroyImmediate(p.gameObject); skillPods.Clear();
        if(ship)DestroyImmediate(ship.gameObject);
        for(int i=0;i<SkillCount;i++)skills[i]=false; armour=0;
        elapsed=0; level=0; score=0; caught=0; strikes=0; ended=false; won=false; paused=false; endReason=""; feed.Clear(); toast=""; toastTimer=0; ejectPulse=strikePulse=catchPulse=armourPulse=pickPulse=endTimer=0; lastEjected=0;
        ship=new GameObject("Snake").AddComponent<OrbitShip>(); ship.transform.SetParent(world);
        ship.Init(Vector3.up,Vector3.forward,ShellRadius(0)); BuildShipArt(ship);
        SeedShell(0); ResetFx();
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

    // Three unowned skills as icons on the new shell, 70 u ahead and 28 u apart across the heading. Fly through one to
    // take it; the other two vanish. Nothing is written anywhere: the icon is the offer.
    public void OfferSkills()
    {
        foreach(var p in skillPods)Destroy(p.gameObject); skillPods.Clear();
        var pool=new List<int>(); for(int i=0;i<SkillCount;i++)if(!skills[i])pool.Add(i);
        int count=Mathf.Min(3,pool.Count); float r=ShellRadius(level);
        for(int k=0;k<count;k++)
        {
            int pick=pool[rng.Next(pool.Count)]; pool.Remove(pick);
            var ahead=Quaternion.AngleAxis(70f/r*Mathf.Rad2Deg,Vector3.Cross(ship.normal,ship.tangent)); Vector3 n=ahead*ship.normal, t=ahead*ship.tangent;
            n=Quaternion.AngleAxis((k-(count-1)*.5f)*28f/r*Mathf.Rad2Deg,t)*n;
            var pod=new GameObject("Skill "+(Skill)pick).AddComponent<SkillPod>(); pod.transform.SetParent(world); pod.Init(level,n.normalized,(Skill)pick); BuildPodArt(pod); skillPods.Add(pod);
        }
    }
    public void Take(SkillPod pod)
    {
        skills[(int)pod.skill]=true; if(pod.skill==Skill.Armour)armour=1; pickPulse=.8f; score+=25; Chime(4);
        foreach(var p in skillPods)Destroy(p.gameObject); skillPods.Clear(); Toast("SKILL "+pod.skill);
    }

    public void Toast(string s){toast=s;toastTimer=4f;feed.Add(s);if(feed.Count>6)feed.RemoveAt(0);}

    void FixedUpdate(){ Advance(Time.fixedDeltaTime); }
    // The pause gate. Step() itself is unconditional so the checks can drive it while the game is paused.
    public void Advance(float dt){ if(paused)return; if(ended){endTimer+=dt;return;} Step(dt); }

    // One deterministic world step: the snake moves, junk advances, then contacts are resolved against this step's poses.
    public void Step(float dt)
    {
        elapsed+=dt; ship.Simulate(dt);
        foreach(var j in junk)j.Tick(dt);
        foreach(var p in skillPods)p.Tick(dt);
        for(int i=falling.Count-1;i>=0;i--){ falling[i].Tick(dt); if(falling[i].done){Destroy(falling[i].gameObject);falling.RemoveAt(i);} }
        if(ship.grace>0)ship.grace-=dt;
        for(int i=skillPods.Count-1;i>=0;i--)if((skillPods[i].Position-ship.Position).magnitude<PodRadius){ Take(skillPods[i]); break; }
        for(int i=junk.Count-1;i>=0;i--)
        {
            var j=junk[i]; if(j.shell!=level)continue;
            if(j.shot)
            {
                // A whipped segment: a bullet on a great circle. It clears the first junk it meets and is spent after 8 s.
                bool spent=j.age>8;
                for(int k=junk.Count-1;k>=0&&!spent;k--){ var o=junk[k]; if(o==j||o.shot||o.shell!=level||(o.Position-j.Position).magnitude>ContactRadius)continue; Destroy(o.gameObject); junk.RemoveAt(k); if(k<i)i--; score+=25; Ping(2); spent=true; }
                if(spent){ Destroy(j.gameObject); junk.RemoveAt(i); }
                continue;
            }
            if(j.age<1||(j.Position-ship.Position).magnitude>ContactRadius)continue;
            float angle=Vector3.Angle(ship.tangent,j.Direction);
            if(angle<CatchAngleNow){ Catch(j); junk.RemoveAt(i); }
            else if(ship.grace<=0){ Strike(j); junk.RemoveAt(i); if(ended)return; }
        }
        ship.CheckSelfBite();
        if(toastTimer>0)toastTimer-=dt; ejectPulse=Mathf.Max(0,ejectPulse-dt); strikePulse=Mathf.Max(0,strikePulse-dt); catchPulse=Mathf.Max(0,catchPulse-dt); armourPulse=Mathf.Max(0,armourPulse-dt); pickPulse=Mathf.Max(0,pickPulse-dt);
    }

    void Catch(OrbitJunk j)
    {
        caught++; score+=10+5*level; catchPulse=.4f;
        if(ship.segments.Count<MaxSegments)ship.AddSegment(); Destroy(j.gameObject); Ping(1); ship.tailFlash=.3f;
    }

    // A strike costs the two hindmost segments; armour eats one strike outright; with nothing left to shed the hull goes.
    void Strike(OrbitJunk j)
    {
        Destroy(j.gameObject); strikes++;
        if(armour>0){ armour=0; armourPulse=.7f; ship.grace=StrikeGrace; Ping(2); return; }
        strikePulse=.6f; ship.shake=1; StrikeRing();
        if(ship.segments.Count==0){ End(false,"Struck by "+(j.wreck?"your own wreckage":"debris")+" with nothing left to shed"); return; }
        ship.Shed(2); ship.grace=StrikeGrace; Toast("STRIKE — two segments lost"); Ping(0);
    }

    public void End(bool win,string why)
    {
        if(ended)return; ended=true; won=win; endReason=why; ship.dead=!win; endTimer=0; Toast(why); Ping(win?3:0);
        if(!win)BreakUp();
    }

    // Space: the whole train is thrown down at the planet and burns; the recoil lifts the ship one shell. Score scales with
    // the square of the train so holding on past the quota is worth something, and the tail you were risking is the stake.
    public bool Eject()
    {
        if(!EjectReady)return false;
        int n=ship.segments.Count; lastEjected=n;
        for(int i=0;i<n;i++){ var f=new GameObject("Falling segment").AddComponent<OrbitFalling>(); f.transform.SetParent(world); f.Init(ship.segments[i].position,i*.06f); BuildFallingArt(f); falling.Add(f); }
        ship.Shed(n); score+=EjectValue(n); ejectPulse=1.2f;
        level++; ship.Lift(ShellRadius(level)); if(junk.FindAll(x=>x.shell==level&&!x.shot).Count==0)SeedShell(level);
        Toast("EJECTED "+n+" — burning up below. Climbing to "+ShellNames[level]); Chime(level);
        if(level>=ShellAltitude.Length-1)End(true,"ESCAPE — you cleared the field. Score "+score); else OfferSkills();
        return true;
    }

    float lastTapA=-1,lastTapD=-1;
    void Update()
    {
        if(screenshotPath!=null)
        {
            if(ship.segments.Count==0&&elapsed<.1f){ for(int i=0;i<5;i++)ship.AddSegment(); skills[(int)Skill.Magnet]=true; skills[(int)Skill.Armour]=true; armour=1; OfferSkills(); }
            ship.turn=Mathf.Sin(elapsed*1.5f);
            if(!shotTaken&&elapsed>3){ ScreenCapture.CaptureScreenshot(screenshotPath); shotTaken=true; Debug.Log("[SHOT] HUD capture -> "+screenshotPath); }
            if(shotTaken&&elapsed>4.5f)Application.Quit();
            return;
        }
        var k=Keyboard.current; if(k==null)return;
        if(k.escapeKey.wasPressedThisFrame&&!ended)paused=!paused;
        if(ended&&k.enterKey.wasPressedThisFrame)Restart((int)(Time.realtimeSinceStartup*1000)&0xffff);
        if(paused||ended){ship.turn=0;ship.brake=false;return;}
        float turn=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
        var m=Mouse.current; if(m!=null&&Cursor.lockState==CursorLockMode.Locked)turn+=m.delta.ReadValue().x*.02f;
        ship.turn=Mathf.Clamp(turn,-1,1);
        ship.brake=Has(Skill.Brake)&&(k.sKey.isPressed||k.downArrowKey.isPressed);
        if(Has(Skill.Phase))
        {
            float now=Time.unscaledTime;
            if(k.aKey.wasPressedThisFrame||k.leftArrowKey.wasPressedThisFrame){ if(now-lastTapA<.3f)ship.Dash(-1); lastTapA=now; }
            if(k.dKey.wasPressedThisFrame||k.rightArrowKey.wasPressedThisFrame){ if(now-lastTapD<.3f)ship.Dash(1); lastTapD=now; }
        }
        if(Has(Skill.Whip)&&k.qKey.wasPressedThisFrame)Whip();
        if(k.spaceKey.wasPressedThisFrame)Eject();
    }

    // Q: the last segment leaves the head forward as a bullet on its own great circle.
    public bool Whip()
    {
        if(ended||ship.segments.Count==0)return false;
        float r=ShellRadius(level); var ahead=Quaternion.AngleAxis(6f/r*Mathf.Rad2Deg,Vector3.Cross(ship.normal,ship.tangent));
        var j=SpawnJunkAt(level,ahead*ship.normal,ahead*ship.tangent,Speed*WhipSpeed,false); j.shot=true; j.age=0; BuildShotArt(j);
        ship.RemoveLast(); Ping(2); return true;
    }

    void LateUpdate(){ if(!ship)return; UpdateCamera(Time.deltaTime); TickFx(Time.deltaTime); }
    void OnDestroy(){ foreach(var o in owned)if(o)Destroy(o); if(I==this)I=null; }
}
