using System;
using UnityEditor;
using UnityEngine;

public static class RiftVerification
{
    static void Check(bool c,string message){if(!c)throw new Exception("ARENA CHECK FAILED: "+message);}
    [MenuItem("Rift/Verify planetary arena")]
    static void Verify()
    {
        var g=AerialCombatPrototype.I;
        Check(EditorApplication.isPlaying && g!=null,"Enter Play Mode");
        bool wasPaused=g.paused;g.paused=false;
        // This harness calls Damage/Shoot/Collect/Tick directly, so it has to own the match phase for the duration.
        var oldPhase=g.phase;float oldPhaseTimer=g.phaseTimer;g.phase=MatchPhase.Playing;g.phaseTimer=0;
        var p=g.player;int oldScore=p.score;
        try
        {
            Check(g.pilots.Count==8 && g.cores.Count==24 && g.gates.Count==6,"Arena population");
            Check(AerialCombatPrototype.Density(700)<AerialCombatPrototype.Density(100)*.2f,"Thinning atmosphere");
            Check(Mathf.Abs(AerialCombatPrototype.Altitude(new Vector3(0,150,0))-150)<.01f,"Radial altitude");
            g.Spawn(p);p.invulnerable=0;p.cargo=53;p.score=123;
            int shards=g.shards.Count;
            g.Kill(p,g.pilots[1]);
            Check(!p.Alive && p.respawn==3 && p.cargo==0 && p.score==123,"Death preserves banked score, drops cargo");
            Check(g.shards.Count>shards,"Death creates salvage");
            p.Simulate(3.1f);
            Check(p.Alive && p.cargo==0 && p.invulnerable>0,"Automatic player respawn");
            var bot=g.pilots[1];g.Kill(bot,p);bot.Simulate(3.1f);
            Check(bot.Alive,"Automatic bot respawn");

            g.SpawnShard(p.transform.position,9);
            var shard=g.shards[g.shards.Count-1];g.Collect(p,shard);g.Collect(p,shard);
            Check(p.cargo==9,"Collect exactly once");
            // Deterministic fixed-dt sweep of the dt-parameterised shard: it must home on the nearest pilot inside 48 m and self-collect under 12 m.
            foreach(var other in g.pilots)if(other!=p)other.transform.position=p.transform.position+p.transform.up*900;
            g.SpawnShard(p.transform.position+p.transform.forward*30,7);
            var drift=g.shards[g.shards.Count-1];bool drawn=false,collected=false;float startGap=Vector3.Distance(drift.transform.position,p.transform.position);
            for(int i=0;i<150 && !collected;i++)
            {
                drift.Tick(.02f);collected=drift.claimed;
                if(!collected && Vector3.Distance(drift.transform.position,p.transform.position)<startGap-1)drawn=true;
            }
            Check(drawn,"Shard is drawn toward the nearest pilot");
            Check(collected && p.cargo==16,"Shard collects itself inside the pickup radius");
            var gate=g.gates[0];p.transform.position=gate.transform.position;p.cargo=40;
            foreach(var other in g.pilots)if(other!=p)other.transform.position=gate.transform.position+Vector3.right*500;
            gate.owner=-1;gate.claimant=-1;gate.progress=0;
            gate.Tick(1.5f);
            Check(gate.owner==0 && p.cargo==0 && p.score==188,"Physical zone claims and banks cargo");
            p.cargo=11;g.pilots[1].transform.position=p.transform.position;gate.Tick(2);
            Check(p.cargo==11,"Contested zone blocks deposit");
            g.Spawn(p);p.invulnerable=0;g.paused=true;float hp=p.health;g.Damage(p,50,bot);
            Check(p.health==hp,"Pause blocks damage");g.paused=false;
            Check(ArenaBolt.SegmentDistance(Vector3.zero,Vector3.left*20,Vector3.right*20)<.01f,"Swept projectile collision");
            g.Spawn(p);p.controls=Vector3.zero;
            float minAlt=99999,maxSpeed=0;
            for(int i=0;i<500;i++){p.Simulate(.02f);minAlt=Mathf.Min(minAlt,p.Altitude);maxSpeed=Mathf.Max(maxSpeed,p.Speed);}
            Check(p.Alive && minAlt>15 && maxSpeed<250,"Ten seconds of neutral flight stays airborne");

            // Holding a refinery is an income stream: 12 s of uncontested ownership is worth 5 banked points, once.
            var income=g.gates[1];
            foreach(var other in g.pilots)other.transform.position=income.transform.position+Vector3.right*4000;
            income.owner=0;income.claimant=-1;income.progress=0;income.ownerAge=11.9f;
            int banked=p.score;income.Tick(.2f);
            Check(p.score==banked+5 && income.ownerAge<.15f,"Held refinery pays banked income every 12 s");
            income.Tick(2);
            Check(p.score==banked+5,"Refinery income does not pay twice inside one cycle");
            income.owner=-1;income.claimant=-1;income.progress=0;income.ownerAge=0;

            // Ended must silence the scoring verbs exactly the way pause silences damage.
            g.Spawn(p);p.invulnerable=0;p.cargo=0;p.fireCooldown=0;p.heat=0;
            g.phase=MatchPhase.Ended;
            float endHealth=p.health;g.Damage(p,40,bot);
            Check(p.health==endHealth,"Ended blocks damage");
            Check(!g.Shoot(p),"Ended blocks the cannon");
            g.SpawnShard(p.transform.position,5);
            var stale=g.shards[g.shards.Count-1];g.Collect(p,stale);
            Check(p.cargo==0 && !stale.claimed,"Ended blocks salvage pickup");

            p.score=77;p.cargo=9;p.kills=3;p.deaths=2;g.gates[0].owner=0;
            g.RestartMatch();
            bool fresh=g.phase==MatchPhase.Countdown && g.gates[0].owner==-1 && g.shards.Count==0;
            foreach(var pilot in g.pilots)fresh&=pilot.score==0 && pilot.cargo==0 && pilot.kills==0 && pilot.deaths==0 && pilot.Alive;
            Check(fresh,"Restart returns every pilot and gate to a fresh countdown");
            g.phase=MatchPhase.Playing;g.phaseTimer=0;

            // Cargo weight: identical launch state, five seconds each; the full hold must end lower and slower.
            g.Spawn(p);p.controls=Vector3.zero;
            Vector3 launch=p.transform.position;Quaternion heading=p.transform.rotation;Vector3 motion=p.velocity;
            for(int i=0;i<250;i++)p.Simulate(.02f);
            float lightAltitude=p.Altitude,lightSpeed=p.Speed;
            g.Spawn(p);p.controls=Vector3.zero;
            p.transform.position=launch;p.transform.rotation=heading;p.velocity=motion;p.cargo=150;
            for(int i=0;i<250;i++)p.Simulate(.02f);
            Check(p.Altitude<lightAltitude-5 && p.Speed<lightSpeed-1,"Full cargo hold flies heavier and slower");
            Debug.Log("ARENA VERIFICATION PASS: population, altitude/density, collection, shard attraction, physical capture, contest, cargo spill, score retention, player/bot respawn, pause, swept hit, stable flight, refinery income, ended gating, match restart, cargo weight. Neutral altitude="+lightAltitude.ToString("F1")+" laden altitude="+p.Altitude.ToString("F1")+" laden speed="+p.Speed.ToString("F1"));
        }
        finally
        {
            p.score=oldScore;g.aceId=-1;
            foreach(var pilot in g.pilots){pilot.streak=0;g.Spawn(pilot);}
            g.phase=oldPhase;g.phaseTimer=oldPhaseTimer;
            g.paused=wasPaused;g.SnapCamera();
        }
    }

    [MenuItem("Rift/Stage atmosphere screenshot")]
    static void Stage()
    {
        var g=AerialCombatPrototype.I;if(!g || !EditorApplication.isPlaying)return;
        g.paused=false;g.Spawn(g.player);g.player.controls=Vector3.zero;g.SnapCamera();
        EditorApplication.isPaused=true;
    }
    [MenuItem("Rift/Stage suborbital screenshot")]
    static void High()
    {
        var g=AerialCombatPrototype.I;if(!g || !EditorApplication.isPlaying)return;
        var p=g.player;p.transform.position=new Vector3(0,850,0);
        p.transform.rotation=Quaternion.Euler(40,30,0);p.velocity=p.transform.forward*125;g.SnapCamera();
        EditorApplication.isPaused=true;
    }
    [MenuItem("Rift/Return to launch")]
    static void Launch()
    {
        var g=AerialCombatPrototype.I;if(!g)return;
        foreach(var pilot in g.pilots)g.Spawn(pilot);
        g.paused=false;g.SnapCamera();EditorApplication.isPaused=false;
    }
}
