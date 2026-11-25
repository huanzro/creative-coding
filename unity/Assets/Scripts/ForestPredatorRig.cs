using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal readonly struct ForestPredatorShot
    {
        public readonly ForestTrapKind Kind;
        public readonly float Age, Enter, Clasp, Leave, Swallow;
        public readonly bool MawRescue;
        public bool Visible => Enter > .015f && Leave < .998f;
        public bool Web => Kind == ForestTrapKind.Web;

        private ForestPredatorShot(ForestTrapKind kind,float age,float enter,float clasp,float leave,float swallow,bool mawRescue=false)
        { Kind=kind; Age=age; Enter=enter; Clasp=clasp; Leave=leave; Swallow=swallow; MawRescue=mawRescue; }

        public static ForestPredatorShot Read(SpiritFrame frame) => frame.forest
            ? Sample(frame.forestTrap,frame.forestSnareAge,frame.forestFall,frame.forestDescent)
            : default;

        public static ForestPredatorShot Read(Journey journey) => Sample(journey.ForestTrap,
            journey.Phase==Stage.ForestTrapped ? journey.ForestRescueAuthoredAge : -1,
            journey.Phase==Stage.ForestFalling ? journey.Progress : -1,
            journey.Phase==Stage.ForestTransition ? journey.Progress : -1);

        private static ForestPredatorShot Sample(ForestTrapKind kind,float capture,float fall,float descent)
        {
            if(kind!=ForestTrapKind.Web && kind!=ForestTrapKind.Maw) return default;
            if(capture>=0)
            {
                if(kind==ForestTrapKind.Maw)
                    return new ForestPredatorShot(kind,capture,Ease(.02f,.62f,capture),
                        Ease(.31f,1.11f,capture),Ease(7.50f,8.93f,capture),0,true);
                float leave=Ease(7.20f,8.95f,capture);
                float clasp=Ease(.24f,.65f,capture)*(1-Ease(6.53f,7.31f,capture));
                return new ForestPredatorShot(kind,capture,Ease(.015f,.34f,capture),clasp,leave,0);
            }
            if(fall>=0)
                return new ForestPredatorShot(kind,fall*Journey.ForestFallDuration,Ease(.23f,.43f,fall),
                    Ease(.43f,.66f,fall),Ease(.78f,.99f,fall),Ease(Journey.ForestFallSwallowStart,.70f,fall));
            if(descent>=0)
                return new ForestPredatorShot(kind,descent*Journey.ForestTransitionDuration,Ease(.09f,.31f,descent),
                    Ease(.31f,.57f,descent),Ease(.71f,.995f,descent),Ease(Journey.ForestDescentSwallowStart,.64f,descent));
            return default;
        }

        private static float Ease(float from,float to,float value) =>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));

        public Matrix4x4 Prey(Vector3 center) => Swallow<=0 ? Matrix4x4.identity :
            Matrix4x4.TRS(center+new Vector3(0,Web ? .22f : -.16f,.22f)*Swallow,Quaternion.identity,
                Vector3.one*Mathf.Lerp(1,.02f,Swallow))*Matrix4x4.Translate(-center);
    }

    internal sealed class ForestPredatorRig : IDisposable
    {
        private readonly List<Mesh> meshes;
        private readonly Material material;
        private readonly Mesh[] spider, pitcher;
        private readonly Matrix4x4[] spiderPose=new Matrix4x4[11], pitcherPose=new Matrix4x4[3];
        private readonly Transform sceneRoot, spiderRoot, pitcherRoot;
        private readonly Transform[] spiderParts, pitcherParts;

        private static readonly Color Shell=new Color(.35f,.21f,.16f), Amber=new Color(1,.52f,.16f),
            Petal=new Color(.43f,.21f,.28f), Vein=new Color(.60f,.33f,.28f), Fang=new Color(.84f,.71f,.43f),
            Leaf=new Color(.31f,.46f,.24f);

        public ForestPredatorRig(Transform parent=null)
        {
            Shader shader=Resources.Load<Shader>("ForestPredator");
            if(shader==null || !shader.isSupported) return;
            material=new Material(shader) {name="Forest predator shell and petals"};
            material.SetFloat("_Projection",parent==null ? 1 : 0);
            meshes=new List<Mesh>(14);
            spider=BuildSpider();
            pitcher=BuildPitcher();
            if(parent==null) return;
            sceneRoot=Node("Forest predator performance",parent);
            spiderRoot=Node("Canopy spider",sceneRoot);
            pitcherRoot=Node("Carnivorous pitcher",sceneRoot);
            spiderParts=Attach(spider,spiderRoot);
            pitcherParts=Attach(pitcher,pitcherRoot);
        }

        public void PoseInWorld(ForestPredatorShot shot,Vector3 actor,Quaternion heading)
        {
            if(sceneRoot==null) return;
            sceneRoot.gameObject.SetActive(shot.Visible);
            if(!shot.Visible) return;
            sceneRoot.localPosition=actor;
            sceneRoot.localRotation=heading;
            sceneRoot.localScale=Vector3.one*.77f;
            spiderRoot.gameObject.SetActive(shot.Web);
            pitcherRoot.gameObject.SetActive(!shot.Web);
            Matrix4x4[] poses=Pose(shot);
            Transform[] parts=shot.Web ? spiderParts : pitcherParts;
            for(int i=0;i<parts.Length;i++)
            {
                Matrix4x4 pose=poses[i];
                parts[i].SetLocalPositionAndRotation(pose.GetColumn(3),pose.rotation);
                parts[i].localScale=pose.lossyScale;
            }
        }

        public void DrawPortrait(ForestPredatorShot shot,Vector3 actor,CommandBuffer buffer)
        {
            if(material==null || !shot.Visible) return;
            Matrix4x4[] poses=Pose(shot);
            Mesh[] model=shot.Web ? spider : pitcher;
            Matrix4x4 basis=Matrix4x4.TRS(actor,Quaternion.identity,Vector3.one*.87f);
            for(int i=0;i<model.Length;i++) buffer.DrawMesh(model[i],basis*poses[i],material);
        }

        private Matrix4x4[] Pose(ForestPredatorShot shot)
        {
            float rise=shot.Enter, clasp=shot.Clasp, leave=shot.Leave;
            float swing=Mathf.Sin(rise*Mathf.PI), age=shot.Age;
            float volume=Mathf.Lerp(.38f,1,rise)*(1-.71f*leave);
            float unsettled=.11f+.89f*swing;
            if(shot.Web)
            {
                Vector3 center=new Vector3(Mathf.Lerp(.44f,.025f,rise),
                    Mathf.Lerp(1.76f,.47f,rise)+.69f*leave-.07f*clasp,
                    Mathf.Lerp(.69f,.14f,rise)+.71f*leave);
                Quaternion heading=Quaternion.Euler(35*(1-rise)-12*clasp,
                    Mathf.Lerp(-31,8,rise)+12*leave,10*Mathf.Sin(age*7)*unsettled);
                spiderPose[0]=Matrix4x4.TRS(center,heading,Vector3.one*volume);
                for(int i=0;i<8;i++)
                {
                    int row=i/2, side=i%2==0?-1:1;
                    float gait=Mathf.Sin(age*15.5f+row*1.19f+side*.81f)*unsettled;
                    float gather=clasp*(.20f+row*.22f);
                    var turn=Quaternion.Euler(12*gait-16*gather,side*(12*gait+24*gather),
                        side*(16*gait-27*gather+16*(1-rise)));
                    spiderPose[1+i]=spiderPose[0]*Matrix4x4.TRS(
                        new Vector3(side*.20f,-.04f,.30f-row*.17f),turn,Vector3.one);
                }
                for(int i=0;i<2;i++)
                {
                    int side=i==0?-1:1;
                    float flex=clasp+.034f*Mathf.Sin(age*24);
                    spiderPose[9+i]=spiderPose[0]*Matrix4x4.TRS(new Vector3(side*.125f,-.095f,-.45f),
                        Quaternion.Euler(6*flex,side*(18+26*flex),-side*(11+45*flex)),Vector3.one);
                }
                return spiderPose;
            }

            if(shot.MawRescue) return PosePitcherRescue(shot,volume);

            Vector3 root=new Vector3(Mathf.Lerp(-.53f,.015f,rise),
                Mathf.Lerp(-1.64f,.015f,rise)-.72f*leave-.14f*clasp,
                Mathf.Lerp(.64f,.51f,rise)+.40f*leave);
            pitcherPose[0]=Matrix4x4.TRS(root,Quaternion.Euler(Mathf.Lerp(29,4,rise)+19*leave,
                -21*(1-rise)+13*leave,5*Mathf.Sin(age*6)*unsettled),Vector3.one*volume);
            float gape=Mathf.Lerp(46,4,clasp)+9*swing+2f*Mathf.Sin(age*8)*(1-clasp);
            pitcherPose[1]=pitcherPose[0]*Matrix4x4.Rotate(Quaternion.Euler(gape,0,0));
            pitcherPose[2]=pitcherPose[0]*Matrix4x4.Rotate(Quaternion.Euler(-gape*.88f,0,0));
            return pitcherPose;
        }

        private Matrix4x4[] PosePitcherRescue(ForestPredatorShot shot,float volume)
        {
            float t=shot.Age, appear=shot.Enter, leave=shot.Leave;
            var rescue=ForestRescueStoryboard.Sample(t,false);
            float shudder=Mathf.Sin(t*20)*rescue.mouthPulse;
            var center=new Vector3(Mathf.Lerp(-.53f,.015f,appear)+.26f*rescue.free+.055f*shudder,
                Mathf.Lerp(-1.64f,.045f,appear)-.13f*shot.Clasp-.84f*leave-.17f*rescue.pop,
                Mathf.Lerp(.64f,.51f,appear)+.50f*leave+.39f*rescue.free);
            var recoil=Quaternion.Euler(Mathf.Lerp(29,4,appear)+20*leave-19*rescue.pop,
                -21*(1-appear)+13*leave+15*rescue.free,shudder*7+11*rescue.pop);
            pitcherPose[0]=Matrix4x4.TRS(center,recoil,Vector3.one*volume);

            // The lower petal loses its grip first. The upper petal yields as the fly burns out.
            float bite=Mathf.Lerp(46,4,shot.Clasp)+8*Mathf.Sin(appear*Mathf.PI);
            float top=Mathf.Max(bite+2.8f*shudder,5+49*rescue.upperJaw+9*rescue.pop);
            float bottom=Mathf.Max(bite*.88f-4.8f*shudder,3+38*rescue.lowerJaw+6*rescue.pop);
            pitcherPose[1]=pitcherPose[0]*Matrix4x4.Rotate(Quaternion.Euler(top,0,0));
            pitcherPose[2]=pitcherPose[0]*Matrix4x4.Rotate(Quaternion.Euler(-bottom,0,0));
            return pitcherPose;
        }

        private Mesh[] BuildSpider()
        {
            var parts=new Mesh[11];
            var body=new WorldMesh(meshes);
            body.Rock(new Vector3(0,.10f,.26f),new Vector3(.39f,.33f,.53f),Shell,.30f,13,24,.09f);
            body.Rock(new Vector3(0,-.02f,-.28f),new Vector3(.29f,.24f,.30f),WorldMesh.Shade(Shell,1.23f),.41f,11,21,.08f);
            for(int row=0;row<5;row++)
            {
                float z=.04f+row*.14f;
                body.Tube(new[]{new Vector3(-.24f,.29f,z-.08f),new Vector3(0,.414f-row*.012f,z),
                    new Vector3(.24f,.29f,z-.08f)},new[]{.020f,.025f,.020f},6,new Color(.56f,.32f,.19f));
            }
            for(int side=-1;side<=1;side+=2)
            for(int eye=0;eye<3;eye++)
            {
                var eyeAt=new Vector3(side*(.055f+eye*.083f),.035f+.038f*eye,-.545f+.036f*eye);
                float size=.045f-eye*.008f;
                body.Rock(eyeAt,Vector3.one*size,Amber,eye*.37f,5,8,.025f);
                body.Rock(eyeAt+new Vector3(-.012f,.014f,-size*.78f),Vector3.one*(size*.28f),Fang,.3f,3,6,.08f);
            }
            parts[0]=body.Finish("Banded spider and six eyes");
            for(int i=0;i<8;i++)
            {
                int row=i/2, side=i%2==0?-1:1;
                float depth=(1.5f-row)*.37f;
                var leg=new WorldMesh(meshes);
                Vector3 rise=new Vector3(side*(.49f+.025f*row),.31f-row*.070f,depth*.58f);
                Vector3 knee=new Vector3(side*(.73f+.036f*row),.28f-row*.10f,depth);
                leg.Tube(new[]{Vector3.zero,Vector3.Lerp(Vector3.zero,rise,.59f),rise,knee,new Vector3(side*(.88f+.04f*row),-.14f-row*.10f,depth*1.46f),
                    new Vector3(side*(.96f+.02f*row),-.25f-row*.095f,depth*1.89f)},
                    new[]{.062f,.055f,.048f,.040f,.025f,.006f},12,Shell);
                leg.Rock(knee,new Vector3(.055f,.063f,.048f),WorldMesh.Shade(Shell,1.37f),row*.23f,4,7,.13f);
                parts[1+i]=leg.Finish("Hinged canopy leg");
            }
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                var fang=new WorldMesh(meshes);
                fang.Rock(new Vector3(side*.032f,-.075f,-.055f),new Vector3(.078f,.105f,.088f),Shell,.44f,5,8,.1f);
                fang.Tube(new[]{new Vector3(side*.050f,-.09f,-.09f),new Vector3(side*.075f,-.255f,-.128f),
                    new Vector3(-side*.037f,-.36f,-.080f)},new[]{.052f,.030f,.006f},8,Fang);
                parts[9+i]=fang.Finish("Curved spider fang");
            }
            return parts;
        }

        private Mesh[] BuildPitcher()
        {
            var plant=new WorldMesh(meshes);
            plant.Tube(new[]{new Vector3(.35f,-1.37f,.30f),new Vector3(-.11f,-.90f,.38f),
                new Vector3(-.24f,-.45f,.37f),new Vector3(0,0,.07f)},new[]{.080f,.108f,.13f,.23f},11,Leaf);
            plant.Rock(new Vector3(0,0,.073f),new Vector3(.42f,.275f,.29f),WorldMesh.Shade(Petal,.66f),.71f,7,13,.14f);
            for(int i=0;i<7;i++)
            {
                float a=i*2.39996f;
                var root=new Vector3(Mathf.Cos(a)*.13f,-.38f-.082f*i,Mathf.Sin(a)*.17f+.24f);
                var tip=root+new Vector3(Mathf.Cos(a)*(.56f+.026f*i),.33f-.071f*i,Mathf.Sin(a)*.37f);
                plant.Leaf(root,tip,Vector3.Cross(tip-root,Vector3.up).normalized,.12f+i*.01f,
                    WorldMesh.Shade(Leaf,.86f+i*.061f));
                plant.Tube(new[]{root,Vector3.Lerp(root,tip,.51f)+Vector3.up*.033f,tip},
                    new[]{.021f,.016f,.001f},5,WorldMesh.Shade(Leaf,1.35f));
            }
            return new[]{plant.Finish("Pitcher stem and bracts"),BuildJaw(1),BuildJaw(-1)};
        }

        private Mesh BuildJaw(int side)
        {
            var jaw=new WorldMesh(meshes);
            var flesh=new Vector3(0,side*.10f,-.40f);
            jaw.Rock(flesh,new Vector3(.685f,.183f,.61f),Petal,side*.59f,13,27,.10f);
            jaw.Rock(new Vector3(0,side*.018f,-.40f),new Vector3(.57f,.069f,.50f),
                new Color(.22f,.077f,.123f),.45f,10,24,.10f);
            for(int i=0;i<11;i++)
            {
                float a=.14f+i*(Mathf.PI-.28f)/10;
                var rim=new Vector3(Mathf.Cos(a)*.63f,side*.063f,-.37f-Mathf.Sin(a)*.57f);
                var tip=new Vector3(rim.x*.91f,-side*(.13f+.062f*Mathf.Sin(i*1.73f)),rim.z+.057f);
                jaw.Tube(new[]{rim,Vector3.Lerp(rim,tip,.50f),Vector3.Lerp(rim,tip,.80f),tip},
                    new[]{.037f,.027f,.014f,.002f},10,Fang);
                var ridge=new Vector3(rim.x*.86f,side*.23f,rim.z*.78f);
                jaw.Tube(new[]{rim,ridge,new Vector3(rim.x*.24f,side*.222f,-.22f)},
                    new[]{.022f,.020f,.005f},6,Vein);
                jaw.Leaf(ridge,new Vector3(rim.x*1.1f,side*.11f,rim.z*.99f),Vector3.up,.055f,Leaf);
            }
            return jaw.Finish(side>0 ? "Upper toothed pitcher lobe" : "Lower toothed pitcher lobe");
        }

        private static Transform Node(string name,Transform parent)
        {
            var node=new GameObject(name) {hideFlags=HideFlags.HideAndDontSave,layer=parent.gameObject.layer};
            node.transform.SetParent(parent,false);
            return node.transform;
        }

        private Transform[] Attach(Mesh[] parts,Transform parent)
        {
            var nodes=new Transform[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                nodes[i]=Node(parts[i].name,parent);
                nodes[i].gameObject.AddComponent<MeshFilter>().sharedMesh=parts[i];
                var renderer=nodes[i].gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
            return nodes;
        }

        public void Dispose()
        {
            if(meshes==null) return;
            if(sceneRoot!=null) UnityEngine.Object.Destroy(sceneRoot.gameObject);
            foreach(Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
        }
    }
}
