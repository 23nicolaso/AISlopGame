using System.Collections.Generic;
using UnityEngine;

// Every orbiting thing. State lives in pos/vel (XZ plane, y=0); the transform only follows it for rendering.
public class OrbitBody : MonoBehaviour
{
    public Vector3 pos,vel,prevPos; public float age;
    public virtual void Init(Vector3 p,Vector3 v){pos=p;vel=v;prevPos=p;transform.position=p;}
    // Velocity Verlet in four substeps: symplectic, so a 30 s orbit keeps its energy for the whole match instead of spiralling.
    public void Integrate(float dt,Vector3 extra)
    {
        prevPos=pos; float h=dt*.25f;
        for(int i=0;i<4;i++)
        {
            Vector3 a0=OrbitSnake.Gravity(pos)+extra; pos+=vel*h+a0*(.5f*h*h);
            Vector3 a1=OrbitSnake.Gravity(pos)+extra; vel+=(a0+a1)*(.5f*h);
        }
        pos.y=0; vel.y=0; age+=dt;
    }
    public virtual void Tick(float dt){Integrate(dt,Vector3.zero);transform.position=pos;}
}

public class OrbitDebris : OrbitBody { public bool fromTail; public override void Tick(float dt){base.Tick(dt);transform.Rotate(0,dt*90,0,Space.World);} }

// A shed tail segment fired retrograde. It is a bullet with an orbit, which is why it arrives at the belt a quarter turn late.
public class TailShot : OrbitBody { }

public class SupplyPod : OrbitBody
{
    public enum Phase{Ascent,Coast}
    public Phase phase; public float targetRadius; public bool insert; public float dir; public Vector3 pad;
    public const float AscentTime=2.4f, AscentAltitude=9f;
    public void Init(Vector3 padPoint,float target,bool insertOrbit,float direction)
    {
        pad=padPoint; targetRadius=target; insert=insertOrbit; dir=direction; phase=Phase.Ascent; Init(pad,Vector3.zero);
    }
    public override void Tick(float dt)
    {
        age+=dt;
        if(phase==Phase.Ascent)
        {
            // Kinematic climb straight up off the pad; at the top it is handed the vis-viva speed for the transfer ellipse.
            float k=Mathf.Clamp01(age/AscentTime); pos=pad.normalized*(OrbitSnake.PlanetRadius+AscentAltitude*k*k); transform.position=pos;
            if(k>=1)
            {
                float r=pos.magnitude; float a=(r+targetRadius)*.5f; float v=Mathf.Sqrt(OrbitSnake.Mu*(2/r-1/a));
                vel=Vector3.Cross(Vector3.up,pos.normalized)*(v*dir); prevPos=pos; phase=Phase.Coast; insertTimer=OrbitSnake.Period(a)*.5f;
            }
            return;
        }
        Integrate(dt,Vector3.zero); transform.position=pos;
        // An inserting pod circularises at apoapsis: the second burn of a Hohmann transfer, applied as an impulse.
        if(insert&&insertTimer>0){ insertTimer-=dt; if(insertTimer<=0){ vel=OrbitSnake.Prograde(pos,vel)*OrbitSnake.CircularSpeed(pos.magnitude); } }
    }
    float insertTimer;
}

public class SnakeShip : OrbitBody
{
    public Vector2 throttle; public float fuel=100, heat, shake; public bool dead;
    public readonly List<Transform> segments=new();
    // Trail the tail follows: head positions with cumulative arc length, oldest first.
    readonly List<Vector3> trail=new(); readonly List<Vector3> trailVel=new(); readonly List<float> trailLen=new();
    public float Mass => OrbitSnake.ShipMass+segments.Count*OrbitSnake.SegmentMass;
    public Vector3 heading=Vector3.forward;
    public override void Init(Vector3 p,Vector3 v){base.Init(p,v);trail.Clear();trailVel.Clear();trailLen.Clear();PushTrail();}
    void PushTrail(){ float len=trailLen.Count>0?trailLen[trailLen.Count-1]+(pos-trail[trail.Count-1]).magnitude:0; trail.Add(pos); trailVel.Add(vel); trailLen.Add(len);
        float keep=(OrbitSnake.MaxSegments+2)*OrbitSnake.SegmentSpacing; while(trailLen.Count>2&&len-trailLen[0]>keep){trail.RemoveAt(0);trailVel.RemoveAt(0);trailLen.RemoveAt(0);} }

