using UnityEngine;

public partial class ArenaPilot : MonoBehaviour
{
    public int id, score, cargo, kills, deaths, generation, streak;
    public string callsign;
    public bool isPlayer, boost;
    public Transform art;
    public Vector3 velocity, controls;
    public float health=100, throttle=.72f, heat, fuel=1, respawn, invulnerable, fireCooldown, seekerCooldown;
    // Airframe temperature, distinct from the weapon's heat above: this one is earned by coming down too fast.
    public float hullHeat;
    // Seeker lock state, player-only for now (no AI pilot fires a seeker — FlyTactics only ever calls Shoot(...,false,...)).
    public Transform lockTarget;
    public float lockTimer;
    // 1.2 s of continuous tracking. Long enough that a missile is a decision, short enough to land inside one firing pass.
    public const float LockTime=1.2f;
    public int autopilot=-1;
    float burnBank;
    // Decays on simulation dt, not wall clock, so rivals hearing gunfire stays deterministic under the verification harness.
    public float firedRecently;
    public bool recovering;
    public float Speed => velocity.magnitude;
    public float Altitude => AerialCombatPrototype.Altitude(transform.position);
    public bool Alive => health>0;
    float decision;
    Vector3 navigation;
    SalvageCore coreTarget;
    SalvageShard shardTarget;
    CaptureGate gateTarget;
    ArenaPilot rivalTarget;
    Transform[] plumes;
    float plumeStretch=1;
    Vector3 angularVelocity;
    Vector3 previousPosition;
    Quaternion previousRotation;
    bool poseReady;
    public int shotsFired, combatShotsFired, hitsLanded;
    public void ResetRenderPose()
    {
        previousPosition=transform.position;previousRotation=transform.rotation;poseReady=true;
        if(art)art.SetPositionAndRotation(transform.position,transform.rotation);
    }
    public void SampleRenderPose(out Vector3 position,out Quaternion rotation)
    {
        float t=Mathf.Clamp01((Time.time-Time.fixedTime)/Time.fixedDeltaTime);
        position=poseReady?Vector3.Lerp(previousPosition,transform.position,t):transform.position;
        rotation=poseReady?Quaternion.Slerp(previousRotation,transform.rotation,t):transform.rotation;
    }
    void LateUpdate()
    {
        var g=AerialCombatPrototype.I;if(!art || !g || g.paused)return;
        Vector3 position;Quaternion rotation;SampleRenderPose(out position,out rotation);
        art.SetPositionAndRotation(position,rotation);
        if(plumes==null)
        {
            var parts=art.GetComponentsInChildren<Transform>();int found=0;
            foreach(var t in parts)if(t.name=="Exhaust")found++;
            plumes=new Transform[found];found=0;
            foreach(var t in parts)if(t.name=="Exhaust")plumes[found++]=t;
        }
        // Render-only tell, which is why it lives here and rides Time.deltaTime: the burner plume stretches 2.5x along
        // its own z off the (.22,.18,1.5) nozzle BuildShip lays down, so boost is legible from behind without a HUD word.
        plumeStretch=Mathf.Lerp(plumeStretch,boost?2.5f:1,1-Mathf.Exp(-7*Time.deltaTime));
        foreach(var t in plumes)t.localScale=new Vector3(.22f,.18f,1.5f*plumeStretch);
    }
    public void ResetFlight()
    {
        angularVelocity=Vector3.zero;controls=Vector3.zero;boost=false;
        fireCooldown=0;seekerCooldown=0;decision=0;firedRecently=0;recovering=false;
        lockTarget=null;lockTimer=0;
        hullHeat=0;burnBank=0;
        coreTarget=null;shardTarget=null;gateTarget=null;rivalTarget=null;
        ResetTactics();ResetRenderPose();
    }

