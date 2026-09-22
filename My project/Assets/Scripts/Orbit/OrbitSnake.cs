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
    // Contact is forgiving the way Canabalt's hitbox is: a catch reaches CatchRadius (bonus space), a strike needs the
    // piece inside the smaller StrikeRadius, and a strike-type piece whose closest approach falls between the two is a
    // near miss: a few points and a whoosh for the close call instead of a punishment. ContactRadius is the whip shot's.
    public const float CatchAngle=45f, MagnetCatchAngle=70f, CatchRadius=7.5f, StrikeRadius=4.5f, NearMissRadius=10f, ContactRadius=6f, BiteRadius=4.2f, SegmentSpacing=7f, StrikeGrace=1.5f;
    public const int NearMissScore=5;
    public const int MaxSegments=30;
    // Skills, one picked per shell by flying through its icon. Magnet widens the catch cone; Armour eats one strike; Whip
    // fires the last segment forward (Q); Brake halves speed while S is held; Phase is a sideways hop on a double tap;
    // Compound pays 1.5x on ejection.
    public enum Skill{Magnet,Armour,Whip,Brake,Phase,Compound}
    public const int SkillCount=6; public const float PodRadius=8f, WhipSpeed=1.6f, BrakeFactor=.55f, DashDistance=14f, DashTime=.25f;
    public static readonly Color[] SkillColors={new Color(1.3f,.35f,1.2f),new Color(1.2f,1.2f,1.3f),new Color(1.4f,.7f,.2f),new Color(1.3f,1.2f,.3f),new Color(.3f,1.2f,1.4f),new Color(.4f,1.3f,.5f)};

    public bool paused, ended, won, started=true;
    public float elapsed; public int level, score, caught, strikes, nearMisses; public string endReason="";
    // Peril: the train was struck down to nothing. One more strike is the end, so the game says so without a word
    // (heartbeat, red edges, engine sags) and the Kessler clock holds until a catch: the rubber band pulls the other way
    // only when you are already on the floor.
    public bool peril;
    // Kessler clock: after KesslerStart seconds on a shell, one more piece of junk appears every KesslerInterval seconds
    // until the shell holds twice its seed count. Camping the safe shell is what the setting says already went wrong.
    public const float KesslerStart=20f, KesslerInterval=6f; public float shellTime, lastKessler, kesslerPulse; public int kesslerSpawned;
    public float KesslerLoad => Mathf.Clamp01((shellTime-KesslerStart)/60f);
    // Wreckage persists: where a run dies, its train and that shell's loose segments are junk on that shell next run.
    public static bool Persist=true; public const string WreckKey="orbit.wreck"; public const int WreckCap=30;
    public OrbitShip ship;
    public readonly List<OrbitJunk> junk=new();
    public readonly List<OrbitFalling> falling=new();
    public readonly List<SkillPod> skillPods=new();
    public readonly bool[] skills=new bool[SkillCount]; public int armour;
    public readonly List<Object> owned=new();
    public Transform world; public Camera cam;
    public string toast=""; public float toastTimer; public readonly List<string> feed=new();
    public float ejectPulse, strikePulse, catchPulse, armourPulse, pickPulse, endTimer; public int lastEjected;
    // Best score across runs, and the hit-stop clock: Slow() dips Time.timeScale for a few real milliseconds on a contact.
    public int best; public bool newBest; float slowUntil=-1, slowScale=1;
    public void Slow(float scale,float seconds){ slowScale=scale; slowUntil=Time.unscaledTime+seconds; }
    System.Random rng;
    // Built player: -orbit-screenshot=<png> captures the full frame with the IMGUI HUD 3 s in (Camera.Render in the
    // Editor cannot see OnGUI), on a scripted weave with a starter train, then quits.
    // -orbit-screenshot=<png> stages a train and turns for 3 s; -orbit-startshot=<png> captures the untouched start
    // screen at 3 s, the same frame a browser shows before the first key, so platforms can be compared pixel for pixel.
    string screenshotPath, startShotPath; bool shotTaken;
    // Debug switches from the page URL on WebGL (?stage&nopost&nobloom&notone&noclouds&nospec&nohead): the same build can
    // be bisected in a browser without a rebuild. `stage` runs the -orbit-screenshot staging without capturing.
    public readonly System.Collections.Generic.HashSet<string> debug=new System.Collections.Generic.HashSet<string>();
    bool Staged => screenshotPath!=null||debug.Contains("stage");

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
        foreach(var arg in System.Environment.GetCommandLineArgs()){ if(arg.StartsWith("-orbit-screenshot="))screenshotPath=arg.Substring(18); if(arg.StartsWith("-orbit-startshot="))startShotPath=arg.Substring(17); }
        best=PlayerPrefs.GetInt("orbit.best",0);
        { string u=Application.absoluteURL??""; int qi=u.IndexOf('?'); if(qi>=0)foreach(var kv in u.Substring(qi+1).Split('&','#')){ var k=kv.Split('=')[0].Trim(); if(k.Length>0)debug.Add(k); } }
        foreach(var c in FindObjectsByType<Camera>())c.enabled=false;
        foreach(var l in FindObjectsByType<Light>())l.enabled=false;
        foreach(var a in FindObjectsByType<AudioListener>())a.enabled=false;
        world=new GameObject("Orbit / generated").transform; world.SetParent(transform,false);
        BuildArt(); BuildAudio(); BuildCamera();
        Restart(7,true);
    }

    // `wait` holds the world until the first key: the start screen is the frozen shell with the enter glyph over it.
    public void Restart(int seed,bool wait=false)
    {
        rng=new System.Random(seed); started=!wait; shellTime=0; lastKessler=0; kesslerSpawned=0; kesslerPulse=0; newBest=false; slowUntil=-1; Time.timeScale=1; catchChain=0; catchChainTimer=0; nearMisses=0; peril=false;
        // Immediate, not deferred: the checks and the screenshot runner restart several times inside one frame.
        foreach(var j in junk)DestroyImmediate(j.gameObject); junk.Clear();
        foreach(var f in falling)DestroyImmediate(f.gameObject); falling.Clear();
        foreach(var p in skillPods)DestroyImmediate(p.gameObject); skillPods.Clear();
        if(ship)DestroyImmediate(ship.gameObject);
        for(int i=0;i<SkillCount;i++)skills[i]=false; armour=0;
        elapsed=0; level=0; score=0; caught=0; strikes=0; ended=false; won=false; paused=false; endReason=""; feed.Clear(); toast=""; toastTimer=0; ejectPulse=strikePulse=catchPulse=armourPulse=pickPulse=endTimer=0; lastEjected=0;
        ship=new GameObject("Snake").AddComponent<OrbitShip>(); ship.transform.SetParent(world);
        ship.Init(Vector3.up,Vector3.forward,ShellRadius(0)); BuildShipArt(ship);
        SeedShell(0); ResetFx(); ResetHud(); if(Persist)LoadWreckage();
        Toast("LOW ORBIT — A/D to turn. Come up behind junk to catch it; hit it head-on and you lose segments.");
        SnapCamera();
    }

    // Wreckage store: "shell|nx,ny,nz,dx,dy,dz;..." in PlayerPrefs. Saved on death from the loose wreck junk on the
    // current shell plus the train the ship died with; loaded on every restart as gold wreck junk on that shell.
    public void SaveWreckage()
    {
        var sb=new System.Text.StringBuilder(); sb.Append(level).Append('|'); int n=0;
        void Add(Vector3 nrm,Vector3 dir){ if(n>=WreckCap)return; var ic=System.Globalization.CultureInfo.InvariantCulture; sb.Append(nrm.x.ToString("R",ic)).Append(',').Append(nrm.y.ToString("R",ic)).Append(',').Append(nrm.z.ToString("R",ic)).Append(',').Append(dir.x.ToString("R",ic)).Append(',').Append(dir.y.ToString("R",ic)).Append(',').Append(dir.z.ToString("R",ic)).Append(';'); n++; }
        for(int i=0;i<ship.segments.Count;i++){ var nrm=ship.TrailNormal((i+1)*SegmentSpacing,out var d); Add(nrm,d); }
        foreach(var j in junk)if(j.wreck&&j.shell==level)Add(j.Normal,j.Direction);
        PlayerPrefs.SetString(WreckKey,sb.ToString()); PlayerPrefs.Save();
    }
    public int LoadWreckage()
    {
        string s=PlayerPrefs.GetString(WreckKey,""); if(string.IsNullOrEmpty(s))return 0;
        int bar=s.IndexOf('|'); if(bar<0||!int.TryParse(s.Substring(0,bar),out int shell))return 0; int n=0;
        foreach(var item in s.Substring(bar+1).Split(';',System.StringSplitOptions.RemoveEmptyEntries))
        {
            var f=item.Split(','); if(f.Length!=6)continue; var v=new float[6]; bool ok=true; for(int i=0;i<6;i++)ok&=float.TryParse(f[i],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v[i]); if(!ok)continue;
            var j=SpawnJunkAt(shell,new Vector3(v[0],v[1],v[2]).normalized,new Vector3(v[3],v[4],v[5]),Speed*.7f,true); j.age=1; n++;
        }
        return n;
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
        foreach(var p in skillPods)Kill(p.gameObject); skillPods.Clear();
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
        skills[(int)pod.skill]=true; if(pod.skill==Skill.Armour)armour=1; pickPulse=.8f; lastSkill=pod.skill; score+=25; Ping(6); Slow(.2f,.2f);
        Burst(pod.Position,SkillColors[(int)pod.skill],40); Popup(pod.Position,25,SkillColors[(int)pod.skill],26); camShake=Mathf.Max(camShake,.3f); ship.squash=new Vector3(1.25f,1.25f,.85f);
        foreach(var p in skillPods){ if(p!=pod)Burst(p.Position,SkillColors[(int)p.skill]*.5f,8); Kill(p.gameObject); } skillPods.Clear(); Toast("SKILL "+pod.skill);
    }

    public void Toast(string s){toast=s;toastTimer=4f;feed.Add(s);if(feed.Count>6)feed.RemoveAt(0);}
    // Destroy is deferred to the end of the frame; hiding first keeps a spent object out of any capture taken this frame.
    public static void Kill(GameObject g){ if(!g)return; g.SetActive(false); Destroy(g); }

    void FixedUpdate(){ Advance(Time.fixedDeltaTime); }
    // The pause gate. Step() itself is unconditional so the checks can drive it while the game is paused.
    // After the end the world keeps turning around the wreck: junk orbits on, the embers burn down, the pulses fade.
    // (The first version froze everything at the end, so the death fragments hung in the air where the hull had been.)
    public void Advance(float dt){ if(paused||!started)return; if(ended){ endTimer+=dt; foreach(var j in junk)j.Tick(dt); TickFalling(dt); TickPulses(dt); return; } Step(dt); }
    void TickFalling(float dt){ for(int i=falling.Count-1;i>=0;i--){ falling[i].Tick(dt); if(falling[i].done){Kill(falling[i].gameObject);falling.RemoveAt(i);} } }
    void TickPulses(float dt){ if(toastTimer>0)toastTimer-=dt; ejectPulse=Mathf.Max(0,ejectPulse-dt); strikePulse=Mathf.Max(0,strikePulse-dt); catchPulse=Mathf.Max(0,catchPulse-dt); armourPulse=Mathf.Max(0,armourPulse-dt); pickPulse=Mathf.Max(0,pickPulse-dt); kesslerPulse=Mathf.Max(0,kesslerPulse-dt); }

    // One deterministic world step: the snake moves, junk advances, then contacts are resolved against this step's poses.
    public void Step(float dt)
    {
        elapsed+=dt; shellTime+=dt; ship.Simulate(dt);
        if(shellTime>KesslerStart&&shellTime-lastKessler>=KesslerInterval&&!peril&&junk.FindAll(x=>x.shell==level&&!x.shot).Count<JunkCount[Mathf.Clamp(level,0,JunkCount.Length-1)]*2)
        { lastKessler=shellTime; kesslerSpawned++; kesslerPulse=.6f; float speed=Mathf.Lerp(JunkSpeedMin,JunkSpeedMax,(float)rng.NextDouble())*Speed*(kesslerSpawned%2==0?1:-1); var nj=SpawnJunk(level,RandomUnit(),(float)rng.NextDouble()*Mathf.PI*2,speed/ShellRadius(level),false); }
        foreach(var j in junk)j.Tick(dt);
        foreach(var p in skillPods)p.Tick(dt);
        TickFalling(dt);
        if(ship.grace>0)ship.grace-=dt;
        for(int i=skillPods.Count-1;i>=0;i--)if((skillPods[i].Position-ship.Position).magnitude<PodRadius){ Take(skillPods[i]); break; }
        for(int i=junk.Count-1;i>=0;i--)
        {
            var j=junk[i]; if(j.shell!=level)continue;
            if(j.shot)
            {
                // A whipped segment: a bullet on a great circle. It clears the first junk it meets and is spent after 8 s.
                bool spent=j.age>8;
                for(int k=junk.Count-1;k>=0&&!spent;k--){ var o=junk[k]; if(o==j||o.shot||o.shell!=level||(o.Position-j.Position).magnitude>ContactRadius)continue; Kill(o.gameObject); junk.RemoveAt(k); if(k<i)i--; score+=25; Ping(2); Burst(o.Position,new Color(1.3f,1.1f,.4f),30); Popup(o.Position,25,new Color(1,.9f,.4f),24); camShake=Mathf.Max(camShake,.3f); spent=true; }
                if(spent){ Kill(j.gameObject); junk.RemoveAt(i); }
                continue;
            }
            if(j.age<1)continue; float d=(j.Position-ship.Position).magnitude; if(d>NearMissRadius){ j.prevDist=-1; continue; }
            float angle=Vector3.Angle(ship.tangent,j.Direction); bool catchable=angle<CatchAngleNow;
            if(catchable&&d<CatchRadius){ Catch(j); junk.RemoveAt(i); continue; }
            if(!catchable&&d<StrikeRadius){ if(ship.grace<=0){ Strike(j); junk.RemoveAt(i); if(ended)return; } continue; }
            // The closest approach of a strike-type piece fell inside the band without a hit: a near miss, paid once.
            if(!catchable&&ship.grace<=0&&j.prevDist>=0&&d>j.prevDist&&!j.nearMissed){ j.nearMissed=true; NearMiss(j); }
            j.prevDist=d;
        }
        ship.CheckSelfBite();
        TickPulses(dt);
    }

    void Catch(OrbitJunk j)
    {
        caught++; int gain=10+5*level; score+=gain; catchPulse=.4f; catchAge=0; peril=false;
        if(ship.segments.Count<MaxSegments)ship.AddSegment(); Kill(j.gameObject); Ping(1); ship.tailFlash=.3f; Burst(j.Position,new Color(.5f,1.4f,.7f),22); Slow(.3f,.05f);
        Popup(j.Position,gain,new Color(.55f,1,.7f),22); camShake=Mathf.Max(camShake,.12f); ship.squash=new Vector3(.9f,.9f,1.18f);
    }
    void NearMiss(OrbitJunk j)
    {
        nearMisses++; score+=NearMissScore; nearRingT=0; Ping(9); Burst(j.Position,Color.white,10); Popup(j.Position,NearMissScore,new Color(1,1,1,.9f),18); camShake=Mathf.Max(camShake,.22f);
    }

    // A strike costs the two hindmost segments; armour eats one strike outright; with nothing left to shed the hull goes.
    void Strike(OrbitJunk j)
    {
        Kill(j.gameObject); strikes++;
        if(armour>0){ armour=0; armourPulse=.7f; ship.grace=StrikeGrace; Ping(2); camShake=Mathf.Max(camShake,.5f); ship.squash=new Vector3(1.3f,1.3f,.8f); return; }
        strikePulse=.6f; ship.shake=1; StrikeRing(); Burst(j.Position,new Color(1.5f,.35f,.2f),36); Slow(.1f,.12f); camShake=1; ship.squash=new Vector3(1.45f,1.1f,.65f);
        if(ship.segments.Count==0){ End(false,"Struck by "+(j.wreck?"your own wreckage":"debris")+" with nothing left to shed"); return; }
        // The two lost segments fall away burning, so the damage is a thing you watch leave rather than a count going down.
        lostPips=Mathf.Min(2,ship.segments.Count); for(int i=0;i<lostPips;i++){ var f=new GameObject("Shed segment").AddComponent<OrbitFalling>(); f.transform.SetParent(world); f.Init(ship.segments[ship.segments.Count-1-i].position,i*.08f); BuildFallingArt(f); falling.Add(f); }
        ship.Shed(2); ship.grace=StrikeGrace; if(ship.segments.Count==0)peril=true; Toast("STRIKE — two segments lost"); Ping(0);
    }

    public void End(bool win,string why)
    {
        if(ended)return; ended=true; won=win; endReason=why; ship.dead=!win; endTimer=0; Toast(why); Ping(win?3:8);
        if(score>best){ best=score; newBest=true; PlayerPrefs.SetInt("orbit.best",best); PlayerPrefs.Save(); }
        // Losing is a scene, not a cut: nine tenths of a second at quarter speed while the hull comes apart and the camera rings.
        if(!win){ if(Persist)SaveWreckage(); BreakUp(); Slow(.25f,.9f); camShake=1.4f; }
        else { if(Persist){ PlayerPrefs.DeleteKey(WreckKey); PlayerPrefs.Save(); } Confetti(ship.Position+ship.normal*12,260); ejectFlash=.3f; }
    }

    // Space: the whole train is thrown down at the planet and burns; the recoil lifts the ship one shell. Score scales with
    // the square of the train so holding on past the quota is worth something, and the tail you were risking is the stake.
    public bool Eject()
    {
        if(!EjectReady)return false;
        int n=ship.segments.Count; lastEjected=n;
        for(int i=0;i<n;i++){ var f=new GameObject("Falling segment").AddComponent<OrbitFalling>(); f.transform.SetParent(world); f.Init(ship.segments[i].position,i*.06f); BuildFallingArt(f); falling.Add(f); }
        int gain=EjectValue(n); ship.Shed(n); score+=gain; ejectPulse=1.2f; Slow(.3f,.45f); Ping(5); peril=false;
        // Recoil: the hull squashes back as the train drops, the camera kicks and punches in, and the value flies off the head.
        ejectFlash=.22f; camShake=.7f; fovKick=-9; ship.squash=new Vector3(.7f,.7f,1.55f); Popup(ship.Position,gain,new Color(1,.85f,.3f),40); Burst(ship.Position-ship.normal*2,new Color(1.5f,.62f,.12f),50);
        level++; shellTime=0; lastKessler=0; kesslerSpawned=0; ship.Lift(ShellRadius(level)); if(junk.FindAll(x=>x.shell==level&&!x.shot&&!x.wreck).Count==0)SeedShell(level);
        Toast("EJECTED "+n+" — burning up below. Climbing to "+ShellNames[level]); Chime(level);
        if(level>=ShellAltitude.Length-1)End(true,"ESCAPE — you cleared the field. Score "+score); else OfferSkills();
        return true;
    }

    float lastTapA=-1,lastTapD=-1;
    void Update()
    {
        Time.timeScale=Time.unscaledTime<slowUntil&&!paused?slowScale:1; TickAudio(Time.unscaledDeltaTime); TickHud(Time.unscaledDeltaTime);
        if(startShotPath!=null)
        {
            if(!shotTaken&&Time.realtimeSinceStartup>3){ ScreenCapture.CaptureScreenshot(startShotPath); shotTaken=true; Debug.Log("[SHOT] start capture -> "+startShotPath); }
            if(shotTaken&&Time.realtimeSinceStartup>4.5f)Application.Quit();
            return;
        }
        if(Staged)
        {
            started=true; slowUntil=-1;
            if(ship.segments.Count==0&&elapsed<.1f){ for(int i=0;i<5;i++)ship.AddSegment(); skills[(int)Skill.Magnet]=true; skills[(int)Skill.Armour]=true; armour=1; OfferSkills(); }
            ship.turn=elapsed<3?Mathf.Sin(elapsed*1.5f):0; // after the capture moment the staged ship flies straight, so a browser frame stays comparable
            if(screenshotPath!=null&&!shotTaken&&elapsed>3){ ScreenCapture.CaptureScreenshot(screenshotPath); shotTaken=true; Debug.Log("[SHOT] HUD capture -> "+screenshotPath); }
            if(screenshotPath!=null&&shotTaken&&elapsed>4.5f)Application.Quit();
            return;
        }
        var k=Keyboard.current; var gp=Gamepad.current; if(k==null&&gp==null)return;
        bool Key(System.Func<Keyboard,bool> f) => k!=null&&f(k); bool Pad(System.Func<Gamepad,bool> f) => gp!=null&&f(gp);
        if(!started){ if((Key(x=>x.anyKey.wasPressedThisFrame&&!x.escapeKey.wasPressedThisFrame))||Pad(x=>x.buttonSouth.wasPressedThisFrame||x.startButton.wasPressedThisFrame)){ started=true; Ping(7); Launch(); } return; }
        if((Key(x=>x.escapeKey.wasPressedThisFrame)||Pad(x=>x.startButton.wasPressedThisFrame))&&!ended)paused=!paused;
        if(ended&&endTimer>.6f&&(Key(x=>x.enterKey.wasPressedThisFrame||x.spaceKey.wasPressedThisFrame)||Pad(x=>x.buttonSouth.wasPressedThisFrame||x.startButton.wasPressedThisFrame)))Restart((int)(Time.realtimeSinceStartup*1000)&0xffff,true);
        if(paused||ended){ship.turn=0;ship.brake=false;return;}
        float turn=Key(x=>x.dKey.isPressed||x.rightArrowKey.isPressed)?1:0; turn-=Key(x=>x.aKey.isPressed||x.leftArrowKey.isPressed)?1:0;
        if(gp!=null){ float sx=gp.leftStick.ReadValue().x; if(Mathf.Abs(sx)>.15f)turn+=sx; }
        var m=Mouse.current; if(m!=null&&Cursor.lockState==CursorLockMode.Locked)turn+=m.delta.ReadValue().x*.02f;
        ship.turn=Mathf.Clamp(turn,-1,1);
        ship.brake=Has(Skill.Brake)&&(Key(x=>x.sKey.isPressed||x.downArrowKey.isPressed)||Pad(x=>x.leftTrigger.isPressed));
        if(Has(Skill.Phase))
        {
            float now=Time.unscaledTime;
            if(Key(x=>x.aKey.wasPressedThisFrame||x.leftArrowKey.wasPressedThisFrame)){ if(now-lastTapA<.3f)ship.Dash(-1); lastTapA=now; }
            if(Key(x=>x.dKey.wasPressedThisFrame||x.rightArrowKey.wasPressedThisFrame)){ if(now-lastTapD<.3f)ship.Dash(1); lastTapD=now; }
            if(Pad(x=>x.leftShoulder.wasPressedThisFrame))ship.Dash(-1); if(Pad(x=>x.rightShoulder.wasPressedThisFrame))ship.Dash(1);
        }
        if(Has(Skill.Whip)&&(Key(x=>x.qKey.wasPressedThisFrame)||Pad(x=>x.buttonWest.wasPressedThisFrame)))Whip();
        if(Key(x=>x.spaceKey.wasPressedThisFrame)||Pad(x=>x.buttonSouth.wasPressedThisFrame))Eject();
    }

    // Q: the last segment leaves the head forward as a bullet on its own great circle.
    public bool Whip()
    {
        if(ended||ship.segments.Count==0)return false;
        float r=ShellRadius(level); var ahead=Quaternion.AngleAxis(6f/r*Mathf.Rad2Deg,Vector3.Cross(ship.normal,ship.tangent));
        var j=SpawnJunkAt(level,ahead*ship.normal,ahead*ship.tangent,Speed*WhipSpeed,false); j.shot=true; j.age=0; BuildShotArt(j);
        ship.RemoveLast(); Ping(2); ship.squash=new Vector3(.85f,.85f,1.3f); camShake=Mathf.Max(camShake,.2f); return true;
    }
    // The first key: exhaust kicked up behind the hull, a short camera kick and a wide-to-normal lens settle, so the
    // start is a launch and not a fade.
    void Launch(){ Burst(ship.Position-ship.tangent*3,new Color(.35f,.9f,1.3f),40); camShake=.35f; fovKick=8; ship.squash=new Vector3(.8f,.8f,1.4f); }

    void LateUpdate(){ if(!ship)return; UpdateCamera(Time.deltaTime); TickFx(Time.deltaTime); }
    void OnDestroy(){ foreach(var o in owned)if(o)Destroy(o); if(I==this)I=null; }
}
