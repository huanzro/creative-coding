using System;
using UnityEngine;

namespace Echoes
{
    internal sealed class TravelSound : IDisposable
    {
        private const int Rate = 24000;
        private readonly AudioSource source, wind, machinery, dialogue, piano;
        private readonly AudioClip[] cues = new AudioClip[Enum.GetValues(typeof(JourneyCue)).Length];
        private readonly AudioClip[] machineScenes = new AudioClip[4];
        private readonly AudioClip[] rescues = new AudioClip[2];
        private AudioClip forestMachineLoop, archiveLoop;
        private int heard;
        private int sceneStamp;
        private RuinRetryPhase dialoguePhase;
        private bool dialoguePaused;
        private bool priceSpoken, finaleStarted, finalePaused, dialogueEnglish;
        private AudioClip finalePiano, priceLine;

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
            dialogue=owner.AddComponent<AudioSource>();
            dialogue.playOnAwake=false;
            dialogue.volume=.82f;
            piano=owner.AddComponent<AudioSource>();
            piano.playOnAwake=false;
            piano.volume=.63f;
        }

        public void Follow(Journey journey, bool paused)
        {
            FollowWind(journey, paused);
            FollowMachine(journey, paused);
            FollowDialogue(journey, paused);
            FollowFinale(journey, paused);
            if (journey.StarFinaleActive)
            {
                if (source.isPlaying) source.Stop();
                heard=journey.CueId;
                return;
            }
            if (paused || journey.IsOpening)
            {
                if(source.isPlaying) source.Stop();
                heard=journey.CueId;
                return;
            }
            if (journey.CueId==heard || journey.Cue==JourneyCue.None) return;
            heard=journey.CueId;
            if (journey.Cue==JourneyCue.ForestCaptured && journey.ForestHasRescue)
            {
                int kind=journey.ForestTrap==ForestTrapKind.Web?0:1;
                if (rescues[kind]==null) rescues[kind]=ComposeRescue(kind==0);
                source.panStereo=journey.ForestLane*.22f;
                source.PlayOneShot(rescues[kind],.90f);
                return;
            }
            if (journey.Cue==JourneyCue.ForestRescueLight || journey.Cue==JourneyCue.ForestDoor) return;
            int index=(int)journey.Cue;
            if(cues[index]==null) cues[index]=Compose(journey.Cue);
            source.panStereo=journey.InRuins ? journey.RuinLane * .31f : journey.InForest ? journey.ForestLane * .42f :
                journey.Cue==JourneyCue.Observe?-.31f:journey.Cue==JourneyCue.Assist?.29f:0;
            source.PlayOneShot(cues[index]);
        }