    public void ClearTrails()
    {
        generation++;
        foreach(var t in GetComponentsInChildren<TrailRenderer>()) t.Clear();
    }
    void FixedUpdate()
    {
        if(!AerialCombatPrototype.I || AerialCombatPrototype.I.paused)return;
        previousPosition=transform.position;previousRotation=transform.rotation;poseReady=true;
        Simulate(Time.fixedDeltaTime);
    }

    public void Simulate(float dt)
    {
        var arena=AerialCombatPrototype.I;
        if(!arena || arena.paused) return;
        if(!Alive)
        {
            respawn-=dt;
            if(respawn<=0) arena.Spawn(this);
            return;
        }
        invulnerable=Mathf.Max(0,invulnerable-dt);
        fireCooldown=Mathf.Max(0,fireCooldown-dt);
        seekerCooldown=Mathf.Max(0,seekerCooldown-dt);
        heat=Mathf.Max(0,heat-dt*.22f);
        firedRecently=Mathf.Max(0,firedRecently-dt);
        // autopilot>=0 hands the player's airframe to a rival personality: the balance harness flies whole matches with it.
        if(!isPlayer || autopilot>=0) Think(dt);
        else TrackLock(dt);

        Vector3 up=AerialCombatPrototype.Up(transform.position);
        float density=AerialCombatPrototype.Density(Altitude);
        float speed=Speed;
        // Re-entry heating goes with the cube of the descent rate, the way convective heating really does, so a slow
        // spiral never out-runs the constant .3/s cooling and only a committed plunge into thick air can build.
        // Capped at 2 so a bad re-entry costs a third of the hull instead of being unrecoverable.
        float plunge=-Vector3.Dot(velocity,up)/100f;
        // 2.4 rather than 3.2: at 3.2 an ordinary 85 m/s dive at 200 m went from cold to burning in three seconds, which
        // the balance runs showed killing rivals on plain approaches. A committed plunge from the coast line still burns.
        hullHeat=Mathf.Clamp(hullHeat+((plunge>0?plunge*plunge*plunge*density*2.4f:0)-.3f)*dt,0,2);
        if(hullHeat>1 && invulnerable<=0 && arena.MatchActive)
        {
            // Burn is banked into 4-point bites: 8/s through Damage() every step would fire the feedback layer 50 times a second.
            burnBank+=dt*8;
            if(burnBank>=4){burnBank-=4;arena.Damage(this,4,null);if(!Alive)return;}
        }
        else burnBank=0;
        // One rotation channel for everyone: AI writes the same -1..1 controls the mouse and keyboard write.
        float authority=Mathf.Lerp(.4f,1,Mathf.Clamp01(speed/65)*density);
        // A pull-up from a terrain prediction is allowed to cheat the air: it is a survival reflex, not a dogfight advantage.
        if(recovering)authority=Mathf.Max(authority,.9f);
        // Wings-level assist. Bank is the signed angle from the wing plane to the local radial, measured about the nose,
        // and the roll channel gets a proportional correction that saturates at AutoLevelRate. It is player-only and
        // only ever runs on a centred roll input with a light hand on the pitch, so a deliberate manoeuvre owns the axis
        // outright; past 100 degrees of bank it stands down entirely and lets an inverted pass finish the roll.
        float assist=0;
        if(isPlayer && AerialCombatPrototype.AutoLevelEnabled && Mathf.Abs(controls.z)<.05f && Mathf.Abs(controls.x)<.5f)
        {
            Vector3 reference=Vector3.ProjectOnPlane(up,transform.forward);
            if(reference.sqrMagnitude>.0001f)
            {
                float bankAngle=Vector3.SignedAngle(transform.up,reference.normalized,transform.forward);
                if(Mathf.Abs(bankAngle)<AerialCombatPrototype.AutoLevelLimit)
                {
                    float roll=Mathf.Clamp(bankAngle*AerialCombatPrototype.AutoLevelGain,-AerialCombatPrototype.AutoLevelRate,AerialCombatPrototype.AutoLevelRate);
                    // Divide out the same authority the rate is about to be multiplied by, floored so thin air cannot
                    // turn a 15 deg/s request into a full-stick command.
                    assist=Mathf.Clamp(-roll/(85*Mathf.Max(.35f,authority)),-AerialCombatPrototype.AutoLevelAuthority,AerialCombatPrototype.AutoLevelAuthority);
                }
            }
        }
        Vector3 rates=new Vector3(-controls.x*44,controls.y*27,-Mathf.Clamp(controls.z+assist,-1,1)*85)*authority;
        angularVelocity=Vector3.Lerp(angularVelocity,rates,1-Mathf.Exp(-(recovering?9:5)*dt));
        transform.rotation*=Quaternion.Euler(angularVelocity*dt);
        bool burner=boost && fuel>.02f;
        fuel=Mathf.Clamp01(fuel+(burner?-.19f:.085f)*dt);
        Vector3 f=transform.forward;
        float forwardSpeed=Mathf.Max(0,Vector3.Dot(velocity,f));
        float angleOfAttack=Vector3.SignedAngle(Vector3.ProjectOnPlane(velocity,transform.right),f,transform.right)*Mathf.Deg2Rad;
        float stall=Mathf.InverseLerp(22,48,forwardSpeed);
        Vector3 liftDirection=Vector3.ProjectOnPlane(transform.up,velocity.normalized).normalized;
        // Periodic lift avoids a force discontinuity when angle of attack wraps at +/-180 degrees.
        float liftCoefficient=(.0009f*Mathf.Max(0,Mathf.Cos(angleOfAttack))-.002f*Mathf.Sin(2*angleOfAttack))*stall;
        Vector3 lift=liftDirection*(forwardSpeed*forwardSpeed*density*liftCoefficient);
        // Every 10 units of cargo is +4% weight and +3% drag, capped: a full hold is the price of greed, for the AI as much as the player.
        Vector3 gravity=-up*(AerialCombatPrototype.Gravity(transform.position)*(1+Mathf.Min(.6f,cargo*.004f)));
        Vector3 thrust=f*(throttle*12+(burner?28:0));
        Vector3 drag=-velocity*speed*((.00115f*density+.00004f)*(1+Mathf.Min(.45f,cargo*.003f)));
        velocity+=(gravity+thrust+drag+lift)*dt;
        // Aerodynamic sideslip damping redirects momentum without snapping the position or speed.
        float align=Mathf.Clamp01(dt*density*1.5f*stall);
        velocity=Vector3.Slerp(velocity.normalized,f,align)*velocity.magnitude;
        transform.position+=velocity*dt;
        // Parallel transport preserves the local flight frame around the small curved planet.
        Vector3 newUp=AerialCombatPrototype.Up(transform.position);
        transform.rotation=Quaternion.FromToRotation(up,newUp)*transform.rotation;
        if(Altitude<4 || Altitude>6500 || float.IsNaN(Altitude)) arena.Kill(this,null);
    }

