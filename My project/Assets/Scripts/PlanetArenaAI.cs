using UnityEngine;

// Seven rivals, seven read-only dispositions. Every AI number that used to be derived from the id lives here instead,
// so a callsign on the leaderboard predicts how that aircraft flies: who hunts, who hoards, who never forgets a hit.
public struct ArenaPersonality
{
    public string tag;
    public float aggression, bankAt, aimJitter, reaction, greed, revenge;
    ArenaPersonality(string tag,float aggression,float bankAt,float aimJitter,float reaction,float greed,float revenge)
    {
        this.tag=tag;this.aggression=aggression;this.bankAt=bankAt;this.aimJitter=aimJitter;
        this.reaction=reaction;this.greed=greed;this.revenge=revenge;
    }
    //                                           tag     reach  bank  jitter  react  greed  revenge
    static readonly ArenaPersonality[] table={
        new ArenaPersonality("SELF",                 0,     0,     0,     0,     1,     0),  // the player: nothing here steers a human
        new ArenaPersonality("HUNT",               800,    45,   .8f,  .30f,  1.2f,     9),  // Moth.exe: commits from further out than anyone and reacts first
        new ArenaPersonality("HORD",               350,    80,  1.8f,  .65f,   .8f,     6),  // Blue Finch: banks late, so it is usually the fattest target on the board
        new ArenaPersonality("VULT",               560,    40,  1.3f,  .50f,  2.5f,     8),  // Periapsis: picks targets almost purely by how much they are carrying
        new ArenaPersonality("STDY",               430,    25,  1.5f,  .55f,     1,     7),  // DustRunner: small frequent deposits, hard to break away from
        new ArenaPersonality("AVNG",               520,    45,  1.6f,  .90f,  1.1f,    15),  // Kite-09: slowest to notice, longest to hold a grudge
        new ArenaPersonality("ROOK",               450,    30,     3,  .85f,   .6f,     5),  // SoupDragon: the first kill the arena hands a new player
        new ArenaPersonality("ELIT",               700,    50,   .5f,  .35f,  1.6f,    11)}; // Last Comet: no weak axis
    public static ArenaPersonality For(int id) => table[Mathf.Clamp(id,0,table.Length-1)];
}

public partial class ArenaPilot
{
    public string tactic="Salvage";
    public ArenaPilot CombatTarget => rivalTarget;
    // Read-only views for the balance harness and diagnostics: where the pilot is steering and what it is mining.
    public Vector3 Navigation => navigation;
    public SalvageCore CoreTarget => coreTarget;
    public CaptureGate GateTarget => gateTarget;
    public ArenaPersonality Profile => ArenaPersonality.For(autopilot>=0?autopilot:id);
    ArenaPilot aggressor, alertTarget;
    float retaliation, engagement, combatRest, breakTime, burstTime, burstRest, alertTimer, searchTimer;
    Vector3 breakDirection, searchDirection, lastKnownPosition;
    bool repairing;
    int reversalSign;
    Vector3 runIn;
    float Reaction => Profile.reaction;
    const float ConeHalfAngle=55;   // 110 degree forward cone
    const float PeripheralRange=250, SightRange=950, HearingRange=600;

