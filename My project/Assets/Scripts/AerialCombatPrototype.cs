using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Small-planet flight arena: distances/time are deliberately compressed for a class prototype.
public partial class AerialCombatPrototype : MonoBehaviour
{
    public static AerialCombatPrototype I;
    public const float PlanetRadius = 1200f;
    public static readonly Vector3 PlanetCenter = new Vector3(0,-PlanetRadius,0);
    public readonly List<ArenaPilot> pilots = new List<ArenaPilot>();
    public readonly List<SalvageCore> cores = new List<SalvageCore>();
    public readonly List<SalvageShard> shards = new List<SalvageShard>();
    public readonly List<CaptureGate> gates = new List<CaptureGate>();
    // Live projectiles, registered by the bolt itself: the missile warning has to scan them every fixed step.
    public readonly List<ArenaBolt> bolts = new List<ArenaBolt>();
    public ArenaPilot player;
    // Whoever is on a three-kill run. The whole field weights its target scoring toward this id, so the leader
    // gets hunted without any explicit difficulty dial, and a trailing pilot gets a way back in by taking the mark.
    public int aceId=-1;
    // Vendetta: whoever last shot the player down is marked for VendettaWindow seconds; taking them back inside the
    // window banks their spilled cargo again as a flat bonus (at least VendettaFloor). One personal fight per death.
    public int vendettaId=-1; public float vendettaTimer;
    public const float VendettaWindow=60; public const int VendettaFloor=20;
    // Onboarding: one objective line that walks a new pilot through the loop once (shoot, scoop, ring, hold), then
    // never again on this machine. Advanced on the match clock from the flags the game already raises.
    public int tutorialStep; public float tutorialPop; public bool playerHitWreck, playerBanked;
    public static readonly string[] Objectives={"SHOOT A WRECK'S REACTOR","FLY INTO THE SPILLED SALVAGE","CARRY IT TO A REFINERY RING","HOLD INSIDE THE RING","LOOP CLOSED   /   NOW GO HUNTING"};
    // Results-screen awards, computed once when the match ends.
    public readonly List<string> awards=new List<string>();
    // Standalone player only: -rift-screenshot=<png> captures the full frame with the HUD 4.5 s in, then quits a second later.
    string screenshotPath; float quitAt;
    public const int AceStreak=3;
    public Camera cam;
    public UniversalAdditionalCameraData camData;
    public bool paused;
    // shake is trauma: callers only ever add to it, decay is linear, and the camera uses its square.
    public float elapsed, hitFlash, damageFlash, shake, hitStop, bannerTimer, lastAttackAge, bountyFresh;
    public string bannerText="";
    public Vector3 lastAttackDirection;
    public Transform world;
    // Readability landmarks, all generated: the camera-locked sky dome, the cloud cluster root, the surface scatter root.
    public Transform sky, cloudRoot, scatterRoot;
    public Material starMaterial;
    public ParticleSystem windStreaks;
    readonly List<Object> owned = new List<Object>();
    Material alloy, dark, teal, red, gold, white, violet, slate, pillarMaterial;
    // Cold alloy shrapnel for a terrain kill, kept as its own instance so the debris variant is testable by reference.
    public Material crashDebris;
    AudioSource audioSource;
    // Two engine loops cross-faded by throttle/boost instead of one clip dragged around by pitch, plus the wind bed on
    // its own source. Public because TickAudio's mix is a verified thing, and the harness reads these volumes directly.
    public AudioSource idleSource, burnerSource, windSource;
    // Four round-robin voices for the scheduled note queue: PlayOneShot takes its pitch from the source, so a three-note
    // chime played through one source would bend the notes already in the air.
    AudioSource[] noteVoices; int noteVoice;
    AudioClip gunSound, hitSound, boomSound, collectSound, missileTone, overheatHiss, crashThud, lockTone, lockConfirm;
    AudioClip chimeNote, bankNote, countPip, stingNote;
    Quaternion cameraRotation;
    Vector3 sunDirection=Vector3.up;
    // 14 m instead of 20: the aircraft has to own a sixth of the frame width, or nothing in the world has a readable scale.
    float cameraDistance=14;
    const float CameraLift=3.5f;
    public int spawnCounter;
    Vector2 mouseStick;
    const float MouseFlightSensitivity=.42f;
    // Mouse-x is a bank input, and a bank with no rudder is an uncoordinated slide: a quarter of the raw stick goes to yaw.
    public const float YawCoupling=.25f;
    // Wings-level assist, player only. 15 deg/s is a fifth of a full-stick roll (85 deg/s), so it reads as the airframe
    // settling rather than the aircraft flying itself, and the authority clamp keeps a deliberate input on top of it.
    public const float AutoLevelRate=15f,AutoLevelAuthority=.35f,AutoLevelGain=1.2f,AutoLevelLimit=100f;
    // A static field, not a const: the verification harness flips it off to prove the levelling is what closes the bank.
    public static bool AutoLevelEnabled=true;
    // Cannon dispersion is the heat's second cost after the .92 lockout: 0 cold, 2.02 degrees at the gate.
    public const float CannonSpread=2.2f;
    // Seeker lock: an 18 degree cone held for 1.2 s inside 650 m, which is the same envelope AimTarget already reports.
    public const float LockCone=18f,LockRange=650f;
    // Redeploy corridors, lat/lon at 170 m altitude, 509-669 m apart, plus the clearance a corridor has to have.
    public static readonly Vector2[] PlayerLanes={new Vector2(0,-14),new Vector2(0,14),new Vector2(16,0)};
    public const float SafeSpawnRange=300f;
    public static float Altitude(Vector3 p) => Vector3.Distance(p,PlanetCenter)-PlanetRadius;
    public static Vector3 Up(Vector3 p) => (p-PlanetCenter).normalized;
    public static float Density(float altitude) => Mathf.Exp(-Mathf.Max(0,altitude)/280f);
    public static float Gravity(Vector3 p) => 12f*Mathf.Pow(PlanetRadius/Vector3.Distance(p,PlanetCenter),2);
    public static Vector3 SurfacePoint(float latitude,float longitude,float altitude)
    {
        return PlanetCenter + Quaternion.Euler(latitude,0,longitude)*Vector3.up*(PlanetRadius+altitude);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(FindAnyObjectByType<AerialCombatPrototype>()==null)
            new GameObject("RIFT / Planetary arena").AddComponent<AerialCombatPrototype>();
    }

