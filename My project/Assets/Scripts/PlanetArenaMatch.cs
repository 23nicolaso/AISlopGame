using UnityEngine;

public enum MatchPhase { Countdown, Playing, Ended }

public partial class AerialCombatPrototype
{
    public const float CountdownLength=3, MatchLength=300;
    public MatchPhase phase=MatchPhase.Countdown;
    public float phaseTimer;
    // Scoring verbs only resolve while Playing: Countdown gives the player a beat to read the horizon, Ended freezes the board for the panel.
    public bool MatchActive => phase==MatchPhase.Playing;
    public float MatchRemaining => phase==MatchPhase.Playing?Mathf.Max(0,MatchLength-phaseTimer):(phase==MatchPhase.Countdown?MatchLength:0);
    int heartbeatMark=-1;

    // Pure wall-clock state machine: it owns no simulation state, so it lives in Update beside the other timers instead of FixedUpdate.
    public void MatchTick(float dt)
    {
        phaseTimer+=dt;
        if(phase==MatchPhase.Countdown)
        {
            if(phaseTimer>=CountdownLength){phase=MatchPhase.Playing;phaseTimer=0;heartbeatMark=-1;}
        }
        else if(phase==MatchPhase.Playing)
        {
            // One beat per whole ten seconds of the final minute; the stored mark stops a slow frame from firing it twice.
            int mark=Mathf.CeilToInt(MatchRemaining/10f);
            if(MatchRemaining<=60 && mark!=heartbeatMark)
            {
                heartbeatMark=mark;
                if(audioSource)audioSource.PlayOneShot(hitSound,MatchRemaining<=10?.5f:.26f);
            }
            if(phaseTimer>=MatchLength){phase=MatchPhase.Ended;phaseTimer=0;Banner("MATCH COMPLETE");}
        }
    }

    public void RestartMatch()
    {
        foreach(var p in pilots)
        {
            p.score=0;p.cargo=0;p.kills=0;p.deaths=0;
            p.shotsFired=0;p.combatShotsFired=0;p.hitsLanded=0;
            Spawn(p,true);
        }
        foreach(var gate in gates){gate.owner=-1;gate.claimant=-1;gate.progress=0;gate.ownerAge=0;gate.payoutFlash=0;}
        // Loose salvage and broken wrecks are board state too: a restart that left them lying around would hand the first lap away.
        for(int i=shards.Count-1;i>=0;i--) if(shards[i]) Destroy(shards[i].gameObject);
        shards.Clear();
        foreach(var core in cores){core.cooldown=0;core.health=65;if(core.art)core.art.gameObject.SetActive(true);}
        elapsed=0;bannerTimer=0;damageFlash=0;lastAttackAge=0;shake=0;
        phase=MatchPhase.Countdown;phaseTimer=0;heartbeatMark=-1;
    }
}