    void ResetTactics()
    {
        aggressor=null;alertTarget=null;
        retaliation=engagement=combatRest=breakTime=burstTime=burstRest=alertTimer=searchTimer=0;reversalSign=0;runIn=Vector3.zero;
        searchDirection=Vector3.zero;lastKnownPosition=Vector3.zero;
        tactic="Salvage";
        repairing=false;
    }
    public void NotifyAttacked(ArenaPilot attacker)
    {
        if(isPlayer || !attacker || attacker==this)return;
        aggressor=attacker;retaliation=Profile.revenge;decision=0;combatRest=0;
        Vector3 toward=attacker.transform.position-transform.position;
        if(toward.sqrMagnitude<.01f)return;
        searchDirection=toward.normalized;lastKnownPosition=attacker.transform.position;
        // Shot from outside the cone: the pilot only knows a bearing, and needs a beat to turn and find the shooter.
        if(Vector3.Angle(transform.forward,searchDirection)>ConeHalfAngle && alertTimer<=0)
        {alertTarget=attacker;alertTimer=Reaction;searchTimer=3;}
    }
    // 0 = unaware, 1 = something is out there, 2 = acquired.
    int Sense(ArenaPilot other)
    {
        if(!other || other==this || !other.Alive || other.invulnerable>0)return 0;
        Vector3 delta=other.transform.position-transform.position;
        float d=delta.magnitude;
        if(d>SightRange || !AerialCombatPrototype.I.VisibleBetween(transform.position,other.transform.position))return 0;
        if(d<PeripheralRange || Vector3.Angle(transform.forward,delta)<ConeHalfAngle)return 2;
        return other.firedRecently>0 && d<HearingRange?1:0;
    }
    bool CanEngage(ArenaPilot other,float range)
    {
        return Sense(other)==2 && Vector3.Distance(transform.position,other.transform.position)<range;
    }