    void Awake()
    {
        if(I && I!=this) { Destroy(gameObject); return; }
        I=this;
        Application.runInBackground=true;
        LoadComfort();
        tutorialStep=PlayerPrefs.GetInt("rift.loopClosed",0)>0?4:0;
        foreach(var arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("-rift-screenshot="))screenshotPath=arg.Substring(17);
        foreach(var c in FindObjectsByType<Camera>()) c.enabled=false;
        foreach(var a in FindObjectsByType<AudioListener>()) a.enabled=false;
        foreach(var l in FindObjectsByType<Light>()) l.enabled=false;
        world=new GameObject("Arena / generated assets").transform; world.SetParent(transform,false);
        alloy=Material(new Color(.62f,.69f,.76f),false); dark=Material(new Color(.025f,.043f,.07f),false);
        // Emissive values sit just over the volume's bloom threshold of 1: bright enough to bleed, low enough not to clip to white.
        teal=Material(new Color(.09f,1.14f,1.44f),true); red=Material(new Color(1.5f,.16f,.07f),true);
        gold=Material(new Color(1.45f,.84f,.14f),true); white=Material(new Color(.72f,.86f,1.06f),true);
        violet=Material(new Color(.55f,.24f,1.32f),true);
        // Armour belts read as mass: darker and duller than the hull alloy so a reinforced wreck is obvious before the first shot.
        slate=Material(new Color(.17f,.19f,.23f),false);
        // Torn skin, not burning fuel: a crash sheds pale cold alloy where a shoot-down sheds hot gold.
        crashDebris=Material(new Color(.86f,.92f,1.02f),true);
        RenderSettings.skybox=null; RenderSettings.fog=false;
        // Trilight gives the hull shading a direction (cold sky above, warm ground bounce below) instead of flat fill.
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.34f,.5f,.72f);
        RenderSettings.ambientEquatorColor=new Color(.13f,.19f,.34f);
        RenderSettings.ambientGroundColor=new Color(.36f,.25f,.17f);
        RenderSettings.ambientIntensity=1;
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world);
        sun.type=LightType.Directional; sun.intensity=1.8f; sun.color=new Color(1,.88f,.74f);
        // 20 degrees of elevation, 32 degrees off the launch heading: low enough to be inside the 66 degree frame from the
        // chase camera, high enough to key the top surfaces the camera actually sees. Sky and stars read the same vector.
        sun.transform.rotation=Quaternion.Euler(20,-148,0);
        sunDirection=-sun.transform.forward;
        cam=new GameObject("Arena chase camera").AddComponent<Camera>(); cam.transform.SetParent(world);
        cam.tag="MainCamera"; cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.012f,.025f,.055f); cam.fieldOfView=66; cam.farClipPlane=14000; cam.nearClipPlane=.2f;
        // URP adds this data component with the Camera; cache it once instead of re-fetching, and opt the runtime camera into the scene's Global Volume.
        camData=cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing=true;
        camData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality=AntialiasingQuality.High;
        cam.gameObject.AddComponent<AudioListener>();
        audioSource=cam.gameObject.AddComponent<AudioSource>(); audioSource.volume=.6f;
        gunSound=Sound("Cannon",.12f,460,80,.3f);
        hitSound=Sound("Armor impact",.16f,220,55,.35f);
        boomSound=Sound("Ship breakup",.7f,75,18,.6f);
        collectSound=Sound("Salvage acquired",.2f,500,1000,.03f);
        // A short falling pip that cannot be mistaken for the cannon, and a wide hiss for the weapon bay venting.
        missileTone=Sound("Seeker lock pip",.08f,1400,1100,.05f);
        overheatHiss=Sound("Weapon overheat",.4f,3000,400,.75f);
        // Ground, not fireball: a long dull thud well below the breakup boom, so a terrain kill is audible as a different event.
        crashThud=Sound("Terrain impact",.6f,42,18,.5f);
        // The lock climbs (900->1500) and the confirmation sits on top of it, so the ear hears the acquisition finish.
        lockTone=Sound("Seeker lock climb",.09f,900,1500,.04f);
        lockConfirm=Sound("Seeker locked",.22f,1500,1900,.03f);
        // Melody stock. One clip per event, played back at rising pitches by the note queue, so a chime is a schedule
        // rather than four more buffers: a claim is 780 Hz at 1 / 1.25 / 1.5, a deposit is the same idea an octave down.
        chimeNote=Sound("Refinery chime",.24f,780,900,.02f);
        bankNote=Sound("Deposit note",.2f,560,700,.02f);
        countPip=Sound("Countdown pip",.14f,520,500,.02f);
        stingNote=Sound("Results sting",.5f,300,430,.03f);
        // 55 Hz of idle with a little grit, 140 Hz of burner with a lot of it: cross-faded, the throttle is audible as a
        // change of character and not just of loudness, which one pitch-shifted clip can never do.
        idleSource=cam.gameObject.AddComponent<AudioSource>(); idleSource.loop=true;
        idleSource.clip=Loop("Engine idle",1,55,.15f); idleSource.volume=.06f; idleSource.Play();
        burnerSource=cam.gameObject.AddComponent<AudioSource>(); burnerSource.loop=true;
        burnerSource.clip=Loop("Engine burner",1,140,.3f); burnerSource.volume=0; burnerSource.Play();
        // Broadband, no tone at all: the wind is the air itself, so it is gated on the same speed x density product the
        // wind streaks are and disappears in vacuum at exactly the same moment they do.
        windSource=cam.gameObject.AddComponent<AudioSource>(); windSource.loop=true;
        windSource.clip=Loop("Slipstream",1.5f,0,1,true); windSource.volume=0; windSource.Play();
        noteVoices=new AudioSource[4];
        for(int i=0;i<noteVoices.Length;i++){noteVoices[i]=cam.gameObject.AddComponent<AudioSource>();noteVoices[i].playOnAwake=false;}
        BuildPlanet();
        BuildSites();
        // Weather and surface scatter need the refinery positions to keep clear of them, so they build after the sites.
        BuildClouds();
        BuildScatter();
        BuildWindStreaks();
        string[] names={"YOU","Moth.exe","Blue Finch","Periapsis","DustRunner","Kite-09","SoupDragon","Last Comet"};
        for(int i=0;i<names.Length;i++)
        {
            var p=new GameObject(names[i]).AddComponent<ArenaPilot>(); p.transform.SetParent(world);
            p.id=i; p.callsign=names[i]; p.isPlayer=i==0;
            // Visual-only 1.35x: the hit radii (4 m pilot, 9 m wreck) and the hull vertex data stay exactly where they were.
            p.art=BuildShip(p.transform,i!=0); p.art.localScale=Vector3.one*1.35f;
            pilots.Add(p); if(i==0) player=p;
            Spawn(p,true);
        }
        SnapCamera();
        Cursor.lockState=CursorLockMode.Confined; Cursor.visible=false;
    }

    void BuildSites()
    {
        // First salvage field is directly ahead at launch; more sites encircle the planet.
        // Two kinds of site, alternating. Surface fields (even index, 170 m) are wide and cheap: five wrecks at 8 a piece
        // in thick air you can actually turn in. Low-orbit fields (odd index, 460 m) are the opposite trade — three
        // wrecks at 24, above the coast line where pickups already double, in air too thin to fight well in. The totals
        // stay at 24 wrecks, so the arena did not get bigger, it got lopsided, which is the whole point.
        // 32 degrees apart (670 m along the surface) instead of spread around the whole planet: neighbouring fields sit
        // at the edge of each other's horizon, so pilots meet. Balance runs on the old ring of six had 77% of every
        // match spent in transit and two of the seven rivals never sighting another aircraft.
        float[] lat={18,50,82,114,146,178};
        for(int s=0;s<lat.Length;s++)
        {
            bool orbit=s%2==1; float lon=orbit?32:0; int wrecks=orbit?3:5;
            var gate=new GameObject("Capture refinery "+(s+1)).AddComponent<CaptureGate>(); gate.transform.SetParent(world);
            gate.index=s; gate.lowOrbit=orbit; gate.transform.position=SurfacePoint(lat[s]+9,lon,170+(orbit?290:0));
            gate.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward,Up(gate.transform.position)).normalized,Up(gate.transform.position));
            BuildGate(gate); gates.Add(gate);
            for(int c=0;c<wrecks;c++)
            {
                var core=new GameObject("Breakable salvage core").AddComponent<SalvageCore>(); core.transform.SetParent(world);
                // Longitude spread is centred on the field whatever its size, so a three-wreck field is not lopsided.
                core.transform.position=SurfacePoint(lat[s]+c*2,lon+(c-(wrecks-1)*.5f)*3,155+(orbit?300:0)+c*8);
                // One unstable reactor per field is a weapon lying on the table; armour only ever appears on a surface
                // field, where there is room to run a seeker pass, and only on two of the three so it stays a choice.
                core.kind=c==2?CoreKind.Volatile:(!orbit && c==3 && s%4==0?CoreKind.Armored:CoreKind.Normal);
                core.maxHealth=core.kind==CoreKind.Armored?200:65; core.health=core.maxHealth;
                core.site=s; core.value=orbit?24:8; BuildCore(core); cores.Add(core);
                if(c==0) for(int n=0;n<5;n++) SpawnShard(core.transform.position+new Vector3(n*7-14,4,-20),core.value);
            }
        }
    }

    public void Spawn(ArenaPilot p,bool initial=false)
    {
        int site=p.isPlayer?0:(p.id-1)%gates.Count;
        float siteLat=new[]{18f,50,82,114,146,178}[site]-8,siteLon=site%2==0?0:32,siteAltitude=site%2==0?165:450;
        // Spawn on the same great-circle route as local resources, heading toward the nearest field.
        Vector3 pos=p.isPlayer?new Vector3(0,165,0):SurfacePoint(siteLat,siteLon,siteAltitude);
        if(!initial && p.isPlayer)
        {
            // Redeploying inside gun range of whoever just shot you is not a respawn. Three fixed corridors are tried in
            // rotating order and the first with 300 m of clearance wins; if the whole board is crowded, take the roomiest.
            pos=SurfacePoint(PlayerLanes[spawnCounter%PlayerLanes.Length].x,PlayerLanes[spawnCounter%PlayerLanes.Length].y,170);
            float widest=-1;
            for(int i=0;i<PlayerLanes.Length;i++)
            {
                Vector2 lane=PlayerLanes[(spawnCounter+i)%PlayerLanes.Length];
                Vector3 at=SurfacePoint(lane.x,lane.y,170);
                float gap=NearestRivalDistance(p,at);
                if(gap>=SafeSpawnRange){pos=at;break;}
                if(gap>widest){widest=gap;pos=at;}
            }
            spawnCounter++;
        }
        // The AI keeps its own site — a rival that abandoned its patrol on death would unstick the whole map — and buys
        // the same 300 m of clearance by walking the longitude out in +-10 degree steps instead.
        else if(!initial)
            for(int nudge=1;nudge<=4 && NearestRivalDistance(p,pos)<SafeSpawnRange;nudge++)
                pos=SurfacePoint(siteLat,siteLon+(nudge%2==1?10:-10)*Mathf.CeilToInt(nudge*.5f),siteAltitude);
        p.transform.position=pos;
        SalvageCore nearest=NearestCore(pos);
        Vector3 direction=nearest ? nearest.transform.position-pos : Vector3.forward;
        p.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Up(pos)).normalized,Up(pos));
        p.velocity=p.transform.forward*82; p.health=100; p.cargo=0; p.respawn=0; p.invulnerable=3;
        p.throttle=.72f; p.heat=0; p.hullHeat=0; p.fuel=1; p.ResetFlight(); p.art.gameObject.SetActive(true); p.ClearTrails();
        if(p==player) { SnapCamera(); damageFlash=0; lastAttackAge=0; CenterStick(); }
    }

    public void TutorialTick(float dt)
    {
        tutorialPop=Mathf.Max(0,tutorialPop-dt);
        if(tutorialStep>=4 || !player)return;
        int before=tutorialStep;
        if(tutorialStep==0 && playerHitWreck)tutorialStep=1;
        if(tutorialStep==1 && player.cargo>0)tutorialStep=2;
        if(tutorialStep==2 && player.cargo>0)foreach(var gate in gates)if(gate.claimant==player.id && gate.progress>0)tutorialStep=3;
        // A pilot who already knows the game skips straight to the end: any bank closes the loop from any step.
        if(playerBanked)tutorialStep=4;
        if(tutorialStep!=before){tutorialPop=tutorialStep==4?4f:.5f;if(tutorialStep==4)PlayerPrefs.SetInt("rift.loopClosed",1);}
    }
    // Four lines under the standings: who fought best, who deposited most in one run, who kept their aircraft, who aimed.
    public void ComputeAwards()
    {
        awards.Clear();
        ArenaPilot topGun=null,banker=null,ironclad=null,marksman=null;
        foreach(var p in pilots)
        {
            if(topGun==null || p.kills>topGun.kills)topGun=p;
            if(banker==null || p.biggestBank>banker.biggestBank)banker=p;
            if(ironclad==null || p.deaths<ironclad.deaths)ironclad=p;
            if(p.combatShotsFired>=20 && (marksman==null || (float)p.hitsLanded/p.combatShotsFired>(float)marksman.hitsLanded/marksman.combatShotsFired))marksman=p;
        }
        if(topGun && topGun.kills>0)awards.Add("TOP GUN   "+topGun.callsign.ToUpper()+"   "+topGun.kills+" KILLS");
        if(banker && banker.biggestBank>0)awards.Add("BIG DEPOSIT   "+banker.callsign.ToUpper()+"   +"+banker.biggestBank);
        if(ironclad)awards.Add("IRONCLAD   "+ironclad.callsign.ToUpper()+"   "+ironclad.deaths+" LOST");
        if(marksman)awards.Add("MARKSMAN   "+marksman.callsign.ToUpper()+"   "+Mathf.RoundToInt(100f*marksman.hitsLanded/marksman.combatShotsFired)+"%");
    }

    // Trauma accumulates instead of overwriting, so a burst of small hits still reads as one big jolt.
    // Scaled by the comfort setting at the single point every caller goes through: 0% removes camera shake outright.
    public void Trauma(float amount) { shake=Mathf.Min(1,shake+amount*shakeScale); }
    public void Banner(string text) { bannerText=text; bannerTimer=1.5f; }

    // Single dispatch point for the three feedback tiers: every event of the same weight gets the same layered package.
    // `crash` is the death variant, not a new tier: same weight, cold alloy shrapnel and a ground thud instead of gold and a boom.
    public void Feedback(string tier,Vector3 pos,ArenaPilot involved=null,AudioClip voice=null,bool crash=false)
    {
        bool mine=involved && involved==player;
        float proximity=player?1-Mathf.Clamp01(Vector3.Distance(pos,player.transform.position)/450):0;
        if(tier=="large")
        {
            Burst(pos,28,3,true,crash);
            AudioClip heavy=voice?voice:crash?crashThud:boomSound;
            if(mine)
            {
                Trauma(.8f); hitFlash=Mathf.Max(hitFlash,.06f);
                // Hit-stop is player-only: AI trading kills across the map must never stutter the frame.
                hitStop=.08f; audioSource.PlayOneShot(heavy,.7f);
            }
            else
            {
                Trauma(proximity*.35f);
                if(proximity>0) audioSource.PlayOneShot(heavy,proximity*.7f);
            }
        }
        else if(tier=="medium")
        {
            // Sparks only spawn inside audible range; off-screen AI trades must not budget particles.
            if(proximity>0) Burst(pos,7,1.2f,false);
            if(mine) { Trauma(.35f); hitFlash=Mathf.Max(hitFlash,.16f); audioSource.PlayOneShot(voice?voice:hitSound,.35f); }
            else if(proximity>0) audioSource.PlayOneShot(voice?voice:hitSound,proximity*.18f);
        }
        else
        {
            if(proximity>0) Burst(pos,4,.8f,false);
            if(mine) { Trauma(.12f); hitFlash=Mathf.Max(hitFlash,.12f); audioSource.PlayOneShot(voice?voice:hitSound,.2f); }
        }
    }

    public void Kill(ArenaPilot victim,ArenaPilot attacker)
    {
        if(!victim.Alive) return;
        int spoils=victim.cargo,cargo=spoils; victim.cargo=0; victim.health=0; victim.respawn=3;
        victim.art.gameObject.SetActive(false); victim.deaths++;
        // Dying always surrenders the run, so the bounty is cleared before the killer's own streak is counted.
        victim.streak=0; if(aceId==victim.id) aceId=-1;
        // Every trade on the board earns a feed row; only the two the player is in get a colour.
        if(victim==player) Toast("KILLED BY  "+(attacker && attacker!=victim?attacker.callsign.ToUpper():"THE PLANET"),new Color(1,.3f,.22f));
        else if(attacker==player) Toast("YOU  ✕  "+victim.callsign.ToUpper(),new Color(.16f,1,.85f));
        // The mark moves to the newest killer; settling it pays the spoils a second time straight into banked score.
        if(victim==player && attacker && attacker!=victim){vendettaId=attacker.id;vendettaTimer=VendettaWindow;Toast("VENDETTA  "+attacker.callsign.ToUpper(),new Color(1,.42f,.3f));}
        else if(victim.id==vendettaId)
        {
            if(attacker==player){int bonus=Mathf.Max(VendettaFloor,spoils);player.score+=bonus;bankPop=.18f;Toast("VENDETTA SETTLED  +"+bonus,new Color(1,.6f,.3f));}
            vendettaId=-1;vendettaTimer=0;
        }
        else if(attacker && attacker!=victim) Toast(attacker.callsign.ToUpper()+"  ✕  "+victim.callsign.ToUpper(),new Color(.72f,.84f,.92f));
        else Toast(victim.callsign.ToUpper()+"  DOWN",new Color(.72f,.84f,.92f));
        if(attacker && attacker!=victim)
        {
            attacker.kills++; attacker.streak++;
            // The crowning gets its own HUD entrance rather than a banner: the kill banner below is the louder, more urgent read.
            if(attacker.streak>=AceStreak && aceId!=attacker.id){aceId=attacker.id;bountyFresh=1.1f;Toast("ACE  "+attacker.callsign.ToUpper(),new Color(1,.8f,.28f));}
        }
        int pieces=Mathf.Min(16,Mathf.CeilToInt(cargo/8f));
        for(int i=0;i<pieces;i++)
        {
            int value=cargo/(pieces-i); cargo-=value;
            SpawnShard(victim.transform.position+Random.insideUnitSphere*10,value);
        }
        bool mine=victim==player || attacker==player;
        // No attacker means the planet took it: terrain impact, burn-through or a NaN state, all of them a crash.
        Feedback("large",victim.transform.position,mine?player:null,null,!attacker || attacker==victim);
        if(victim==player)
        {
            damageFlash=.5f;
            if(attacker && attacker!=victim) { RecordIncoming(attacker.transform.position); Banner("SPLASHED BY  "+attacker.callsign); }
            else Banner(victim.hullHeat>1?"HULL BURNED THROUGH":"TERRAIN IMPACT");
        }
        else if(attacker==player) Banner("SPLASHED  "+victim.callsign+"   +"+spoils+" SALVAGE");
    }

    // Stored as a world bearing; the HUD converts it into camera space every frame so the wedge tracks while the ship rolls.
    public void RecordIncoming(Vector3 source)
    {
        Vector3 d=source-player.transform.position;
        if(d.sqrMagnitude<.01f) return;
        lastAttackDirection=d.normalized; lastAttackAge=1;
    }

    public void Damage(ArenaPilot p,float damage,ArenaPilot attacker,bool precise=false)
    {
        if(!p.Alive || p.invulnerable>0 || paused || !MatchActive) return;
        if(attacker && attacker!=p){p.NotifyAttacked(attacker);attacker.hitsLanded++;}
        // Fired before the lethality test so the killing shot still confirms on the crosshair.
        if(attacker==player && p!=player) Hitmarker(precise);
        if(p.health<=damage) { Kill(p,attacker); return; }
        p.health-=damage;
        if(p==player) { damageFlash=.25f; if(attacker && attacker!=p) RecordIncoming(attacker.transform.position); }
        Feedback("medium",p.transform.position,(p==player || attacker==player)?player:null);
    }

    public SalvageCore NearestCore(Vector3 position)
    {
        SalvageCore best=null; float distance=float.MaxValue;
        foreach(var c in cores) if(c.Available) { float d=(c.transform.position-position).sqrMagnitude; if(d<distance){distance=d;best=c;} }
        return best;
    }
    // Distance from a prospective spawn to the nearest pilot who could shoot at it. float.MaxValue when the board is empty.
    public float NearestRivalDistance(ArenaPilot p,Vector3 at)
    {
        float best=float.MaxValue;
        foreach(var other in pilots) if(other!=p && other.Alive) best=Mathf.Min(best,Vector3.Distance(other.transform.position,at));
        return best;
    }
    public CaptureGate NearestGate(Vector3 position)
    {
        CaptureGate best=null; float distance=float.MaxValue;
        foreach(var c in gates) { float d=(c.transform.position-position).sqrMagnitude; if(d<distance){distance=d;best=c;} }
        return best;
    }
    public bool VisibleBetween(Vector3 a,Vector3 b)
    {
        Vector3 d=b-a;
        float t=Mathf.Clamp01(Vector3.Dot(PlanetCenter-a,d)/Mathf.Max(.01f,d.sqrMagnitude));
        return Vector3.Distance(a+d*t,PlanetCenter)>PlanetRadius+2;
    }

    public void SpawnShard(Vector3 position,int value)
    {
        if(value<=0) return;
        var g=Shape("Salvage / fly through",world,PrimitiveType.Cube,position,Vector3.one*2.7f,gold);
        g.transform.rotation=Random.rotation;
        var shard=g.AddComponent<SalvageShard>(); shard.value=value; shards.Add(shard);
        var trail=g.AddComponent<TrailRenderer>(); trail.sharedMaterial=gold; trail.time=.35f; trail.startWidth=.4f; trail.endWidth=0;
    }

    public void Collect(ArenaPilot p,SalvageShard s)
    {
        if(!p.Alive || !s || s.claimed || paused || !MatchActive) return;
        Vector3 at=s.transform.position;
        // Salvage taken above the coast line is worth double, judged on the collector's own altitude rather than the
        // shard's: shards drift to whoever is nearest, so the only honest question is who has to fly it back down.
        s.claimed=true; p.cargo+=p.Altitude>550?s.value*2:s.value; shards.Remove(s); Destroy(s.gameObject);
        if(p==player) cargoPop=.18f;
        Feedback("small",at,p,collectSound);
    }

    public Transform AimTarget(ArenaPilot p,float angle,out Vector3 targetVelocity)
    {
        Transform result=null; float best=angle; targetVelocity=Vector3.zero;
        foreach(var other in pilots)
        {
            if(other==p || !other.Alive || other.invulnerable>0) continue;
            Vector3 delta=other.transform.position-p.transform.position;
            if(delta.magnitude>650 || !VisibleBetween(p.transform.position,other.transform.position)) continue;
            float a=Vector3.Angle(p.transform.forward,delta);
            if(a<best){best=a;result=other.transform;targetVelocity=other.velocity;}
        }
        foreach(var core in cores)
        {
            if(!core.Available) continue;
            Vector3 delta=core.transform.position-p.transform.position;
            if(delta.magnitude>650 || !VisibleBetween(p.transform.position,core.transform.position)) continue;
            float a=Vector3.Angle(p.transform.forward,delta);
            if(a<best){best=a;result=core.transform;targetVelocity=Vector3.zero;}
        }
        return result;
    }

    public bool Shoot(ArenaPilot p,bool seeker=false,Transform preferredTarget=null)
    {
        if(!p.Alive || paused || !MatchActive || p.fireCooldown>0 || p.heat>.92f) return false;
        Vector3 targetVelocity; Transform target;
        if(preferredTarget)
        {
            var victim=preferredTarget.GetComponent<ArenaPilot>();
            var wreck=preferredTarget.GetComponent<SalvageCore>();
            if((!victim && !wreck) || (victim && (victim==p || !victim.Alive || victim.invulnerable>0)) || (wreck && !wreck.Available))return false;
            target=preferredTarget;targetVelocity=victim?victim.velocity:Vector3.zero;
            Vector3 aim=InterceptPoint(p,target.position,targetVelocity,360)-p.transform.position;
            // A launched seeker gets the whole lock cone: it was earned over 1.2 s of tracking, so it must not be refused
            // by the cannon's 8 degree solution gate. Cannon fire through this branch is unchanged.
            if(Vector3.Distance(target.position,p.transform.position)>LockRange || Vector3.Angle(p.transform.forward,aim)>(seeker?LockCone:14) || !VisibleBetween(p.transform.position,target.position))return false;
        }
        else target=AimTarget(p,seeker?18:6,out targetVelocity);
        if(seeker && (!target || p.seekerCooldown>0)) return false;
        if(seeker) p.seekerCooldown=7;
        // Barrel temperature at the instant of the trigger pull, read before this round's own contribution: the shot
        // that finally cooks the gun is still an accurate one, and a cold first round is exactly on the bore.
        float barrel=p.heat;
        p.fireCooldown=seeker?.25f:.12f; p.heat+=seeker?.1f:.045f;
        p.shotsFired++;if(target && target.GetComponent<ArenaPilot>())p.combatShotsFired++;
        Vector3 direction=p.transform.forward;
        if(target)
        {
            direction=(InterceptPoint(p,target.position,targetVelocity,360)-p.transform.position).normalized;
        }
        // Signature aim error: the rookie sprays four degrees wide of what the ace does, and it is applied after the
        // firing solution passes its angle gate so a jittery pilot still shoots, it just does not shoot straight.
        float jitter=p.isPlayer && p.autopilot<0?0:p.Profile.aimJitter;
        if(jitter>0)direction=Quaternion.AngleAxis(Random.Range(-jitter,jitter),Random.onUnitSphere)*direction;
        // The player's version of that error is earned, not innate: a cold gun is exact, a gun held at the overheat gate
        // throws two degrees wide. The deflection axis is perpendicular to the bore, so a 2 degree cone really is 2 wide.
        float spread=p.isPlayer && !seeker?barrel*CannonSpread:0;
        if(spread>0)
        {
            Vector3 axis=Vector3.Cross(direction,Random.onUnitSphere);
            if(axis.sqrMagnitude<.0001f)axis=p.transform.up;
            direction=Quaternion.AngleAxis(Random.Range(-spread,spread),axis.normalized)*direction;
        }
        // Gold tracer marks the bounty from the receiving end too: you can tell who is shooting at you before you turn around.
        Material tracer=p.id==aceId?gold:p.isPlayer?teal:red;
        // .32 cross-section instead of .22: at 100 m a .22 bolt is under two pixels, which reads as nobody shooting at all.
        var g=Shape(seeker?"Seeker":"Cannon tracer",world,PrimitiveType.Cube,p.transform.position+p.transform.forward*5, new Vector3(.32f,.32f,seeker?2.5f:5),tracer);
        var bolt=g.AddComponent<ArenaBolt>(); bolt.owner=p; bolt.velocity=direction*(seeker?170:360)+p.velocity;
        bolt.target=target; bolt.seeker=seeker;
        var t=g.AddComponent<TrailRenderer>(); t.sharedMaterial=tracer; t.time=seeker?.6f:.1f; t.startWidth=seeker?.35f:.3f; t.endWidth=0;
        p.firedRecently=1.5f;
        // Per-shot recoil trauma is small: at 8 shots/s it settles near .2 trauma, which squares down to a faint buzz.
        if(p==player){audioSource.PlayOneShot(gunSound,.4f); Trauma(.03f);}
        else
        {
            float proximity=1-Mathf.Clamp01(Vector3.Distance(p.transform.position,player.transform.position)/450);
            if(proximity>0)audioSource.PlayOneShot(gunSound,proximity*.2f);
        }
        return true;
    }

    // Solve interception in the firing aircraft's moving frame.
    public static Vector3 InterceptPoint(ArenaPilot p,Vector3 target,Vector3 targetVelocity,float muzzleSpeed)
    {
        Vector3 delta=target-p.transform.position, relative=targetVelocity-p.velocity;
        float a=relative.sqrMagnitude-muzzleSpeed*muzzleSpeed,b=2*Vector3.Dot(delta,relative),c=delta.sqrMagnitude;
        float time=0, discriminant=b*b-4*a*c;
        if(Mathf.Abs(a)<.001f) { if(Mathf.Abs(b)>.001f) time=Mathf.Max(0,-c/b); }
        else if(discriminant>=0)
        {
            float root=Mathf.Sqrt(discriminant),t1=(-b-root)/(2*a),t2=(-b+root)/(2*a);
            time=t1>0 && t2>0?Mathf.Min(t1,t2):Mathf.Max(0,Mathf.Max(t1,t2));
        }
        return target+relative*Mathf.Min(time,3);
    }

    // The whole stick-to-control mapping, pure and static so the harness can measure it without a Keyboard.current.
    // `keys` is the raw keyboard triple (pitch, rudder, bank) in -1..1; `stick` is the normalised mouse deflection.
    // Mouse bank is damped to .42 so an aiming correction is not a snap roll, then a quarter of the RAW mouse-x is
    // added to yaw: mouse turns come out coordinated, while Q/E rudder stays full strength and A/D stays pure bank.
    public static Vector3 PilotControls(Vector2 stick,Vector3 keys)
    {
        return new Vector3(
            Mathf.Clamp(keys.x+stick.y*MouseFlightSensitivity,-1,1),
            Mathf.Clamp(keys.y+stick.x*YawCoupling,-1,1),
            Mathf.Clamp(keys.z+stick.x*MouseFlightSensitivity,-1,1));
    }

    // RMB is two presses, never a hold: the first starts (or re-points) a lock, the second launches the missile it earned.
    // Wrecks are exempt — a derelict does not evade, and the armoured belt is already priced for an instant seeker.
    void SeekerPress()
    {
        if(player.lockTimer>=ArenaPilot.LockTime && player.lockTarget)
        {
            if(Shoot(player,true,player.lockTarget)){player.lockTarget=null;player.lockTimer=0;}
            return;
        }
        Vector3 ignored; Transform candidate=AimTarget(player,LockCone,out ignored);
        if(!candidate) return;
        if(!candidate.GetComponent<ArenaPilot>()){Shoot(player,true,candidate);return;}
        if(candidate!=player.lockTarget){player.lockTarget=candidate;player.lockTimer=0;}
    }

    void CenterStick()
    {
        mouseStick=Vector2.zero;
        if(Mouse.current!=null && Application.isFocused)
            Mouse.current.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));
    }
    void OnApplicationFocus(bool focused)
    {
        if(focused)CenterStick();
        else if(player){player.controls=Vector3.zero;player.boost=false;mouseStick=Vector2.zero;}
    }
    // A scheduled note. Four fields in a struct because a melody is at most four of them and none of it outlives a second.
    public struct Note { public AudioClip clip; public float delay,pitch,volume; }
    public readonly List<Note> notes=new List<Note>();
    public void Cue(AudioClip clip,float delay,float pitch,float volume){ if(clip)notes.Add(new Note{clip=clip,delay=delay,pitch=pitch,volume=volume}); }
    // A melody is one clip and a list of ratios: 1 / 1.25 / 1.5 is a major triad, and .09 s of spacing reads as an
    // arpeggio rather than a chord without ever running past the 3 s toast it sits beside.
    public void Chord(AudioClip clip,float spacing,float volume,params float[] pitches)
    {
        for(int i=0;i<pitches.Length;i++) Cue(clip,i*spacing,pitches[i],volume);
    }
    public void ClaimChime(float volume){ Chord(chimeNote,.09f,volume,1,1.25f,1.5f); }
    public void BankArpeggio(float volume){ Chord(bankNote,.075f,volume,1,1.2f,1.5f,1.8f); }

    // Every continuous audio level, in one dt method so the harness can drive the mix without waiting for frames.
    // Volumes are a function of simulation state only; the clock is Update's, because nothing here feeds back into the sim.
    public void TickAudio(float dt)
    {
        if(!idleSource || !burnerSource || !windSource) return;
        bool flying=player && player.Alive && !paused;
        float throttle=flying?player.throttle:0, speed=flying?player.Speed:0;
        // Idle is loudest with the throttle closed and never disappears; the burner is what throttle and boost buy.
        idleSource.volume=flying?.06f+.06f*(1-throttle):0;
        burnerSource.volume=flying?throttle*.09f+(player.boost?.08f:0):0;
        // Pitch drift is clamped to +-.25 on the burner (and .6 of that on the idle bed): past that a loop stops sounding
        // like an engine under load and starts sounding like a tape being spun.
        float drift=Mathf.Clamp(speed/240f-.25f,-.25f,.25f);
        idleSource.pitch=1+drift*.6f; burnerSource.pitch=1+drift;
        // Same gate as DriveWindStreaks: nothing under 40 m/s, and the density term is read at the camera, not the
        // aircraft, so the bed fades out of the listener's ear rather than the ship's.
        float air=flying?Mathf.Clamp01((speed-40)/140f)*Density(Altitude(cam?cam.transform.position:player.transform.position)):0;
        windSource.volume=air*.65f;
        // A paused world keeps its queue: a countdown pip firing behind the comfort overlay would be a ghost.
        if(paused) return;
        for(int i=notes.Count-1;i>=0;i--)
        {
            var n=notes[i]; n.delay-=dt;
            if(n.delay>0){notes[i]=n;continue;}
            notes.RemoveAt(i);
            if(noteVoices==null)continue;
            var voice=noteVoices[noteVoice=(noteVoice+1)%noteVoices.Length];
            voice.pitch=n.pitch; voice.PlayOneShot(n.clip,n.volume);
        }
    }

    void Update()
    {
        var k=Keyboard.current;
        if(k!=null && k.escapeKey.wasPressedThisFrame) { paused=!paused; Cursor.visible=paused; Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Confined; }
        // Hit-stop and pause share the one time scale; pause always wins so a freeze frame can never unpause the world.
        hitStop=Mathf.Max(0,hitStop-Time.unscaledDeltaTime);
        Time.timeScale=paused?0:(hitStop>0?.05f:1);
        if(paused)
        {
            // Comfort rows, edited only while the world is stopped: up/down (or W/S) walks them, left/right (or A/D) changes one.
            if(k!=null)
            {
                if(k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) accessRow=(accessRow+2)%3;
                if(k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) accessRow=(accessRow+1)%3;
                if(k.digit1Key.wasPressedThisFrame) accessRow=0;
                if(k.digit2Key.wasPressedThisFrame) accessRow=1;
                if(k.digit3Key.wasPressedThisFrame) accessRow=2;
                int step=(k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame?1:0)-(k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame?1:0);
                if(step!=0)
                {
                    if(accessRow==0) shakeScale=Mathf.Clamp01(shakeScale+step*.1f);
                    else if(accessRow==1) reduceFlashing=!reduceFlashing;
                    else reduceCameraMotion=!reduceCameraMotion;
                    SaveComfort();
                }
            }
            TickAudio(0); return;
        }
        float dt=Time.deltaTime; elapsed+=dt; MatchTick(dt);
        if(screenshotPath!=null && elapsed>4.5f){ScreenCapture.CaptureScreenshot(screenshotPath);screenshotPath=null;quitAt=elapsed+1;}
        if(quitAt>0 && elapsed>quitAt)Application.Quit();
        if(k!=null && phase==MatchPhase.Ended && k.enterKey.wasPressedThisFrame) RestartMatch();
        hitFlash=Mathf.Max(0,hitFlash-dt); damageFlash=Mathf.Max(0,damageFlash-dt);
        // Purely visual timers run on unscaled time so hit-stop does not stretch a banner or a hit wedge.
        float raw=Time.unscaledDeltaTime;
        bannerTimer=Mathf.Max(0,bannerTimer-raw); lastAttackAge=Mathf.Max(0,lastAttackAge-raw);
        bountyFresh=Mathf.Max(0,bountyFresh-raw); TickHud(raw);
        // Overheat is an edge, not a state: the hiss fires on the crossing, so a player parked at the ceiling is not deafened.
        bool cooked=player.heat>.92f;
        if(cooked && !overheated && audioSource) audioSource.PlayOneShot(overheatHiss,.45f);
        overheated=cooked;
        // The lock is resolved exactly once per frame here, because OnGUI runs twice a frame and must never pick a target itself.
        if(player.Alive)
        {
            Transform aim=AimTarget(player,6,out hudTargetVelocity);
            if(aim!=hudTarget){hudTarget=aim;targetLock=aim?.15f:0;}
        }
        else { hudTarget=null; targetLock=0; }
        if(k!=null && player.Alive && Application.isFocused)
        {
            player.throttle=Mathf.Clamp01(player.throttle+((k.leftShiftKey.isPressed?1:0)-(k.leftCtrlKey.isPressed?1:0))*dt*.35f);
            var m=Mouse.current;
            if(k.cKey.wasPressedThisFrame)CenterStick();
            Vector2 stick=Vector2.zero;
            if(m!=null)
            {
                Vector2 pos=m.position.ReadValue();
                stick=new Vector2((pos.x/Screen.width-.5f)*2,(pos.y/Screen.height-.5f)*2);
                stick=Vector2.ClampMagnitude(stick,1);
                if(stick.magnitude<.07f) stick=Vector2.zero;
            }
            mouseStick=stick;
            player.controls=PilotControls(stick,new Vector3(
                (k.sKey.isPressed?1:0)-(k.wKey.isPressed?1:0),
                (k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),
                (k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0)));
            player.boost=k.spaceKey.isPressed;
            if((m!=null && m.leftButton.isPressed)||k.fKey.isPressed) Shoot(player);
            if(m!=null && m.rightButton.wasPressedThisFrame) SeekerPress();
        }
        // Single writer for every looping level and the scheduled notes; nothing else in Update touches an AudioSource.
        TickAudio(raw);
    }

    // Missile proximity and the lock tone are both rates, and a rate needs the simulation clock, not Update's.
    void FixedUpdate() { if(paused) return; MissileTick(Time.fixedDeltaTime); LockTick(Time.fixedDeltaTime); }

    public void SnapCamera()
    {
        if(!player || !cam) return;
        cameraRotation=player.transform.rotation;
        player.ResetRenderPose();
        cameraDistance=player.boost?17:14;
        cam.transform.rotation=cameraRotation*Quaternion.Euler(2,0,0);
        cam.transform.position=player.transform.position-player.transform.forward*cameraDistance+player.transform.up*CameraLift;
        FollowSky();
    }
    // The dome is a 6 km inverted sphere with no fixed place in the world: it only ever surrounds whatever the camera is.
    void FollowSky()
    {
        if(sky)sky.position=cam.transform.position;
        // Stars belong to the frame only once the air is too thin to scatter daylight: blind at 150 m, full sky by 600 m.
        if(starMaterial)starMaterial.SetFloat("_Fade",Mathf.Clamp01((Altitude(cam.transform.position)-150)/450));
    }
    void LateUpdate()
    {
        if(!player || !cam || paused) return;
        Vector3 position;Quaternion rotation;player.SampleRenderPose(out position,out rotation);
        UpdateChaseCamera(Time.deltaTime,position,rotation);
    }
    public void UpdateChaseCamera(float dt,Vector3 position,Quaternion rotation)
    {
        if(!player || !cam || paused || dt<=0)return;
        shake=Mathf.Max(0,shake-dt*1.3f);
        // One continuous quaternion frame for the entire rig: no conflicting LookRotation up vector.
        cameraRotation=Quaternion.Slerp(cameraRotation,rotation,1-Mathf.Exp(-8*dt));
        cameraDistance=Mathf.Lerp(cameraDistance,player.boost?17:14,1-Mathf.Exp(-5*dt));
        // Squaring trauma keeps chip damage almost still while a kill genuinely kicks the rig.
        float kick=shake*shake*2.6f;
        Vector3 noise=new Vector3(Mathf.PerlinNoise(Time.unscaledTime*19,0)-.5f,Mathf.PerlinNoise(0,Time.unscaledTime*17)-.5f,Mathf.PerlinNoise(Time.unscaledTime*13,7)-.5f)*kick;
        // The lift shrinks with the distance (3.5/14 is the old 5/20), so the aircraft keeps its exact place in the frame.
        cam.transform.position=position+cameraRotation*(new Vector3(0,CameraLift,-cameraDistance)+noise);
        // Angular shake reads far harder than translation at a 14 m chase distance, so roll carries most of the punch.
        // Roll is the component that reads as motion sickness rather than impact, so reduced camera motion drops it first.
        cam.transform.rotation=cameraRotation*Quaternion.Euler(2+noise.y*.9f,noise.x*.9f,reduceCameraMotion?0:noise.z*2.2f);
        cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,player.boost && !reduceCameraMotion?76:66,1-Mathf.Exp(-3*dt));
        // Kept as the clear colour behind the dome: if the sky shader ever fails to compile the frame is still flyable.
        cam.backgroundColor=Color.Lerp(new Color(.075f,.19f,.3f),new Color(.003f,.006f,.022f),Mathf.Clamp01(player.Altitude/600));
        FollowSky();
        DriveWindStreaks();
    }
    // Speed cue, not weather: nothing under 40 m/s, full rate by 160 m/s, doubled on the burner, and gone in vacuum.
    void DriveWindStreaks()
    {
        if(!windStreaks || !player)return;
        float rate=player.Alive?Mathf.Clamp01((player.Speed-40)/120)*(player.boost?2:1)*Density(Altitude(cam.transform.position))*70:0;
        var emission=windStreaks.emission; emission.rateOverTime=rate;
        // World-space drift opposite the aircraft: the streaks stretch along it, which is what sells the direction of travel.
        Vector3 drift=-player.velocity;
        var velocity=windStreaks.velocityOverLifetime;
        velocity.x=new ParticleSystem.MinMaxCurve(drift.x);velocity.y=new ParticleSystem.MinMaxCurve(drift.y);velocity.z=new ParticleSystem.MinMaxCurve(drift.z);
    }
    void OnDestroy()
    {
        if(I!=this) return;
        I=null; Time.timeScale=1;
        foreach(var a in owned) if(a) Destroy(a);
        Cursor.visible=true; Cursor.lockState=CursorLockMode.None;
    }
}