    public void Simulate(float dt)
    {
        if(dead)return;
        Vector3 up=pos.normalized, pro=OrbitSnake.Prograde(pos,vel);
        Vector3 thrust=Vector3.zero;
        if(fuel>0&&throttle.sqrMagnitude>0)
        {
            thrust=(pro*throttle.x+up*throttle.y).normalized*(OrbitSnake.Thrust/Mass);
            fuel=Mathf.Max(0,fuel-OrbitSnake.FuelBurn*dt*Mathf.Min(1,throttle.magnitude));
        }
        Integrate(dt,thrust);
        if((pos-trail[trail.Count-1]).sqrMagnitude>.04f)PushTrail();
        heading=Vector3.Slerp(heading,pro,dt*8); transform.position=pos; transform.rotation=Quaternion.LookRotation(heading,Vector3.up);
        PlaceSegments(); shake=Mathf.Max(0,shake-dt*2);
    }

    // Segment i sits (i+1) spacings of arc length behind the head, interpolated on the trail.
    public Vector3 TrailPoint(float back,out Vector3 v)
    {
        float target=trailLen[trailLen.Count-1]-back; v=vel;
        if(target<=trailLen[0]){v=trailVel[0];return trail[0];}
        int i=trailLen.Count-1; while(i>0&&trailLen[i-1]>target)i--;
        float span=trailLen[i]-trailLen[i-1]; float k=span>1e-5f?(target-trailLen[i-1])/span:1;
        v=Vector3.Lerp(trailVel[i-1],trailVel[i],k); return Vector3.Lerp(trail[i-1],trail[i],k);
    }
    void PlaceSegments(){ for(int i=0;i<segments.Count;i++){ var p=TrailPoint((i+1)*OrbitSnake.SegmentSpacing,out _); segments[i].position=p; if(i==0)segments[i].rotation=Quaternion.LookRotation((pos-p).normalized,Vector3.up); else segments[i].rotation=Quaternion.LookRotation((segments[i-1].position-p).normalized,Vector3.up); } }

    public void AddSegment(){ var s=OrbitSnake.I.BuildSegmentArt(this,segments.Count); segments.Add(s); PlaceSegments(); }
    public void RemoveLast(){ if(segments.Count==0)return; var s=segments[segments.Count-1]; segments.RemoveAt(segments.Count-1); Destroy(s.gameObject); }
    public void Shed(int n){ for(int i=0;i<n;i++)RemoveLast(); }
    // After a boost the head's velocity changed but the trail did not: restart it so the tail re-forms behind the new heading.
    public void RebuildTailFromHead(){ trail.Clear();trailVel.Clear();trailLen.Clear(); Vector3 back=-OrbitSnake.Prograde(pos,vel);
        for(int i=OrbitSnake.MaxSegments+1;i>=0;i--){ trail.Add(pos+back*(i*OrbitSnake.SegmentSpacing)); trailVel.Add(vel); trailLen.Add((OrbitSnake.MaxSegments+1-i)*OrbitSnake.SegmentSpacing); } PlaceSegments(); }

    // The head touching any segment past the third severs the tail there; the loose segments keep the head's velocity
    // from when it passed that point, so they stay on roughly the ship's old orbit and come round again as debris.
    public void CheckSelfCollision()
    {
        for(int i=3;i<segments.Count;i++)
            if((segments[i].position-pos).magnitude<OrbitSnake.DebrisRadius*.8f)
            {
                int lost=segments.Count-i;
                for(int j=segments.Count-1;j>=i;j--){ TrailPoint((j+1)*OrbitSnake.SegmentSpacing,out var v); OrbitSnake.I.SpawnDebris(segments[j].position,v,true); RemoveLast(); }
                OrbitSnake.I.Toast("SEVERED — "+lost+" segments cut loose and left in orbit"); OrbitSnake.I.Ping(0); shake=1; return;
            }
    }

    // Q: the last segment leaves retrograde as a projectile. Reaction mass costs nothing but the segment itself.
    public bool Whip()
    {
        if(segments.Count==0||dead)return false;
        var shot=new GameObject("Tail shot").AddComponent<TailShot>(); shot.transform.SetParent(OrbitSnake.I.world);
        shot.Init(segments[segments.Count-1].position,vel-OrbitSnake.Prograde(pos,vel)*OrbitSnake.WhipSpeed); OrbitSnake.I.BuildShotArt(shot); OrbitSnake.I.shots.Add(shot);
        RemoveLast(); OrbitSnake.I.Ping(2); return true;
    }
}
