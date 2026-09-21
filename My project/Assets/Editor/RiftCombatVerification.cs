using System;
using UnityEditor;
using UnityEngine;

public static class RiftCombatVerification
{
    static void Check(bool value,string message)
    {
        if(!value)throw new Exception("FLIGHT / COMBAT CHECK FAILED: "+message);
    }
    static void ClearBolts()
    {
        foreach(var bolt in UnityEngine.Object.FindObjectsByType<ArenaBolt>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(bolt.gameObject);
    }
    static void ClearDebris()
    {
        foreach(var piece in UnityEngine.Object.FindObjectsByType<ArenaDebris>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(piece.gameObject);
    }
    // Live debris split by which material the burst chose: `alloy` counts the crash variant, its inverse the hot gold.
    static int CountDebris(AerialCombatPrototype g,bool alloy)
    {
        int found=0;
        foreach(var piece in UnityEngine.Object.FindObjectsByType<ArenaDebris>(FindObjectsSortMode.None))
        {
            var renderer=piece.GetComponent<Renderer>();
            if(renderer && (renderer.sharedMaterial==g.crashDebris)==alloy)found++;
        }
        return found;
    }
    // The same measurement the auto-level assist makes: signed roll of the wing plane against the local radial, about the nose.
    static float BankOf(ArenaPilot pilot)
    {
        Vector3 reference=Vector3.ProjectOnPlane(AerialCombatPrototype.Up(pilot.transform.position),pilot.transform.forward);
        if(reference.sqrMagnitude<.0001f)return 0;
        return Vector3.SignedAngle(pilot.transform.up,reference.normalized,pilot.transform.forward);
    }
    // Forty rounds fired at a fixed weapon temperature with nothing in the sky to aim at, so the bore IS the intended
    // aim: the angle between each round's own velocity (less the aircraft's) and the nose is exactly the dispersion.
    static float MeanSpread(AerialCombatPrototype g,ArenaPilot shooter,float heat)
    {
        float sum=0; const int shots=40;
        for(int i=0;i<shots;i++)
        {
            ClearBolts();shooter.fireCooldown=0;shooter.heat=heat;
            if(!g.Shoot(shooter))throw new Exception("FLIGHT / COMBAT CHECK FAILED: spread probe could not fire at heat "+heat);
            var bolt=UnityEngine.Object.FindAnyObjectByType<ArenaBolt>();
            if(!bolt)throw new Exception("FLIGHT / COMBAT CHECK FAILED: spread probe produced no round");
            sum+=Vector3.Angle(bolt.velocity-shooter.velocity,shooter.transform.forward);
        }
        ClearBolts();shooter.heat=0;shooter.fireCooldown=0;
        return sum/shots;
    }
    static void Place(ArenaPilot pilot,Vector3 position,Quaternion rotation)
    {
        pilot.transform.SetPositionAndRotation(position,rotation);
        pilot.velocity=rotation*Vector3.forward*82;pilot.health=100;pilot.invulnerable=0;pilot.cargo=0;
        pilot.ResetFlight();
    }
    // Hand-built cannon round flown straight past a parked victim, `offset` metres off its centre: the only way to
    // measure the precision multiplier without also measuring the AI's aim.
    static float ProbeBolt(ArenaPilot shooter,ArenaPilot victim,float offset)
    {
        ClearBolts();
        victim.health=100;victim.invulnerable=0;
        var go=new GameObject("probe bolt");
        go.transform.position=victim.transform.position+new Vector3(offset,0,-30);
        var bolt=go.AddComponent<ArenaBolt>();
        bolt.owner=shooter;bolt.velocity=new Vector3(0,0,360);
        for(int i=0;i<12 && victim.health>=100;i++)bolt.Tick(.02f);
        float dealt=100-victim.health;ClearBolts();return dealt;
    }
    // One 20 ms step of a seeker launched 45 degrees off the bearing to its target: the angle it recovers IS the turn rate.
    static float SeekerTurn(AerialCombatPrototype g,ArenaPilot chased,bool boosting)
    {
        ClearBolts();
        chased.boost=boosting;
        var go=new GameObject("probe seeker");
        go.transform.position=chased.transform.position-new Vector3(0,0,300);
        var bolt=go.AddComponent<ArenaBolt>();
        bolt.owner=g.pilots[2];bolt.seeker=true;bolt.target=chased.transform;
        bolt.velocity=Quaternion.AngleAxis(45,Vector3.up)*new Vector3(0,0,260);
        Vector3 before=bolt.velocity;
        bolt.Tick(.02f);
        float turned=Vector3.Angle(before,bolt.velocity);
        chased.boost=false;ClearBolts();return turned;
    }
    [MenuItem("Rift/Verify flip stability and rival combat")]
    public static void Verify() { Debug.Log(Run()); }
    public static string Run()
    {
        var g=AerialCombatPrototype.I;
        Check(EditorApplication.isPlaying && g,"Enter Play Mode");
        bool paused=g.paused;g.paused=false;
        // Damage and Shoot only resolve while the match is live, so the harness pins the phase like it pins pause.
        var oldPhase=g.phase;float oldPhaseTimer=g.phaseTimer;g.phase=MatchPhase.Playing;g.phaseTimer=0;
        var p=g.player;var bot=g.pilots[1];float maxCameraRate=0;
        int initialShots=bot.combatShotsFired,initialHits=bot.hitsLanded;
        // WP-D readouts, declared up here so the PASS line can print what each new check actually measured.
        float assisted=0,unassisted=0,hotSpread=0,coldSpread=0,lockReached=0,redeployGap=0;
        int lockDropSteps=0,crashPieces=0,hotPieces=0;
        try
        {
            ClearBolts();
            foreach(var other in g.pilots)Place(other,new Vector3(5000+other.id*2000,1800,0),Quaternion.identity);
            // Repeated pitch-through-vertical and rolls at multiple render rates.
            foreach(int fps in new[]{30,60,144})
            {
                Place(p,new Vector3(0,1400,0),Quaternion.identity);g.SnapCamera();g.shake=0;
                Quaternion previous=g.cam.transform.rotation;
                for(int i=1;i<=fps*8;i++)
                {
                    float t=(float)i/fps;
                    Quaternion rotation=Quaternion.AngleAxis(t*150,Vector3.right)*Quaternion.AngleAxis(t*210,Vector3.forward);
                    g.UpdateChaseCamera(1f/fps,p.transform.position,rotation);
                    float rate=Quaternion.Angle(previous,g.cam.transform.rotation)*fps;
                    maxCameraRate=Mathf.Max(maxCameraRate,rate);previous=g.cam.transform.rotation;
                    Vector3 view=g.cam.WorldToViewportPoint(p.transform.position);
                    Check(!float.IsNaN(view.x) && view.z>10 && view.x>.3f && view.x<.7f && view.y>.1f && view.y<.8f,"Camera retains ship through flips at "+fps+"fps");
                    Check(rate<430,"No camera pole flip at "+fps+"fps");
                }
            }
            Place(p,new Vector3(0,1400,0),Quaternion.identity);
            g.SnapCamera();Vector3 cameraBeforeBoost=g.cam.transform.position;p.boost=true;
            g.UpdateChaseCamera(1f/60,p.transform.position,p.transform.rotation);
            Check(Vector3.Distance(cameraBeforeBoost,g.cam.transform.position)<.5f,"Boost camera distance eases instead of jumping");
            p.boost=false;
            p.controls=new Vector3(.9f,.2f,1);int generation=p.generation;
            for(int i=0;i<500;i++)p.Simulate(.02f);
            Check(!float.IsNaN(p.Speed) && p.Speed<400 && p.generation==generation && p.Alive,"Ten seconds of combined flight rotations remain finite and alive");

            // Combat now runs at a fighting altitude: the AI shares the player's air-density-scaled control authority,
            // so an engagement staged at 1400 m would only be measuring how badly both sides turn in near-vacuum.
            Place(p,new Vector3(0,320,170),Quaternion.identity);p.velocity=Vector3.zero;
            Place(bot,new Vector3(0,320,0),Quaternion.identity);
            bot.Simulate(.02f);
            Check(bot.CombatTarget==p,"Engages an empty-cargo rival");ClearBolts();

            // Perception cone: approached from dead astern at 400 m by a rival who has not fired, the AI stays unaware.
            Place(bot,new Vector3(0,900,0),Quaternion.identity);
            Place(p,new Vector3(0,900,-400),Quaternion.identity);p.velocity=p.transform.forward*82;
            for(int i=0;i<12;i++)bot.Simulate(.02f);
            Check(bot.CombatTarget==null,"Blind-spot approach goes unnoticed");

            Place(p,new Vector3(0,320,170),Quaternion.identity);p.velocity=Vector3.zero;
            Place(bot,new Vector3(0,320,0),Quaternion.Euler(0,180,0));bot.cargo=80;
            g.Damage(bot,1,p);
            // Worst-case geometry: struck from dead astern, the AI goes Alert (0.4-0.8 s), acquires by peripheral range,
            // coasts wide on its own momentum before the shared flight model's turn authority can bite, loses the lock,
            // then homes on the remembered position and re-acquires for good around the 9-10 s mark. 550 steps (11 s)
            // clears that with margin without papering over a regression if the chase breaks again.
            for(int i=0;i<550;i++)bot.Simulate(.02f);
            Check(bot.CombatTarget==p,"Returns fire on attacker despite carrying cargo");
            int landed=bot.hitsLanded;
            // Widened from 650 steps: turning through the shared authority model costs the AI several seconds per firing pass.
            for(int i=0;i<1600 && p.Alive;i++)
            {
                // The player keeps plinking every 5 s so the 8.4 s engagement timer cannot lapse the fight into a salvage run.
                if(i>0 && i%250==0 && bot.Alive)g.Damage(bot,1,p);
                bot.Simulate(.02f);
                foreach(var bolt in UnityEngine.Object.FindObjectsByType<ArenaBolt>(FindObjectsSortMode.None))bolt.Tick(.02f);
            }
            Check(bot.combatShotsFired>initialShots && bot.hitsLanded>landed && p.health<100,"Turns, fires real projectiles, and damages attacker");
            int combatShots=bot.combatShotsFired-initialShots,combatHits=bot.hitsLanded-landed;
            ClearBolts();
            Place(bot,new Vector3(0,400,0),Quaternion.identity);
            Place(p,new Vector3(0,400,160),Quaternion.identity);p.velocity=Vector3.zero;p.invulnerable=2;
            Check(!g.Shoot(bot,false,p.transform),"Respects respawn protection");
            p.invulnerable=0;
            Check(g.Shoot(bot,false,p.transform),"Clear firing solution accepted");
            Check(!g.Shoot(bot,false,p.transform),"Weapon cooldown enforced");ClearBolts();
            bot.fireCooldown=0;p.transform.position=new Vector3(0,-2800,0);
            Check(!g.Shoot(bot,false,p.transform),"No firing through planet");
            Place(bot,new Vector3(0,70,0),Quaternion.Euler(35,0,0));
            bot.velocity=new Vector3(0,-40,70);int deaths=bot.deaths;
            float minimum=bot.Altitude;
            for(int i=0;i<300;i++){bot.Simulate(.02f);minimum=Mathf.Min(minimum,bot.Altitude);}
            Check(bot.deaths==deaths && minimum>4,"Predictive terrain recovery prevents crash");

            SalvageCore armored=null,unstable=null;
            foreach(var c in g.cores)
            {
                if(!armored && c.kind==CoreKind.Armored)armored=c;
                if(!unstable && c.kind==CoreKind.Volatile)unstable=c;
            }
            Check(armored && unstable,"Arena seeds armored and volatile wrecks");
            // Armored belts price the seeker: identical 100 damage lands as 30 from the cannon and 100 from a missile.
            armored.cooldown=0;armored.health=armored.maxHealth;
            armored.Hit(100,p,false);float afterCannon=armored.maxHealth-armored.health;
            armored.health=armored.maxHealth;
            armored.Hit(100,p,true);float afterMissile=armored.maxHealth-armored.health;
            Check(armored.maxHealth>=200 && Mathf.Abs(afterCannon-30)<.01f && Mathf.Abs(afterMissile-100)<.01f,"Armored wreck discounts cannon fire but not missiles");
            armored.health=armored.maxHealth;

            // Volatile detonation is a radius weapon: 45 m in takes the full 55, a rival watching from 300 m takes nothing.
            Vector3 blastUp=AerialCombatPrototype.Up(unstable.transform.position);
            foreach(var other in g.pilots)Place(other,unstable.transform.position+blastUp*900,Quaternion.identity);
            Place(p,unstable.transform.position+blastUp*30,Quaternion.identity);
            Place(bot,unstable.transform.position+blastUp*300,Quaternion.identity);
            unstable.cooldown=0;unstable.health=unstable.maxHealth;
            unstable.Hit(unstable.maxHealth,bot,false);
            Check(!unstable.Available,"Volatile wreck breaks up");
            Check(Mathf.Abs(p.health-(100-SalvageCore.BlastDamage))<.01f,"Volatile detonation damages pilots inside 45 m");
            Check(Mathf.Abs(bot.health-100)<.01f,"Volatile detonation spares pilots outside the blast");

            // Precision: the same cannon round, the same parked target, 1.0 m off the hull against 3.0 m. Both land
            // (the pilot radius is 4 m); only the close one is inside the 1.4 m marksman band and it must pay 1.75x.
            foreach(var other in g.pilots)Place(other,new Vector3((other.id+1)*3000,3000,0),Quaternion.identity);
            Place(p,new Vector3(0,3000,0),Quaternion.identity);
            float grazeDamage=ProbeBolt(bot,p,1f),wideDamage=ProbeBolt(bot,p,3f);
            Check(wideDamage>0 && Mathf.Abs(grazeDamage-wideDamage*ArenaBolt.PreciseMultiplier)<.01f,"A hit inside 1.4 m of the hull pays 1.75x");
            // Counter-play with no new key: a boosting target halves what the seeker can turn in one step.
            p.health=100;p.invulnerable=0;
            float coldTurn=SeekerTurn(g,p,false),burnerTurn=SeekerTurn(g,p,true);
            Check(coldTurn>2 && Mathf.Abs(burnerTurn/coldTurn-1.2f/2.3f)<.03f,"Boost halves the seeker's turn rate");

            // Auto-level: 40 degrees of bank, stick released, three seconds. The assist is player-only, roll-input gated
            // and dt-driven, so the identical 150 steps with the static switch off must leave the bank exactly standing.
            Place(p,new Vector3(0,300,0),Quaternion.Euler(0,0,40));p.controls=Vector3.zero;
            for(int i=0;i<150;i++)p.Simulate(.02f);
            assisted=Mathf.Abs(BankOf(p));
            AerialCombatPrototype.AutoLevelEnabled=false;
            Place(p,new Vector3(0,300,0),Quaternion.Euler(0,0,40));p.controls=Vector3.zero;
            for(int i=0;i<150;i++)p.Simulate(.02f);
            unassisted=Mathf.Abs(BankOf(p));
            AerialCombatPrototype.AutoLevelEnabled=true;
            Check(assisted<8,"Released stick rolls the wings level inside three seconds");
            Check(unassisted>25,"Auto-level disabled leaves the bank standing");

            // Coordinated turn: full mouse-x is .42 of bank AND a quarter of rudder, while A/D bank stays pure.
            Vector3 mouseOnly=AerialCombatPrototype.PilotControls(new Vector2(1,0),Vector3.zero);
            Vector3 keysOnly=AerialCombatPrototype.PilotControls(Vector2.zero,new Vector3(0,0,1));
            Check(Mathf.Abs(mouseOnly.y-AerialCombatPrototype.YawCoupling)<.001f && Mathf.Abs(mouseOnly.z-.42f)<.001f,"Mouse bank carries a quarter of coordinated rudder");
            Check(Mathf.Abs(keysOnly.y)<.001f && Mathf.Abs(keysOnly.z-1)<.001f,"Keyboard bank stays uncoupled rudderless roll");

            // Cannon dispersion is the heat's second price. Seeded so the sample mean is a fixed number, not a coin flip.
            var randomBefore=UnityEngine.Random.state;UnityEngine.Random.InitState(20260921);
            foreach(var other in g.pilots)Place(other,new Vector3(9000+other.id*2000,3000,0),Quaternion.identity);
            Place(p,new Vector3(0,3000,0),Quaternion.identity);
            hotSpread=MeanSpread(g,p,.9f);coldSpread=MeanSpread(g,p,0);
            UnityEngine.Random.state=randomBefore;
            Check(hotSpread>.8f,"A gun held at the overheat gate throws the cone open");
            Check(coldSpread<.01f,"A cold gun puts the round exactly on the bore");

            // Seeker lock: 1.2 s of tracking inside the 18 degree cone, and forfeit the moment the target leaves it.
            ClearBolts();
            foreach(var other in g.pilots)Place(other,new Vector3(9000+other.id*2000,1200,0),Quaternion.identity);
            Place(p,new Vector3(0,1200,0),Quaternion.identity);
            Place(bot,new Vector3(0,1200,400),Quaternion.identity);
            p.lockTarget=bot.transform;p.lockTimer=0;
            for(int i=0;i<60;i++)p.Simulate(.02f);
            lockReached=p.lockTimer;
            Check(lockReached>=ArenaPilot.LockTime-.0001f && p.lockTarget==bot.transform,"Seeker lock completes after 1.2 s inside the cone");
            bot.transform.position=p.transform.position+p.transform.right*500;
            for(int i=0;i<5 && p.lockTarget;i++){p.Simulate(.02f);lockDropSteps=i+1;}
            Check(!p.lockTarget && p.lockTimer==0 && lockDropSteps<=5,"A target leaving the cone forfeits the whole lock");

            // Redeploy: park a living rival 100 m off the corridor the rotation is about to hand out. The spawn has to
            // walk to another one, and the same kill has to shed alloy debris rather than the shoot-down fireball.
            Vector2 lane=AerialCombatPrototype.PlayerLanes[g.spawnCounter%AerialCombatPrototype.PlayerLanes.Length];
            Vector3 blocked=AerialCombatPrototype.SurfacePoint(lane.x,lane.y,170),laneUp=AerialCombatPrototype.Up(blocked);
            foreach(var other in g.pilots)if(other!=p)Place(other,blocked+laneUp*7000,Quaternion.identity);
            Place(bot,blocked+laneUp*100,Quaternion.identity);
            Place(p,new Vector3(0,600,0),Quaternion.identity);
            ClearDebris();g.Kill(p,null);
            crashPieces=CountDebris(g,true);int crashHot=CountDebris(g,false);
            Check(crashPieces>=20 && crashHot==0,"A terrain kill sheds cold alloy debris");
            ClearDebris();
            for(int i=0;i<200 && !p.Alive;i++)p.Simulate(.02f);
            Check(p.Alive,"The three second redeploy timer returns the player to the board");
            redeployGap=g.NearestRivalDistance(p,p.transform.position);
            Check(redeployGap>=AerialCombatPrototype.SafeSpawnRange,"Redeploy lands 300 m clear of every living rival");
            p.invulnerable=0;g.Kill(p,bot);
            hotPieces=CountDebris(g,false);
            Check(hotPieces>=20 && CountDebris(g,true)==0,"A shot-down kill keeps the hot gold fireball");
            ClearDebris();
            foreach(var pilot in g.pilots){pilot.streak=0;g.Spawn(pilot);}

            // Three unanswered kills crowns an ace; dying hands the mark back to nobody.
            g.aceId=-1;foreach(var pilot in g.pilots){pilot.streak=0;pilot.cargo=0;}
            for(int i=2;i<=4;i++){var prey=g.pilots[i];prey.health=100;prey.invulnerable=0;g.Kill(prey,bot);}
            Check(g.aceId==bot.id && bot.streak>=AerialCombatPrototype.AceStreak,"Three kills crowns an ace");
            bot.health=100;bot.invulnerable=0;g.Kill(bot,g.pilots[5]);
            Check(g.aceId==-1 && bot.streak==0,"Killing the ace clears the bounty");

            return "FLIGHT / COMBAT PASS: camera flips at 30/60/144fps; combined flight rotations; empty-cargo engagement; blind-spot perception cone; delayed retaliation while loaded; actual projectile hits; protection/cooldown/occlusion; terrain recovery; armored cannon discount; volatile blast radius; precision 1.75x band; boost halves seeker turn rate; auto-level on a released stick and its off switch; coordinated mouse rudder versus uncoupled keyboard bank; heat-widened cannon spread and a cold bore; seeker lock timing and cone drop-out; safe redeploy spacing; crash versus shot-down debris; ace bounty crowning and clearing. Max camera rate="+maxCameraRate.ToString("F1")+" deg/s, combat shots="+combatShots+", hits="+combatHits+", recovery min altitude="+minimum.ToString("F1")+", graze damage="+grazeDamage.ToString("F2")+" vs wide="+wideDamage.ToString("F2")+", seeker turn cold="+coldTurn.ToString("F2")+" deg vs burner="+burnerTurn.ToString("F2")+" deg, bank after 3 s assisted="+assisted.ToString("F1")+" deg vs unassisted="+unassisted.ToString("F1")+" deg, spread hot="+hotSpread.ToString("F2")+" deg vs cold="+coldSpread.ToString("F3")+" deg, lock="+lockReached.ToString("F2")+" s dropped in "+lockDropSteps+" step(s), redeploy gap="+redeployGap.ToString("F0")+" m, crash debris="+crashPieces+" vs shot-down="+hotPieces;
        }
        finally
        {
            // Every static the harness flipped goes back, or the next run measures this one's settings.
            AerialCombatPrototype.AutoLevelEnabled=true;
            ClearDebris();
            foreach(var pilot in g.pilots){pilot.lockTarget=null;pilot.lockTimer=0;}
            ClearBolts();foreach(var pilot in g.pilots){pilot.streak=0;g.Spawn(pilot);}
            g.aceId=-1;g.bountyFresh=0;
            foreach(var core in g.cores){core.cooldown=0;core.health=core.maxHealth;if(core.art)core.art.gameObject.SetActive(true);}
            for(int i=g.shards.Count-1;i>=0;i--)if(g.shards[i])UnityEngine.Object.DestroyImmediate(g.shards[i].gameObject);
            g.shards.Clear();
            g.phase=oldPhase;g.phaseTimer=oldPhaseTimer;
            g.paused=paused;g.SnapCamera();
        }
    }
}
