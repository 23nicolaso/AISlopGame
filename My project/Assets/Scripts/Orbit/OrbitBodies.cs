using System.Collections.Generic;
using UnityEngine;

// The snake. State is a unit normal and a unit tangent on a shell of fixed radius; the trail is a list of past head
// positions with cumulative arc length, and segment i sits (i+1) spacings behind the head along it.
public class OrbitShip : MonoBehaviour
{
    public Vector3 normal, tangent; public float radius, targetRadius, turn, grace, shake, tailFlash; public bool dead, brake;
    public float dashTimer, dashSide;
    public readonly List<Transform> segments=new();
    readonly List<Vector3> trail=new(), trailDir=new(); readonly List<float> trailLen=new();
    public Vector3 Position => normal*radius;
    public float SpeedNow => OrbitSnake.Speed*(brake?OrbitSnake.BrakeFactor:1);
    public Vector3 Right => Vector3.Cross(normal,tangent);
    public void Init(Vector3 n,Vector3 t,float r)
    {
        normal=n.normalized; tangent=(t-normal*Vector3.Dot(t,normal)).normalized; radius=targetRadius=r; dead=false; grace=0; turn=0; shake=0; brake=false; dashTimer=0; tailFlash=0;
        foreach(var s in segments)Destroy(s.gameObject); segments.Clear();
        trail.Clear(); trailDir.Clear(); trailLen.Clear();
        // Prime the trail straight behind the head so a fresh tail has somewhere to sit.
        for(int i=OrbitSnake.MaxSegments+1;i>=0;i--){ float back=i*OrbitSnake.SegmentSpacing/radius; Vector3 p=Quaternion.AngleAxis(-back*Mathf.Rad2Deg,Vector3.Cross(normal,tangent))*normal; trail.Add(p); trailDir.Add(Quaternion.AngleAxis(-back*Mathf.Rad2Deg,Vector3.Cross(normal,tangent))*tangent); trailLen.Add((OrbitSnake.MaxSegments+1-i)*OrbitSnake.SegmentSpacing); }
        Place();
    }

    public void Simulate(float dt)
    {
        if(dead)return;
        // Turning rotates the tangent about the normal; moving rotates both about their cross product — an exact great-circle step.
        tangent=Quaternion.AngleAxis(turn*OrbitSnake.TurnRate*dt,normal)*tangent;
        Vector3 axis=Vector3.Cross(normal,tangent); float speed=SpeedNow; float deg=speed*dt/radius*Mathf.Rad2Deg;
        var step=Quaternion.AngleAxis(deg,axis); normal=(step*normal).normalized; tangent=(step*tangent);
        // Phase hop: the normal slides sideways along Right over DashTime, heading untouched.
        if(dashTimer>0){ float a=dashSide*OrbitSnake.DashDistance/radius*Mathf.Min(dt,dashTimer)/OrbitSnake.DashTime; Vector3 right=Right; normal=(normal*Mathf.Cos(a)+right*Mathf.Sin(a)).normalized; dashTimer-=dt; }
        tangent=(tangent-normal*Vector3.Dot(tangent,normal)).normalized;
        radius=Mathf.MoveTowards(radius,targetRadius,dt*60);
        float len=trailLen[trailLen.Count-1]+(normal-trail[trail.Count-1]).magnitude*radius; trail.Add(normal); trailDir.Add(tangent); trailLen.Add(len);
        float keep=(OrbitSnake.MaxSegments+2)*OrbitSnake.SegmentSpacing; while(trailLen.Count>2&&len-trailLen[0]>keep){trail.RemoveAt(0);trailDir.RemoveAt(0);trailLen.RemoveAt(0);}
        shake=Mathf.Max(0,shake-dt*2); tailFlash=Mathf.Max(0,tailFlash-dt);
        Place();
    }
    public void Dash(float side){ if(dashTimer>0)return; dashTimer=OrbitSnake.DashTime; dashSide=side; grace=Mathf.Max(grace,OrbitSnake.DashTime+.1f); }

    // Trail sample `back` units of arc behind the head: the normal there and the direction the head was moving.
    public Vector3 TrailNormal(float back,out Vector3 dir)
    {
        float target=trailLen[trailLen.Count-1]-back;
        if(target<=trailLen[0]){dir=trailDir[0];return trail[0];}
        int i=trailLen.Count-1; while(i>0&&trailLen[i-1]>target)i--;
        float span=trailLen[i]-trailLen[i-1]; float k=span>1e-5f?(target-trailLen[i-1])/span:1;
        dir=Vector3.Slerp(trailDir[i-1],trailDir[i],k); return Vector3.Slerp(trail[i-1],trail[i],k);
    }
    void Place()
    {
        transform.position=Position; transform.rotation=Quaternion.LookRotation(tangent,normal);
        for(int i=0;i<segments.Count;i++){ var n=TrailNormal((i+1)*OrbitSnake.SegmentSpacing,out var d); segments[i].position=n*radius; segments[i].rotation=Quaternion.LookRotation(d,n); }
    }

