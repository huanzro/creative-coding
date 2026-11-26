using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private static readonly Color Mint = new Color(.46f,.95f,.79f);
        private static readonly Color Iris = new Color(.76f,.58f,1);
        private static readonly Color InkGlass = new Color(.018f,.037f,.059f,.78f);

        private void DrawForestHud(Journey j)
        {
            if (j.Phase == Stage.ForestTransition || j.Phase == Stage.ForestDoor || j.Phase == Stage.ForestFalling) return;
            PrepareInterface();
            Paint(new Rect(997, 79, 254, 130), InkGlass);
            LocaleGui.Label(new Rect(1013, 88, 220, 26), $"随行萤火  {j.ForestLights}", body);
            if (j.Phase == Stage.ForestPlanting)
                LocaleGui.Label(new Rect(1013, 118, 220, 26), $"种下根光  {j.Plants}/{j.ForestPlantsRequired}", body);
            if (!j.ForestMazeWalkArea)
            {
                float x = Mathf.Lerp(1025, 1222, (j.ForestLane + 1) * .5f);
                Paint(new Rect(1025,160,197,3),new Color(.33f,.49f,.44f,.75f));
                Orb(x,162,15,Amber,.83f);
                LocaleGui.Label(new Rect(1017,172,86,24), "← 按住 A", body);
                LocaleGui.Label(new Rect(1153,172,86,24), "按住 B →", body);
            }
            if (j.ForestPlantCharge > 0)
                Paint(new Rect(1014, 198, 218 * Mathf.Clamp01(j.ForestPlantCharge), 3), Amber);
        }

        private void DrawChapterFeedback(Journey j)
        {
            PrepareInterface();
            switch (j.Phase)
            {
                case Stage.Tutorial:
                    DrawPractice(j);
                    break;
                case Stage.ForestRoute:
                case Stage.ForestPlant:
                case Stage.ForestWind:
                case Stage.ForestCrossing:
                    DrawForestClues(j);
                    break;
                case Stage.RuinGather:
                case Stage.RuinVoice:
                case Stage.RuinRelease:
                case Stage.RuinMisread:
                case Stage.RuinRepair:
                case Stage.RuinSide:
                case Stage.RuinCrossing:
                    DrawRuinClues(j);
                    break;
            }
        }

        private void DrawPractice(Journey j)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 493 + 145*i;
                float fill = i < j.Lesson ? 1 : i == j.Lesson ? j.Progress : 0;
                float pulse = .5f + .5f * Mathf.Sin(j.PoseTime*3.6f+i);
                Knot(x, 545+12*Mathf.Sin(i*.95f),i==1?Mint:Amber,fill,i==j.Lesson?pulse*.42f:0);
            }
            if (j.Lesson==2 && j.HintReady)
                Stroke(new Vector2(557,524),new Vector2(719,516),4,Mint,.68f,glow);
        }

        private void DrawForestClues(Journey j)
        {
            for (int i=0;i<2;i++)
            {
                float x=i==0?400:884;
                float y=541-i*44;
                bool planted=j.Plants>i;
                float pending=j.Phase==Stage.ForestPlant && i==j.Plants?j.Progress:0;
                Knot(x,y,Amber,planted?1:pending,j.IsObserving?.54f:0);
                if (!planted) continue;
                Vector2 from=new Vector2(x,y+7);
                Vector2 to =new Vector2(i==0?511:770,579-i*13);
                Stroke(from,Vector2.Lerp(from,to,.52f)+new Vector2(6,-12),5,Amber,.54f,glow);
                Stroke(Vector2.Lerp(from,to,.52f)+new Vector2(6,-12),to,4,Amber,.69f,glow);
            }
            if (j.Phase==Stage.ForestPlant)
                Meter(new Rect(484,600,312,5),j.Progress,Amber,"把光种在根边");
            if (j.Phase==Stage.ForestCrossing)
                Meter(new Rect(484,600,312,5),j.Progress,Amber,"穿过光桥");
            if (j.Phase!=Stage.ForestWind) return;
            float warning=j.GustWarning;
            if (warning>.01f)
            {
                float blink=Mathf.Pow(Mathf.Max(0,Mathf.Sin(warning*4*Mathf.PI)),.62f);
                Vector2 flash=new Vector2(Mathf.Lerp(1110,708,warning),221+warning*111);
                DrawFirefly(flash,j.PoseTime,true,1.15f,.52f+.40f*blink);
                for (int i=0;i<5;i++)
                {
                    Vector2 a=new Vector2(927+i*39-warning*149, 290+i*23);
                    Stroke(a,a+new Vector2(-87,20),4,Scarlet,(.24f+.15f*blink)*warning,glow);
                }
            }
            Meter(new Rect(484,600,312,5),j.Progress,warning>.10f?Scarlet:Mint,"风廊");
        }

        private void DrawRuinClues(Journey j)
        {
            if (j.Phase==Stage.RuinMisread || j.Phase==Stage.RuinRepair)
            {
                DrawHandEcho(new Vector2(385,345),true,Mint);
                DrawHandEcho(new Vector2(895,345),false,Scarlet);
                LocaleGui.Label(new Rect(310,419,180,32),"你做的 · 张手",body);
                LocaleGui.Label(new Rect(820,419,206,32),"门记成 · 握拳",body);
                if (j.Phase==Stage.RuinRepair)
                    Meter(new Rect(480,592,320,6),j.Progress,Mint,"安静片刻");
                return;
            }

            if (j.Phase==Stage.RuinSide)
            {
                for (int i=0;i<3;i++)
                    Knot(485+155*i,408+Mathf.Sin(i*Mathf.PI*.5f)*21,i==1?Iris:Mint,
                        i<j.ReflectionIndex?1:0,i==j.ReflectionIndex?.81f:0);
                BeatMeter(j,j.Beat,j.TimingWindow,Iris);
                return;
            }

            if (j.Phase==Stage.RuinRelease) BeatMeter(j,j.Beat,j.TimingWindow,j.FastEcho?Iris:Mint);
            else if (j.Phase==Stage.RuinVoice) BeatMeter(j,1.28f,.90f,Amber,2.28f);
            else if (j.Phase==Stage.RuinGather)
                Meter(new Rect(470,574,340,6),j.Progress,Amber,"把光握稳");
            else if (j.Phase==Stage.RuinCrossing)
                Meter(new Rect(470,586,340,6),j.Progress,Mint,"石板正在对齐");
        }

        private void DrawHandEcho(Vector2 center,bool open,Color color)
        {
            SoftLight(center.x,center.y,92,color,.26f);
            Vector2 palm=center+new Vector2(0,22);
            for (int f=0;f<5;f++)
            {
                float offset=f-2;
                Vector2 basis=palm+new Vector2(offset*13,-13-Mathf.Sin(f*.61f)*12);
                Vector2 bent=basis+new Vector2(offset*3,-(open?26:10));
                Vector2 tip=bent+new Vector2(offset*(open?9:1),-(open?35-Mathf.Abs(offset)*5:-4));
                Stroke(basis,bent,9,color,.81f,glow);
                Stroke(bent,tip,8,color,.73f,glow);
                SoftLight(tip.x,tip.y,8,color,.56f);
            }
            Paint(new Rect(palm.x-36,palm.y-29,72,62),Opacity(color,.61f),glow);
            Stroke(palm,palm+new Vector2(-9,39),19,color,.56f,glow);
        }

        private void BeatMeter(Journey j,float beat,float room,Color tint,float window=0)
        {
            float duration=window>0?window:beat+room+.53f;
            Rect outer=new Rect(407,552,466,50);
            Rect bar=new Rect(423,578,434,6);
            Paint(outer,InkGlass);
            string caption=j.Phase==Stage.RuinVoice?"琥珀音窗亮起时发声":j.Phase==Stage.RuinSide?"光碰到缺口时张手":"回声抵达亮区时张手";
            LocaleGui.Label(new Rect(422,553,422,27),caption,body);
            Paint(bar,new Color(.70f,.88f,.90f,.23f));
            float low=Mathf.Clamp01((beat-room)/duration);
            float high=Mathf.Clamp01((beat+room)/duration);
            Paint(new Rect(bar.x+low*bar.width,bar.y-4,(high-low)*bar.width,14),Opacity(tint,.64f));
            SoftLight(bar.x+Mathf.Clamp01(j.Age/duration)*bar.width,bar.center.y,11,tint,.9f);
            Paint(new Rect(bar.x+Mathf.Clamp01(j.Age/duration)*bar.width-1.5f,bar.y-5,3,17),Color.white);
        }

        private void Knot(float x,float y,Color tint,float fill,float emphasis)
        {
            SoftLight(x,y,18+fill*15,tint,.28f+.38f*fill+emphasis);
            Ripple(x,y,13+fill*3,tint,.41f+.46f*fill);
            if (fill>.02f) Orb(x,y,5+5*fill,tint,.74f);
        }

        private void DottedPath(Vector2 a,Vector2 b,Color tint,float strength)
        {
            for(int i=1;i<=13;i++)
            {
                var point=Vector2.Lerp(a,b,i/14f)+new Vector2(0,-11*Mathf.Sin(i*Mathf.PI/14));
                SoftLight(point.x,point.y,3.8f,tint,strength*.72f);
            }
        }

        private void Meter(Rect bar,float fill,Color tint,string label)
        {
            Paint(new Rect(bar.x-10,bar.y-28,bar.width+20,bar.height+38),InkGlass);
            Paint(bar,new Color(.71f,.88f,.90f,.22f));
            Paint(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(fill),bar.height),Opacity(tint,.88f));
            LocaleGui.Label(new Rect(bar.x,bar.y-25,bar.width,24),label,body);
        }
    }
}
