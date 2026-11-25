using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class TravelSound : IDisposable
    {
        private const int Rate = 24000;
        private readonly AudioSource source, wind, machinery;
        private readonly AudioClip[] cues = new AudioClip[Enum.GetValues(typeof(JourneyCue)).Length];
        private readonly AudioClip[] machineScenes = new AudioClip[4];
        private int heard;
        private int sceneStamp;

        public TravelSound(GameObject owner)
        {
            source=owner.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.volume=.62f;
            wind=owner.AddComponent<AudioSource>();
            wind.loop=true;
            wind.playOnAwake=false;
            wind.volume=0;
            machinery=owner.AddComponent<AudioSource>();
            machinery.loop=true;
            machinery.playOnAwake=false;
            machinery.volume=0;
        }

        public void Follow(Journey journey, bool paused)
        {
            FollowWind(journey, paused);
            FollowMachine(journey, paused);
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

        private void FollowMachine(Journey journey, bool paused)
        {
            float distance=journey.InForest && journey.Branch==ForestBranch.Right && !paused ? journey.Distance : 0;
            float target=OpeningStoryboard.Ease(117,163,distance)*.19f;
            if (journey.Phase==Stage.ForestTransition) target*=1-OpeningStoryboard.Ease(.59f,1,journey.Progress);
            machinery.volume=Mathf.MoveTowards(machinery.volume,target,Time.unscaledDeltaTime*.38f);
            if (target>0 && !machinery.isPlaying)
            {
                if (machinery.clip==null) machinery.clip=ComposeMachineLoop();
                machinery.Play();
            }
            if (target<=0 && machinery.volume<=0 && machinery.isPlaying) machinery.Stop();
            if (paused) return;

            int scene=journey.Phase==Stage.ForestDoor ? journey.ForestDoor :
                journey.Phase==Stage.ForestTransition && journey.ForestTrap==ForestTrapKind.Cable ? 3 : -1;
            if (scene<0 || sceneStamp==journey.CueId) return;
            sceneStamp=journey.CueId;
            if (machineScenes[scene]==null) machineScenes[scene]=ComposeMachineScene(scene);
            source.PlayOneShot(machineScenes[scene], .85f);
        }

        private static AudioClip ComposeMachineLoop()
        {
            var samples=new float[Rate*4];
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)Rate;
                samples[i]=(float)((Tone(t,44)+.49*Tone(t,66)+.23*Tone(t,132))*(.105+.045*Tone(t,.5)));
            }
            return MachineClip("Buried machine",samples);
        }

        private static AudioClip ComposeMachineScene(int scene)
        {
            var samples=new float[(int)(Rate*(scene==3?5.0:3.0))];
            for(int i=0;i<samples.Length;i++)
            {
                double time=i/(double)Rate;
                double value;
                switch(scene)
                {
                    case 0:
                        double press=Math.Max(0,1-Math.Abs(time-1.86)*1.1);
                        value=.27*press*Tone(time,69+41*time)+.095*press*Tone(time,134+37*time);
                        break;
                    case 1:
                        double foot=Math.Exp(-12*((time+.12)%.29));
                        value=.23*foot*Tone(time,85)+.13*foot*Tone(time,249)*Math.Sin(time*8);
                        break;
                    case 2:
                        double surface=Math.Max(0,1-Math.Abs(time-1.8)*.56);
                        value=.13*surface*(Tone(time,156+17*time)+Tone(time,231-19*time));
                        break;
                    default:
                        double beat=Decay(time, .96, 6.7)+.44*Decay(time,1.19,10)+.72*Decay(time,2.04,6.2);
                        double whip=Decay(time,2.63,9.4);
                        value=.56*beat*Tone(time,55)+.21*whip*Tone(time,257-21*time);
                        break;
                }
                samples[i]=(float)(value*Math.Min(1,time*18)*Math.Min(1,(samples.Length-i)/(Rate*.36)));
            }
            return MachineClip("Buried machine scene "+scene,samples);
        }

        private static double Decay(double time,double start,double rate) => time<start ? 0 : Math.Exp(-(time-start)*rate);

        private static AudioClip MachineClip(string name,float[] data)
        {
            var clip=AudioClip.Create(name,data.Length,1,Rate,false);
            clip.SetData(data,0);
            return clip;
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
            if(machinery!=null)
            {
                if(machinery.clip!=null) UnityEngine.Object.Destroy(machinery.clip);
                UnityEngine.Object.Destroy(machinery);
            }
            foreach(var clip in machineScenes)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
            foreach(var clip in cues)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
        }
    }
}
