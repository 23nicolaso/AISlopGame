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
    static void Place(ArenaPilot pilot,Vector3 position,Quaternion rotation)
    {
        pilot.transform.SetPositionAndRotation(position,rotation);
        pilot.velocity=rotation*Vector3.forward*82;pilot.health=100;pilot.invulnerable=0;pilot.cargo=0;
        pilot.ResetFlight();
    }
    [MenuItem("Rift/Verify flip stability and rival combat")]
    public static void Verify() { Debug.Log(Run()); }
    public static string Run()
    {
        var g=AerialCombatPrototype.I;
        Check(EditorApplication.isPlaying && g,"Enter Play Mode");
        bool paused=g.paused;g.paused=false;
        var p=g.player;var bot=g.pilots[1];float maxCameraRate=0;
        int initialShots=bot.combatShotsFired,initialHits=bot.hitsLanded;
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

            Place(p,new Vector3(0,1400,170),Quaternion.identity);p.velocity=Vector3.zero;
            Place(bot,new Vector3(0,1400,0),Quaternion.identity);
            bot.Simulate(.02f);
            Check(bot.CombatTarget==p,"Engages an empty-cargo rival");ClearBolts();
            Place(bot,new Vector3(0,1400,0),Quaternion.Euler(0,180,0));bot.cargo=80;
            g.Damage(bot,1,p);bot.Simulate(.02f);
            Check(bot.CombatTarget==p,"Returns fire on attacker despite carrying cargo");
            int landed=bot.hitsLanded;
            for(int i=0;i<650 && p.Alive;i++)
            {
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
            return "FLIGHT / COMBAT PASS: camera flips at 30/60/144fps; combined flight rotations; empty-cargo engagement; loaded retaliation; actual projectile hits; protection/cooldown/occlusion; terrain recovery. Max camera rate="+maxCameraRate.ToString("F1")+" deg/s, combat shots="+combatShots+", hits="+combatHits+", recovery min altitude="+minimum.ToString("F1");
        }
        finally
        {
            ClearBolts();foreach(var pilot in g.pilots)g.Spawn(pilot);
            g.paused=paused;g.SnapCamera();
        }
    }
}
