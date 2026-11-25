using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private readonly MeshRenderer[] courtSeals = new MeshRenderer[3];
        private Transform sideDoor, starCompass;
        private readonly MeshRenderer[] flies = new MeshRenderer[2];
        private readonly Transform[] flyWings = new Transform[4];
        private readonly MeshRenderer[] linksLight = new MeshRenderer[3];
        private readonly MeshRenderer[] mirrors = new MeshRenderer[3];
        private readonly Transform[] reflections = new Transform[3];

        private void BuildRuins()
        {
            Color slate=new Color(.27f,.35f,.36f), pale=new Color(.37f,.45f,.436f);
            var masonry=Geometry();
            var walls=Geometry();
            var metal=Geometry();
            var lights=Geometry();

            for (int ring=0;ring<8;ring++)
            {
                float y=Mathf.Lerp(CaveFloor,-.19f,ring/8f);
                for (int side=0;side<24;side++)
                {
                    float a=(side+.5f*(ring%2))*Mathf.PI/12;
                    var point=new Vector3(Mathf.Sin(a)*4.75f,y,SinkZ+Mathf.Cos(a)*5.03f);
                    if (ring<5 && Mathf.Abs(side-12)>8) continue;
                    float r=Range(.7f,1.32f);
                    walls.Rock(point,new Vector3(r,Range(.86f,1.37f),r*.83f),
                        Color.Lerp(slate,pale,Range(.12f,.70f)),Range(0,27),4,10,.37f);
                }
            }

            masonry.Surface(20,83,(x,z)=>
            {
                float px=(x-10)*.97f,pz=5.2f+z*1.43f;
                float depression = Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.3f,7.7f,Mathf.Abs(px)));
                return new Vector3(px,CaveFloor-.44f-Mathf.Sin(pz*.16f)*.18f-depression*1.12f,pz);
            },p=>WorldMesh.Shade(slate,.57f+.26f*Mathf.PerlinNoise(p.x*.63f,p.z*.58f)),null);

            for (int bay=0;bay<12;bay++)
            {
                float z=21+bay*7.33f;
                Arch(masonry, .36f*Mathf.Sin(bay*1.3f),CaveFloor-.14f,z,
                    4.55f+Mathf.Sin(bay)*.54f,3.35f,.92f,1.3f,slate,18);
                if (bay%3==1)
                    Arch(masonry,-.28f,CaveFloor-.08f,z+.47f,3.91f,2.86f,.33f,.22f,pale,23);
                for(int side=-1;side<=1;side+=2)
                {
                    walls.Box(new Vector3(side*6.05f,CaveFloor+.43f,z),new Vector3(2.4f,1.8f,6.4f),
                        Quaternion.Euler(0,side*5,3*Mathf.Sin(bay)),WorldMesh.Shade(slate,.83f));
                    for (int step=0;step<3;step++)
                        walls.Box(new Vector3(side*(7.85f+.23f*step),CaveFloor+3.9f+step*3.75f,z+.29f),
                            new Vector3(.81f,3.2f,6.7f),Quaternion.Euler(0,side*2,Mathf.Sin(bay)*4),WorldMesh.Shade(slate,Range(.58f,.89f)));
                    for (int i=0;i<7;i++)
                    {
                        Vector3 p=new Vector3(side*Range(3.45f,8.4f),CaveFloor+Range(-.18f,.29f),z+Range(-2.8f,3.4f));
                        walls.Rock(p,new Vector3(Range(.25f,.92f),Range(.28f,.78f),Range(.29f,1.12f)),slate,Range(0,4));
                    }
                    metal.Box(new Vector3(side*3.98f,CaveFloor+1.51f,z-.80f),new Vector3(.34f,.40f,.36f),
                        Quaternion.Euler(0,25*side,0),new Color(.49f,.48f,.30f));
                    lights.Rock(new Vector3(side*3.98f,CaveFloor+1.86f,z-.78f),
                        new Vector3(.093f,.221f,.09f), new Color(.245f,.52f,.57f),bay*.7f,4,6);
                }
            }

            for (int j=0;j<38;j++)
            {
                float z=10+j*2.53f;
                float x=.72f*Mathf.Sin(j*.52f);
                if (z > 48 && z < 66) continue;
                masonry.Box(new Vector3(x,CaveFloor-.10f,z),new Vector3(Range(2.84f,4.24f),.42f,Range(2.04f,2.45f)),
                    Quaternion.Euler(Range(-1.8f,1),Range(-4,4),Range(-2,2)),WorldMesh.Shade(pale,Range(.58f,.88f)));
            }
            Mesh stepMesh;
            {
                var step=Geometry();
                step.Box(Vector3.zero,new Vector3(4.18f,.46f,1.82f),Quaternion.identity,pale);
                step.Box(new Vector3(0,.195f,-.01f),new Vector3(3.7f,.15f,1.64f),Quaternion.identity,WorldMesh.Shade(pale,1.13f));
                stepMesh=step.Finish("Paired bridge stones");
            }
            for(int i=0;i<bridge.Length;i++)
                bridge[i]=Put("Moving bridge slab "+i,stepMesh,stone,
                    new Vector3(Mathf.Sin(i)*.16f,CaveFloor-1.65f,48.8f+i*1.88f),Vector3.one);

            var door=Geometry();
            Vector3 doorAt=new Vector3(4.6f,CaveFloor+1.46f,30.3f);
            door.Box(Vector3.zero,new Vector3(.35f,2.73f,2.45f),Quaternion.identity,slate);
            door.Box(new Vector3(-.21f,.22f,0),new Vector3(.095f,.91f,1.76f),Quaternion.identity,pale);
            sideDoor=Put("Ruin side door",door.Finish("Cut stone door"),stone,doorAt,Vector3.one);

            for(int i=0;i<gates.Length;i++)
            {
                float z=24f+i*6.82f;
                gates[i]=Lamp("Echo light "+i,new Vector3(i==2?3.36f:0,CaveFloor+2.66f,z),.24f,Jade,.1f);
                Ring(metal,new Vector3(i==2?3.36f:0,CaveFloor+2.66f,z+.10f),i==2?.67f:1.07f,
                    .048f, new Color(.57f,.47f,.25f), Quaternion.identity, 29, .9f);
            }
            for (int j=0;j<9;j++)
            {
                float a=j*6.283f/9;
                var top=new Vector3(Mathf.Sin(a)*3.8f,-.34f,SinkZ+Mathf.Cos(a)*3.64f);
                var bend=top+new Vector3(Mathf.Sin(a+2)*.59f,-2.48f,Mathf.Cos(a+2)*.54f);
                var lower=bend+new Vector3(Mathf.Sin(a+.3f)*.45f,-2.83f,Mathf.Cos(a+1)*.70f);
                walls.Tube(new[]{top,bend,lower},new[]{.19f,.105f,.019f},7,new Color(.23f,.178f,.123f));
            }
            BuildReflections(masonry,metal,slate);
            Put("Ruin columns, archways and causeway",masonry,stone);
            Put("Broken masonry and open shaft",walls,stone);
            Put("Gate etching",metal,stone);
            Put("Ruin lamps",lights,glow);
            LightVolume(new Vector3(.52f,-.43f,SinkZ+.15f),new Vector3(2.2f,CaveFloor-.12f,SinkZ+6.6f),
                .44f,4.2f,new Color(.47f,.75f,.72f,.62f),"Daylight in the shaft");
        }

        private void BuildReflections(WorldMesh masonry,WorldMesh metal,Color slate)
        {
            var ray=Geometry();
            ray.Tube(new[]{Vector3.zero,Vector3.forward},new[]{.012f,.023f},5,Color.white);
            Mesh rayMesh=ray.Finish("Reflected rays");
            for(int i=0;i<mirrors.Length;i++)
            {
                Vector3 plate=new Vector3(3.26f+i*.91f,CaveFloor+.15f,33.62f+i*1.73f);
                masonry.Box(plate+Vector3.up*.86f,new Vector3(.58f,1.70f,.46f),
                    Quaternion.Euler(0,i*25,0),slate);
                metal.Box(plate+Vector3.up*1.81f,new Vector3(.49f,.71f,.14f),
                    Quaternion.Euler(12,43+i*21,14-i*11),new Color(.53f,.60f,.52f));
                mirrors[i]=Lamp("Reflection target "+(i+1),plate+Vector3.up*1.83f,.117f,i==1?IrisBlue:Jade,.45f);
                reflections[i]=Put("Reflection beam "+(i+1),rayMesh,glow,plate+Vector3.up*1.84f,Vector3.one);
                reflections[i].rotation=Quaternion.LookRotation(new Vector3(-.52f,.23f,.51f+i*.04f));
            }
        }

        private void BuildStars()
        {
            var islands=Geometry();
            var sparks=Geometry();
            var halos=Geometry();
            var cool=new Color(.222f,.254f,.43f);
            var orbitCenter = new Vector3(3,StarY+3.7f,43);
            for(int i=0;i<20;i++)
            {
                float z=5+i*5.5f;
                float x = (i%2==0 ? 1 : -1)*Range(3.8f,12.8f);
                float y = StarY-Range(1.8f,10f)+2*Mathf.Sin(i*1.5f);
                Vector3 top=new Vector3(x,y,z);
                var size=new Vector3(Range(1.29f,4.5f),Range(.44f,.95f),Range(1.5f,3.6f));
                islands.Rock(top,size,WorldMesh.Shade(cool,Range(.78f,1.47f)),Range(0,5),5,11);
                islands.Rock(top-Vector3.up*(size.y+1.25f),new Vector3(size.x*.58f,2.12f,size.z*.63f),
                    WorldMesh.Shade(cool,.53f),Range(0,5),5,9,.36f);
                sparks.Tube(new[]{top+Vector3.left*size.x*.86f,top+new Vector3(.43f,.69f,.33f),top+Vector3.right*size.x*.82f},
                    new[]{.035f,.026f,.014f},5,new Color(.29f,.56f,.92f));
                for(int j=0;j<3;j++)
                {
                    Vector3 at=top+new Vector3(Range(-1.04f,1.04f),.75f,Range(-.63f,.63f));
                    islands.Rock(at,new Vector3(.18f,Range(.36f,.82f),.21f),
                        new Color(.37f,.54f,.86f),Range(0,6),3,5);
                }
            }
            for (int i=0;i<700;i++)
            {
                var at=new Vector3(Range(-104,104),StarY+Range(-20,67),Range(22,223));
                float size= Mathf.Pow(Range(.14f,1),3) * .095f + .013f;
                if(i%43==0) size*=2.9f;
                Color color=Color.Lerp(new Color(.34f,.42f,.88f), new Color(.87f,.93f,1),Range(.12f,1));
                sparks.Rock(at,new Vector3(size,size,size),color,i*.08f,3,5,.025f);
            }
            for(int i=0;i<5;i++)
            {
                Quaternion angle=Quaternion.Euler(18+i*9f,36+i*21f,23-i*3.8f);
                Color color = Color.Lerp(new Color(.23f,.39f,.88f),new Color(.90f,.63f,.41f),i*.17f);
                Ring(halos,orbitCenter+new Vector3(0,(i-2)*.36f,i*1.21f),6+i*1.91f,
                    i%2==0?.030f:.016f,color,angle,82,i==2?.84f:.52f + i*.074f);
            }
            var moon=Geometry();
            moon.Rock(new Vector3(-27,StarY+20,121),new Vector3(4.8f,4.8f,4.8f),
                new Color(.61f,.77f,.95f),.07f,14,28,.017f);
            Put("Suspended basalt islands",islands,stone);
            Put("Scattered stars at different depths",sparks,glow);
            Put("Orbital filaments",halos,glow);
            Put("Moon body",moon,stone);
            starCompass = Lamp("Constellation anchor",new Vector3(0,StarY+.5f,18),.125f,Pale,.4f).transform;

            var line=Geometry();
            line.Tube(new[]{Vector3.zero,Vector3.forward},new[]{.018f,.015f},6,Color.white);
            Mesh cord=line.Finish("Constellation light segment");
            for(int i=0;i<3;i++)
            {
                starLamps[i]=Lamp("Constellation star "+i,new Vector3((i-1)*2.2f,StarY+1+Mathf.Sin(i),22+i*3f),.155f,Warm,.4f);
                stars[i]=starLamps[i].transform;
                links[i]=Put("Constellation connection "+i,cord,glow,Vector3.zero,Vector3.one);
                linksLight[i]=links[i].GetComponent<MeshRenderer>();
            }
        }

        private void LightVolume(Vector3 start,Vector3 end,float from,float to,Color color,string name)
        {
            var mesh=Geometry();
            mesh.Tube(new[]{start,end}, new[]{from,to},18,color);
            Put(name,mesh,mist);
        }

        private void BuildFlies()
        {
            for (int i=0;i<2;i++)
            {
                Color color=i==0?Warm:Red;
                flies[i]=Lamp("Opening firefly "+i,Vector3.zero,.025f,color,2.2f);
                for(int side=0;side<2;side++)
                    flyWings[i*2+side]=Lamp("Firefly wing",Vector3.zero,.012f,Color.Lerp(Pale,color,.28f),.66f).transform;
            }
        }

        public void Flies(Vector3 amber,float warm,Vector3 scarlet,float red,float time)
        {
            for(int i=0;i<2;i++)
            {
                Vector3 at=i==0?amber:scarlet;
                float scale=i==0?warm:red;
                bool show=scale>.012f;
                Transform body=flies[i].transform;
                body.gameObject.SetActive(show);
                if(show)
                {
                    body.position=at;
                    body.localScale=Vector3.one*(.019f+.010f*scale);
                    Light(flies[i],i==0?Warm:Red,scale*(2.5f+.31f*Mathf.Sin(time*(i==0?5.4f:11.2f))));
                }
                for(int side=0;side<2;side++)
                {
                    Transform wing=flyWings[i*2+side];
                    wing.gameObject.SetActive(show);
                    if(!show) continue;
                    float sign=side==0?-1:1;
                    float beat=Mathf.Sin(time*44+i*2.8f);
                    wing.position=at+new Vector3(sign*(.022f+.012f*beat),.019f+.007f*beat,-.004f);
                    wing.localScale=new Vector3(.014f+.010f*Mathf.Abs(beat),.011f,.005f)*scale;
                    wing.rotation=Quaternion.Euler(0,sign*beat*36,-sign*26);
                }
            }
        }

        private static void Ring(WorldMesh mesh,Vector3 center,float radius,float thickness,Color color,Quaternion rotation,int steps,float arc)
        {
            var points=new Vector3[steps+1];
            var widths=new float[steps+1];
            for(int i=0;i<=steps;i++)
            {
                float a=i/(float)steps*6.283185f*arc;
                points[i]=center+rotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
                widths[i]=thickness;
            }
            mesh.Tube(points,widths,6,color);
        }

        public void Respond(Journey journey, float opening)
        {
            float time = opening >= 0 ? opening : journey.PoseTime;
            for(int i=0;i<courtSeals.Length;i++)
            {
                bool marked=journey.Lesson>i || (journey.Lesson==2 && journey.Cue==JourneyCue.Assist && i==2);
                Light(courtSeals[i],i==1?Jade:Warm,marked?1.19f:.18f+.07f*Mathf.Sin(time*2.8f+i));
            }
            for (int i=0;i<plantRays.Length;i++)
            {
                bool visible=!journey.InForest;
                bool ready=visible && journey.Plants>i && opening<0;
                plantRays[i].gameObject.SetActive(ready);
                plantLamps[i].gameObject.SetActive(visible);
                float flash=ready ? .54f+.08f*Mathf.Sin(time*3+i) : .13f;
                Light(plantLamps[i],Warm,flash);
                for(int j=0;j<2;j++)
                {
                    MeshRenderer landing=restLamps[i*2+j];
                    landing.gameObject.SetActive(ready);
                    if(ready) Light(landing,Warm,.66f+.21f*Mathf.Sin(time*3.04f+i-j*.54f));
                }
            }

            float cross = journey.Phase==Stage.RuinCrossing ? journey.Progress :
                journey.Chapter>=1 && journey.RuinResult!=RuinPath.Unfinished ? 1 : 0;
            for(int i=0;i<bridge.Length;i++)
            {
                Vector3 at=bridge[i].localPosition;
                at.y=CaveFloor+Mathf.Lerp(-1.55f-i*.065f,-.04f,Mathf.SmoothStep(0,1,Mathf.Clamp01(cross*2.1f-i*.125f)));
                bridge[i].localPosition=at;
            }
            Vector3 door=sideDoor.localPosition;
            door.y=CaveFloor+1.46f+(journey.Phase==Stage.RuinSide?Mathf.Min(1,journey.Age*1.8f)*3.05f:0);
            sideDoor.localPosition=door;
            float beat = Mathf.Abs(journey.Age - journey.Beat);
            bool side = journey.Phase == Stage.RuinSide;
            bool misread = journey.Phase==Stage.RuinMisread || journey.Phase==Stage.RuinRepair;
            for (int i=0;i<gates.Length;i++)
            {
                Color color = misread && i==1 ? Red : i==0 ? Warm : Jade;
                float phase = (journey.Phase==Stage.RuinVoice && i==0) ? .75f : i==1 && journey.Phase==Stage.RuinRelease
                    ? Mathf.Lerp(.28f,1.2f,1-Mathf.Clamp01(beat/.53f)) : side && i==2
                    ? Mathf.Lerp(.28f,1.2f,1-Mathf.Clamp01(beat/.64f)) : .48f;
                Light(gates[i],color,phase);
            }
            for(int i=0;i<mirrors.Length;i++)
            {
                bool passed=i<journey.ReflectionIndex || journey.RuinResult==RuinPath.Arcade;
                bool focus=side && i==journey.ReflectionIndex;
                Light(mirrors[i],i==1?IrisBlue:Jade,passed?1.17f:focus?.8f:.10f);
                reflections[i].gameObject.SetActive(passed || focus);
                reflections[i].localScale=new Vector3(1,1,passed?3.34f:
                    Mathf.Lerp(.50f,3.1f,1-Mathf.Clamp01(beat/.87f)));
            }

            for (int i=0;i<stars.Length;i++)
            {
                bool visible=opening<0 && journey.Chapter==2;
                stars[i].gameObject.SetActive(visible);
                StarPath path=journey.PathAt(i);
                Color color=path==StarPath.Corrected?new Color(.89f,.54f,.97f):path==StarPath.Delegated?Jade:Warm;
                Light(starLamps[i],color,path!=StarPath.Unfinished?1.8f:.34f+.16f*Mathf.Sin(time*4+i));
                links[i].gameObject.SetActive(visible && path!=StarPath.Unfinished);
                Vector3 from=i==0?starCompass.position:stars[i-1].position;
                Vector3 axis=stars[i].position-from;
                links[i].position=from;
                links[i].rotation=Quaternion.LookRotation(axis);
                links[i].localScale=new Vector3(1,1,axis.magnitude);
                Light(linksLight[i],color,.96f);
            }
        }
    }
}
