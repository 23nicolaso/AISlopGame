using UnityEngine;

// All audio is synthesised at start-up: no clips on disk. Music is five generated loops, one per shell, in a shared
// key that steps up a whole tone with every climb and gains a layer (pad, then arpeggio, then pulse and hats, then a
// high arpeggio); a detuned drone fades in with the Kessler clock. Two sources cross-fade between shells. Sound effects
// are short additive tones; consecutive catches step the catch note up the scale.
public partial class OrbitSnake
{
    const int Rate=22050; const float Bpm=100f;
    AudioSource sfxSrc, catchSrc, musicA, musicB, tensionSrc, engineSrc, heartSrc; bool musicOnA=true; float musicFade=1, catchStep, catchChainTimer; int catchChain;
    AudioClip[] loops=new AudioClip[5]; AudioClip tension, engine, heart; AudioClip[] fx=new AudioClip[10]; int musicLevel=-1;
    // A minor pentatonic in semitones from the root, and the progression i-VI-III-VII as chord roots (A F C G).
    static readonly int[] Scale={0,3,5,7,10,12,15,17,19,22}; static readonly int[] ChordRoots={0,8,3,10};
    static float Hz(int semi) => 220f*Mathf.Pow(2,semi/12f);

    // ---- synthesis helpers ---------------------------------------------------------------------------------------
    // Additive voice: sine plus a second and third harmonic, attack/decay envelope, written into the buffer at a sample offset.
    static void Voice(float[] buf,int at,int len,float hz,float amp,float attack,float release,float h2=.25f,float h3=.08f,float detune=0)
    {
        int a=(int)(attack*Rate), r=(int)(release*Rate); float w=hz*Mathf.PI*2/Rate, w2=(hz+detune)*Mathf.PI*2/Rate;
        for(int i=0;i<len&&at+i<buf.Length;i++)
        {
            float env=Mathf.Min(1,a>0?i/(float)a:1)*Mathf.Min(1,r>0?(len-i)/(float)r:1);
            float s=Mathf.Sin(w*i)+h2*Mathf.Sin(2*w*i)+h3*Mathf.Sin(3*w*i); if(detune!=0)s=s*.5f+.5f*Mathf.Sin(w2*i);
            buf[at+i]+=s*env*amp;
        }
    }
    static void Pluck(float[] buf,int at,float hz,float amp,float seconds){ int len=(int)(seconds*Rate); float w=hz*Mathf.PI*2/Rate; for(int i=0;i<len&&at+i<buf.Length;i++){ float t=i/(float)len; buf[at+i]+=(Mathf.Sin(w*i)*.8f+Mathf.Sin(2*w*i)*.2f*(1-t))*Mathf.Pow(1-t,3)*amp; } }
    static void Tick(float[] buf,int at,float amp,float seconds,System.Random rnd){ int len=(int)(seconds*Rate); for(int i=0;i<len&&at+i<buf.Length;i++){ float t=i/(float)len; buf[at+i]+=((float)rnd.NextDouble()*2-1)*Mathf.Pow(1-t,4)*amp; } }
    AudioClip Clip(string name,float[] buf){ float peak=0; foreach(var v in buf)peak=Mathf.Max(peak,Mathf.Abs(v)); if(peak>.98f)for(int i=0;i<buf.Length;i++)buf[i]*=.98f/peak; var c=AudioClip.Create(name,buf.Length,1,Rate,false); c.SetData(buf,0); owned.Add(c); return c; }

