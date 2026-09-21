using System.Collections.Generic;
using UnityEngine;

public enum MatchPhase { Countdown, Playing, Ended }

public partial class AerialCombatPrototype
{
    public const float CountdownLength=3, MatchLength=300;
    // A surge every 75 s lasting 30 s: long enough to cross a continent of sky, short enough that nobody camps it.
    public const float OverchargeInterval=75, OverchargeLength=30, OverchargeReach=1500;
    public MatchPhase phase=MatchPhase.Countdown;
    public float phaseTimer, overchargeTimer;
    readonly List<CaptureGate> surgeCandidates=new List<CaptureGate>();
    // Scoring verbs only resolve while Playing: Countdown gives the player a beat to read the horizon, Ended freezes the board for the panel.
    public bool MatchActive => phase==MatchPhase.Playing;
    public float MatchRemaining => phase==MatchPhase.Playing?Mathf.Max(0,MatchLength-phaseTimer):(phase==MatchPhase.Countdown?MatchLength:0);
    int heartbeatMark=-1;
    // Last whole second of the countdown that has already been sounded; -1 so the first frame fires the "3" pip.
    int countdownMark=-1;

    // Pure wall-clock state machine: it owns no simulation state, so it lives in Update beside the other timers instead of FixedUpdate.
    public void MatchTick(float dt)
    {
        phaseTimer+=dt;
        if(phase==MatchPhase.Countdown)
        {
            // One pip per numeral, each a step higher than the last, then a fifth above the lot of them on GO. The stored
            // mark is what stops a slow frame sounding the same second twice, exactly like the heartbeat below.
            int tick=Mathf.CeilToInt(CountdownLength-phaseTimer);
            if(tick!=countdownMark){countdownMark=tick;if(tick>0)Cue(countPip,0,1+(CountdownLength-tick)*.12f,.4f);}
            if(phaseTimer>=CountdownLength){phase=MatchPhase.Playing;phaseTimer=0;heartbeatMark=-1;countdownMark=-1;Cue(countPip,0,1.5f,.55f);}
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
            overchargeTimer+=dt;
            if(overchargeTimer>=OverchargeInterval){overchargeTimer-=OverchargeInterval;TriggerOvercharge();}
            // A four-note fanfare under the results panel: the only cue in the game that is allowed to be slow.
            if(phaseTimer>=MatchLength){phase=MatchPhase.Ended;phaseTimer=0;Banner("MATCH COMPLETE");Chord(stingNote,.22f,.55f,1,1.25f,1.5f,2);}
        }
    }

    public CaptureGate OverchargedGate()
    {
        foreach(var gate in gates) if(gate.overcharge>0) return gate;
        return null;
    }

    // Picks the refinery the surge should land on, or none. Returns whether one was lit so the harness can assert the exclusion.
    public bool TriggerOvercharge()
    {
        int leader=-1,best=int.MinValue;
        foreach(var p in pilots) if(p.score>best){best=p.score;leader=p.id;}
        surgeCandidates.Clear();
        foreach(var gate in gates)
        {
            // Never reward the pilot already ahead, and never light a ring nobody can reach inside the 30 s window.
            if(gate.overcharge>0 || (gate.owner>=0 && gate.owner==leader)) continue;
            float nearest=float.MaxValue;
            foreach(var p in pilots) if(p.Alive) nearest=Mathf.Min(nearest,Vector3.Distance(p.transform.position,gate.transform.position));
            if(nearest>OverchargeReach) continue;
            surgeCandidates.Add(gate);
        }
        if(surgeCandidates.Count==0) return false;
        var pick=surgeCandidates[Random.Range(0,surgeCandidates.Count)];
        pick.overcharge=OverchargeLength;
        Banner("OVERCHARGE   REFINERY "+(pick.index+1));
        Toast("OVERCHARGE  REFINERY "+(pick.index+1),new Color(.82f,.44f,1));
        Feedback("large",pick.transform.position);
        return true;
    }

    public void RestartMatch()
    {
        foreach(var p in pilots)
        {
            p.score=0;p.cargo=0;p.kills=0;p.deaths=0;p.streak=0;
            p.shotsFired=0;p.combatShotsFired=0;p.hitsLanded=0;
            Spawn(p,true);
        }
        foreach(var gate in gates){gate.owner=-1;gate.claimant=-1;gate.progress=0;gate.ownerAge=0;gate.payoutFlash=0;gate.overcharge=0;}
        // Loose salvage and broken wrecks are board state too: a restart that left them lying around would hand the first lap away.
        for(int i=shards.Count-1;i>=0;i--) if(shards[i]) Destroy(shards[i].gameObject);
        shards.Clear();
        foreach(var core in cores){core.cooldown=0;core.health=core.maxHealth;if(core.art)core.art.gameObject.SetActive(true);}
        elapsed=0;bannerTimer=0;damageFlash=0;lastAttackAge=0;shake=0;aceId=-1;bountyFresh=0;
        // The information layer is board state too: a restart that kept last match's feed would open on somebody else's kills.
        toasts.Clear();cargoPop=0;bankPop=0;hitPop=0;hitGold=0;preciseTag=0;targetLock=0;hudTarget=null;
        missileRange=-1;missileBeep=0;overheated=false;lockBeep=0;
        phase=MatchPhase.Countdown;phaseTimer=0;overchargeTimer=0;heartbeatMark=-1;countdownMark=-1;
        // A queued chime from last match's final claim would land over the new countdown; the note queue is board state too.
        notes.Clear();
    }
}
