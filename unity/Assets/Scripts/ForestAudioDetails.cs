using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Echoes
{
    internal sealed class ForestAudioDetails : IDisposable
    {
        private const int SampleRate = 24000;
        private readonly AudioSource threat, shaft;
        private AudioClip webSnap, flowerSnap, chute;
        private int heard;

        public ForestAudioDetails(GameObject actor)
        {
            threat=actor.AddComponent<AudioSource>();
            shaft=actor.AddComponent<AudioSource>();
            threat.playOnAwake=shaft.playOnAwake=false;
            threat.volume=.53f;
            shaft.loop=true;
        }

        public void Follow(Journey journey,bool paused)
        {
            if (journey.ForestThreatPulse!=heard)
            {
                heard=journey.ForestThreatPulse;
                if (journey.InForest && !paused)
                {
                    bool web=journey.ForestThreatKind==ForestFeatureKind.Web;
                    if (web && webSnap==null) webSnap=Snap(true);
                    if (!web && flowerSnap==null) flowerSnap=Snap(false);
                    threat.panStereo=journey.ForestLane*.23f;
                    threat.PlayOneShot(web?webSnap:flowerSnap);
                }
            }
            if(paused && threat.isPlaying) threat.Stop();

            bool sliding=!paused && journey.InForest && journey.Section==ForestSection.Shaft;
            float progress=journey.ForestShaftProgress;
            float volume=sliding ? .18f+.27f*Mathf.Sin(progress*Mathf.PI) : 0;
            shaft.volume=Mathf.MoveTowards(shaft.volume,volume,Time.unscaledDeltaTime*.72f);
            shaft.pitch=Mathf.Lerp(1.15f,.72f,progress);
            if (sliding && !shaft.isPlaying)
            {
                if (chute==null) chute=Conduit();
                shaft.clip=chute;
                shaft.Play();
            }
            else if (!sliding && shaft.volume<=0 && shaft.isPlaying) shaft.Stop();
        }

        private static AudioClip Snap(bool web)
        {
            var samples=new float[(int)(SampleRate*.63f)];
            var random=new System.Random(web?215:412);
            float damp=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)SampleRate;
                float noise=(float)(random.NextDouble()*2-1);
                damp+=(noise-damp)*(web?.21f:.056f);
                float hit=Mathf.Clamp01(t*85)*Mathf.Exp(-t*(web?9.8f:6.0f));
                float tone=Mathf.Sin(2*Mathf.PI*(web?174f*t-60*t*t:65*t-14*t*t));
                samples[i]=hit*(.38f*damp+tone*(web?.13f:.20f))*Mathf.Clamp01((samples.Length-i)/(SampleRate*.12f));
            }
            return MakeClip(web?"Silk suddenly unfurls":"Flower breaks the mulch",samples);
        }

        private static AudioClip Conduit()
        {
            var samples=new float[4*SampleRate];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)SampleRate;
                float pulse=.59f+.22f*Mathf.Sin(2*Mathf.PI*2.25f*t)+.19f*Mathf.Sin(2*Mathf.PI*1.5f*t);
                samples[i]=pulse*(.14f*Mathf.Sin(2*Mathf.PI*39*t)+.061f*Mathf.Sin(2*Mathf.PI*78*t)+
                    .030f*Mathf.Sin(2*Mathf.PI*117*t));
            }
            return MakeClip("Air through the machine spine",samples);
        }

        private static AudioClip MakeClip(string label,float[] samples)
        {
            var clip=AudioClip.Create(label,samples.Length,1,SampleRate,false);
            clip.SetData(samples,0);
            return clip;
        }

        public void Dispose()
        {
            Object.Destroy(threat);
            Object.Destroy(shaft);
            if(webSnap!=null) Object.Destroy(webSnap);
            if(flowerSnap!=null) Object.Destroy(flowerSnap);
            if(chute!=null) Object.Destroy(chute);
        }
    }
}
