using UnityEngine;

public partial class ArenaPilot : MonoBehaviour
{
    public int id, score, cargo, kills, deaths, generation;
    public string callsign;
    public bool isPlayer, boost;
    public Transform art;
    public Vector3 velocity, controls;
    public float health=100, throttle=.72f, heat, fuel=1, respawn, invulnerable, fireCooldown, seekerCooldown;
    public float Speed => velocity.magnitude;
    public float Altitude => AerialCombatPrototype.Altitude(transform.position);
    public bool Alive => health>0;
    float decision;
    Vector3 navigation;
    SalvageCore coreTarget;
    SalvageShard shardTarget;
    CaptureGate gateTarget;
    ArenaPilot rivalTarget;
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
    }
    public void ResetFlight()
    {
        angularVelocity=Vector3.zero;controls=Vector3.zero;boost=false;
        fireCooldown=0;seekerCooldown=0;decision=0;
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
        if(!isPlayer) Think(dt);

        Vector3 up=AerialCombatPrototype.Up(transform.position);
        float density=AerialCombatPrototype.Density(Altitude);
        float speed=Speed;
        if(isPlayer)
        {
            float authority=Mathf.Lerp(.4f,1,Mathf.Clamp01(speed/65)*density);
            Vector3 rates=new Vector3(-controls.x*44,controls.y*27,-controls.z*85)*authority;
            angularVelocity=Vector3.Lerp(angularVelocity,rates,1-Mathf.Exp(-5*dt));
            transform.rotation*=Quaternion.Euler(angularVelocity*dt);
        }
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
        Vector3 gravity=-up*AerialCombatPrototype.Gravity(transform.position);
        Vector3 thrust=f*(throttle*12+(burner?28:0));
        Vector3 drag=-velocity*speed*(.00115f*density+.00004f);
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
}

public class SalvageCore : MonoBehaviour
{
    public int value;
    public float health=65, cooldown;
    public Transform art, weakPoint;
    public bool Available => cooldown<=0 && health>0;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var arena=AerialCombatPrototype.I; if(!arena || arena.paused) return;
        if(cooldown>0)
        {
            cooldown-=dt;
            if(cooldown<=0){health=65;art.gameObject.SetActive(true);}
        }
        else if(art) art.Rotate(0,dt*8,dt*3);
        if(weakPoint) weakPoint.localScale=Vector3.one*(5.5f+Mathf.Sin(arena.elapsed*3)*.35f);
    }
    public void Hit(float damage,ArenaPilot shooter)
    {
        if(!Available) return;
        var g=AerialCombatPrototype.I;
        health-=damage;
        if(shooter==g.player) g.hitFlash=.16f;
        g.Burst(transform.position,4,.8f,false);
        if(health>0) return;
        cooldown=28; art.gameObject.SetActive(false);
        for(int i=0;i<7;i++) g.SpawnShard(transform.position+Random.insideUnitSphere*16,value);
        g.Burst(transform.position,18,2,true);
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
    public float progress;
    public const float Radius=105;
    public LineRenderer ring, progressRing;
    public Renderer core;
    MaterialPropertyBlock colors;
    public float ownerAge;
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        var g=AerialCombatPrototype.I; if(!g || g.paused) return;
        ArenaPilot occupant=null; int count=0; ownerAge+=dt;
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
                }
                if(occupant.cargo>0){occupant.score+=occupant.cargo;occupant.cargo=0;}
                occupant.health=Mathf.Min(100,occupant.health+dt*10);
            }
        }
        else if(count==0) progress=Mathf.Max(0,progress-dt*.6f);
        // Two or more competing pilots contest the zone: progress and banking stop.
        Color color=count>1?new Color(1,.18f,.08f):owner==0?new Color(.1f,1,.8f):owner<0?new Color(.1f,.7f,1):new Color(1,.35f,.13f);
        if(colors==null)colors=new MaterialPropertyBlock();
        colors.SetColor("_BaseColor",color);
        if(ring)ring.SetPropertyBlock(colors);
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
    bool spent;
    public static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {
        Vector3 d=b-a; return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));
    }
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float dt)
    {
        if(spent)return;
        var g=AerialCombatPrototype.I; if(!g){Destroy(gameObject);return;} if(g.paused)return;
        life-=dt;
        if(life<=0){spent=true;Destroy(gameObject);return;}
        if(seeker && target) velocity=Vector3.RotateTowards(velocity.normalized,(target.position-transform.position).normalized,dt*2.3f,0)*260;
        Vector3 a=transform.position,b=a+velocity*dt;
        transform.position=b;transform.rotation=Quaternion.LookRotation(velocity);
        if(!g.VisibleBetween(a,b)){spent=true;Destroy(gameObject);return;}
        // Resolve nearest intersection along the movement segment, avoiding frame-rate tunneling.
        float nearest=float.MaxValue; ArenaPilot pilot=null; SalvageCore core=null;
        foreach(var p in g.pilots)
        {
            if(p==owner || !p.Alive || p.invulnerable>0)continue;
            float along=Vector3.Dot(p.transform.position-a,velocity.normalized);
            if(along>=-5 && along<nearest && SegmentDistance(p.transform.position,a,b)<4)
            {nearest=along;pilot=p;core=null;}
        }
        foreach(var c in g.cores)
        {
            if(!c.Available)continue;
            float along=Vector3.Dot(c.transform.position-a,velocity.normalized);
            if(along>=-9 && along<nearest && SegmentDistance(c.transform.position,a,b)<9)
            {nearest=along;core=c;pilot=null;}
        }
        if(pilot){spent=true;g.Damage(pilot,seeker?60:12,owner);Destroy(gameObject);}
        else if(core){spent=true;core.Hit(seeker?80:18,owner);Destroy(gameObject);}
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
