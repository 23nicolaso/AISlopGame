using UnityEngine;

// All audio is synthesised at start-up. Pings are one-shots; chimes step up with the shell.
public partial class OrbitSnake
{
    AudioSource audioSrc; AudioClip[] pings=new AudioClip[4]; AudioClip[] chimes=new AudioClip[5];

    AudioClip Sound(string name,float length,float start,float end,float noise)
    {
        int count=(int)(22050*length); var samples=new float[count]; var random=new System.Random(44); float phase=0;
        for(int i=0;i<count;i++){ float t=(float)i/count; phase+=Mathf.Lerp(start,end,t)*Mathf.PI*2/22050; samples[i]=(Mathf.Sin(phase)*(1-noise)+((float)random.NextDouble()*2-1)*noise)*Mathf.Pow(1-t,2)*.5f; }
        var clip=AudioClip.Create(name,count,1,22050,false); clip.SetData(samples,0); owned.Add(clip); return clip;
    }
    void BuildAudio()
    {
        audioSrc=gameObject.AddComponent<AudioSource>(); audioSrc.spatialBlend=0;
        pings[0]=Sound("strike",.35f,160,50,.6f); pings[1]=Sound("catch",.18f,520,880,.05f); pings[2]=Sound("shot",.12f,900,400,.3f); pings[3]=Sound("win",1.2f,440,880,0);
        for(int i=0;i<chimes.Length;i++)chimes[i]=Sound("chime"+i,.6f,330*(1+i*.25f),660*(1+i*.25f),0);
    }
    public void Ping(int kind){ if(audioSrc&&Application.isPlaying)audioSrc.PlayOneShot(pings[Mathf.Clamp(kind,0,3)],.7f); }
    public void Chime(int lvl){ if(audioSrc&&Application.isPlaying)audioSrc.PlayOneShot(chimes[Mathf.Clamp(lvl,0,chimes.Length-1)],.8f); }
}
