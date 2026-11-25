using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class TravelSound : IDisposable
    {
        private const int Rate = 24000;
        private readonly AudioSource source, wind;
        private readonly AudioClip[] cues = new AudioClip[Enum.GetValues(typeof(JourneyCue)).Length];
        private int heard;

        public TravelSound(GameObject owner)
        {
            source=owner.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.volume=.62f;
            wind=owner.AddComponent<AudioSource>();
            wind.loop=true;
            wind.playOnAwake=false;
            wind.volume=0;
        }

        public void Follow(Journey journey, bool paused)
        {
            FollowWind(journey, paused);
            if (paused || journey.IsOpening)
            {
                if(source.isPlaying) source.Stop();
                heard=journey.CueId;
                return;
            }
            if (journey.CueId==heard || journey.Cue==JourneyCue.None) return;
            heard=journey.CueId;
            int index=(int)journey.Cue;
            if(cues[index]==null) cues[index]=Compose(journey.Cue);
            source.panStereo=journey.InForest ? journey.ForestLane * .42f :
                journey.Cue==JourneyCue.Observe?-.31f:journey.Cue==JourneyCue.Assist?.29f:0;
            source.PlayOneShot(cues[index]);
        }

        private void FollowWind(Journey journey, bool paused)
        {
            if (paused || !journey.InForest)
            {
                if (wind.isPlaying) wind.Stop();
                wind.volume=0;
                return;
            }

            float height=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.90f,journey.Flight.Height));
            float target=(.019f+height*.34f)*(journey.ForestAirborne?1:.58f);
            if (journey.Phase==Stage.ForestTransition) target*=1-Mathf.Clamp01(journey.Progress*1.3f);
            wind.volume=Mathf.MoveTowards(wind.volume,target,Time.unscaledDeltaTime*.54f);
            wind.pitch=.86f+height*.22f;
            if (wind.isPlaying) return;
            if (wind.clip==null) wind.clip=ComposeWind();
            wind.Play();
        }

        private static AudioClip ComposeWind()
        {
            int count=Rate*5, seam=Rate/3;
            var samples=new float[count+seam];
            var random=new System.Random(214);
            float low=0, air=0;
            for(int i=0;i<samples.Length;i++)
            {
                float noise=(float)(random.NextDouble()*2-1);
                low+=(noise-low)*.016f;
                air+=(noise-air)*.085f;
                samples[i]=(low*1.65f+air*.58f)*(.77f+.23f*Mathf.Sin(i*(2*Mathf.PI/count)));
            }
            for(int i=0;i<seam;i++) samples[i]=Mathf.Lerp(samples[count+i],samples[i],i/(float)seam);
            Array.Resize(ref samples,count);
            var clip=AudioClip.Create("Canopy wind",count,1,Rate,false);
            clip.SetData(samples,0);
            return clip;
        }

        private static AudioClip Compose(JourneyCue cue)
        {
            bool misread=cue==JourneyCue.Misread || cue==JourneyCue.ForestScarlet;
            bool gust=cue==JourneyCue.Gust || cue==JourneyCue.ForestCaptured;
            double seconds=cue==JourneyCue.Seal?1.6:gust?.66:.64;
            double pitch=cue==JourneyCue.Withdraw?329.6:cue==JourneyCue.Assist?392:cue==JourneyCue.Observe?659.25:
                cue==JourneyCue.ForestAmber?783.99:cue==JourneyCue.ForestRescued?587.33:
                cue==JourneyCue.Correct?493.88:misread?293.66:cue==JourneyCue.Gate?440:523.25;
            var data=new float[(int)(seconds*Rate)];
            var random=new System.Random(38+(int)cue);
            double noise=0;
            for(int i=0;i<data.Length;i++)
            {
                double t=i/(double)Rate;
                noise+=((random.NextDouble()*2-1)-noise)*.04;
                double envelope=Math.Min(1,t*74)*Math.Min(1,(seconds-t)*11);
                if(misread) envelope*=.28+.72*Math.Pow(Math.Cos(t*11),2);
                double pluck=Tone(t,pitch)*Math.Exp(-t*4.1);
                double overtone=Tone(t,pitch*(misread?1.0595:1.506))*Math.Exp(-t*6.9);
                double air=gust?noise*Math.Sin(t*Math.PI/seconds)*.67:noise*.021;
                data[i]=(float)(envelope*(pluck*(gust?.04:.20)+overtone*.087+air));
            }
            var clip=AudioClip.Create("Echoes "+cue,data.Length,1,Rate,false);
            clip.SetData(data,0);
            return clip;
        }

        private static double Tone(double t,double hz) => Math.Sin(t*hz*2*Math.PI);

        public void Dispose()
        {
            if(source!=null) UnityEngine.Object.Destroy(source);
            if(wind!=null)
            {
                if(wind.clip!=null) UnityEngine.Object.Destroy(wind.clip);
                UnityEngine.Object.Destroy(wind);
            }
            foreach(var clip in cues)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
        }
    }
}
