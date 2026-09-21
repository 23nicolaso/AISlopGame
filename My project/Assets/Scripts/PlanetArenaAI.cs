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
    public ArenaPersonality Profile => ArenaPersonality.For(id);
    ArenaPilot aggressor, alertTarget;
    float retaliation, engagement, combatRest, breakTime, burstTime, burstRest, alertTimer, searchTimer;
    Vector3 breakDirection, searchDirection;
    bool repairing;
    float Reaction => Profile.reaction;
    const float ConeHalfAngle=55;   // 110 degree forward cone
    const float PeripheralRange=250, SightRange=950, HearingRange=600;

    void ResetTactics()
    {
        aggressor=null;alertTarget=null;
        retaliation=engagement=combatRest=breakTime=burstTime=burstRest=alertTimer=searchTimer=0;
        searchDirection=Vector3.zero;
        tactic="Salvage";
        repairing=false;
    }
    public void NotifyAttacked(ArenaPilot attacker)
    {
        if(isPlayer || !attacker || attacker==this)return;
        aggressor=attacker;retaliation=Profile.revenge;decision=0;combatRest=0;
        Vector3 toward=attacker.transform.position-transform.position;
        if(toward.sqrMagnitude<.01f)return;
        searchDirection=toward.normalized;
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
            if(alertTimer<=0){if(CanEngage(alertTarget,SightRange)){rivalTarget=alertTarget;engagement=8+id*.4f;decision=0;}alertTarget=null;}
        }
        if(rivalTarget)
        {
            engagement-=dt;
            bool lost=!CanEngage(rivalTarget,SightRange);
            if(lost || engagement<=0)
            {
                // Lost contact is a search, not an instant shrug: fly the last known bearing for three seconds first.
                Vector3 last=rivalTarget.transform.position-transform.position;
                if(lost && last.sqrMagnitude>.01f){searchDirection=last.normalized;searchTimer=3;}
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
            // A doubling refinery overrides the personality's patience: even a hoarder will cash 15 units at 2x rather than wait.
            var surge=arena.OverchargedGate();
            gateTarget=surge && cargo>=15?surge:(cargo>=Profile.bankAt || repairing)?arena.NearestGate(transform.position):null;
            if(gateTarget && cargo==0 && health>=85)gateTarget=null;
            // Retaliation also waits out the reaction delay, otherwise a blind-side hit would be answered instantly.
            bool retaliate=!repairing && retaliation>0 && alertTimer<=0 && CanEngage(aggressor,900);
            if(retaliate)
            {
                if(rivalTarget!=aggressor)engagement=9;
                rivalTarget=aggressor;gateTarget=null;
            }
            else if(gateTarget)rivalTarget=null;
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
                        if(!alertTarget){alertTarget=other;alertTimer=Reaction;searchTimer=3;searchDirection=(other.transform.position-transform.position).normalized;}
                        continue;
                    }
                    if(d>reach)continue;
                    // The whole field converges on the bounty without any scripted rubber band: double greed on what the ace
                    // is carrying, plus a flat pull so an ace who just banked is still worth more than a fat bystander.
                    bool bounty=arena.aceId==other.id;
                    float rating=d-Mathf.Min(other.cargo,100)*Profile.greed*(bounty?2:1)-(bounty?300:0);
                    if(rating<best){best=rating;rivalTarget=other;}
                }
                if(rivalTarget){engagement=8+id*.4f;alertTarget=null;alertTimer=0;}
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
        tactic=alert?"Alert":gateTarget?"Bank / repair":rivalTarget?"Intercept":searching?"Search":shardTarget?"Collect":"Salvage";
        if(alert)navigation=transform.position+searchDirection*400;
        else if(searching)
        {
            // Weave across the last known bearing rather than flying a straight line past it.
            Vector3 sweep=Quaternion.AngleAxis(Mathf.Sin((3-searchTimer)*2.2f)*38,up)*searchDirection;
            navigation=transform.position+sweep*400;
        }
        else if(gateTarget)navigation=gateTarget.transform.position;
        else if(rivalTarget)navigation=AerialCombatPrototype.InterceptPoint(this,rivalTarget.transform.position,rivalTarget.velocity,360);
        else if(shardTarget)navigation=shardTarget.transform.position;
        else if(coreTarget && coreTarget.Available)navigation=coreTarget.transform.position;
        else navigation=arena.gates[id%arena.gates.Count].transform.position;

        Vector3 delta=navigation-transform.position;
        float distance=delta.magnitude;
        if(!arena.VisibleBetween(transform.position,navigation))
            delta=Vector3.ProjectOnPlane(delta,up).normalized*300+up*Mathf.Max(35,300-Altitude);
        // Commit to a short exit instead of trying to reverse on top of a target.
        if(rivalTarget && breakTime<=0 && distance<85)
        {
            breakTime=1.7f;
            breakDirection=(transform.forward+up*.35f+transform.right*(id%2==0?.4f:-.4f)).normalized;
        }
        if(!rivalTarget || gateTarget)breakTime=0;
        if(breakTime>0){delta=breakDirection*250;tactic="Extend";}
        if(gateTarget && distance<40)delta=transform.forward*150;
        float descent=Mathf.Max(0,-Vector3.Dot(velocity,up));
        bool recover=Altitude<85+descent*1.8f || AerialCombatPrototype.Altitude(transform.position+velocity*1.4f)<40;
        recovering=recover;
        if(recover)
        {
            Vector3 tangent=Vector3.ProjectOnPlane(velocity,up).normalized;
            if(tangent.sqrMagnitude<.1f)tangent=Vector3.ProjectOnPlane(transform.forward,up).normalized;
            delta=tangent*170+up*240;tactic="Terrain recovery";
        }
        Vector3 direction=delta.sqrMagnitude>.01f?delta.normalized:transform.forward;
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
        // Degrees of error that already demand full deflection; a pull-up commits harder than a dogfight correction.
        float gain=recover?12:22;
        controls=new Vector3(Mathf.Clamp(pitchError/gain,-1,1),Mathf.Clamp(yawError/(gain*1.3f),-1,1),Mathf.Clamp(rollError/(gain*1.6f),-1,1));
        throttle=recover?1:gateTarget?.42f:rivalTarget?(distance>240?.95f:.58f):.72f;
        boost=recover && Speed<80 && fuel>.25f;

        // Explicit target selection prevents a nearby wreck stealing a combat shot.
        Transform shootTarget=rivalTarget?rivalTarget.transform:(!gateTarget && !shardTarget && coreTarget && coreTarget.Available?coreTarget.transform:null);
        Vector3 targetVelocity=rivalTarget?rivalTarget.velocity:Vector3.zero;
        // Alert means the contact has been noticed but not yet processed: turn onto it, hold fire.
        if(!recover && breakTime<=0 && alertTimer<=0 && shootTarget)
        {
            Vector3 aim=AerialCombatPrototype.InterceptPoint(this,shootTarget.position,targetVelocity,360)-transform.position;
            bool linedUp=aim.magnitude<600 && Vector3.Angle(transform.forward,aim)<7 && arena.VisibleBetween(transform.position,shootTarget.position);
            if(linedUp && burstRest<=0)
            {
                burstTime+=dt;
                arena.Shoot(this,false,shootTarget);
                if(burstTime>.65f){burstTime=0;burstRest=.6f+id*.04f;}
            }
            else if(!linedUp)burstTime=0;
        }
        if(gateTarget && cargo==0 && health>=85){gateTarget=null;decision=0;}
    }
}