    public void AddSegment(){ segments.Add(OrbitSnake.I.BuildSegmentArt(this,segments.Count)); Place(); }
    public void RemoveLast(){ if(segments.Count==0)return; var s=segments[segments.Count-1]; segments.RemoveAt(segments.Count-1); Destroy(s.gameObject); }
    public void Shed(int n){ for(int i=0;i<n;i++)RemoveLast(); }
    public void Lift(float r){ targetRadius=r; }

    // The head touching any segment past the third severs the tail there. Each loose segment becomes junk on the great
    // circle the head was drawing when it laid it, moving the way the snake was moving, so the wreck comes round again.
    public void CheckSelfBite()
    {
        if(dead)return;
        for(int i=3;i<segments.Count;i++)
            if((segments[i].position-Position).magnitude<OrbitSnake.BiteRadius)
            {
                int lost=segments.Count-i;
                for(int j=segments.Count-1;j>=i;j--){ var n=TrailNormal((j+1)*OrbitSnake.SegmentSpacing,out var d); OrbitSnake.I.SpawnJunkAt(OrbitSnake.I.level,n,d,OrbitSnake.Speed*.7f,true); RemoveLast(); }
                OrbitSnake.I.Toast("SEVERED — "+lost+" segments cut loose on this shell"); OrbitSnake.I.Ping(0); shake=1; return;
            }
    }
}

// Junk on a great circle: position = R (cos phi u + sin phi w), advancing at a fixed angular rate. `axis` is the circle's
// pole; u,w span its plane. Rate carries the sign, so retrograde junk is just a negative rate. A `shot` is a whipped
// segment on the same rails that clears junk instead of feeding or striking the snake.
public class OrbitJunk : MonoBehaviour
{
    public int shell; public Vector3 axis,u,w; public float phase,rate,age; public bool wreck,shot; public float spin; public Renderer body;
    public void Init(int s,Vector3 a,float p,float r,bool isWreck)
    {
        shell=s; axis=a.normalized; phase=p; rate=r; wreck=isWreck; age=0; spin=(p*57)%360;
        u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized; w=Vector3.Cross(axis,u);
        transform.position=Position;
    }
    public void SetBasis(Vector3 normal,Vector3 dir){ u=normal.normalized; w=dir.normalized; phase=0; if(rate<0){rate=-rate;} transform.position=Position; }
    public Vector3 Normal => u*Mathf.Cos(phase)+w*Mathf.Sin(phase);
    public Vector3 Position => Normal*OrbitSnake.ShellRadius(shell);
    public Vector3 Direction => (-u*Mathf.Sin(phase)+w*Mathf.Cos(phase))*Mathf.Sign(rate);
    public float Speed => Mathf.Abs(rate)*OrbitSnake.ShellRadius(shell);
    public void Tick(float dt){ phase+=rate*dt; age+=dt; spin+=dt*70; transform.position=Position; transform.rotation=Quaternion.LookRotation(Direction,Normal)*Quaternion.Euler(0,0,spin); }
}

// A skill on offer: a static icon on the shell. Flying through it takes the skill.
public class SkillPod : MonoBehaviour
{
    public int shell; public Vector3 normal; public OrbitSnake.Skill skill; public float age;
    public Vector3 Position => normal*OrbitSnake.ShellRadius(shell);
    public void Init(int s,Vector3 n,OrbitSnake.Skill k){ shell=s; normal=n; skill=k; age=0; transform.position=Position; transform.rotation=Quaternion.LookRotation(Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right),normal); }
    public void Tick(float dt){ age+=dt; transform.position=Position+normal*(Mathf.Sin(age*3)*1.2f); transform.Rotate(0,dt*60,0,Space.Self); }
}

// An ejected segment: purely cosmetic, drops from the shell into the atmosphere and burns.
public class OrbitFalling : MonoBehaviour
{
    public Vector3 start; public float delay,t; public bool done;
    public void Init(Vector3 p,float d){start=p;delay=d;t=0;transform.position=p;}
    public void Tick(float dt)
    {
        t+=dt; float k=Mathf.Clamp01((t-delay)/1.8f);
        float r=Mathf.Lerp(start.magnitude,OrbitSnake.PlanetRadius+4,k*k); transform.position=start.normalized*r+Vector3.Cross(start.normalized,Vector3.up)*(k*30);
        transform.localScale=Vector3.one*(1.6f*(1-k)+.2f); done=k>=1;
    }
}