    // One 8-bar loop for a shell. Every layer is quantised to the bar grid so the loop seam is silent.
    AudioClip MusicLoop(int lvl)
    {
        float bpm=Bpm+lvl*6; int beat=(int)(60f/bpm*Rate), bar=beat*4, bars=8; var buf=new float[bar*bars]; int key=lvl*2; var rnd=new System.Random(11+lvl);
        for(int b=0;b<bars;b++)
        {
            int root=key+ChordRoots[b%4]; int at=b*bar;
            // Pad: root, third-ish (pentatonic), fifth, an octave up, slow attack, detuned for width.
            foreach(int semi in new[]{0,3,7,12})Voice(buf,at,bar,Hz(root+semi),.07f,.6f,.5f,.2f,.05f,.9f);
            // Bass: sub root on beat 1, and on beat 3 from shell 2.
            Voice(buf,at,beat*2,Hz(root-12),.16f,.01f,.3f,.1f,0); if(lvl>=2)Voice(buf,at+beat*2,beat*2,Hz(root-12),.12f,.01f,.3f,.1f,0);
            // Arpeggio: eighth notes walking the chord tones, from shell 1; sixteenths an octave higher from shell 3.
            if(lvl>=1)for(int e=0;e<8;e++){ int semi=new[]{0,7,12,7,3,7,12,15}[e]; Pluck(buf,at+e*beat/2,Hz(root+semi+(lvl>=3?0:0)),.1f,.3f); }
            if(lvl>=3)for(int e=0;e<16;e++){ int semi=Scale[(e*3+b)%Scale.Length]; Pluck(buf,at+e*beat/4,Hz(root+semi+12),.05f,.15f); }
            // Pulse and hats from shell 2: a soft tick on every beat, noise on the off-beats.
            if(lvl>=2)for(int q=0;q<4;q++){ Voice(buf,at+q*beat,beat/6,Hz(root-24),.12f,.002f,.05f,0,0); Tick(buf,at+q*beat+beat/2,.05f,.05f,rnd); }
        }
        return Clip("loop"+lvl,buf);
    }
    AudioClip TensionLoop(){ int len=Rate*6; var buf=new float[len]; Voice(buf,0,len,Hz(7),.12f,.5f,.5f,.3f,.15f,3.1f); Voice(buf,0,len,Hz(7-12),.08f,.5f,.5f,.2f,0,-1.7f); return Clip("tension",buf); }
    // Engine: a one-second loop of a 55 Hz drone with harmonics and a 110 Hz voice detuned by exactly 1 Hz, so every
    // partial completes whole cycles per loop and the seam is silent. The source's pitch follows the stick and the brake.
    AudioClip EngineLoop(){ int len=Rate; var buf=new float[len]; Voice(buf,0,len,55,.3f,0,0,.5f,.3f); Voice(buf,0,len,110,.14f,0,0,.2f,.05f,1f); return Clip("engine",buf); }
    // Heartbeat: two low thumps a sixth of a second apart in a one-second loop, faded in while the train is gone.
    AudioClip HeartLoop(){ int len=Rate; var buf=new float[len]; void Thump(float at,float amp){ int a=(int)(at*Rate), n=(int)(.2f*Rate); for(int i=0;i<n&&a+i<len;i++){ float t=i/(float)n; buf[a+i]+=Mathf.Sin(i*52*Mathf.PI*2/Rate)*Mathf.Pow(1-t,3)*amp; } } Thump(0,.7f); Thump(.17f,.5f); return Clip("heart",buf); }

