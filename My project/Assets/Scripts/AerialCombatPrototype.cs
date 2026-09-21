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
    public ArenaPilot player;
    // Whoever is on a three-kill run. The whole field weights its target scoring toward this id, so the leader
    // gets hunted without any explicit difficulty dial, and a trailing pilot gets a way back in by taking the mark.
    public int aceId=-1;
    public const int AceStreak=3;
    public Camera cam;
    public UniversalAdditionalCameraData camData;
    public bool paused;
    // shake is trauma: callers only ever add to it, decay is linear, and the camera uses its square.
    public float elapsed, hitFlash, damageFlash, shake, hitStop, bannerTimer, lastAttackAge, bountyFresh;
    public string bannerText="";
    public Vector3 lastAttackDirection;
    public Transform world;
    readonly List<Object> owned = new List<Object>();
    Material alloy, dark, teal, red, gold, white, violet, slate;
    AudioSource audioSource, engineSource;
    AudioClip gunSound, hitSound, boomSound, collectSound;
    Quaternion cameraRotation;
    float cameraDistance=20;
    int spawnCounter;
    Vector2 mouseStick;
    const float MouseFlightSensitivity=.42f;
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
        RenderSettings.skybox=null; RenderSettings.fog=false;
        // Trilight gives the hull shading a direction (cold sky above, warm ground bounce below) instead of flat fill.
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.34f,.5f,.72f);
        RenderSettings.ambientEquatorColor=new Color(.13f,.19f,.34f);
        RenderSettings.ambientGroundColor=new Color(.36f,.25f,.17f);
        RenderSettings.ambientIntensity=1;
        var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(world);
        sun.type=LightType.Directional; sun.intensity=1.8f; sun.color=new Color(1,.9f,.78f);
        sun.transform.rotation=Quaternion.Euler(38,-28,0);
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
        engineSource=cam.gameObject.AddComponent<AudioSource>(); engineSource.loop=true;
        gunSound=Sound("Cannon",.12f,460,80,.3f);
        hitSound=Sound("Armor impact",.16f,220,55,.35f);
        boomSound=Sound("Ship breakup",.7f,75,18,.6f);
        collectSound=Sound("Salvage acquired",.2f,500,1000,.03f);
        engineSource.clip=Sound("Engine",1,70,70,.12f); engineSource.volume=.08f; engineSource.Play();
        BuildPlanet();
        BuildSites();
        string[] names={"YOU","Moth.exe","Blue Finch","Periapsis","DustRunner","Kite-09","SoupDragon","Last Comet"};
        for(int i=0;i<names.Length;i++)
        {
            var p=new GameObject(names[i]).AddComponent<ArenaPilot>(); p.transform.SetParent(world);
            p.id=i; p.callsign=names[i]; p.isPlayer=i==0;
            p.art=BuildShip(p.transform,i!=0);
            pilots.Add(p); if(i==0) player=p;
            Spawn(p,true);
        }
        SnapCamera();
        Cursor.lockState=CursorLockMode.Confined; Cursor.visible=false;
    }

    void BuildSites()
    {
        // First salvage field is directly ahead at launch; more sites encircle the planet.
        float[] lat={18,62,115,173,235,295};
        for(int s=0;s<lat.Length;s++)
        {
            float lon=s%2==0?0:32;
            var gate=new GameObject("Capture refinery "+(s+1)).AddComponent<CaptureGate>(); gate.transform.SetParent(world);
            gate.index=s; gate.transform.position=SurfacePoint(lat[s]+9,lon,170+(s%2)*290);
            gate.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward,Up(gate.transform.position)).normalized,Up(gate.transform.position));
            BuildGate(gate); gates.Add(gate);
            for(int c=0;c<4;c++)
            {
                var core=new GameObject("Breakable salvage core").AddComponent<SalvageCore>(); core.transform.SetParent(world);
                core.transform.position=SurfacePoint(lat[s]+c*2,lon+(c-1.5f)*3,155+(s%2)*300+c*8);
                // One unstable reactor per field is a weapon lying on the table; armour is rarer so the seeker stays a choice, not a chore.
                core.kind=c==2?CoreKind.Volatile:(c==3 && s%3==0?CoreKind.Armored:CoreKind.Normal);
                core.maxHealth=core.kind==CoreKind.Armored?200:65; core.health=core.maxHealth;
                core.value=s%2==0?8:16; BuildCore(core); cores.Add(core);
                if(c==0) for(int n=0;n<5;n++) SpawnShard(core.transform.position+new Vector3(n*7-14,4,-20),core.value);
            }
        }
    }

    public void Spawn(ArenaPilot p,bool initial=false)
    {
        int site=p.isPlayer?0:(p.id-1)%gates.Count;
        // Spawn on the same great-circle route as local resources, heading toward the nearest field.
        Vector3 pos=p.isPlayer?new Vector3(0,165,0):SurfacePoint(new[]{18f,62,115,173,235,295}[site]-8,site%2==0?0:32,site%2==0?165:450);
        if(!initial && p.isPlayer) pos=SurfacePoint((spawnCounter++%3)*4,0,170);
        p.transform.position=pos;
        SalvageCore nearest=NearestCore(pos);
        Vector3 direction=nearest ? nearest.transform.position-pos : Vector3.forward;
        p.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Up(pos)).normalized,Up(pos));
        p.velocity=p.transform.forward*82; p.health=100; p.cargo=0; p.respawn=0; p.invulnerable=3;
        p.throttle=.72f; p.heat=0; p.fuel=1; p.ResetFlight(); p.art.gameObject.SetActive(true); p.ClearTrails();
        if(p==player) { SnapCamera(); damageFlash=0; lastAttackAge=0; CenterStick(); }
    }

    // Trauma accumulates instead of overwriting, so a burst of small hits still reads as one big jolt.
    public void Trauma(float amount) { shake=Mathf.Min(1,shake+amount); }
    public void Banner(string text) { bannerText=text; bannerTimer=1.5f; }

    // Single dispatch point for the three feedback tiers: every event of the same weight gets the same layered package.
    public void Feedback(string tier,Vector3 pos,ArenaPilot involved=null,AudioClip voice=null)
    {
        bool mine=involved && involved==player;
        float proximity=player?1-Mathf.Clamp01(Vector3.Distance(pos,player.transform.position)/450):0;
        if(tier=="large")
        {
            Burst(pos,28,3,true);
            if(mine)
            {
                Trauma(.8f); hitFlash=Mathf.Max(hitFlash,.06f);
                // Hit-stop is player-only: AI trading kills across the map must never stutter the frame.
                hitStop=.08f; audioSource.PlayOneShot(voice?voice:boomSound,.7f);
            }
            else
            {
                Trauma(proximity*.35f);
                if(proximity>0) audioSource.PlayOneShot(voice?voice:boomSound,proximity*.7f);
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
        if(attacker && attacker!=victim)
        {
            attacker.kills++; attacker.streak++;
            // The crowning gets its own HUD entrance rather than a banner: the kill banner below is the louder, more urgent read.
            if(attacker.streak>=AceStreak && aceId!=attacker.id){aceId=attacker.id;bountyFresh=1.1f;}
        }
        int pieces=Mathf.Min(16,Mathf.CeilToInt(cargo/8f));
        for(int i=0;i<pieces;i++)
        {
            int value=cargo/(pieces-i); cargo-=value;
            SpawnShard(victim.transform.position+Random.insideUnitSphere*10,value);
        }
        bool mine=victim==player || attacker==player;
        Feedback("large",victim.transform.position,mine?player:null);
        if(victim==player)
        {
            damageFlash=.5f;
            if(attacker && attacker!=victim) { RecordIncoming(attacker.transform.position); Banner("SPLASHED BY  "+attacker.callsign); }
            else Banner("TERRAIN IMPACT");
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

    public void Damage(ArenaPilot p,float damage,ArenaPilot attacker)
    {
        if(!p.Alive || p.invulnerable>0 || paused || !MatchActive) return;
        if(attacker && attacker!=p){p.NotifyAttacked(attacker);attacker.hitsLanded++;}
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
        s.claimed=true; p.cargo+=s.value; shards.Remove(s); Destroy(s.gameObject);
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
            if(Vector3.Distance(target.position,p.transform.position)>650 || Vector3.Angle(p.transform.forward,aim)>8 || !VisibleBetween(p.transform.position,target.position))return false;
        }
        else target=AimTarget(p,seeker?18:6,out targetVelocity);
        if(seeker && (!target || p.seekerCooldown>0)) return false;
        if(seeker) p.seekerCooldown=7;
        p.fireCooldown=seeker?.25f:.12f; p.heat+=seeker?.1f:.045f;
        p.shotsFired++;if(target && target.GetComponent<ArenaPilot>())p.combatShotsFired++;
        Vector3 direction=p.transform.forward;
        if(target)
        {
            direction=(InterceptPoint(p,target.position,targetVelocity,360)-p.transform.position).normalized;
        }
        // Signature aim error: the rookie sprays four degrees wide of what the ace does, and it is applied after the
        // firing solution passes its angle gate so a jittery pilot still shoots, it just does not shoot straight.
        float jitter=p.isPlayer?0:p.Profile.aimJitter;
        if(jitter>0)direction=Quaternion.AngleAxis(Random.Range(-jitter,jitter),Random.onUnitSphere)*direction;
        // Gold tracer marks the bounty from the receiving end too: you can tell who is shooting at you before you turn around.
        Material tracer=p.id==aceId?gold:p.isPlayer?teal:red;
        var g=Shape(seeker?"Seeker":"Cannon tracer",world,PrimitiveType.Cube,p.transform.position+p.transform.forward*5, new Vector3(.22f,.22f,seeker?2.5f:5),tracer);
        var bolt=g.AddComponent<ArenaBolt>(); bolt.owner=p; bolt.velocity=direction*(seeker?170:360)+p.velocity;
        bolt.target=target; bolt.seeker=seeker;
        var t=g.AddComponent<TrailRenderer>(); t.sharedMaterial=tracer; t.time=seeker?.6f:.1f; t.startWidth=seeker?.35f:.18f; t.endWidth=0;
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
    void Update()
    {
        var k=Keyboard.current;
        if(k!=null && k.escapeKey.wasPressedThisFrame) { paused=!paused; Cursor.visible=paused; Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Confined; }
        // Hit-stop and pause share the one time scale; pause always wins so a freeze frame can never unpause the world.
        hitStop=Mathf.Max(0,hitStop-Time.unscaledDeltaTime);
        Time.timeScale=paused?0:(hitStop>0?.05f:1);
        if(paused) { engineSource.volume=0; return; }
        float dt=Time.deltaTime; elapsed+=dt; MatchTick(dt);
        if(k!=null && phase==MatchPhase.Ended && k.enterKey.wasPressedThisFrame) RestartMatch();
        hitFlash=Mathf.Max(0,hitFlash-dt); damageFlash=Mathf.Max(0,damageFlash-dt);
        // Purely visual timers run on unscaled time so hit-stop does not stretch a banner or a hit wedge.
        float raw=Time.unscaledDeltaTime;
        bannerTimer=Mathf.Max(0,bannerTimer-raw); lastAttackAge=Mathf.Max(0,lastAttackAge-raw);
        bountyFresh=Mathf.Max(0,bountyFresh-raw);
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
            player.controls=new Vector3(
                Mathf.Clamp((k.sKey.isPressed?1:0)-(k.wKey.isPressed?1:0)+stick.y*MouseFlightSensitivity,-1,1),
                (k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),
                Mathf.Clamp((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0)+stick.x*MouseFlightSensitivity,-1,1));
            player.boost=k.spaceKey.isPressed;
            if((m!=null && m.leftButton.isPressed)||k.fKey.isPressed) Shoot(player);
            if(m!=null && m.rightButton.wasPressedThisFrame) Shoot(player,true);
        }
        engineSource.volume=player.Alive?.07f+player.throttle*.08f:0;
        engineSource.pitch=.65f+player.Speed/180+ (player.boost?.4f:0);
    }

    public void SnapCamera()
    {
        if(!player || !cam) return;
        cameraRotation=player.transform.rotation;
        player.ResetRenderPose();
        cameraDistance=player.boost?24:20;
        cam.transform.rotation=cameraRotation*Quaternion.Euler(2,0,0);
        cam.transform.position=player.transform.position-player.transform.forward*cameraDistance+player.transform.up*5;
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
        cameraDistance=Mathf.Lerp(cameraDistance,player.boost?24:20,1-Mathf.Exp(-5*dt));
        // Squaring trauma keeps chip damage almost still while a kill genuinely kicks the rig.
        float kick=shake*shake*2.6f;
        Vector3 noise=new Vector3(Mathf.PerlinNoise(Time.unscaledTime*19,0)-.5f,Mathf.PerlinNoise(0,Time.unscaledTime*17)-.5f,Mathf.PerlinNoise(Time.unscaledTime*13,7)-.5f)*kick;
        cam.transform.position=position+cameraRotation*(new Vector3(0,5,-cameraDistance)+noise);
        // Angular shake reads far harder than translation at a 20 m chase distance, so roll carries most of the punch.
        cam.transform.rotation=cameraRotation*Quaternion.Euler(2+noise.y*.9f,noise.x*.9f,noise.z*2.2f);
        cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,player.boost?76:66,1-Mathf.Exp(-3*dt));
        cam.backgroundColor=Color.Lerp(new Color(.075f,.19f,.3f),new Color(.003f,.006f,.022f),Mathf.Clamp01(player.Altitude/600));
    }
    void OnDestroy()
    {
        if(I!=this) return;
        I=null; Time.timeScale=1;
        foreach(var a in owned) if(a) Destroy(a);
        Cursor.visible=true; Cursor.lockState=CursorLockMode.None;
    }
}