    void Think(float dt) { FlyTactics(dt); }

    // The seeker lock, advanced on simulation dt like everything else that is a rate. The lock holds only while the
    // target is a living pilot still inside the firing cone, in range and not behind the planet; break any one of those
    // and the whole 1.2 s is forfeit, which is what makes a hard break the counter-play to being locked.
    void TrackLock(float dt)
    {
        var arena=AerialCombatPrototype.I;
        if(!lockTarget){lockTimer=0;return;}
        var foe=lockTarget.GetComponent<ArenaPilot>();
        bool held=foe && foe.Alive && foe!=this && foe.invulnerable<=0
            && Vector3.Distance(transform.position,lockTarget.position)<=AerialCombatPrototype.LockRange
            && Vector3.Angle(transform.forward,lockTarget.position-transform.position)<=AerialCombatPrototype.LockCone
            && arena.VisibleBetween(transform.position,lockTarget.position);
        if(!held){lockTarget=null;lockTimer=0;return;}
        lockTimer=Mathf.Min(LockTime,lockTimer+dt);
    }
}

public enum CoreKind { Normal, Volatile, Armored }

public class SalvageCore : MonoBehaviour
{
    public int value;
    // Which refinery field this wreck belongs to. Build-time state, so it survives RestartMatch like the mesh does.
    public int site;
    public CoreKind kind;
    public float health=65, maxHealth=65, cooldown;
    public const float BlastRadius=45, BlastDamage=55;
    public Transform art, weakPoint;
    public bool Available => cooldown<=0 && health>0;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var arena=AerialCombatPrototype.I; if(!arena || arena.paused) return;
        if(cooldown>0)
        {
            cooldown-=dt;
            if(cooldown<=0){health=maxHealth;art.gameObject.SetActive(true);}
        }
        else if(art) art.Rotate(0,dt*8,dt*3);
        // An unstable reactor pulses twice as fast and twice as wide: it reads as a fuse from across the field.
        if(weakPoint) weakPoint.localScale=Vector3.one*((kind==CoreKind.Volatile?7:kind==CoreKind.Armored?4.5f:5.5f)+Mathf.Sin(arena.elapsed*(kind==CoreKind.Volatile?7:3))*(kind==CoreKind.Volatile?.8f:.35f));
    }
    public void Hit(float damage,ArenaPilot shooter,bool missile=false)
    {
        if(!Available) return;
        var g=AerialCombatPrototype.I;
        // Belt armour shrugs off cannon fire; the seeker is what it is priced for, which finally gives the missile a second job.
        if(kind==CoreKind.Armored && !missile) damage*=.3f;
        health-=damage;
        g.Feedback("small",transform.position,shooter);
        if(health>0) return;
        // 70 s (was 28): a field has to run dry so its pilots move on. At 28 s every rival camped its home site for
        // the whole match and the balance runs never saw two of them in the same sky.
        cooldown=70; art.gameObject.SetActive(false);
        int pieces=kind==CoreKind.Armored?21:kind==CoreKind.Volatile?11:7;
        for(int i=0;i<pieces;i++) g.SpawnShard(transform.position+Random.insideUnitSphere*16,value);
        if(kind==CoreKind.Volatile)
        {
            // The blast is indiscriminate on purpose: lighting the fuse from inside the radius is the price of taking the shot early.
            foreach(var victim in g.pilots)
                if(victim.Alive && Vector3.Distance(victim.transform.position,transform.position)<BlastRadius) g.Damage(victim,BlastDamage,shooter);
            g.Feedback("large",transform.position,shooter);
        }
        else g.Feedback("medium",transform.position,shooter);
    }
}

