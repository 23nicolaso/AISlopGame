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
        bool wasPaused=g.paused;
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

            return "ORBIT VERIFICATION PASS: shell walk, great/small circles, junk rails, catch, strike, zero-segment death, train spacing, self-bite, eject/lift/seed, pause, determinism";
        }
        finally { g.Restart(7); g.paused=wasPaused; }
    }

    static int Play(OrbitSnake g,int seed)
    {
        g.Restart(seed); for(int i=0;i<1500;i++){ g.ship.turn=Mathf.Sin(i*Dt*.7f); if(g.ended)break; g.Step(Dt); if(g.ship.segments.Count>=OrbitSnake.EjectQuota[Mathf.Min(g.level,3)]&&i%50==0)g.Eject(); }
        return g.score;
    }
}
