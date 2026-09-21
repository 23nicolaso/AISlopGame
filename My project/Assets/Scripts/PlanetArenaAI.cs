using UnityEngine;

public partial class ArenaPilot
{
    public string tactic="Salvage";
    public ArenaPilot CombatTarget => rivalTarget;
    ArenaPilot aggressor;
    float retaliation, engagement, combatRest, breakTime, burstTime, burstRest;
    Vector3 breakDirection;
    bool repairing;

    void ResetTactics()
    {
        aggressor=null;retaliation=engagement=combatRest=breakTime=burstTime=burstRest=0;
        tactic="Salvage";
        repairing=false;
    }
    public void NotifyAttacked(ArenaPilot attacker)
    {
        if(isPlayer || !attacker || attacker==this)return;
        aggressor=attacker;retaliation=9;decision=0;combatRest=0;
    }
    bool CanEngage(ArenaPilot other,float range)
    {
        return other && other!=this && other.Alive && other.invulnerable<=0 &&
            Vector3.Distance(transform.position,other.transform.position)<range &&
            AerialCombatPrototype.I.VisibleBetween(transform.position,other.transform.position);
    }

    void FlyTactics(float dt)
    {
        var arena=AerialCombatPrototype.I;
        retaliation=Mathf.Max(0,retaliation-dt);combatRest=Mathf.Max(0,combatRest-dt);
        breakTime=Mathf.Max(0,breakTime-dt);burstRest=Mathf.Max(0,burstRest-dt);
        if(rivalTarget)
        {
            engagement-=dt;
            if(!CanEngage(rivalTarget,950) || engagement<=0)
            {rivalTarget=null;combatRest=5;decision=0;}
        }
        decision-=dt;
        if(decision<=0)
        {
            decision=.28f+id*.025f;
            bool hurt=health<34;
            if(hurt)repairing=true;
            if(health>=85)repairing=false;
            gateTarget=(cargo>=35 || repairing)?arena.NearestGate(transform.position):null;
            if(gateTarget && cargo==0 && health>=85)gateTarget=null;
            bool retaliate=!repairing && retaliation>0 && CanEngage(aggressor,900);
            if(retaliate)
            {
                if(rivalTarget!=aggressor)engagement=9;
                rivalTarget=aggressor;gateTarget=null;
            }
            else if(gateTarget)rivalTarget=null;
            else if(!rivalTarget && combatRest<=0)
            {
                float best=float.MaxValue;
                foreach(var other in arena.pilots)
                {
                    if(!CanEngage(other,id%3==0?720:470))continue;
                    float rating=Vector3.Distance(transform.position,other.transform.position)-Mathf.Min(other.cargo,100)*1.5f;
                    if(rating<best){best=rating;rivalTarget=other;}
                }
                if(rivalTarget)engagement=8+id*.4f;
            }
            shardTarget=null;float nearest=420;
            foreach(var shard in arena.shards)
            {
                if(!shard || shard.claimed)continue;
                float d=Vector3.Distance(transform.position,shard.transform.position);
                if(d<nearest && arena.VisibleBetween(transform.position,shard.transform.position))
                {nearest=d;shardTarget=shard;}
            }
            coreTarget=arena.NearestCore(transform.position);
        }

        Vector3 up=AerialCombatPrototype.Up(transform.position);
        tactic=gateTarget?"Bank / repair":rivalTarget?"Intercept":shardTarget?"Collect":"Salvage";
        if(gateTarget)navigation=gateTarget.transform.position;
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
        Quaternion desired=Quaternion.LookRotation(direction,Quaternion.AngleAxis(bank,direction)*safeUp.normalized);
        transform.rotation=Quaternion.RotateTowards(transform.rotation,desired,dt*(recover?105:80));
        throttle=recover?1:gateTarget?.42f:rivalTarget?(distance>240?.95f:.58f):.72f;
        boost=recover && Speed<80 && fuel>.25f;

        // Explicit target selection prevents a nearby wreck stealing a combat shot.
        Transform shootTarget=rivalTarget?rivalTarget.transform:(!gateTarget && !shardTarget && coreTarget && coreTarget.Available?coreTarget.transform:null);
        Vector3 targetVelocity=rivalTarget?rivalTarget.velocity:Vector3.zero;
        if(!recover && breakTime<=0 && shootTarget)
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