public class SalvageShard : MonoBehaviour
{
    public int value;
    public bool claimed;
    public float life=95;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var g=AerialCombatPrototype.I; if(!g || g.paused || claimed) return;
        life-=dt;
        if(life<=0) { g.shards.Remove(this); Destroy(gameObject); return; }
        transform.Rotate(18*dt,38*dt,15*dt);
        ArenaPilot nearest=null; float distance=48;
        foreach(var p in g.pilots)
        {
            if(!p.Alive) continue;
            float d=Vector3.Distance(p.transform.position,transform.position);
            if(d<distance){distance=d;nearest=p;}
        }
        if(!nearest) return;
        if(distance<12){g.Collect(nearest,this);return;}
        transform.position=Vector3.MoveTowards(transform.position,nearest.transform.position,dt*110);
    }
}

public class CaptureGate : MonoBehaviour
{
    public int index, owner=-1, claimant=-1;
    // True for the three 460 m fields: sets the dock lighting at build time and marks the high-value band everywhere else.
    public bool lowOrbit;
    public float progress;
    // Seconds left on a double-rate refining surge. The match clock owns the trigger; the gate only counts it down.
    public float overcharge;
    public const float Radius=105;
    public LineRenderer ring, progressRing;
    // The 400 m beacon beam. It is fed by the same MaterialPropertyBlock as the rings, so the owner colour rules live once.
    public Renderer pillar;
    public Renderer core;
    MaterialPropertyBlock colors;
    public float ownerAge, payoutFlash;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var g=AerialCombatPrototype.I; if(!g || g.paused || !g.MatchActive) return;
        ArenaPilot occupant=null; int count=0;
        foreach(var p in g.pilots)
        {
            bool inside=p.Alive && Vector3.Distance(p.transform.position,transform.position)<Radius;
            if(!inside)continue;
            count++; occupant=p;
        }
        if(count==1)
        {
            if(claimant!=occupant.id){claimant=occupant.id;progress=0;}
            progress=Mathf.Clamp01(progress+dt/1.4f);
            if(progress>=1)
            {
                if(owner!=occupant.id)
                {
                    owner=occupant.id; ownerAge=0; occupant.score+=25;
                    g.Toast("REFINERY "+(index+1)+"  CLAIMED  +25",occupant==g.player?new Color(.16f,1,.85f):new Color(1,.45f,.18f));
                    // Three rising notes beside the toast. A rival taking a ring across the planet is still board news,
                    // so it chimes too, at a third of the volume: you hear the map change without being shouted at.
                    g.ClaimChime(occupant==g.player?.5f:.18f);
                    if(occupant==g.player)g.bankPop=.18f;
                }
                // The ace refines at a premium: the mark is worth holding onto, which is what makes hunting it worth the risk.
                // An overcharged refinery doubles on top of that, which is what drags the whole field onto one point.
                if(occupant.cargo>0)
                {
                    float rate=(g.aceId==occupant.id?1.5f:1)*(overcharge>0?2:1);
                    int banked=Mathf.RoundToInt(occupant.cargo*rate);
                    occupant.score+=banked;occupant.cargo=0;
                    // Four notes for a deposit against the claim's three: the ear can tell the two events apart blind.
                    g.BankArpeggio(occupant==g.player?.45f:.14f);
                    if(occupant==g.player){g.Toast("+"+banked+"  BANKED",new Color(.16f,1,.85f));g.bankPop=.18f;}
                }
                occupant.health=Mathf.Min(100,occupant.health+dt*10);
            }
        }
        else if(count==0) progress=Mathf.Max(0,progress-dt*.6f);
        // Holding a refinery is an income stream, not a one-off bonus: 5 points every 12 s straight into banked score.
        if(owner>=0)
        {
            ownerAge+=dt;
            if(ownerAge>=12){ownerAge-=12;if(owner<g.pilots.Count)g.pilots[owner].score+=5;payoutFlash=.4f;}
        }
        else ownerAge=0;
        payoutFlash=Mathf.Max(0,payoutFlash-dt);
        overcharge=Mathf.Max(0,overcharge-dt);
        // Two or more competing pilots contest the zone: progress and banking stop.
        Color color=count>1?new Color(1,.18f,.08f):owner==0?new Color(.1f,1,.8f):owner<0?new Color(.1f,.7f,1):new Color(1,.35f,.13f);
        // Violet washes over whoever holds it rather than replacing the ownership read: the ring still says who and now also says how much.
        if(overcharge>0)color=Color.Lerp(color,new Color(1.1f,.28f,2.1f),.5f+.22f*Mathf.Sin(g.elapsed*6));
        if(payoutFlash>0)color=Color.Lerp(color,new Color(2.4f,2.4f,2.2f),payoutFlash/.4f);
        if(colors==null)colors=new MaterialPropertyBlock();
        colors.SetColor("_BaseColor",color);
        if(ring)ring.SetPropertyBlock(colors);
        if(pillar)pillar.SetPropertyBlock(colors);
        if(progressRing)
        {
            int points=Mathf.Max(2,Mathf.RoundToInt(progress*96)); progressRing.positionCount=points;
            for(int i=0;i<points;i++){float a=i*Mathf.PI*2/95;progressRing.SetPosition(i,new Vector3(Mathf.Cos(a)*88,Mathf.Sin(a)*88,0));}
            progressRing.SetPropertyBlock(colors);
        }
    }
}

