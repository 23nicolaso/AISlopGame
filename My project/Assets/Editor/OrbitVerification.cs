using System;
using UnityEditor;
using UnityEngine;

// Deterministic rule checks for Orbit Snake. Every check drives Simulate/Tick/Step at a fixed dt and asserts the rule
// in the design document; none of them says anything about whether the game is fun.
public static class OrbitVerification
{
    const float Dt=.02f;
    static void Check(bool ok,string msg){ if(!ok)throw new Exception("ORBIT CHECK FAILED: "+msg); }
    [MenuItem("Orbit/Verify orbit snake")] static void Menu(){ Debug.Log(Run()); }

    // Junk placed `ahead` units in front of the head on its shell, moving along `dir` at `speed`, old enough to be live.
    static OrbitJunk Ahead(OrbitSnake g,float ahead,Vector3 dir,float speed)
    {
        var s=g.ship; float deg=ahead/OrbitSnake.ShellRadius(g.level)*Mathf.Rad2Deg; var q=Quaternion.AngleAxis(deg,Vector3.Cross(s.normal,s.tangent));
        var j=g.SpawnJunkAt(g.level,q*s.normal,q*dir,speed,false); j.age=2; return j;
    }

    public static string Run()
    {
        var g=OrbitSnake.I; Check(g&&g.ship,"orbit snake booted");
        bool wasPaused=g.paused, wasPersist=OrbitSnake.Persist; string savedWreck=PlayerPrefs.GetString(OrbitSnake.WreckKey,"");
        OrbitSnake.Persist=false;
        try
        {
            // 1. Shell walk: normal stays unit, radius stays on the shell, tangent stays orthogonal through 60 s of hard turning.
            g.Restart(1); var s=g.ship; s.turn=1;
            for(int i=0;i<3000;i++){ s.Simulate(Dt); Check(Mathf.Abs(s.normal.magnitude-1)<1e-3f,"normal stays unit"); Check(Mathf.Abs(s.Position.magnitude-OrbitSnake.ShellRadius(0))<1e-2f,"radius stays on the shell"); Check(Mathf.Abs(Vector3.Dot(s.normal,s.tangent))<1e-3f,"tangent stays in the tangent plane"); }

            // 2. A straight run closes a great circle; a held turn closes a small circle of the documented radius.
            g.Restart(1); s=g.ship; s.turn=0; Vector3 n0=s.normal; float lap=2*Mathf.PI*OrbitSnake.ShellRadius(0)/OrbitSnake.Speed;
            for(int i=0;i<Mathf.RoundToInt(lap/Dt);i++)s.Simulate(Dt);
            Check(Vector3.Angle(n0,s.normal)<2,"a straight run returns to its start after one lap ("+Vector3.Angle(n0,s.normal).ToString("F2")+" deg off)");
            g.Restart(1); s=g.ship; s.turn=1; Vector3 p0=s.Position; float rho=Mathf.Atan(OrbitSnake.Speed/(OrbitSnake.TurnRate*Mathf.Deg2Rad)/OrbitSnake.ShellRadius(0)); float loop=2*Mathf.PI*OrbitSnake.ShellRadius(0)*Mathf.Sin(rho)/OrbitSnake.Speed;
            for(int i=0;i<Mathf.RoundToInt(loop/Dt);i++)s.Simulate(Dt);
            Check((s.Position-p0).magnitude<6,"a held turn closes a small circle ("+(s.Position-p0).magnitude.ToString("F1")+" u off after "+loop.ToString("F2")+" s)");

            // 3. Junk keeps its great circle, its rate and its shell.
            g.Restart(1); var j=g.junk[0]; float phase0=j.phase;
            for(int i=0;i<100;i++)j.Tick(Dt);
            Check(Mathf.Abs(Vector3.Dot(j.Normal,j.axis))<1e-3f,"junk stays on its orbit plane"); Check(Mathf.Abs(j.phase-phase0-j.rate*2)<1e-3f,"junk advances at its rate"); Check(Mathf.Abs(j.Position.magnitude-OrbitSnake.ShellRadius(j.shell))<1e-2f,"junk stays on its shell");

            // 4. Catch: junk ahead moving the same way is overtaken and becomes a segment.
            g.Restart(2); s=g.ship; s.turn=0; foreach(var other in g.junk)other.age=-100; Ahead(g,20,s.tangent,40);
            for(int i=0;i<100;i++)g.Step(Dt);
            Check(s.segments.Count==1&&g.caught==1&&g.strikes==0,"overtaking junk on a matched heading catches it (segments "+s.segments.Count+", caught "+g.caught+", strikes "+g.strikes+")");

            // 5. Strike: junk ahead moving against the heading costs two segments and starts the grace window.
            g.Restart(2); s=g.ship; s.turn=0; foreach(var other in g.junk)other.age=-100; for(int i=0;i<3;i++)s.AddSegment(); Ahead(g,30,-s.tangent,40);
            for(int i=0;i<60;i++)g.Step(Dt);
            Check(s.segments.Count==1&&g.strikes==1&&s.grace>0&&!g.ended,"a head-on contact is a strike that sheds two segments (segments "+s.segments.Count+", strikes "+g.strikes+")");

            // 6. With no segments to shed a strike ends the run.
            g.Restart(2); s=g.ship; s.turn=0; foreach(var other in g.junk)other.age=-100; Ahead(g,30,-s.tangent,40);
            for(int i=0;i<60&&!g.ended;i++)g.Step(Dt);
            Check(g.ended&&!g.won&&s.dead,"a strike with zero segments ends the run");

            // 7. The train keeps its spacing and its shell through a turn.
            g.Restart(3); s=g.ship; for(int i=0;i<10;i++)s.AddSegment(); s.turn=1;
            for(int i=0;i<200;i++)s.Simulate(Dt);
            for(int i=0;i<s.segments.Count;i++){ Vector3 prev=i==0?s.Position:s.segments[i-1].position; float d=(s.segments[i].position-prev).magnitude; Check(Mathf.Abs(d-OrbitSnake.SegmentSpacing)<OrbitSnake.SegmentSpacing*.1f,"segment "+i+" spacing "+d.ToString("F2")); Check(Mathf.Abs(s.segments[i].position.magnitude-OrbitSnake.ShellRadius(0))<.05f,"segment "+i+" stays on the shell"); }

            // 8. Self-bite: a long train turned hard is crossed by its own head, the suffix becomes wreck junk on this shell.
            g.Restart(3); s=g.ship; foreach(var other in g.junk)other.age=-100; for(int i=0;i<OrbitSnake.MaxSegments;i++)s.AddSegment(); s.turn=1; int wrecksBefore=g.junk.FindAll(x=>x.wreck).Count;
            for(int i=0;i<400&&s.segments.Count==OrbitSnake.MaxSegments;i++)g.Step(Dt);
            int lost=OrbitSnake.MaxSegments-s.segments.Count; int wrecks=g.junk.FindAll(x=>x.wreck).Count-wrecksBefore;
            Check(lost>0&&lost==wrecks&&s.segments.Count>=3,"self-bite severs the tail into wreck junk (lost "+lost+", wrecks "+wrecks+")");
            foreach(var w in g.junk)if(w.wreck)Check(w.shell==g.level&&Mathf.Abs(w.Position.magnitude-OrbitSnake.ShellRadius(g.level))<.05f,"wreck junk lives on the current shell");

            // 9. Eject: inert below quota; at quota it throws the train, scores, lifts the ship a shell and seeds it.
            g.Restart(4); s=g.ship; for(int i=0;i<OrbitSnake.EjectQuota[0]-1;i++)s.AddSegment();
            Check(!g.Eject()&&g.level==0,"Space below the quota does nothing");
            s.AddSegment(); int before=g.score; Check(g.Eject(),"Space at quota ejects");
            Check(g.level==1&&s.segments.Count==0&&g.score>before&&g.falling.Count==OrbitSnake.EjectQuota[0]&&Mathf.Approximately(s.targetRadius,OrbitSnake.ShellRadius(1)),"eject lifts one shell and burns the train (level "+g.level+", falling "+g.falling.Count+")");
            Check(g.junk.FindAll(x=>x.shell==1).Count==OrbitSnake.JunkCount[1],"the new shell is seeded with its junk");
            for(int i=0;i<150;i++)g.Step(Dt); Check(Mathf.Abs(s.radius-OrbitSnake.ShellRadius(1))<1e-3f&&g.falling.Count==0,"the ship settles on the new shell and the embers burn out");

            // 10. Pause blocks the world.
            g.Restart(4); g.paused=true; float t=g.elapsed; g.Advance(Dt); Check(Mathf.Approximately(g.elapsed,t),"paused blocks Advance"); g.paused=false;

            // 11. Determinism: the same seed and the same scripted input give the same world.
            int scoreA=Play(g,5); Vector3 nA=g.ship.normal; int segA=g.ship.segments.Count; int scoreB=Play(g,5);
            Check(scoreA==scoreB&&segA==g.ship.segments.Count&&(nA-g.ship.normal).magnitude<1e-4f,"two runs from seed 5 match (score "+scoreA+"/"+scoreB+")");

            // 12. A climb offers three distinct unowned skills on the new shell; flying through one takes it and clears the rest.
            g.Restart(6); s=g.ship; Fill(s,OrbitSnake.EjectQuota[0]); g.Eject();
            Check(g.skillPods.Count==3,"a climb offers three skills ("+g.skillPods.Count+")");
            var seen=new System.Collections.Generic.HashSet<OrbitSnake.Skill>(); foreach(var p in g.skillPods){ Check(seen.Add(p.skill),"offered skills are distinct"); Check(p.shell==1&&Mathf.Abs(p.Position.magnitude-OrbitSnake.ShellRadius(1))<.05f,"skill pods sit on the new shell"); }
            var chosen=g.skillPods[1]; s.Init(chosen.normal,s.tangent,OrbitSnake.ShellRadius(1)); g.Step(Dt);
            Check(g.Has(chosen.skill)&&g.skillPods.Count==0,"flying through a pod takes that skill and removes the offer");

            // 13. Magnet: a 60 degree contact is a strike bare and a catch with the magnet.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); for(int i=0;i<3;i++)s.AddSegment(); Ahead(g,20,Quaternion.AngleAxis(60,s.normal)*s.tangent,0);
            for(int i=0;i<80;i++)g.Step(Dt); Check(g.strikes==1&&g.caught==0,"60 degrees off is a strike without the magnet");
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); g.skills[(int)OrbitSnake.Skill.Magnet]=true; Ahead(g,20,Quaternion.AngleAxis(60,s.normal)*s.tangent,0);
            for(int i=0;i<80;i++)g.Step(Dt); Check(g.caught==1&&g.strikes==0,"60 degrees off is a catch with the magnet");

            // 14. Armour eats one strike without shedding, then is spent.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); for(int i=0;i<3;i++)s.AddSegment(); g.skills[(int)OrbitSnake.Skill.Armour]=true; g.armour=1; Ahead(g,30,-s.tangent,40);
            for(int i=0;i<60;i++)g.Step(Dt); Check(s.segments.Count==3&&g.strikes==1&&g.armour==0&&!g.ended,"armour absorbs a strike and is spent (segments "+s.segments.Count+", armour "+g.armour+")");

            // 15. Whip: Q spends the last segment on a shot that clears the junk ahead.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); for(int i=0;i<3;i++)s.AddSegment(); g.skills[(int)OrbitSnake.Skill.Whip]=true; var target=Ahead(g,40,-s.tangent,0); int junkBefore=g.junk.Count;
            Check(g.Whip()&&s.segments.Count==2&&g.junk.Count==junkBefore+1,"whip spends a segment and fires a shot");
            for(int i=0;i<60;i++)g.Step(Dt); Check(!g.junk.Contains(target)&&g.junk.FindAll(x=>x.shot).Count==0&&g.strikes==0,"the shot clears the junk ahead and is spent");

            // 16. Brake: speed while braking is the documented fraction.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); g.skills[(int)OrbitSnake.Skill.Brake]=true; s.brake=true; Vector3 b0=s.normal; for(int i=0;i<50;i++)s.Simulate(Dt);
            float arc=Vector3.Angle(b0,s.normal)*Mathf.Deg2Rad*OrbitSnake.ShellRadius(0); Check(Mathf.Abs(arc-OrbitSnake.Speed*OrbitSnake.BrakeFactor)<1.5f,"braking runs at "+OrbitSnake.BrakeFactor+" speed ("+arc.ToString("F1")+" u/s)");

            // 17. Phase: a dash moves the head sideways by the documented distance and carries it through junk unharmed.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); for(int i=0;i<3;i++)s.AddSegment(); g.skills[(int)OrbitSnake.Skill.Phase]=true; Ahead(g,10,-s.tangent,0);
            Vector3 d0=s.normal, right0=s.Right; s.Dash(1); for(int i=0;i<15;i++)g.Step(Dt);
            float side=Vector3.Dot((s.normal-d0)*OrbitSnake.ShellRadius(0),right0);
            Check(Mathf.Abs(side-OrbitSnake.DashDistance)<2f&&g.strikes==0&&s.segments.Count==3,"a dash hops "+side.ToString("F1")+" u sideways through junk without a strike");

            // 18. Compound pays 1.5x on ejection.
            g.Restart(6); s=g.ship; Fill(s,OrbitSnake.EjectQuota[0]); int baseValue=g.EjectValue(OrbitSnake.EjectQuota[0]); g.skills[(int)OrbitSnake.Skill.Compound]=true;
            Check(g.EjectValue(OrbitSnake.EjectQuota[0])==Mathf.RoundToInt(baseValue*1.5f),"compound multiplies the eject value by 1.5");

            // 19. Wordless HUD: the HUD source draws exactly one label, and that label is an integer.
            string hud=System.IO.File.ReadAllText(Application.dataPath+"/Scripts/Orbit/OrbitHUD.cs");
            Check(Count(hud,"GUI.Label(")==1&&hud.Contains("GUI.Label(r,v.ToString(),digits)")&&Count(hud,"GUI.Box(")==0&&Count(hud,"GUI.Button(")==0,"the HUD draws one label and it is digits");

            // 20. Kessler clock: nothing for 20 s, then one piece every 6 s on the current shell, reset by a climb, capped at 2x.
            // The ship is parked dead and far off the shell so nothing it could touch changes the count.
            g.Restart(8); s=g.ship; s.dead=true; s.radius=1e5f; int seed0=g.junk.Count;
            for(int i=0;i<990;i++)g.Step(Dt); Check(g.kesslerSpawned==0&&g.junk.Count==seed0,"no Kessler junk in the first 20 s (spawned "+g.kesslerSpawned+", junk "+g.junk.Count+" vs "+seed0+")");
            for(int i=0;i<2010;i++)g.Step(Dt); Check(g.kesslerSpawned>=6&&g.kesslerSpawned<=7&&g.junk.Count==seed0+g.kesslerSpawned,"Kessler adds a piece every 6 s after 20 s ("+g.kesslerSpawned+" in 40 s)");
            foreach(var kj in g.junk)Check(kj.shell==0,"Kessler junk lands on the current shell");
            for(int i=0;i<30000&&g.junk.FindAll(x=>x.shell==0).Count<OrbitSnake.JunkCount[0]*2;i++)g.Step(Dt); int atCap=g.junk.Count; for(int i=0;i<1000;i++)g.Step(Dt);
            Check(g.junk.Count==atCap&&atCap==OrbitSnake.JunkCount[0]*2,"the clock stops at twice the seed count ("+atCap+")");
            s.dead=false; s.radius=OrbitSnake.ShellRadius(0); Fill(s,OrbitSnake.EjectQuota[0]); g.Eject(); Check(g.shellTime==0&&g.kesslerSpawned==0,"a climb resets the clock");

            // 21. Wreckage persists: a death with three segments leaves three gold wrecks on that shell next run; a win clears it.
            OrbitSnake.Persist=true; PlayerPrefs.DeleteKey(OrbitSnake.WreckKey);
            g.Restart(9); s=g.ship; Fill(s,3); g.End(false,"test"); Check(PlayerPrefs.HasKey(OrbitSnake.WreckKey),"death writes the wreckage store");
            g.Restart(9); var loaded=g.junk.FindAll(x=>x.wreck&&x.shell==0); Check(loaded.Count==3,"next run loads three wrecks on shell 0 ("+loaded.Count+")");
            foreach(var w in loaded)Check(Mathf.Abs(w.Position.magnitude-OrbitSnake.ShellRadius(0))<.05f,"loaded wreck sits on its shell");
            g.End(true,"test"); Check(!PlayerPrefs.HasKey(OrbitSnake.WreckKey),"a win clears the store"); OrbitSnake.Persist=false;

            // 22. Start gate: a waiting restart holds the world until started.
            g.Restart(9,true); Check(!g.started,"a waiting restart is not started"); float e0=g.elapsed; g.Advance(Dt); Check(Mathf.Approximately(g.elapsed,e0),"the world holds before the first key"); g.started=true; g.Advance(Dt); Check(g.elapsed>e0,"the world runs once started");

            // 23. Audio is synthesised: five shell loops of exactly eight bars at their tempo, a tension loop, nine effects, and
            // consecutive catches step the catch note up the pentatonic scale.
            for(int i=0;i<5;i++){ var lp=g.LoopClip(i); float bpm=100+Mathf.Min(i,3)*6; int expect=(int)(60f/bpm*22050)*4*8; Check(lp&&lp.samples==expect,"loop "+i+" is eight bars at "+bpm+" bpm ("+(lp?lp.samples:0)+" vs "+expect+")"); }
            Check(g.TensionClip&&g.FxCount==10,"tension loop and ten effects exist");
            g.Restart(1); g.Ping(1); float p1=g.CatchPitch; g.Ping(1); float p2=g.CatchPitch; g.Ping(1); float p3=g.CatchPitch;
            Check(Mathf.Approximately(p1,1)&&Mathf.Abs(p2-Mathf.Pow(2,3/12f))<1e-3f&&Mathf.Abs(p3-Mathf.Pow(2,5/12f))<1e-3f,"catch chain steps the pitch up the scale ("+p1.ToString("F3")+", "+p2.ToString("F3")+", "+p3.ToString("F3")+")");

            // 24. Best score: a run that beats the stored best records it and flags the HUD; a lower one does not.
            int savedBest=PlayerPrefs.GetInt("orbit.best",0); PlayerPrefs.SetInt("orbit.best",50); g.best=50;
            g.Restart(1); g.score=120; g.End(false,"test"); Check(g.best==120&&g.newBest&&PlayerPrefs.GetInt("orbit.best")==120,"a higher score becomes the best");
            g.Restart(1); g.score=30; g.End(false,"test"); Check(g.best==120&&!g.newBest,"a lower score leaves the best alone");
            PlayerPrefs.SetInt("orbit.best",savedBest); g.best=savedBest;

            // 25. Forgiving contact: a head-on piece passing 6 u to the side is a near miss (points, no strike), the same
            // offset on a matched heading is still a catch, 3.5 u head-on is a strike, and a near miss pays once.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); Fill(s,3); Beside(g,25,6,-s.tangent,0);
            for(int i=0;i<60;i++)g.Step(Dt); Check(g.strikes==0&&g.nearMisses==1&&g.score==OrbitSnake.NearMissScore&&s.segments.Count==3,"a head-on piece 6 u to the side is a near miss, not a strike (strikes "+g.strikes+", near misses "+g.nearMisses+", score "+g.score+")");
            for(int i=0;i<100;i++)g.Step(Dt); Check(g.nearMisses==1,"a near miss pays once per piece ("+g.nearMisses+")");
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); Beside(g,25,6,s.tangent,0);
            for(int i=0;i<60;i++)g.Step(Dt); Check(g.caught==1&&g.strikes==0,"a matched piece 6 u to the side is still a catch (bonus space)");
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); Fill(s,3); Beside(g,25,3.5f,-s.tangent,0);
            for(int i=0;i<60;i++)g.Step(Dt); Check(g.strikes==1&&g.nearMisses==0&&s.segments.Count==1,"a head-on piece 3.5 u to the side is a strike (strikes "+g.strikes+")");

            // 26. Peril: struck down to nothing flags peril and holds the Kessler clock; a catch clears both.
            g.Restart(2); s=g.ship; s.turn=0; Quiet(g); Fill(s,1); Ahead(g,30,-s.tangent,40);
            for(int i=0;i<60;i++)g.Step(Dt); Check(g.strikes==1&&s.segments.Count==0&&!g.ended&&g.peril,"a strike that empties the train sets peril");
            g.shellTime=OrbitSnake.KesslerStart+5; g.lastKessler=0; for(int i=0;i<10;i++)g.Step(Dt); Check(g.kesslerSpawned==0,"the Kessler clock holds while in peril");
            s.grace=0; Ahead(g,20,s.tangent,40); for(int i=0;i<100&&g.caught==0;i++)g.Step(Dt); Check(g.caught==1&&!g.peril,"a catch clears peril");
            for(int i=0;i<5;i++)g.Step(Dt); Check(g.kesslerSpawned>=1,"the Kessler clock resumes after peril");

            // 27. Eased climb: after an eject the radius rises monotonically, never overshoots, starts slowly and settles on
            // the new shell within LiftTime.
            g.Restart(6); s=g.ship; Fill(s,OrbitSnake.EjectQuota[0]); g.Eject(); float r0=s.radius, r1=OrbitSnake.ShellRadius(1), last=r0; g.Step(Dt); float first=s.radius-r0;
            Check(first>=0&&first<.05f,"the climb starts slowly (first step "+first.ToString("F4")+" u)");
            for(int i=0;i<Mathf.CeilToInt(OrbitShip.LiftTime/Dt)+2;i++){ g.Step(Dt); Check(s.radius>=last-1e-4f&&s.radius<=r1+1e-3f,"the climb is monotonic and never overshoots"); last=s.radius; }
            Check(Mathf.Abs(s.radius-r1)<1e-3f,"the climb settles on the new shell within LiftTime ("+s.radius.ToString("F3")+" vs "+r1+")");

            // 28. The world keeps turning after the end: junk orbits on and the death embers burn out under Advance.
            g.Restart(2); s=g.ship; g.End(false,"test"); Check(g.falling.Count>0,"death scatters embers"); float ph=g.junk[0].phase;
            for(int i=0;i<150;i++)g.Advance(Dt); Check(g.falling.Count==0&&g.junk[0].phase!=ph&&g.ended,"after the end the embers finish and junk still moves");

            // 29. Engine and heartbeat: two one-second loops, ten effects, and the engine pitch rises with the stick.
            Check(g.FxCount==10&&g.EngineClip&&g.EngineClip.samples==22050&&g.HeartClip&&g.HeartClip.samples==22050,"engine and heartbeat loops exist beside ten effects");
            g.Restart(1); g.ship.turn=1; for(int i=0;i<80;i++)g.TickAudio(.05f); Check(g.EnginePitch>1.18f,"the engine pitch rises with the stick ("+g.EnginePitch.ToString("F3")+")");

            // 30. The score display chases the score and settles; a popup lives for a second.
            g.Restart(1); g.score=200; g.TickHud(.02f); Check(g.shownScore>0&&g.shownScore<200&&g.scorePop>0,"the shown score lags the score and swells ("+g.shownScore.ToString("F1")+")");
            for(int i=0;i<150;i++)g.TickHud(.02f); Check(Mathf.Approximately(g.shownScore,200),"the shown score settles on the score");
            g.Popup(g.ship.Position,10,Color.white,20); Check(g.pops.Count==1,"a popup is queued"); for(int i=0;i<60;i++)g.TickHud(.02f); Check(g.pops.Count==0,"a popup is gone after a second");

            return "ORBIT VERIFICATION PASS: shell walk, great/small circles, junk rails, catch, strike, zero-segment death, train spacing, self-bite, eject/lift/seed, pause, determinism, skill offer and pick, magnet, armour, whip, brake, phase, compound, wordless HUD, Kessler clock, wreckage persistence, start gate, synthesised music and catch chain, best score, forgiving contact and near miss, peril and its rubber band, eased climb, world after the end, engine and heartbeat, score tween and popups";
        }
        finally { OrbitSnake.Persist=false; g.Restart(7); OrbitSnake.Persist=wasPersist; if(string.IsNullOrEmpty(savedWreck))PlayerPrefs.DeleteKey(OrbitSnake.WreckKey); else PlayerPrefs.SetString(OrbitSnake.WreckKey,savedWreck); g.paused=wasPaused; }
    }

    // Junk `ahead` units in front and `side` units across the heading (rotated about the local tangent), moving along `dir`.
    static OrbitJunk Beside(OrbitSnake g,float ahead,float side,Vector3 dir,float speed)
    {
        var s=g.ship; float R=OrbitSnake.ShellRadius(g.level); var q=Quaternion.AngleAxis(ahead/R*Mathf.Rad2Deg,Vector3.Cross(s.normal,s.tangent)); Vector3 n=q*s.normal, t=q*s.tangent;
        n=Quaternion.AngleAxis(side/R*Mathf.Rad2Deg,t)*n; var j=g.SpawnJunkAt(g.level,n,q*dir,speed,false); j.age=2; return j;
    }
    static void Quiet(OrbitSnake g){ foreach(var other in g.junk)other.age=-100; }
    static void Fill(OrbitShip s,int n){ for(int i=0;i<n;i++)s.AddSegment(); }
    static int Count(string text,string needle){ int c=0,i=0; while((i=text.IndexOf(needle,i,System.StringComparison.Ordinal))>=0){c++;i+=needle.Length;} return c; }

    static int Play(OrbitSnake g,int seed)
    {
        g.Restart(seed); for(int i=0;i<1500;i++){ g.ship.turn=Mathf.Sin(i*Dt*.7f); if(g.ended)break; g.Step(Dt); if(g.ship.segments.Count>=OrbitSnake.EjectQuota[Mathf.Min(g.level,3)]&&i%50==0)g.Eject(); }
        return g.score;
    }
}