    void FlyTactics(float dt)
    {
        var arena=AerialCombatPrototype.I;
        retaliation=Mathf.Max(0,retaliation-dt);combatRest=Mathf.Max(0,combatRest-dt);
        breakTime=Mathf.Max(0,breakTime-dt);burstRest=Mathf.Max(0,burstRest-dt);
        searchTimer=Mathf.Max(0,searchTimer-dt);
        if(alertTimer>0)
        {
            alertTimer=Mathf.Max(0,alertTimer-dt);
            // The delay has run out: commit to the contact if it is genuinely in sight now, otherwise keep sweeping.
            if(alertTimer<=0){if(CanEngage(alertTarget,SightRange)){rivalTarget=alertTarget;engagement=12+id*.4f;decision=0;}alertTarget=null;}
        }
        if(rivalTarget)
        {
            engagement-=dt;
            bool lost=!CanEngage(rivalTarget,SightRange);
            if(lost || engagement<=0)
            {
                // Lost contact is a search, not an instant shrug: fly back to the last known position for three seconds first.
                Vector3 last=rivalTarget.transform.position-transform.position;
                if(lost && last.sqrMagnitude>.01f){searchDirection=last.normalized;lastKnownPosition=rivalTarget.transform.position;searchTimer=3;}
                else combatRest=5;
                rivalTarget=null;decision=0;
            }
        }
        decision-=dt;
        if(decision<=0)
        {
            decision=.28f+id*.025f;
            bool hurt=health<34;
            if(hurt)repairing=true;
            if(health>=85)repairing=false;
            // An unresolved grudge or an unprocessed contact holds off banking entirely: a pilot with cargo to cash does not
            // quietly divert to a refinery mid-vendetta. Without this, gateTarget wins every decision tick (cargo>=bankAt
            // stays true) and permanently short-circuits both retaliation and the opportunistic scan below, forever.
            bool avenging=!repairing && (retaliation>0 || alertTimer>0);
            var surge=arena.OverchargedGate();
            // The ring is latched once chosen: with fields 670 m apart two rings are often equidistant, and re-picking the
            // nearest every 0.3 s had laden pilots zig-zagging between a 170 m ring and a 460 m ring for minutes.
            gateTarget=avenging?null:surge && cargo>=15?surge:(cargo>=Profile.bankAt || repairing)?(gateTarget?gateTarget:arena.NearestGate(transform.position)):null;
            if(gateTarget && cargo==0 && health>=85)gateTarget=null;
            // Retaliation also waits out the reaction delay, otherwise a blind-side hit would be answered instantly.
            bool retaliate=!repairing && retaliation>0 && alertTimer<=0 && CanEngage(aggressor,900);
            if(retaliate)
            {
                if(rivalTarget!=aggressor)engagement=9;
                rivalTarget=aggressor;gateTarget=null;
            }
            // No unconditional "gateTarget means give up the fight" branch here: an already-engaged rivalTarget (from
            // this retaliation or from the opportunistic scan below) lives or dies by its own engagement/CanEngage
            // checks above, not by whether the retaliation clock happens to run out on the same tick.
            else if(!rivalTarget && combatRest<=0 && alertTimer<=0)
            {
                float best=float.MaxValue,reach=Profile.aggression;
                foreach(var other in arena.pilots)
                {
                    int sense=Sense(other);
                    if(sense==0)continue;
                    float d=Vector3.Distance(transform.position,other.transform.position);
                    // Heard but not seen: turn toward the noise and take the reaction delay before committing.
                    if(sense==1)
                    {
                        if(!alertTarget){alertTarget=other;alertTimer=Reaction;searchTimer=3;searchDirection=(other.transform.position-transform.position).normalized;lastKnownPosition=other.transform.position;}
                        continue;
                    }
                    if(d>reach)continue;
                    // The whole field converges on the bounty without any scripted rubber band: double greed on what the ace
                    // is carrying, plus a flat pull so an ace who just banked is still worth more than a fat bystander.
                    bool bounty=arena.aceId==other.id;
                    float rating=d-Mathf.Min(other.cargo,100)*Profile.greed*(bounty?2:1)-(bounty?300:0);
                    if(rating<best){best=rating;rivalTarget=other;}
                }
                if(rivalTarget){engagement=12+id*.4f;alertTarget=null;alertTimer=0;}
            }
            shardTarget=null;float nearest=420;
            foreach(var shard in arena.shards)
            {
                if(!shard || shard.claimed)continue;
                float d=Vector3.Distance(transform.position,shard.transform.position);
                if(d<nearest && arena.VisibleBetween(transform.position,shard.transform.position))
                {nearest=d;shardTarget=shard;}
            }
            // A volatile reactor inside its own blast radius is not salvage, it is a bomb under the nose. A vulture
            // (greed >= 2) accepts a far tighter margin, which is how it ends up detonating one on top of a rival.
            float standoff=Profile.greed>=2?25:60;
            coreTarget=null;float closest=float.MaxValue;
            foreach(var core in arena.cores)
            {
                if(!core.Available)continue;
                float d=Vector3.Distance(transform.position,core.transform.position);
                if(core.kind==CoreKind.Volatile && d<standoff)continue;
                if(d<closest){closest=d;coreTarget=core;}
            }
        }

        Vector3 up=AerialCombatPrototype.Up(transform.position);
        bool alert=alertTimer>0 && searchDirection.sqrMagnitude>.01f;
        bool searching=!alert && !rivalTarget && searchTimer>0 && searchDirection.sqrMagnitude>.01f;
        // Combat outranks banking: a rival actively being intercepted must not lose the chase to a mid-fight cargo run.
        tactic=alert?"Alert":rivalTarget?"Intercept":gateTarget?"Bank / repair":searching?"Search":shardTarget?"Collect":"Salvage";
        // Fly to the remembered world position, not a bearing that drifts with the pilot's own momentum: a target
        // glimpsed once while coasting the wrong way must still be something the pilot can turn back toward.
        if(!gateTarget)runIn=Vector3.zero;
        if(alert)navigation=lastKnownPosition;
        else if(searching)
        {
            // Converge on the last known position, then weave laterally across it instead of sitting on top of it.
            Vector3 toward=lastKnownPosition-transform.position;
            Vector3 lateral=Vector3.Cross(up,toward.sqrMagnitude>1?toward.normalized:transform.forward);
            navigation=lastKnownPosition+lateral*Mathf.Sin((3-searchTimer)*2.2f)*120;
        }
        else if(rivalTarget)navigation=AerialCombatPrototype.InterceptPoint(this,rivalTarget.transform.position,rivalTarget.velocity,360);
        else if(gateTarget)
        {
            // Racing line into the ring. A turn at 100 m/s is 240 m wide in thick air and 640 m at the 460 m fields, so
            // chasing a 105 m sphere from inside that circle only orbits it: the bank sampler had laden pilots circling
            // a ring at 200-600 m for two minutes. Off the nose and close, keep going straight to build room, then turn
            // once and fly the ring in a straight run.
            // The run-in point is latched, not recomputed: a stateless "close and off the nose" test flipped every time
            // the turn crossed 450 m and orbited the ring at 160-660 m for a whole match.
            Vector3 toGate=gateTarget.transform.position-transform.position;
            float gateAngle=Vector3.Angle(transform.forward,toGate);
            if(runIn==Vector3.zero && toGate.magnitude<450 && gateAngle>35)runIn=transform.position+transform.forward*600;
            if(runIn!=Vector3.zero && (Vector3.Distance(transform.position,runIn)<90 || (gateAngle<20 && toGate.magnitude>450)))runIn=Vector3.zero;
            navigation=runIn!=Vector3.zero?runIn:gateTarget.transform.position;
        }
        else if(shardTarget)navigation=shardTarget.transform.position;
        else if(coreTarget && coreTarget.Available)navigation=coreTarget.transform.position;
        else
        {
            // Idle hunters (reach >= 500) patrol toward the nearest rival instead of parking at a home ring: telemetry had
            // the seven rivals spread one per site with 950 m of sight, which is a planet of strangers, not a match.
            ArenaPilot prey=null;float near=2500;
            if(Profile.aggression>=500)foreach(var other in arena.pilots){if(other==this || !other.Alive || other.invulnerable>0)continue;float d=Vector3.Distance(transform.position,other.transform.position);if(d<near){near=d;prey=other;}}
            navigation=prey?prey.transform.position:arena.gates[id%arena.gates.Count].transform.position;
        }

        Vector3 delta=navigation-transform.position;
        float distance=delta.magnitude;
        if(!arena.VisibleBetween(transform.position,navigation))
            // Over-the-horizon target: climb a little at low level to see it, but above the fields descend toward it —
            // a permanent 35 m/300 m nose-up in vacuum is how every rival ended up at the 6500 m ceiling.
            delta=Vector3.ProjectOnPlane(delta,up).normalized*300+up*(Altitude>400?-140:Mathf.Max(35,300-Altitude));
        float descent=Mathf.Max(0,-Vector3.Dot(velocity,up));
        float density=AerialCombatPrototype.Density(Altitude);
        // Balance telemetry (docs/balance) showed the old 85+1.8*descent / 1.4 s horizon arming at ~240 m against an
        // 85 m/s sink: at half density the pull-up needs ~3 s and 250 m, so the aircraft hit the ground still pitching.
        bool recover=Altitude<85+descent*2.6f || AerialCombatPrototype.Altitude(transform.position+velocity*2.2f)<40;
        recovering=recover;

        // Fire BEFORE the close-range break-off is armed: the pass that finally closes inside 85 m is exactly the pass
        // with the cleanest shot, and gating on the same breakTime the trigger below is about to set would deny that
        // one frame every time, so a fighter could reach point-blank range and break away having never fired a shot.
        Transform shootTarget=rivalTarget?rivalTarget.transform:(!gateTarget && !shardTarget && coreTarget && coreTarget.Available?coreTarget.transform:null);
        Vector3 targetVelocity=rivalTarget?rivalTarget.velocity:Vector3.zero;
        if(!recover && breakTime<=0 && alertTimer<=0 && shootTarget)
        {
            Vector3 aim=AerialCombatPrototype.InterceptPoint(this,shootTarget.position,targetVelocity,360)-transform.position;
            // 7 degrees was a laser-rotation-era constant: the shared bank/pitch/yaw flight model tops out its best
            // intercept angle right around 7 on a curved closing pass (measured empirically), so the old threshold
            // could sit forever just outside a shot that was, in every practical sense, already lined up.
            // 14 degrees, matched to Shoot()'s gimbal: two matches of telemetry had hunters holding a target in range for
            // 70-100 s and squeezing off under six seconds of fire, because a 9 degree cone on a curving pursuit is a
            // window that opens for a frame or two per pass. The round is still aimed at the intercept point.
            bool linedUp=aim.magnitude<600 && Vector3.Angle(transform.forward,aim)<14 && arena.VisibleBetween(transform.position,shootTarget.position);
            if(linedUp && burstRest<=0)
            {
                burstTime+=dt;
                arena.Shoot(this,false,shootTarget);
                if(burstTime>.65f){burstTime=0;burstRest=.6f+id*.04f;}
            }
            else if(!linedUp)burstTime=0;
        }

        // Commit to a short exit instead of trying to reverse on top of a target.
        if(rivalTarget && breakTime<=0 && distance<85)
        {
            breakTime=1.7f;
            breakDirection=(transform.forward+up*.35f+transform.right*(id%2==0?.4f:-.4f)).normalized;
        }
        if(!rivalTarget)breakTime=0;
        if(breakTime>0){delta=breakDirection*250;tactic="Extend";}
        // Final-approach nudge only applies when the gate is actually the navigation target this frame.
        if(gateTarget && !rivalTarget && !alert && !searching && distance<40)delta=transform.forward*150;
        if(recover)
        {
            Vector3 tangent=Vector3.ProjectOnPlane(velocity,up).normalized;
            if(tangent.sqrMagnitude<.1f)tangent=Vector3.ProjectOnPlane(transform.forward,up).normalized;
            // Pull only as hard as the sink demands: a fixed 55 degree climb at full burner arrested the dive and then
            // launched the aircraft — the balance runs' remaining launches all began as a terrain reflex.
            delta=tangent*170+up*Mathf.Lerp(50,240,Mathf.Clamp01(descent/70));tactic="Terrain recovery";
        }
        Vector3 direction=delta.sqrMagnitude>.01f?delta.normalized:transform.forward;
        // Dive limiter: gravity already supplies the descent in thin air, so the nose never points more than ~17 degrees
        // below the horizon on the way to a lower target. Telemetry had rivals arriving at 240 m with 85 m/s of sink.
        float down=Vector3.Dot(direction,up);
        float steepest=Altitude>800?-.85f:-.3f;
        if(!recover && down<steepest)direction=(Vector3.ProjectOnPlane(direction,up).normalized*Mathf.Sqrt(1-steepest*steepest)+up*steepest).normalized;
        Vector3 safeUp=Vector3.ProjectOnPlane(up,direction);
        if(safeUp.sqrMagnitude<.01f)safeUp=Vector3.ProjectOnPlane(transform.up,direction);
        if(safeUp.sqrMagnitude<.01f)safeUp=Vector3.ProjectOnPlane(transform.right,direction);
        float turn=Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward,up),Vector3.ProjectOnPlane(direction,up),up);
        float bank=recover?0:Mathf.Clamp(-turn*.7f,-55,55);
        // Attitude solved as stick deflection, not as a rotation: Simulate integrates it through the same authority the player gets.
        Vector3 desiredUp=Quaternion.AngleAxis(bank,direction)*safeUp.normalized;
        Quaternion inverse=Quaternion.Inverse(transform.rotation);
        Vector3 localDirection=inverse*direction,localUp=inverse*desiredUp;
        float pitchError=Mathf.Asin(Mathf.Clamp(localDirection.y,-1,1))*Mathf.Rad2Deg;       // positive: target sits above the nose
        float yawError=Mathf.Atan2(localDirection.x,localDirection.z)*Mathf.Rad2Deg;         // positive: target sits to the right
        float rollError=Mathf.Atan2(localUp.x,localUp.y)*Mathf.Rad2Deg;                      // positive: wings need to drop right
        // Diagnosed from a per-step trace: a target that has just passed behind the aircraft made yaw and roll saturate
        // and flip sign every step around 180 degrees, and the nose was thrown far off the horizon; at the 460 m fields
        // the velocity simply follows the nose and the aircraft went ballistic. The cure is the reversal hysteresis and
        // the nose band below — NOT a pitch clamp for targets astern: that was tried, and it turned a bank-and-pull
        // reversal into a rudder-only one, which at 320 m took 25 s to come around and broke retaliation outright.
        // Hysteresis on the reversal direction: keep turning the way we already are once the target is near dead astern.
        if(Mathf.Abs(yawError)>150 && reversalSign!=0)yawError=Mathf.Abs(yawError)*reversalSign;
        reversalSign=Mathf.Abs(yawError)>90?(int)Mathf.Sign(yawError):0;
        // Outside a terrain reflex the nose stays inside a horizon band that narrows with the air: 35 degrees in thick
        // air, ~18 at the 460 m fields, 8 in vacuum. Past it, thin air turns the flight model into a cannon.
        float noseElevation=Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.forward,up),-1,1))*Mathf.Rad2Deg;
        float climb=Vector3.Dot(velocity,up);
        float maxNose=Mathf.Lerp(8,35,Mathf.Clamp01(density/.5f));
        if(!recover)
        {
            if(noseElevation>maxNose)pitchError=Mathf.Min(pitchError,maxNose-noseElevation);
            else if(noseElevation<-35)pitchError=Mathf.Max(pitchError,-35-noseElevation);
            // Energy rule: the height this climb rate will still gain with the engine off must not overshoot the target's
            // altitude by more than 40 m. Launch traces showed 145 m/s aircraft pitching 35 degrees at a 460 m ring and
            // coasting to 1600 m, because nothing up there bleeds speed. Applies to intercepts too: no zoom past the prey.
            // Not while intercepting: a replay of the projectile check showed the rule pushing the nose down on a climbing
            // gun pass at a target 120 m above, so the aim never closed inside 14 degrees. Fights need the vertical.
            float zoom=climb>0?climb*climb/(2*AerialCombatPrototype.Gravity(transform.position)):0;
            if(!rivalTarget && zoom>Mathf.Max(0,AerialCombatPrototype.Altitude(navigation)-Altitude)+40)pitchError=Mathf.Min(pitchError,-12);
        }
        // Degrees of error that already demand full deflection; a pull-up commits harder than a dogfight correction.
        float gain=recover?12:22;
        controls=new Vector3(Mathf.Clamp(pitchError/gain,-1,1),Mathf.Clamp(yawError/(gain*1.3f),-1,1),Mathf.Clamp(rollError/(gain*1.6f),-1,1));
        // Same outranking as tactic/navigation: an engaged or alerted pilot never throttles down to the lazy banking speed.
        // Full power in a pull-up only while still sinking; once the nose is above the horizon the speed is the danger.
        throttle=recover?(descent>0?1:.6f):alert?.72f:rivalTarget?(distance>240?.95f:.58f):gateTarget?.42f:.72f;
        // Level flight at density .2 (the 460 m fields) needs ~190 m/s; the lazy .42 banking throttle there is a
        // slow-motion fall the pilot never notices until the pull-up. Floor the throttle on thinness: nothing changes
        // below 170 m, full power from ~400 m up, and the burner arrests a sink that thrust alone cannot.
        // Only while actually sinking: an unconditional floor in near-zero drag runs away to the 6500 m ceiling (one
        // balance run lost 46 aircraft to it). Above the coast line and climbing, coast instead; gravity brings them home.
        // The floor buys airspeed for lift, so it only makes sense with the nose near the horizon and inside the band
        // where the fields are; nose-down it would just steepen the dive. Climbing past the target's altitude, past
        // 650 m and climbing, or anywhere above 800 m, the engine is off: no drag up there, only gravity brings them home.
        if(climb<-15 && Altitude<650 && down>-.15f)throttle=Mathf.Max(throttle,Mathf.Clamp01((.5f-density)/.3f));
        else if(!recover && !rivalTarget && ((Altitude>650 && (climb>10 || Altitude>800)) || (climb>15 && Altitude>AerialCombatPrototype.Altitude(navigation)+60)))throttle=0;
        boost=fuel>.25f && ((recover && Altitude<350 && descent>0 && (Speed<80 || descent>45)) || (Altitude>280 && Altitude<650 && descent>35 && !rivalTarget));

        if(gateTarget && cargo==0 && health>=85){gateTarget=null;decision=0;}
    }
}