public class ArenaBolt : MonoBehaviour
{
    public ArenaPilot owner;
    public Transform target;
    public Vector3 velocity;
    public bool seeker;
    float life=3;
    bool spent,chasedResolved;
    ArenaPilot chased;
    // A round that shaves the hull inside 1.4 m of its swept segment is a marksman's shot, and pays 1.75x for it.
    public const float PreciseRadius=1.4f,PreciseMultiplier=1.75f;
    public static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {
        Vector3 d=b-a; return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));
    }
    // The missile warning needs the live set every fixed step; registering here covers bolts the harness builds by hand too.
    void OnEnable() { var g=AerialCombatPrototype.I; if(g && !g.bolts.Contains(this)) g.bolts.Add(this); }
    void OnDestroy() { var g=AerialCombatPrototype.I; if(g) g.bolts.Remove(this); }
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        if(spent)return;
        var g=AerialCombatPrototype.I; if(!g){Destroy(gameObject);return;} if(g.paused)return;
        life-=dt;
        if(life<=0){spent=true;Destroy(gameObject);return;}
        if(seeker && target)
        {
            if(!chasedResolved){chased=target.GetComponent<ArenaPilot>();chasedResolved=true;}
            // Burner-jinking cuts the seeker's authority from 2.3 to 1.2 rad/s: boost is the counter-play, so no new key is needed.
            float turn=chased && chased.boost?1.2f:2.3f;
            velocity=Vector3.RotateTowards(velocity.normalized,(target.position-transform.position).normalized,dt*turn,0)*260;
        }
        Vector3 a=transform.position,b=a+velocity*dt;
        transform.position=b;transform.rotation=Quaternion.LookRotation(velocity);
        if(!g.VisibleBetween(a,b)){spent=true;Destroy(gameObject);return;}
        // Resolve nearest intersection along the movement segment, avoiding frame-rate tunneling.
        float nearest=float.MaxValue,miss=0; ArenaPilot pilot=null; SalvageCore core=null;
        Vector3 heading=velocity.normalized;
        foreach(var p in g.pilots)
        {
            if(p==owner || !p.Alive || p.invulnerable>0)continue;
            float along=Vector3.Dot(p.transform.position-a,heading);
            if(along>=-5 && along<nearest && SegmentDistance(p.transform.position,a,b)<4)
            // The hit still comes off the swept segment, but precision is judged on the line of flight: a 7.2 m step
            // must never be what decides whether a shot counted as good, only whether it connected at all.
            {nearest=along;pilot=p;core=null;miss=Vector3.Distance(p.transform.position,a+heading*along);}
        }
        foreach(var c in g.cores)
        {
            if(!c.Available)continue;
            float along=Vector3.Dot(c.transform.position-a,velocity.normalized);
            if(along>=-9 && along<nearest && SegmentDistance(c.transform.position,a,b)<9)
            {nearest=along;core=c;pilot=null;}
        }
        if(pilot)
        {
            spent=true;bool precise=miss<PreciseRadius;
            // 18 per round (was 12): six ordinary hits or four with a graze to a kill. At 12 the balance matches ended
            // with three kills between sixteen aircraft; at 14, seventy-five landed hits across three matches bought six.
            g.Damage(pilot,(seeker?60:18)*(precise?PreciseMultiplier:1),owner,precise);
            Destroy(gameObject);
        }
        else if(core){spent=true;core.Hit(seeker?80:18,owner,seeker);Destroy(gameObject);}
    }
}

public class ArenaDebris : MonoBehaviour
{
    public Vector3 velocity;
    public float life;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var g=AerialCombatPrototype.I; if(g && g.paused)return;
        life-=dt;transform.position+=velocity*dt;
        transform.localScale*=Mathf.Exp(-2*dt);
        if(life<=0)Destroy(gameObject);
    }
}
