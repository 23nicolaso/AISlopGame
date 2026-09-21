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
            g.bankPop=0;
            gate.Tick(1.5f);
            Check(gate.owner==0 && p.cargo==0 && p.score==188,"Physical zone claims and banks cargo");
            // The BANKED readout has to be struck, not merely updated: banking arms the pop timer on the same call.
            Check(g.bankPop>0,"Banking arms the BANKED number pop");
            // The beacon pillar is fed by the ring's own MaterialPropertyBlock, so owning the ring has to repaint the beam.
            var beam=new MaterialPropertyBlock();gate.pillar.GetPropertyBlock(beam);
            Color beamColor=beam.GetColor("_BaseColor");
            Check(beamColor.g>.9f && beamColor.r<.3f,"Beacon pillar wears the ring's owner colour");
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

            // Information layer: the feed is a four-row window on unscaled time, not a scrollback.
            g.toasts.Clear();
            for(int i=0;i<7;i++)g.Toast("EVENT "+i,Color.white);
            Check(g.toasts.Count==4 && g.toasts[0].text=="EVENT 6","Event feed caps at four rows, newest first");
            g.TickHud(AerialCombatPrototype.ToastLife+.05f);
            Check(g.toasts.Count==0,"A toast expires three seconds after it lands");
            // The overheat lockout is a real gate, not only a red bar: .93 refuses the trigger and .6 accepts it again.
            g.Spawn(p);p.invulnerable=0;p.fireCooldown=0;p.heat=.93f;
            Check(!g.Shoot(p),"Overheat blocks the cannon above .92");
            p.fireCooldown=0;p.heat=.6f;
            Check(g.Shoot(p),"The cannon fires again once heat recovers");
            p.heat=0;p.fireCooldown=0;
            // Comfort: the shake slider multiplies at the single point every caller of Trauma goes through.
            float comfort=g.shakeScale;
            g.shake=0;g.shakeScale=0;g.Trauma(.8f);
            Check(g.shake==0,"Shake at 0% removes camera trauma entirely");
            g.shake=0;g.shakeScale=1;g.Trauma(.8f);
            Check(Mathf.Abs(g.shake-.8f)<.001f,"Shake at 100% passes trauma through untouched");
            g.shake=0;g.shakeScale=comfort;

            // Cargo weight: identical launch state, five seconds each; the full hold must end lower and slower.
            g.Spawn(p);p.controls=Vector3.zero;
            Vector3 launch=p.transform.position;Quaternion heading=p.transform.rotation;Vector3 motion=p.velocity;
            for(int i=0;i<250;i++)p.Simulate(.02f);
            float lightAltitude=p.Altitude,lightSpeed=p.Speed;
            g.Spawn(p);p.controls=Vector3.zero;
            p.transform.position=launch;p.transform.rotation=heading;p.velocity=motion;p.cargo=150;
            for(int i=0;i<250;i++)p.Simulate(.02f);
            Check(p.Altitude<lightAltitude-5 && p.Speed<lightSpeed-1,"Full cargo hold flies heavier and slower");
            float ladenAltitude=p.Altitude,ladenSpeed=p.Speed;

            // Overcharge: the surge must land on exactly one reachable ring, and must skip the cycle entirely when
            // every ring is more than 1.5 km from the whole field, because 30 s does not buy a trip around the planet.
            foreach(var pilot in g.pilots){pilot.score=0;pilot.cargo=0;}
            foreach(var ring in g.gates){ring.overcharge=0;ring.owner=-1;ring.claimant=-1;ring.progress=0;ring.ownerAge=0;}
            // Sitting the whole field inside refinery 4 leaves it the only ring within 1.5 km: its nearest neighbour is 1609 m away.
            var reachable=g.gates[3];g.aceId=-1;
            foreach(var pilot in g.pilots)pilot.transform.position=reachable.transform.position;
            g.overchargeTimer=0;g.MatchTick(AerialCombatPrototype.OverchargeInterval+.1f);
            int lit=0;foreach(var ring in g.gates)if(ring.overcharge>0)lit++;
            Check(lit==1 && reachable.overcharge>0,"Overcharge lights exactly one refinery, and only one within reach");
            reachable.overcharge=0;
            // Park the field high above the planet: now every ring is thousands of metres from anybody.
            foreach(var pilot in g.pilots)pilot.transform.position=AerialCombatPrototype.PlanetCenter+Vector3.up*6000;
            Check(!g.TriggerOvercharge(),"Overcharge skips a cycle when no refinery is reachable");
            foreach(var ring in g.gates)Check(ring.overcharge<=0,"Unreachable cycle leaves every refinery cold");

            // Same 40 units, same ring, twice: the surge is worth exactly double.
            var surge=g.gates[2];
            foreach(var other in g.pilots)if(other!=p)other.transform.position=surge.transform.position+Vector3.right*4000;
            p.transform.position=surge.transform.position;
            surge.owner=-1;surge.claimant=-1;surge.progress=0;surge.ownerAge=0;surge.overcharge=0;
            p.score=0;p.cargo=40;surge.Tick(1.5f);
            int plainBank=p.score-25;
            surge.owner=-1;surge.claimant=-1;surge.progress=0;surge.ownerAge=0;surge.overcharge=AerialCombatPrototype.OverchargeLength;
            p.score=0;p.cargo=40;surge.Tick(1.5f);
            int surgeBank=p.score-25;
            Check(plainBank==40 && surgeBank==80,"Overcharged refinery banks cargo at double rate");
            surge.owner=-1;surge.claimant=-1;surge.progress=0;surge.ownerAge=0;surge.overcharge=0;p.cargo=0;

            // Re-entry heat: nose straight down at 120 m/s from the coast line has to cook the airframe inside 4 s.
            g.Spawn(p);p.invulnerable=0;p.cargo=0;p.controls=Vector3.zero;
            p.transform.SetPositionAndRotation(new Vector3(0,900,0),Quaternion.Euler(90,0,0));
            p.velocity=new Vector3(0,-120,0);p.ResetRenderPose();
            for(int i=0;i<200;i++)p.Simulate(.02f);
            float plungeHeat=p.hullHeat,plungeHealth=p.health;
            Check(plungeHeat>1 && plungeHealth<100,"Vertical plunge from 900 m burns through the hull");
            // 19.5 degrees nose down at 120 m/s is 40 m/s of descent: an ordinary dogfight dive, which must cost nothing.
            g.Spawn(p);p.invulnerable=0;p.cargo=0;p.controls=Vector3.zero;
            p.transform.SetPositionAndRotation(new Vector3(0,300,0),Quaternion.Euler(19.47f,0,0));
            p.velocity=p.transform.forward*120;p.ResetRenderPose();
            for(int i=0;i<150;i++)p.Simulate(.02f);
            Check(p.health>=100 && p.hullHeat<1,"A combat dive never cooks the airframe");

            // Salvage prised loose above the coast line is worth double, judged on the collector's altitude.
            g.Spawn(p);p.invulnerable=0;p.cargo=0;
            p.transform.position=new Vector3(0,700,0);
            g.SpawnShard(p.transform.position,10);g.Collect(p,g.shards[g.shards.Count-1]);
            int suborbital=p.cargo;p.cargo=0;
            p.transform.position=new Vector3(0,300,0);
            g.SpawnShard(p.transform.position,10);g.Collect(p,g.shards[g.shards.Count-1]);
            Check(suborbital==20 && p.cargo==10,"Suborbital salvage is worth double on pickup");
            p.cargo=0;

            // Readability pass: every landmark has to exist, and the sky has to be a thing that tracks the camera rather
            // than a clear colour, or a 2 km approach to a refinery reads as empty space in every direction.
            foreach(var ring in g.gates)Check(ring.pillar && ring.pillar.enabled,"Every refinery raises a beacon pillar");
            Check(g.scatterRoot && g.scatterRoot.childCount>=150,"Surface scatter populates every site");
            Check(g.cloudRoot && g.cloudRoot.childCount>=18 && g.cloudRoot.childCount<=22,"Cloud banks build as clusters");
            foreach(Transform cluster in g.cloudRoot)
                foreach(var ring in g.gates)
                    Check(Vector3.Distance(cluster.position,ring.transform.position)>400,"Cloud banks clear every refinery");
            Check(g.sky && g.starMaterial,"Sky dome and starfield exist");
            g.Spawn(p);p.transform.position=new Vector3(0,100,0);p.ResetRenderPose();
            g.UpdateChaseCamera(.02f,p.transform.position,p.transform.rotation);
            Check(Vector3.Distance(g.sky.position,g.cam.transform.position)<.01f,"Sky dome follows the camera");
            float lowStars=g.starMaterial.GetFloat("_Fade");
            p.transform.position=new Vector3(0,800,0);p.ResetRenderPose();
            g.UpdateChaseCamera(.02f,p.transform.position,p.transform.rotation);
            float highStars=g.starMaterial.GetFloat("_Fade");
            Check(lowStars<.05f && highStars>.95f,"Stars fade in with altitude");
            // Wind streaks are driven from the camera update, not Update(): 160 m/s in thick air emits, the same speed in vacuum does not.
            p.transform.position=new Vector3(0,120,0);p.velocity=p.transform.forward*160;p.ResetRenderPose();
            g.UpdateChaseCamera(.02f,p.transform.position,p.transform.rotation);
            float lowRate=g.windStreaks.emission.rateOverTime.constant;
            p.transform.position=new Vector3(0,5200,0);p.ResetRenderPose();
            g.UpdateChaseCamera(.02f,p.transform.position,p.transform.rotation);
            Check(lowRate>20 && g.windStreaks.emission.rateOverTime.constant<1,"Wind streaks scale with speed and air density");

            Debug.Log("ARENA VERIFICATION PASS: population, altitude/density, collection, shard attraction, physical capture, contest, cargo spill, score retention, player/bot respawn, pause, swept hit, stable flight, refinery income, ended gating, match restart, cargo weight, overcharge selection and reach exclusion, overcharge double bank, re-entry burn-through, combat dive immunity, suborbital salvage doubling, beacon pillar colour, surface scatter, cloud clusters clear of refineries, camera-locked sky dome, altitude star fade, wind streak speed/density gate, banked number pop, event feed cap and expiry, overheat cannon lockout and recovery, shake comfort scale. Neutral altitude="+lightAltitude.ToString("F1")+" laden altitude="+ladenAltitude.ToString("F1")+" laden speed="+ladenSpeed.ToString("F1")+" plain bank="+plainBank+" surge bank="+surgeBank+" plunge heat="+plungeHeat.ToString("F2")+" plunge hull="+plungeHealth.ToString("F0"));
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