    AudioClip Fx(string name,float length,System.Action<float[]> fill){ var buf=new float[(int)(length*Rate)]; fill(buf); return Clip(name,buf); }
    void BuildAudio()
    {
        AudioSource Src(float vol,bool loop){ var s=gameObject.AddComponent<AudioSource>(); s.spatialBlend=0; s.volume=vol; s.loop=loop; s.playOnAwake=false; return s; }
        sfxSrc=Src(.8f,false); catchSrc=Src(.7f,false); musicA=Src(0,true); musicB=Src(0,true); tensionSrc=Src(0,true); engineSrc=Src(0,true); heartSrc=Src(0,true);
        var rnd=new System.Random(5);
        fx[0]=Fx("strike",.4f,b=>{ Voice(b,0,b.Length,90,.5f,.002f,.35f,.3f,.1f); Tick(b,0,.5f,.25f,rnd); });                          // thud
        fx[1]=Fx("catch",.22f,b=>{ Pluck(b,0,Hz(12),.6f,.22f); Pluck(b,(int)(.05f*Rate),Hz(19),.4f,.17f); });                        // pluck, stepped by pitch
        fx[2]=Fx("shot",.15f,b=>{ Voice(b,0,b.Length,900,.4f,.002f,.12f,.2f,.1f); Tick(b,0,.3f,.06f,rnd); });                        // armour / whip
        fx[3]=Fx("win",2.4f,b=>{ int[] seq={0,7,12,19,24,19,24,31}; for(int i=0;i<seq.Length;i++)Voice(b,(int)(i*.2f*Rate),(int)(.9f*Rate),Hz(seq[i]),.3f,.01f,.6f); });
        fx[4]=Fx("sever",.5f,b=>{ Tick(b,0,.6f,.12f,rnd); Voice(b,0,b.Length,140,.35f,.002f,.45f,.4f,.2f); Voice(b,(int)(.08f*Rate),(int)(.3f*Rate),100,.3f,.002f,.3f); });
        fx[5]=Fx("eject",1.6f,b=>{ for(int i=0;i<b.Length;i++){ float t=i/(float)b.Length; b[i]+=((float)rnd.NextDouble()*2-1)*Mathf.Sin(t*Mathf.PI)*.35f*(1-t*.5f); } Voice(b,0,b.Length,Hz(-12),.3f,.05f,1.2f,.3f,.1f); Voice(b,(int)(.3f*Rate),(int)(1.2f*Rate),Hz(0),.2f,.1f,.9f); });
        fx[6]=Fx("skill",.9f,b=>{ int[] seq={0,4,7,12}; for(int i=0;i<4;i++)Voice(b,(int)(i*.09f*Rate),(int)(.6f*Rate),Hz(seq[i]+12),.28f,.01f,.4f); });
        fx[7]=Fx("start",1.4f,b=>{ Voice(b,0,b.Length,Hz(0),.3f,.6f,.6f,.3f,.1f,1.5f); Voice(b,(int)(.4f*Rate),(int)(1f*Rate),Hz(7),.25f,.3f,.5f); Voice(b,(int)(.7f*Rate),(int)(.7f*Rate),Hz(12),.22f,.2f,.4f); });
        fx[8]=Fx("death",1.8f,b=>{ for(int i=0;i<b.Length;i++){ float t=i/(float)b.Length; float hz=Mathf.Lerp(220,40,t*t); b[i]+=Mathf.Sin(i*hz*Mathf.PI*2/Rate)*(1-t)*.4f+((float)rnd.NextDouble()*2-1)*Mathf.Pow(1-t,3)*.3f; } });
        fx[9]=Fx("whoosh",.35f,b=>{ for(int i=0;i<b.Length;i++){ float t=i/(float)b.Length; float hz=Mathf.Lerp(1400,260,t); b[i]+=((float)rnd.NextDouble()*2-1)*Mathf.Sin(t*Mathf.PI)*.3f+Mathf.Sin(i*hz*Mathf.PI*2/Rate)*Mathf.Sin(t*Mathf.PI)*.12f; } });   // near miss
        for(int i=0;i<loops.Length;i++)loops[i]=MusicLoop(Mathf.Min(i,3)); tension=TensionLoop(); engine=EngineLoop(); heart=HeartLoop();
        tensionSrc.clip=tension; tensionSrc.Play(); engineSrc.clip=engine; engineSrc.Play(); heartSrc.clip=heart; heartSrc.Play();
    }
    // Ping kinds: 0 strike, 1 catch, 2 shot/armour, 3 win, 4 sever, 5 eject, 6 skill, 7 start, 8 death, 9 near miss.
    public void Ping(int kind)
    {
        if(!sfxSrc||!Application.isPlaying)return; kind=Mathf.Clamp(kind,0,fx.Length-1);
        if(kind==1){ catchChain=catchChainTimer>0?Mathf.Min(catchChain+1,7):0; catchChainTimer=1.5f; catchSrc.pitch=Mathf.Pow(2,Scale[catchChain]/12f); catchSrc.PlayOneShot(fx[1],.7f); return; }
        sfxSrc.PlayOneShot(fx[kind],kind==3||kind==7?.9f:kind==9?.5f:.75f);
    }
    // The climb already plays the eject cue; Chime stays as a no-op so older call sites compile.
    public void Chime(int lvl){ }
    // Read-only views for the checks.
    public AudioClip LoopClip(int i) => loops[Mathf.Clamp(i,0,loops.Length-1)]; public AudioClip TensionClip => tension; public AudioClip EngineClip => engine; public AudioClip HeartClip => heart; public int FxCount => fx.Length; public float CatchPitch => catchSrc?catchSrc.pitch:1; public float EnginePitch => engineSrc?engineSrc.pitch:1;

    // Per frame on the unscaled clock: pick the loop for the shell, cross-fade, follow pause and the Kessler drone; the
    // engine rises with the stick and sags under the brake or in peril; the heartbeat fades in with peril.
    public void TickAudio(float dt)
    {
        if(!musicA||!Application.isPlaying)return;
        int want=Mathf.Min(level,loops.Length-1);
        if(want!=musicLevel){ musicLevel=want; var next=musicOnA?musicB:musicA; next.clip=loops[want]; next.volume=0; next.Play(); musicOnA=!musicOnA; musicFade=0; }
        musicFade=Mathf.Min(1,musicFade+dt/1.5f); float master=(started?1:.35f)*(paused?.4f:1)*(ended&&!won?Mathf.Max(0,1-endTimer*.5f):1)*.5f;
        var cur=musicOnA?musicA:musicB; var old=musicOnA?musicB:musicA; cur.volume=master*musicFade; old.volume=master*(1-musicFade); if(musicFade>=1&&old.isPlaying)old.Stop();
        tensionSrc.volume=(started&&!ended?KesslerLoad:0)*.45f;
        bool live=started&&!ended&&!paused&&ship; float k=1-Mathf.Exp(-dt*6);
        engineSrc.pitch=Mathf.Lerp(engineSrc.pitch,live?1+.22f*Mathf.Abs(ship.turn)-(ship.brake?.28f:0)-(peril?.1f:0):.7f,k); engineSrc.volume=Mathf.Lerp(engineSrc.volume,live?.22f:0,k);
        heartSrc.volume=Mathf.Lerp(heartSrc.volume,live&&peril?.5f:0,k);
        if(catchChainTimer>0)catchChainTimer-=dt;
    }
}
