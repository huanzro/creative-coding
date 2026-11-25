// ARTISTIC RATIONALE
// The archive makes abstract human discoveries visible as floating objects: a ray bends,
// a geometric relationship balances, and a memory links to another. Color marks each idea
// within its own instrument or diagram, while the tunnel preserves a shared visual language.
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private static void ExhibitPrism(WorldMesh form)
        {
            Color glass = new Color(.43f,.75f,.86f);
            Vector3 a = new Vector3(-.54f,-.42f,-.28f), b = new Vector3(.42f,-.42f,-.28f), c = new Vector3(-.05f,.54f,-.28f);
            Vector3 front = new Vector3(.15f,.05f,.59f);
            form.Triangle(a,b,c,WorldMesh.Shade(glass,.67f));
            form.Triangle(a+front,c+front,b+front,WorldMesh.Shade(glass,.92f));
            form.Quad(a,b,b+front,a+front,WorldMesh.Shade(glass,.66f));
            form.Quad(b,c,c+front,b+front,WorldMesh.Shade(glass,.82f));
            form.Quad(c,a,a+front,c+front,WorldMesh.Shade(glass,.99f));
            Vector3[] corners = { a,b,c };
            for (int side=0;side<corners.Length;side++)
            {
                Vector3 start=corners[side], next=corners[(side+1)%corners.Length];
                ExhibitBeam(form,start,next,.034f,LimeStone);
                ExhibitBeam(form,start+front,next+front,.027f,glass);
                ExhibitBeam(form,start,start+front,.028f,glass);
            }

            Vector3 contact = new Vector3(-.34f,.08f,-.32f);
            ExhibitBeam(form,new Vector3(-1.43f,.42f,-.34f),contact,.049f,LimeStone);
            ExhibitBeam(form,contact,new Vector3(.24f,-.067f,-.33f),.032f,new Color(.77f,.96f,1));
            Color[] spectrum = {
                new Color(1,.31f,.27f),new Color(1,.61f,.32f),new Color(1,.97f,.42f),
                new Color(.48f,1,.48f),new Color(.39f,.8f,1),new Color(.71f,.53f,1)
            };
            for(int ray=0;ray<spectrum.Length;ray++)
                ExhibitBeam(form,new Vector3(.19f,-.063f-ray*.018f,-.34f),
                    new Vector3(1.37f,.21f-ray*.129f,-.36f-ray*.005f),.037f,spectrum[ray]);
        }

        private static void ExhibitPendulum(WorldMesh form)
        {
            Color brass = FiredBronze, ink = new Color(.27f,.38f,.49f);
            Vector3 floor = new Vector3(0,-.57f,0);
            form.Rock(floor,new Vector3(.83f,.11f,.70f),ink,.09f,4,24,.04f);
            ExhibitCircle(form,floor+Vector3.up*.105f,.72f,.58f,Vector3.right,Vector3.forward,.029f,brass,40);
            for(int tick=0;tick<24;tick++)
            {
                float angle=tick*Mathf.PI/12;
                Vector3 ray=new Vector3(Mathf.Cos(angle)*.72f,0,Mathf.Sin(angle)*.58f);
                ExhibitBeam(form,floor+Vector3.up*.13f+ray*.91f,floor+Vector3.up*.13f+ray,.017f,LimeStone);
            }
            Vector3 top=new Vector3(0,.90f,.07f), bob=new Vector3(.25f,-.18f,-.19f);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 foot=new Vector3(side*.61f,-.49f,.18f), shoulder=new Vector3(side*.42f,.66f,.18f);
                form.Tube(new[]{foot,shoulder,top},new[]{.047f,.036f,.034f},9,brass);
            }
            ExhibitBeam(form,top,bob,.023f,LimeStone);
            form.Rock(bob,new Vector3(.18f,.21f,.18f),brass,.03f,8,21,.015f);
        }

        private static void ExhibitProof(WorldMesh form)
        {
            var a=new Vector3(-.53f,-.45f,.045f);
            var b=new Vector3(.67f,-.45f,.045f);
            var c=new Vector3(-.53f,.45f,.045f);
            ProofSquare(form,a,b,new Color(.33f,.91f,.85f),4);
            ProofSquare(form,b,c,new Color(.94f,.66f,.39f),5);
            ProofSquare(form,c,a,new Color(.74f,.61f,1f),3);
            form.Triangle(c,b,a,new Color(.60f,.68f,.84f));
            ExhibitBeam(form,a,b,.054f,LimeStone);
            ExhibitBeam(form,b,c,.054f,LimeStone);
            ExhibitBeam(form,c,a,.054f,LimeStone);
            Vector3 inner = a + new Vector3(.19f,.19f,-.042f);
            ExhibitBeam(form,a+new Vector3(.19f,0,-.042f),inner,.045f,LimeStone);
            ExhibitBeam(form,inner,a+new Vector3(0,.19f,-.042f),.045f,LimeStone);
        }

        private static void ProofSquare(WorldMesh form,Vector3 a,Vector3 b,Color color,int cells)
        {
            Vector3 edge=b-a, outside=new Vector3(edge.y,-edge.x,0);
            form.Quad(a,b,b+outside,a+outside,WorldMesh.Shade(color,.33f));
            for(int grid=0;grid<=cells;grid++)
            {
                Vector3 row=outside*(grid/(float)cells), col=edge*(grid/(float)cells), front=Vector3.back*.04f;
                float width=grid==0 || grid==cells ? .046f : .019f;
                ExhibitBeam(form,a+row+front,b+row+front,width,color);
                ExhibitBeam(form,a+col+front,a+col+outside+front,width,color);
            }
        }

        private static void ExhibitConics(WorldMesh form)
        {
            Color wire=new Color(.48f,.63f,.89f), solution=new Color(1f,.65f,.41f);
            Vector3 apex=new Vector3(-.51f,0,.15f);
            for(int direction=-1;direction<=1;direction+=2)
            {
                Vector3 ring=apex+Vector3.up*direction*.83f;
                ExhibitCircle(form,ring,.48f,.48f,Vector3.right,Vector3.forward,.028f,wire,29);
                for(int rib=0;rib<10;rib++)
                {
                    float angle=rib*Mathf.PI/5;
                    ExhibitBeam(form,apex,ring+new Vector3(Mathf.Cos(angle)*.48f,0,Mathf.Sin(angle)*.48f),.025f,wire);
                }
            }
            Vector3 section=apex+new Vector3(0,-.41f,.07f);
            ExhibitCircle(form,section,.34f,.23f,new Vector3(.88f,.46f,0),Vector3.forward,.047f,solution,27);
            var plane=new Vector3(.8f,.03f,.20f);
            form.Box(plane,new Vector3(.73f,1.48f,.075f),Quaternion.identity,SlateInk);
            ExhibitCircle(form,plane+new Vector3(0,.42f,-.051f),.227f,.125f,Vector3.right,Vector3.up,.026f,LimeStone,23);
            Vector3 a=plane+new Vector3(-.27f,-.55f+4.9f*.27f*.27f,-.052f);
            for(int step=1;step<=14;step++)
            {
                float x=(step/14f-.5f)*.54f;
                Vector3 b=plane+new Vector3(x,-.55f+4.9f*x*x,-.052f);
                ExhibitBeam(form,a,b,.026f,solution);
                a=b;
            }
        }

        private static void ExhibitCodex(WorldMesh form)
        {
            Color leather=new Color(.58f,.26f,.31f), paper=new Color(.96f,.84f,.66f), text=new Color(.48f,.26f,.26f);
            ExhibitBeam(form,new Vector3(0,-.7f,.16f),new Vector3(0,.69f,.16f),.133f,leather);
            for(int side=-1;side<=1;side+=2)
            {
                var cover=Quaternion.Euler(0,side*13,0);
                form.Box(new Vector3(side*.43f,0,.24f),new Vector3(.92f,1.50f,.112f),cover,leather);
                for(int leaf=0;leaf<6;leaf++)
                    form.Box(new Vector3(side*(.416f-leaf*.004f),0,.158f-leaf*.021f),
                        new Vector3(.86f-leaf*.010f,1.39f,.025f),cover,WorldMesh.Shade(paper,.8f+leaf*.04f));
                for(int line=0;line<9;line++)
                {
                    float y=.42f-line*.10f;
                    Vector3 start=new Vector3(side*.17f,y,-.074f);
                    Vector3 end=new Vector3(side*(.65f-.11f*Mathf.Pow(Mathf.Sin(line*2f+side),2)),y,-.177f);
                    ExhibitBeam(form,start,end,.017f,text);
                }
                form.Box(new Vector3(side*.28f,.51f,-.062f),new Vector3(.23f,.12f,.024f),cover,FiredBronze);
                ExhibitBeam(form,new Vector3(side*.716f,.58f,-.136f),new Vector3(side*.716f,-.54f,-.136f),.026f,FiredBronze);
            }
            ExhibitBeam(form,new Vector3(.06f,.41f,-.02f),new Vector3(.03f,-.82f,-.18f),.051f,new Color(.85f,.43f,.45f));
        }

        private static void ExhibitPunchedCard(WorldMesh form)
        {
            Color card=new Color(.85f,.67f,.44f), edge=new Color(.57f,.41f,.27f);
            const int columns=15, rows=8;
            for(int copy=0;copy<3;copy++)
            {
                var turn=Quaternion.Euler(1,copy*13-17,copy*17-17);
                Vector3 centre=new Vector3(copy*.14f-.12f,copy*.10f-.10f,copy*.12f);
                for(int col=0;col<columns;col++)
                for(int row=0;row<rows;row++)
                {
                    float x=(col-7)*.106f, y=(row-3.5f)*.103f;
                    Vector3 at=centre+turn*new Vector3(x,y,0);
                    bool punched=row>0 && row<rows-1 && col>0 && col<columns-1 && (row*11+col*5+copy*7)%13<4;
                    Color tint=WorldMesh.Shade(card,.89f+.06f*((col+copy)%3));
                    if (!punched) form.Box(at,new Vector3(.107f,.104f,.027f),turn,tint);
                    else
                    {
                        for(int side=-1;side<=1;side+=2)
                        {
                            form.Box(at+turn*new Vector3(side*.044f,0,0),new Vector3(.020f,.103f,.028f),turn,tint);
                            form.Box(at+turn*new Vector3(0,side*.037f,0),new Vector3(.074f,.031f,.028f),turn,edge);
                        }
                    }
                }
            }
        }

        private static void ExhibitLogic(WorldMesh form)
        {
            Color input=new Color(.40f,.98f,.74f), output=new Color(.98f,.74f,.36f);
            form.Box(Vector3.zero,new Vector3(2.15f,1.24f,.105f),Quaternion.identity,new Color(.124f,.27f,.30f));
            for(int row=0;row<3;row++)
            {
                float y=(row-1)*.35f;
                var origin=new Vector3(-.90f,y,-.071f);
                form.Rock(origin,new Vector3(.072f,.073f,.048f),input,row*.3f,4,11,.04f);
                ExhibitBeam(form,origin,new Vector3(-.49f,y,-.073f),.027f,input);
                ExhibitBeam(form,new Vector3(-.49f,y,-.073f),new Vector3(.18f,y*.34f,-.073f),.026f,input);
            }
            Vector3 gate=new Vector3(.07f,0,-.089f), top=new Vector3(-.35f,.46f,-.089f), bottom=new Vector3(-.35f,-.46f,-.089f);
            ExhibitBeam(form,top,bottom,.075f,LimeStone);
            ExhibitBeam(form,top,gate+new Vector3(.38f,0,0),.075f,LimeStone);
            ExhibitBeam(form,bottom,gate+new Vector3(.38f,0,0),.075f,LimeStone);
            ExhibitCircle(form,new Vector3(.52f,0,-.093f),.079f,.079f,Vector3.right,Vector3.up,.035f,LimeStone,15);
            ExhibitBeam(form,new Vector3(.61f,0,-.076f),new Vector3(.92f,0,-.076f),.044f,output);
            form.Rock(new Vector3(.91f,0,-.082f),new Vector3(.082f,.095f,.051f),output,.083f,5,12,.028f);
        }

        private static void ExhibitChip(WorldMesh form)
        {
            Color substrate=new Color(.30f,.53f,.50f), gold=new Color(.95f,.75f,.34f);
            form.Box(Vector3.zero,new Vector3(1.31f,1.31f,.21f),Quaternion.identity,SlateInk);
            form.Box(new Vector3(0,0,-.125f),new Vector3(.97f,.97f,.115f),Quaternion.identity,substrate);
            form.Box(new Vector3(0,0,-.192f),new Vector3(.65f,.65f,.052f),Quaternion.identity,gold);
            for(int x=-2;x<=2;x++)
            for(int y=-2;y<=2;y++)
                form.Box(new Vector3(x*.113f,y*.113f,-.227f),new Vector3(.076f,.077f,.029f),Quaternion.identity,
                    WorldMesh.Shade(ExhibitBlue,.74f+.10f*((x+y+4)%4)));
            for(int side=0;side<4;side++)
            {
                var turn=Quaternion.Euler(0,0,side*90);
                for (int pin=-5;pin<=5;pin++)
                {
                    float x=pin*.104f;
                    ExhibitBeam(form,turn*new Vector3(x,.57f,-.114f),turn*new Vector3(x,.78f,-.02f),.033f,gold);
                    ExhibitBeam(form,turn*new Vector3(x,.78f,-.02f),turn*new Vector3(x,.94f,.044f),.030f,gold);
                    ExhibitBeam(form,turn*new Vector3(x*.83f,.35f,-.222f),turn*new Vector3(x,.55f,-.163f),.018f,substrate);
                }
            }
        }

        private static void ExhibitLesson(WorldMesh form)
        {
            Color chalk=new Color(.80f,.94f,.81f), timber=new Color(.70f,.48f,.31f);
            form.Box(new Vector3(0,.08f,.091f),new Vector3(1.89f,1.19f,.13f),Quaternion.identity,timber);
            form.Box(new Vector3(0,.08f,.014f),new Vector3(1.73f,1.04f,.069f),Quaternion.identity,new Color(.17f,.37f,.32f));
            for(int index=-2;index<=2;index++)
                ExhibitBeam(form,new Vector3(-.73f,index*.16f-.03f,-.033f),new Vector3(.73f,index*.16f-.03f,-.033f),.013f,WorldMesh.Shade(chalk,.59f));
            ExhibitBeam(form,new Vector3(-.62f,-.26f,-.052f),new Vector3(-.42f,.36f,-.052f),.050f,chalk);
            ExhibitBeam(form,new Vector3(-.42f,.36f,-.052f),new Vector3(-.21f,-.26f,-.052f),.050f,chalk);
            ExhibitBeam(form,new Vector3(-.55f,-.047f,-.052f),new Vector3(-.28f,-.047f,-.052f),.044f,chalk);
            ExhibitCircle(form,new Vector3(.17f,.053f,-.054f),.20f,.16f,Vector3.right,Vector3.up,.037f,chalk,25);
            for(int i=0;i<3;i++)
                form.Box(new Vector3(.45f+i*.10f,-.22f+i*.081f,-.047f),new Vector3(.057f,.13f+i*.16f,.023f),Quaternion.identity,chalk);
            form.Box(new Vector3(.53f,-.48f,-.061f),new Vector3(.43f,.068f,.143f),Quaternion.identity,timber);
            form.Box(new Vector3(.51f,-.427f,-.113f),new Vector3(.207f,.05f,.085f),Quaternion.Euler(0,-16,0),LimeStone);
        }

        private static void ExhibitClassroom(WorldMesh form)
        {
            Color wood=new Color(.72f,.51f,.36f), ink=new Color(.23f,.39f,.37f);
            form.Box(new Vector3(0,-.65f,0),new Vector3(1.86f,.085f,1.93f),Quaternion.identity,wood);
            form.Box(new Vector3(0,.40f,.70f),new Vector3(1.48f,.68f,.115f),Quaternion.identity,wood);
            form.Box(new Vector3(0,.40f,.623f),new Vector3(1.29f,.53f,.071f),Quaternion.identity,ink);
            for(int row=0;row<2;row++)
            for(int col=-1;col<=1;col+=2)
            {
                Vector3 desk = new Vector3(col*.45f,-.31f,.25f-row*.78f);
                for(int side=-1;side<=1;side+=2)
                    ExhibitBeam(form,desk+new Vector3(side*.195f,-.26f,-.055f),
                        desk+new Vector3(side*.195f,.11f,.095f),.067f,ink);
                form.Box(desk+new Vector3(0,.11f,0),new Vector3(.52f,.073f,.29f),Quaternion.Euler(-11,0,0),wood);
                form.Box(desk+new Vector3(.03f,.16f,-.017f),new Vector3(.23f,.043f,.177f),Quaternion.Euler(-8,9,0),LimeStone);
                form.Box(desk+new Vector3(0,-.081f,-.34f),new Vector3(.39f,.065f,.23f),Quaternion.identity,wood);
                form.Box(desk+new Vector3(0,.058f,-.45f),new Vector3(.38f,.27f,.053f),Quaternion.identity,ink);
            }
        }

        private static void ExhibitMicroscope(WorldMesh form)
        {
            Color metal=new Color(.70f,.77f,.72f), enamel=new Color(.32f,.61f,.55f), lens=new Color(.50f,.84f,.94f);
            form.Rock(new Vector3(0,-.65f,0),new Vector3(.64f,.15f,.51f),enamel,.14f,4,24,.028f);
            var hinge=new[]{new Vector3(.41f,-.55f,.09f),new Vector3(.53f,-.21f,.09f),new Vector3(.46f,.21f,.09f),
                new Vector3(.17f,.52f,.09f),new Vector3(-.15f,.54f,.09f)};
            form.Tube(hinge,new[]{.111f,.126f,.113f,.097f,.077f},13,metal);
            var tube=new[]{new Vector3(-.47f,.91f,-.06f),new Vector3(-.23f,.56f,-.06f),new Vector3(-.09f,.22f,-.06f)};
            form.Tube(tube,new[]{.105f,.12f,.10f},16,enamel);
            form.Rock(tube[0],new Vector3(.10f,.082f,.10f),lens,.11f,5,15,.03f);
            form.Tube(new[]{tube[2],new Vector3(-.065f,.021f,-.06f)},new[]{.073f,.061f},12,metal);
            form.Box(new Vector3(-.12f,-.135f,-.05f),new Vector3(.55f,.075f,.42f),Quaternion.identity,SlateInk);
            form.Box(new Vector3(-.151f,-.084f,-.047f),new Vector3(.31f,.025f,.160f),Quaternion.identity,lens);
            form.Rock(new Vector3(.41f,.006f,-.105f),new Vector3(.16f,.16f,.072f),enamel,.16f,5,17,.044f);
            form.Rock(new Vector3(-.1f,-.39f,0),new Vector3(.124f,.049f,.134f),lens,.10f,5,16,.03f);
        }

        private static void ExhibitRadiograph(WorldMesh form)
        {
            Color glass=new Color(.18f,.29f,.42f), bone=new Color(.63f,.86f,.94f);
            form.Box(Vector3.zero,new Vector3(1.34f,1.64f,.108f),Quaternion.identity,SlateInk);
            form.Box(new Vector3(0,.025f,-.064f),new Vector3(1.24f,1.49f,.044f),Quaternion.identity,glass);
            for(int rib=0;rib<8;rib++)
            {
                float y=.40f-rib*.103f;
                float width=.37f+.14f*Mathf.Sin((rib+1)*Mathf.PI/10);
                ExhibitBeam(form,new Vector3(0,y+.060f,-.1f),new Vector3(0,y-.040f,-.10f),.041f,bone);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 before=new Vector3(side*.057f,y,-.10f);
                    for(int point=1;point<=12;point++)
                    {
                        float t=point/12f;
                        var after=new Vector3(side*(.054f+width*Mathf.Sin(t*2.89f)),y-.10f*t+.076f*Mathf.Sin(t*2.5f),-.103f);
                        ExhibitBeam(form,before,after,.026f,bone);
                        before=after;
                    }
                }
            }
            ExhibitBeam(form,new Vector3(-.42f,.481f,-.107f),new Vector3(-.049f,.589f,-.107f),.050f,bone);
            ExhibitBeam(form,new Vector3(.42f,.481f,-.107f),new Vector3(.049f,.589f,-.107f),.050f,bone);
            for(int side=-1;side<=1;side+=2)
                form.Box(new Vector3(side*.585f,.705f,-.100f),new Vector3(.11f,.099f,.036f),Quaternion.identity,PaleJade);
        }

        private static void ExhibitPerception(WorldMesh form)
        {
            Color[] edges={new Color(.90f,.62f,.94f),new Color(.47f,.83f,1f),new Color(1f,.74f,.53f)};
            var skew=Quaternion.Euler(-9,26,18);
            for(int axis=0;axis<3;axis++)
            for(int u=-1;u<=1;u+=2)
            for(int v=-1;v<=1;v+=2)
            {
                var a=new Vector3(u*.51f,v*.51f,-.51f);
                var b=new Vector3(u*.51f,v*.51f,.51f);
                var turn=Quaternion.Euler(axis==1 ? 90 : 0,axis==2 ? 90 : 0,0);
                ExhibitBeam(form,skew*(turn*a),skew*(turn*b),.077f,edges[axis]);
            }
            for(int corner=0;corner<8;corner++)
            {
                var point=new Vector3((corner&1)==0?-.51f:.51f,(corner&2)==0?-.51f:.51f,(corner&4)==0?-.51f:.51f);
                form.Rock(skew*point,new Vector3(.056f,.056f,.055f),LimeStone,corner*.16f,3,8,.025f);
            }
            ExhibitCircle(form,new Vector3(0,0,.25f),1.0f,.91f,Vector3.right,Vector3.up,.022f,WorldMesh.Shade(edges[0],.78f),42);
        }

        private static void ExhibitMemory(WorldMesh form)
        {
            var points=new[] {new Vector3(0,0,-.13f),new Vector3(-.78f,.41f,-.10f),new Vector3(-.64f,-.51f,.085f),
                new Vector3(.51f,.56f,-.035f),new Vector3(.81f,.20f,.21f),new Vector3(.37f,-.60f,-.065f),
                new Vector3(-.12f,.72f,.29f),new Vector3(.26f,.04f,.65f),new Vector3(-.70f,-.045f,.58f)};
            Color central = new Color(.97f,.69f,.88f), link = new Color(.52f,.70f,.96f);
            for(int i=1;i<points.Length;i++)
            {
                ExhibitBeam(form,points[0],points[i],.025f,link);
                var next=points[i==points.Length-1 ? 1 : i+1];
                ExhibitBeam(form,points[i],next,.018f,WorldMesh.Shade(link,.79f));
                form.Rock(points[i],new Vector3(.091f,.09f,.092f),i%2==0 ? link : central,i*.09f,5,12,.05f);
            }
            form.Rock(points[0],new Vector3(.20f,.20f,.20f),central,.04f,7,17,.04f);
        }

        private static void ExhibitReaction(WorldMesh form)
        {
            Color bronze=new Color(.71f,.58f,.46f), cue=new Color(.93f,.53f,.80f);
            form.Box(new Vector3(0,-.32f,.13f),new Vector3(1.55f,.53f,.55f),Quaternion.identity,SlateInk);
            for(int side=-1;side<=1;side+=2)
            {
                var button=new Vector3(side*.43f,-.066f,-.02f);
                form.Rock(button,new Vector3(.16f,.076f,.15f),side<0 ? PaleJade : cue,.09f,5,14,.025f);
                ExhibitCircle(form,button,.194f,.19f,Vector3.right,Vector3.forward,.021f,bronze,23);
            }
            Vector3 dial=new Vector3(0,.34f,-.08f);
            form.Rock(dial+Vector3.forward*.09f,new Vector3(.40f,.42f,.13f),bronze,.12f,7,23,.028f);
            ExhibitCircle(form,dial,.35f,.35f,Vector3.right,Vector3.up,.028f,LimeStone,39);
            for(int tick=0;tick<12;tick++)
            {
                float a=tick*Mathf.PI/6;
                Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                ExhibitBeam(form,dial+d*.31f,dial+d*.25f,.016f,LimeStone);
            }
            ExhibitBeam(form,dial-Vector3.forward*.046f,dial+new Vector3(.134f,.183f,-.046f),.039f,cue);
            form.Rock(dial-Vector3.forward*.047f,new Vector3(.047f,.047f,.038f),cue,.19f,4,11,.023f);
        }
    }
}