        private void FollowMachine(Journey journey, bool paused)
        {
            float distance=journey.InForest && journey.Branch==ForestBranch.Right && !paused ? journey.Distance : 0;
            float target=OpeningStoryboard.Ease(117,163,distance)*.19f;
            float ruinLayer = journey.RuinLayer;
            if (journey.InRuins && !paused)
            {
                float bend = 0;
                if (journey.Phase == Stage.RuinTransfer && journey.RuinEntrance != null)
                {
                    float move = Mathf.SmoothStep(0, 1, journey.Progress);
                    ruinLayer = Mathf.Lerp(ruinLayer, journey.RuinRoomLayerFor(journey.RuinEntrance.TargetRoom), move);
                    bend = Mathf.Sin(Mathf.PI * move);
                }
                target=(.10f + .028f * Math.Abs(ruinLayer)) * (1 - .33f * bend);
            }
            switch (journey.RuinRetryState)
            {
                case RuinRetryPhase.Impact:
                case RuinRetryPhase.Gate:
                    target *= .30f;
                    break;
                case RuinRetryPhase.Survey:
                case RuinRetryPhase.Arrival:
                    target *= .68f;
                    break;
                case RuinRetryPhase.Valuation:
                case RuinRetryPhase.Bargain:
                    target *= .26f;
                    break;
                case RuinRetryPhase.Accepted:
                case RuinRetryPhase.Declined:
                    target *= .32f * (1 - OpeningStoryboard.Ease(2.7f, Journey.RuinExitDuration, journey.Age));
                    break;
            }
            if (journey.Phase==Stage.ForestTransition) target*=1-OpeningStoryboard.Ease(.59f,1,journey.Progress);
            machinery.volume=Mathf.MoveTowards(machinery.volume,target,Time.unscaledDeltaTime*.38f);
            if (target>0)
            {
                AudioClip bed = journey.InRuins ? archiveLoop ?? (archiveLoop=ComposeArchiveLoop()) :
                    forestMachineLoop ?? (forestMachineLoop=ComposeMachineLoop());
                if (machinery.clip != bed)
                {
                    machinery.Stop();
                    machinery.clip=bed;
                }
                if (!machinery.isPlaying) machinery.Play();
                machinery.pitch = journey.InRuins ? 1 + .048f * ruinLayer : 1;
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

        private void FollowDialogue(Journey journey, bool paused)
        {
            var phase = journey.RuinRetryState;
            bool english = EchoesLocale.English;
            string language=english ? "en_" : "zh_";
            if (dialoguePhase != phase || dialogueEnglish != english)
            {
                dialoguePhase = phase;
                dialogueEnglish = english;
                dialogue.Stop();
                dialoguePaused = priceSpoken = false;
                if (priceLine != null) UnityEngine.Object.Destroy(priceLine);
                string line = phase == RuinRetryPhase.Valuation ? "intro" :
                    phase == RuinRetryPhase.Bargain ? "ask" :
                    phase == RuinRetryPhase.Declined ? "refusal" : null;
                dialogue.clip = line == null ? null : Resources.Load<AudioClip>("RuinBroker/" + language + line);
                float limit = phase == RuinRetryPhase.Valuation ? Journey.RuinValuationFirstLineDuration - .16f :
                    phase == RuinRetryPhase.Declined ? 4.88f : 7.5f;
                dialogue.pitch = dialogue.clip != null ? Mathf.Max(1, dialogue.clip.length / limit) : 1;
                if (dialogue.clip != null && journey.Age * dialogue.pitch < dialogue.clip.length)
                {
                    dialogue.time = journey.Age * dialogue.pitch;
                    dialogue.Play();
                }
            }
            if (!paused && phase == RuinRetryPhase.Valuation && !priceSpoken &&
                journey.Age >= Journey.RuinValuationFirstLineDuration)
            {
                priceSpoken = true;
                priceLine = ComposePrice(language, journey.RuinTokenCost);
                if (priceLine != null)
                {
                    dialogue.Stop();
                    dialogue.clip = priceLine;
                    dialogue.pitch = Mathf.Max(1,priceLine.length / (Journey.RuinValuationDuration -
                        Journey.RuinValuationFirstLineDuration - .12f));
                    dialogue.Play();
                }
            }
            if (paused && dialogue.isPlaying)
            {
                dialogue.Pause();
                dialoguePaused = true;
            }
            else if (!paused && dialoguePaused)
            {
                dialogue.UnPause();
                dialoguePaused = false;
            }
        }

        private static AudioClip ComposePrice(string language, int coins)
        {
            string path="RuinBroker/"+language;
            var bank=Resources.Load<AudioClip>(path+"numbers");
            var map=Resources.Load<TextAsset>(path+"number_map");
            var prefix=Resources.Load<AudioClip>(path+"prefix");
            var suffix=Resources.Load<AudioClip>(path+"suffix");
            if (bank==null || map==null || prefix==null || suffix==null) return null;
            string[] records=map.text.Split('\n');
            string[] pair=records[Mathf.Clamp(coins,0,90)].Split(',');
            if (pair.Length!=2 || !int.TryParse(pair[0],out int from) || !int.TryParse(pair[1],out int length)) return null;
            int gap=bank.frequency/18;
            var head=new float[prefix.samples];
            var number=new float[length];
            var tail=new float[suffix.samples];
            if (!prefix.GetData(head,0) || !bank.GetData(number,from) || !suffix.GetData(tail,0)) return null;
            var data=new float[head.Length+number.Length+tail.Length+2*gap];
            Array.Copy(head,data,head.Length);
            Array.Copy(number,0,data,head.Length+gap,number.Length);
            Array.Copy(tail,0,data,head.Length+number.Length+2*gap,tail.Length);
            var clip=AudioClip.Create("Archive exit price",data.Length,1,bank.frequency,false);
            clip.SetData(data,0);
            return clip;
        }

        private void FollowFinale(Journey journey, bool paused)
        {
            if (!journey.StarFinaleActive)
            {
                if (finaleStarted) piano.Stop();
                finaleStarted=finalePaused=false;
                return;
            }
            if (!finaleStarted && !paused)
            {
                finaleStarted=true;
                if (finalePiano==null) finalePiano=ComposeFinalePiano();
                piano.clip=finalePiano;
                if (journey.StarFinaleAge<finalePiano.length)
                {
                    piano.time=journey.StarFinaleAge;
                    piano.Play();
                }
            }
            if (paused && piano.isPlaying)
            {
                piano.Pause();
                finalePaused=true;
            }
            else if (!paused && finalePaused)
            {
                piano.UnPause();
                finalePaused=false;
            }
        }

        private static AudioClip ComposeFinalePiano()
        {
            const float seconds=14.7f;
            var notes=new[] { 48,55,60,64,67,52,59,64,71,57,64,69,72,53,60,65,69,48,55,60,64,67,72 };
            var onsets=new[] { .40f,.74f,1.19f,1.77f,2.31f,3.18f,3.56f,4.13f,4.81f,5.65f,6.03f,6.61f,
                7.11f,8.04f,8.38f,8.86f,9.39f,10.43f,10.79f,11.28f,11.80f,12.07f,12.49f };
            var data=new float[(int)(Rate*seconds)];
            for (int n=0;n<notes.Length;n++)
            {
                double hz=440*Math.Pow(2,(notes[n]-69)/12d);
                float amplitude=notes[n]<58 ? .146f : .105f + .027f*(n%3)/2f;
                int start=Mathf.RoundToInt(onsets[n]*Rate);
                for (int i=start;i<Math.Min(data.Length,start+4*Rate);i++)
                {
                    double t=(i-start)/(double)Rate;
                    double attack=Math.Min(1,t*158)*Math.Exp(-.98*t);
                    double strings=Tone(t,hz)+.31*Tone(t,hz*2.002)*Math.Exp(-2.05*t)+.10*Tone(t,hz*3.011)*Math.Exp(-3.8*t);
                    data[i]+=(float)(amplitude*attack*strings);
                }
            }
            for (int i=Rate/9;i<data.Length;i++)
                data[i]=(data[i]+data[i-Rate/9]*.16f)*Mathf.Clamp01((data.Length-i)/(Rate*1.52f));
            return MachineClip("Golden impression piano",data);
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

        private static AudioClip ComposeArchiveLoop()
        {
            const int length = Rate * 8;
            var samples = new float[length];
            for (int index = 0; index < length; index++)
            {
                double t = index / (double)Rate;
                double drift = .64 + .19 * Tone(t, .25) + .17 * Tone(t, .375);
                samples[index] = (float)(drift * (.14 * Tone(t, 64) + .069 * Tone(t, 96) +
                    .031 * Tone(t, 144.125) * Tone(t, 1.25) + .014 * Tone(t, 216) * Tone(t, .50)));
            }
            return MachineClip("Civilization archive", samples);
        }

        private static AudioClip ComposeMachineScene(int scene)
        {
            float duration=scene==3 ? Journey.ForestCableTransitionDuration : Journey.ForestDoorDuration;
            var samples=new float[(int)(Rate*duration)];
            var grain=new System.Random(220+scene);
            double noise=0;
            for(int i=0;i<samples.Length;i++)
            {
                double time=i/(double)Rate;
                double beatTime=time/duration;
                noise+=((2*grain.NextDouble()-1)-noise)*.11;
                double value;
                switch(scene)
                {
                    case 0:
                        double press=Math.Max(0,1-Math.Abs(beatTime-.68)*3.2);
                        double slam=Decay(time,duration*.83,5.2);
                        double teeth=Math.Exp(-11*((time+.17)%(beatTime<.53?.46:.28)));
                        value=press*(.17*Tone(time,47+8*beatTime)+teeth*(noise*.20+.06*Tone(time,91)))
                            + slam*(.36*Tone(time,47)+.32*noise);
                        break;
                    case 1:
                        double foot=Math.Exp(-12*((time+.12)%(beatTime<.39?.65:.34)));
                        double call=Decay(time,duration*.30,3.1)+Decay(time,duration*.43,3.8)+Decay(time,duration*.63,7.1);
                        double lid=Decay(time,duration*.86,6.5);
                        value=foot*(.12*Tone(time,83)+.12*noise)+call*.12*Tone(time,370+26*Tone(time,1.7))
                            + lid*(.22*noise+.27*Tone(time,52));
                        break;
                    case 2:
                        double surface=Math.Max(0,1-Math.Abs(beatTime-.75)*1.71);
                        double first=Decay(time,duration*.32,1.3)+.75*Decay(time,duration*.47,1.1);
                        value=surface*(.083*Tone(time,114-38*beatTime)+.09*noise)+first*(.10*Tone(time,230)+.06*Tone(time,277))
                            + .11*Decay(time,duration*.78,1.15)*Tone(time,359);
                        break;
                    default:
                        double beat=Decay(time,duration*.20,6.7)+.44*Decay(time,duration*.23,10)+.72*Decay(time,duration*.49,6.2);
                        double wire=Decay(time,duration*.60,4.6)+.53*Decay(time,duration*.73,6.5);
                        double close=Decay(time,duration*.94,7.2);
                        value=.52*beat*Tone(time,55)+wire*(.15*Tone(time,241-70*beatTime)+.18*noise)+close*.33*Tone(time,42);
                        break;
                }
                samples[i]=(float)(value*Math.Min(1,time*18)*Math.Min(1,(samples.Length-i)/(Rate*.36)));
            }
            return MachineClip("Buried machine scene "+scene,samples);
        }

        private static AudioClip ComposeRescue(bool web)
        {
            if (!web) return ComposeFlowerRescue();
            var data=new float[(int)(Rate*Journey.ForestRescueDuration)];
            var random=new System.Random(4271);
            double grain=0, fiber=0;
            for(int i=0;i<data.Length;i++)
            {
                double t=i/(double)Rate;
                grain+=(random.NextDouble()*2-1-grain)*.24;
                fiber+=(grain-fiber)*.13;
                double snag=Decay(t,.31,5.7)+.57*Decay(t,.82,6.9)+.31*Decay(t,1.51,5);
                double work=Decay(t,3.25,5.6)+.61*Decay(t,5.46,7)+.66*Decay(t,6.03,6.1);
                double strand=Decay(t,6.72,11)+Decay(t,7.06,13)+.58*Decay(t,7.43,10.5);
                double glow=.35*Decay(t,2.15,4.2)+.62*Decay(t,3.14,6.1)+.92*Decay(t,5.48,3.9)+1.18*Decay(t,6.29,2);
                double cloth=Decay(t,7.67,4.1)+.53*Decay(t,8.32,4.3);
                double tension=(snag*.39+work*.19+strand*.27)*grain;
                double tonal=glow*(Tone(t,784)*.14+Tone(t,1174)*.05);
                double body=cloth*(.13*fiber+.12*Tone(t,72))+.13*snag*Tone(t,173);
                data[i]=(float)((tension+tonal+body)*Math.Min(1,t*12)*Math.Min(1,(data.Length-i)/(Rate*.37)));
            }
            return MachineClip("One firefly, web rescue",data);
        }

        private static AudioClip ComposeFlowerRescue()
        {
            var data = new float[(int)(Rate*Journey.ForestMawRescueDuration)];
            var random = new System.Random(7785);
            double soil = 0, leaf = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double seconds = i/(double)Rate;
                double t = seconds*Journey.ForestRescueDuration/Journey.ForestMawRescueDuration;
                soil += (random.NextDouble()*2-1-soil)*.17;
                leaf += (soil-leaf)*.26;
                double bite = Decay(t,.24,5.2) + .62*Decay(t,.63,6.1);
                double first = Decay(t,ForestRescueStoryboard.MawFirstContact,7);
                double second = Decay(t,ForestRescueStoryboard.MawSecondContact,5.8);
                double release = Decay(t,ForestRescueStoryboard.Break,5.2);
                double footing = Decay(t,8.24,5.7);
                double petals = .31*bite*soil + (.16*second + .26*release + .28*footing)*leaf;
                double ground = (.17*bite + .058*footing)*Tone(seconds,77);
                double pulses = first*.16*Tone(seconds,880) + second*.20*Tone(seconds,1047) +
                    release*(.20*Tone(seconds,1319) + .10*Tone(seconds,1760));
                double fade = Math.Min(1,seconds*16)*Math.Min(1,(data.Length-i)/(Rate*.23));
                data[i] = (float)((petals + ground + pulses)*fade);
            }
            return MachineClip("One firefly opens the petals",data);
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
            if (cue == JourneyCue.RuinHeartbeat) return ComposeHeart();
            if (cue == JourneyCue.RuinRetryBeat) return ComposeHeart(true);
            if (cue == JourneyCue.RuinWall) return ComposeWall();
            if (cue == JourneyCue.RuinSurvey || cue == JourneyCue.RuinBroker ||
                cue == JourneyCue.RuinTradeAccepted || cue == JourneyCue.RuinTradeDeclined)
                return ComposeBrokerTransition(cue);
            bool misread=cue==JourneyCue.Misread || cue==JourneyCue.ForestScarlet;
            bool gust=cue==JourneyCue.Gust || cue==JourneyCue.ForestCaptured;
            double seconds=cue==JourneyCue.Seal || cue==JourneyCue.RuinEscape ? 1.6 : gust?.66:.64;
            double pitch=cue==JourneyCue.Withdraw?329.6:cue==JourneyCue.Assist?392:cue==JourneyCue.Observe?659.25:
                cue==JourneyCue.ForestAmber?783.99:cue==JourneyCue.ForestRescued?587.33:
                cue==JourneyCue.RuinCollect?698.46:cue==JourneyCue.RuinDoor?369.99:cue==JourneyCue.RuinTravel?311.13:
                cue==JourneyCue.RuinReplay?273.4:cue==JourneyCue.RuinEscape?659.25:cue==JourneyCue.Correct?493.88:misread?293.66:cue==JourneyCue.Gate?440:523.25;
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

        private static AudioClip ComposeBrokerTransition(JourneyCue cue)
        {
            bool arrival = cue == JourneyCue.RuinBroker;
            bool reveal = cue == JourneyCue.RuinSurvey;
            bool accepted = cue == JourneyCue.RuinTradeAccepted;
            float seconds = arrival ? Journey.RuinArrivalDuration : reveal ? 3.2f : Journey.RuinExitDuration;
            var samples = new float[(int)(seconds * Rate)];
            var random = new System.Random(205 + (int)cue);
            double breath = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)Rate;
                breath += .04 * (random.NextDouble() * 2 - 1 - breath);
                double edge = Math.Min(1, t * 5) * Math.Min(1, (seconds - t) * 2.3);
                double fundamental = arrival ? 43 : reveal ? 73.4 : accepted ? 98 : 65.4;
                double bed = .08 * Tone(t, fundamental) + .052 * Tone(t, fundamental * 1.5) + .07 * breath;
                double bloom = arrival ? Math.Sin(Math.PI * t / seconds) :
                    reveal ? .8 * Math.Exp(-t * .83) : .73 * Math.Sin(Math.PI * Math.Max(0, t - 2.6) / Math.Max(.1, seconds-2.6));
                double shimmer = .064 * Tone(t, accepted ? 784 : 659.25) + .038 * Tone(t, accepted ? 1174 : 987.8);
                double arrivalLatch = arrival ? Decay(t, 3.05, 6.1) * .12 * Tone(t, 142) : 0;
                samples[i] = (float)(edge * (bed * (arrival ? .9 : .48) + bloom * shimmer + arrivalLatch));
            }
            return MachineClip("Archive " + cue, samples);
        }

        private static AudioClip ComposeHeart(bool retry = false)
        {
            var data = new float[Rate];
            for (int i = 0; i < data.Length; i++)
            {
                double t = i / (double)Rate;
                double impulse = HeartStroke(t - (retry ? .08 : .024)) + .67 * HeartStroke(t - (retry ? .46 : .201));
                data[i] = (float)impulse;
            }
            return MachineClip(retry ? "Recall the heartbeat" : "First heart close-up", data);
        }

        private static AudioClip ComposeWall()
        {
            var samples = new float[Rate];
            var grains = new System.Random(119);
            double low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)Rate;
                low += (grains.NextDouble() * 2 - 1 - low) * .085;
                samples[i] = (float)(Math.Min(1,t*180) * Math.Exp(-10*t) *
                    (.42 * Tone(t,52) + .16 * Tone(t,88) + .36 * low * Math.Exp(-24*t)));
            }
            return MachineClip("Archive wall impact", samples);
        }

        private static double HeartStroke(double t)
        {
            if (t < 0) return 0;
            return Math.Min(1, t * 130) * Math.Exp(-t * 26) *
                (.42 * Tone(t, 61) + .105 * Tone(t, 131) + .062 * Tone(t, 282) * Math.Exp(-t * 27));
        }

        private static double Tone(double t,double hz) => Math.Sin(t*hz*2*Math.PI);

        public void Dispose()
        {
            if (piano!=null) UnityEngine.Object.Destroy(piano);
            if (dialogue!=null) UnityEngine.Object.Destroy(dialogue);
            if (finalePiano!=null) UnityEngine.Object.Destroy(finalePiano);
            if (priceLine!=null) UnityEngine.Object.Destroy(priceLine);
            if(source!=null) UnityEngine.Object.Destroy(source);
            if(wind!=null)
            {
                if(wind.clip!=null) UnityEngine.Object.Destroy(wind.clip);
                UnityEngine.Object.Destroy(wind);
            }
            if(machinery!=null)
            {
                UnityEngine.Object.Destroy(machinery);
            }
            if(forestMachineLoop!=null) UnityEngine.Object.Destroy(forestMachineLoop);
            if(archiveLoop!=null) UnityEngine.Object.Destroy(archiveLoop);
            foreach(var clip in machineScenes)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
            foreach(var clip in rescues)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
            foreach(var clip in cues)
                if(clip!=null) UnityEngine.Object.Destroy(clip);
        }
    }
}
