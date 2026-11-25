using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal readonly struct MachineDoorShot
    {
        public readonly float Progress, Threshold, Drop, Recoil, Press, Reverse, GearAngle, GearSlit;
        public readonly float RobotRetreat, RobotPass, RobotDrop, Hatch, Water, Sink;

        public MachineDoorShot(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            Threshold = Ease(.14f, .32f, Progress);
            Drop = Ease(.18f, .34f, Progress);
            Recoil = Ease(.45f, .59f, Progress) - Ease(.69f, .74f, Progress);
            Press = Ease(.58f, .87f, Progress);
            Reverse = Ease(.49f, .63f, Progress);
            GearAngle = (Ease(.31f, .48f, Progress) - Reverse) * 47
                + 65 * Ease(.37f, .54f, Progress) - 230 * Ease(.52f, .89f, Progress);
            GearSlit = Ease(.80f, .89f, Progress) * (1 - .94f * Ease(.91f, 1, Progress));
            RobotRetreat = Ease(.42f, .56f, Progress) - Ease(.57f, .68f, Progress);
            RobotPass = Ease(.60f, .81f, Progress);
            RobotDrop = Ease(Journey.ForestRecyclerSwallowStart, .95f, Progress);
            Hatch = Ease(.67f, .78f, Progress) * (1 - Ease(.88f, .97f, Progress));
            Water = Ease(.44f, .91f, Progress);
            Sink = Ease(.58f, .91f, Progress);
        }

        public float GearCompression => Ease(.77f, .88f, Progress);
        public float Spark(int index) => Ease(.84f + index * .002f, .867f + index * .003f, Progress) *
            (1 - Ease(.89f, .97f, Progress));
        public float RobotApproach(int index) => Ease(.34f + index * .014f, .67f + index * .017f, Progress);
        public float RobotReach(int index)
        {
            float cue = .52f + (index % 4) * .061f;
            return Ease(cue, cue + .115f, Progress) * (1 - Ease(cue + .15f, cue + .20f, Progress));
        }
        public float RobotAlarm(int index) => Ease(.43f + index * .009f, .57f + index * .011f, Progress);
        public float EchoAppear(int index)
        {
            float cue = index == 0 ? .34f : .51f + index * .014f;
            return Ease(cue, cue + .13f, Progress);
        }
        public float EchoRemain(int index) => index == 0 ? 1 : 1 - Ease(.85f + index * .002f, .97f, Progress);
        private static float Ease(float from, float to, float t) => OpeningStoryboard.Ease(from, to, t);
    }

    internal sealed class MachineDoorPortrait : IDisposable
    {
        private static readonly Color Bronze = new Color(.69f, .45f, .26f), Iron = new Color(.35f, .46f, .54f);
        private static readonly Color Warm = new Color(1, .59f, .22f), Circuit = new Color(.36f, .81f, 1);
        private static readonly Color Alarm = new Color(1, .29f, .17f), Pool = new Color(.45f, .52f, 1);
        private static readonly Color Dark = new Color(.11f, .17f, .21f);
        private static readonly Vector3[] RobotStations =
        {
            new Vector3(-.89f, .19f, -.18f), new Vector3(.91f, .17f, .12f), new Vector3(.57f, .23f, 1.05f)
        };
        private static readonly Vector3[] EchoStations =
        {
            new Vector3(.68f, .27f, .23f), new Vector3(-.70f, .20f, .43f),
            new Vector3(-.38f, .42f, 1.10f), new Vector3(.46f, .38f, 1.37f)
        };
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Art = Shader.PropertyToID("_Art");
        private readonly List<Mesh> meshes = new List<Mesh>(12);
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Material material;
        private readonly Mesh bronzeGear, ironGear, robot, lamp, arm, sill, hatch, hatchRim, echo, water, lightRing;

        public MachineDoorPortrait()
        {
            Shader shader = Resources.Load<Shader>("MachineDoorPortrait");
            if (shader == null || !shader.isSupported) return;
            material = new Material(shader) { name = "Mechanical door portrait materials" };
            bronzeGear = Gear("Bronze portrait press", Bronze);
            ironGear = Gear("Iron portrait press", Iron);
            var crawler = new WorldMesh(meshes);
            WorldSet.ModelMachineRecycler(crawler);
            robot = crawler.Finish("Portrait sorting crawler");

            var bulb = new WorldMesh(meshes);
            bulb.Rock(Vector3.zero, Vector3.one, Color.white, .27f, 6, 12, .025f);
            lamp = bulb.Finish("Portrait machine lamp");
            var linkage = new WorldMesh(meshes);
            linkage.Box(Vector3.zero, new Vector3(2,.96f,2), Quaternion.identity, Color.white);
            arm = linkage.Finish("Portrait plated arm");

            var entry = new WorldMesh(meshes);
            entry.Box(Vector3.zero,new Vector3(1.98f,.16f,.57f),Quaternion.identity,Iron);
            for(int strip = 0; strip < 9; strip++)
                entry.Box(new Vector3(strip*.21f-.84f,.09f,-.21f),new Vector3(.14f,.022f,.092f),
                    Quaternion.identity,strip%2==0 ? Bronze : Dark);
            sill = entry.Finish("Tilting portrait entry sill");

            var gate = new WorldMesh(meshes);
            gate.Box(new Vector3(.44f,0,0), new Vector3(.88f,.125f,1.37f), Quaternion.identity, Iron);
            for (int i = 0; i < 5; i++)
                gate.Box(new Vector3(.44f,.073f,i*.22f-.44f), new Vector3(.81f,.034f,.070f), Quaternion.identity,
                    WorldMesh.Shade(Bronze,.68f+i*.073f));
            hatch = gate.Finish("Portrait twin hopper plates");
            var throat = new WorldMesh(meshes);
            WorldSet.ModelMachineRing(throat, Vector3.zero, .91f, .071f, Bronze, Quaternion.Euler(90,0,0));
            hatchRim = throat.Finish("Portrait recycling hopper");

            var figure = new WorldMesh(meshes);
            WorldSet.ModelMachinePrediction(figure);
            echo = figure.Finish("Portrait prediction silhouette");
            var liquid = new WorldMesh(meshes);
            liquid.Quad(new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0),
                new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0), Color.white);
            water = liquid.Finish("Portrait predictive liquid");
            var ripple = new WorldMesh(meshes);
            WorldSet.ModelMachineRing(ripple, Vector3.zero, 1, .015f, Color.white, Quaternion.Euler(90,0,0));
            lightRing = ripple.Finish("Portrait water ring");
        }

        private Mesh Gear(string name, Color metal)
        {
            var mesh = new WorldMesh(meshes);
            WorldSet.ModelMachineCrusher(mesh, metal);
            WorldSet.ModelMachineRing(mesh, Vector3.back*.38f, .54f, .035f, WorldMesh.Shade(Warm,.74f), Quaternion.identity);
            return mesh.Finish(name);
        }

        public static bool Active(SpiritFrame frame) => frame.forest && frame.forestDoorAge >= 0 &&
            frame.forestDoor >= 0 && frame.forestDoor < 3;

        public static float Framing(MachineDoorShot shot) => 7 * OpeningStoryboard.Ease(.14f,.34f,shot.Progress);

        public static Matrix4x4 Performer(int door, MachineDoorShot shot, Vector3 center)
        {
            float t = shot.Progress;
            float step = OpeningStoryboard.Ease(.09f,.18f,t);
            float land = OpeningStoryboard.Ease(.32f,.43f,t);
            float entryHeight = .20f*step-.54f*shot.Drop+.34f*land;
            var position = center + new Vector3(0,entryHeight,.12f*step);
            Vector3 scale = Vector3.one;
            if (door == 0)
            {
                position += new Vector3(.25f * shot.Recoil, .14f * shot.Recoil - .11f * shot.Press,-.20f * shot.Recoil);
                scale = new Vector3(Mathf.Lerp(1,.033f,shot.GearCompression),Mathf.Lerp(1,.62f,shot.GearCompression),1);
                position.z += .16f * shot.GearCompression;
            }
            else if (door == 1)
            {
                position += new Vector3(-.29f * shot.RobotRetreat + .22f * Mathf.Sin(Mathf.PI * shot.RobotPass),
                    .27f * shot.RobotPass - 1.55f * shot.RobotDrop,.23f * shot.RobotPass);
                scale *= 1 - .48f * OpeningStoryboard.Ease(.86f,.96f,t);
            }
            else
            {
                position += new Vector3(.16f * Mathf.Sin(t * 12) * shot.Water,-1.79f * shot.Sink,.17f * shot.Water);
                scale *= 1 - .51f * OpeningStoryboard.Ease(.82f,.94f,t);
            }
            return Matrix4x4.TRS(position,Quaternion.identity,scale)*Matrix4x4.Translate(-center);
        }

        public void Draw(SpiritFrame frame, MachineDoorShot shot, Vector3 actor, CommandBuffer buffer)
        {
            if (material == null) return;
            if (shot.Threshold < .999f)
                DrawPart(buffer,sill,new Vector3(0,-.04f-1.18f*shot.Threshold,.12f-.44f*shot.Threshold),Vector3.one,
                    Quaternion.Euler(-72*shot.Threshold,0,0),Color.white,0,1-shot.Threshold*.26f);
            switch (frame.forestDoor)
            {
                case 0: DrawGear(shot, actor, buffer); break;
                case 1: DrawRecycler(shot, frame.forestDoorAge, actor, buffer); break;
                case 2: DrawPool(shot, frame, buffer); break;
            }
        }

        private void DrawGear(MachineDoorShot shot, Vector3 actor, CommandBuffer buffer)
        {
            float show = OpeningStoryboard.Ease(.18f,.42f,shot.Progress);
            if (show <= 0) return;
            for (int side = -1; side <= 1; side += 2)
            {
                float spacing = (Mathf.Lerp(2.32f,1.48f,show) - .55f * shot.Press) * .95f;
                var at = new Vector3(side*spacing,1.17f+side*.047f,side < 0 ? .15f : -.14f);
                var rotation = Quaternion.Euler(-side*4,0,-side*shot.GearAngle-(side>0 ? 15 : 0));
                DrawPart(buffer,side < 0 ? bronzeGear : ironGear,at,new Vector3(.82f,.82f,.68f),rotation,Color.white,softEdge:true);
            }
            if (shot.GearSlit > .002f)
                DrawPart(buffer,lamp,new Vector3(actor.x,actor.y,-.74f),new Vector3(.019f,.59f,.026f),
                    Quaternion.identity,Warm,shot.GearSlit * 1.3f);
            for (int i = 0; i < 9; i++)
            {
                float spark = shot.Spark(i);
                if (spark < .005f) continue;
                float direction = i * 2.39996f;
                var vector = new Vector3(Mathf.Cos(direction)*.66f,Mathf.Sin(direction)*.90f,-.59f);
                var at = new Vector3(actor.x,actor.y,-.65f)+vector*(.08f+1.19f*spark)
                    +Vector3.down*(spark*spark*.24f);
                DrawPart(buffer,lamp,at,new Vector3(.022f,.065f,.022f)*(1-.49f*spark),
                    Quaternion.FromToRotation(Vector3.up,vector),Warm,spark);
            }
        }

        private void DrawRecycler(MachineDoorShot shot, float time, Vector3 actor, CommandBuffer buffer)
        {
            float floor = OpeningStoryboard.Ease(.25f,.40f,shot.Progress);
            if (floor <= 0) return;
            DrawPart(buffer,hatchRim,new Vector3(0,.24f-floor*.20f,.12f),new Vector3(1,.75f,.87f),
                Quaternion.identity,WorldMesh.Shade(Color.white,.81f));
            for (int side = -1; side <= 1; side += 2)
            {
                DrawPart(buffer,hatch,new Vector3(0,.34f-floor*.26f,.12f),new Vector3(side,.8f,1),
                    Quaternion.Euler(0,0,-side*75*shot.Hatch),Color.white);
            }

            for (int i = 0; i < RobotStations.Length; i++)
            {
                float prompt = OpeningStoryboard.Ease(.24f+i*.026f,.49f+i*.03f,shot.Progress);
                if (prompt < .005f) continue;
                Vector3 here = RobotStations[i];
                float approach = shot.RobotApproach(i);
                Vector3 from = here + new Vector3(here.x*(1-prompt)*1.28f,-.32f*(1-prompt),.41f*(1-approach));
                from.x *= 1-.17f*approach;
                from.y += .025f * Mathf.Sin(time*12.7f+i*1.2f) * approach;
                Vector3 toward = actor - from;
                toward.y = 0;
                Quaternion facing = Quaternion.LookRotation(toward.sqrMagnitude > .003f ? -toward : Vector3.back, Vector3.up);
                var view = Quaternion.Euler(0,from.x < 0 ? -30 : 30,0);
                facing = Quaternion.Slerp(facing,view,.48f);
                float scale = .76f;
                DrawPart(buffer,robot,from,Vector3.one*scale,facing,Color.white);
                Vector3 eye = from + facing*new Vector3(0,.69f,-.325f)*scale;
                float danger = shot.RobotAlarm(i);
                DrawPart(buffer,lamp,eye,new Vector3(.15f,.032f,.025f),facing,Color.Lerp(Circuit,Alarm,danger),.48f+.64f*danger);
                Vector3 shoulder = from + Vector3.up*.59f;
                float reach = shot.RobotReach(i);
                Vector3 rest = shoulder + toward.normalized*.19f - Vector3.up*.08f;
                Vector3 contact = actor + new Vector3(i == 0 ? -.17f : .18f,i == 1 ? -.23f : .08f,-.23f+i*.18f);
                Vector3 claw = Vector3.Lerp(rest,contact,reach);
                Vector3 joint = Vector3.Lerp(shoulder,claw,.57f) + Vector3.up*(.10f+.17f*reach);
                Link(buffer,shoulder,joint,.046f,Iron);
                Link(buffer,joint,claw,.034f,Bronze);
                Vector3 axis = (claw - joint).normalized;
                Vector3 split = Vector3.Cross(axis,Vector3.forward).normalized*(.068f-.029f*reach);
                for (int side = -1; side <= 1; side += 2)
                    Link(buffer,claw+split*side-axis*.080f,claw+axis*.075f+split*(.55f*side),.022f,Iron);
            }
        }

        private void DrawPool(MachineDoorShot shot, SpiritFrame frame, CommandBuffer buffer)
        {
            float t = shot.Progress;
            float level = -.13f+1.85f*shot.Water;
            for (int i = EchoStations.Length-1; i >= 0; i--)
            {
                float enter = shot.EchoAppear(i), remaining = shot.EchoRemain(i);
                if (enter*remaining > .014f) DrawEcho(shot,frame,i,enter,remaining,buffer);
            }

            float wave = OpeningStoryboard.Ease(.30f,.59f,t);
            if (wave > .004f)
            {
                DrawPart(buffer,water,new Vector3(0,(level-1.34f)*.5f,-.98f),new Vector3(2.81f,level+1.34f,1),
                    Quaternion.identity,Pool,.63f + .50f*shot.Water,wave*(.55f+.22f*shot.Water),true,frame.forestDoorAge);
                for (int i=0; i<3; i++)
                {
                    float radius = .32f+.31f*i+.12f*Mathf.Sin(frame.forestDoorAge*2.9f+i);
                    DrawPart(buffer,lightRing,new Vector3(0,level-.036f+i*.011f,.46f),new Vector3(radius,.56f,radius*.78f),
                        Quaternion.identity,Circuit,.83f,wave*(.48f-i*.095f)*(1-.58f*shot.Water));
                }
            }
            if (t > .83f)
            {
                // The remaining prediction continues moving after the original disappears under the liquid.
                DrawEcho(shot,frame,0,1,OpeningStoryboard.Ease(.83f,.93f,t),buffer,true);
            }
        }

        private void DrawEcho(MachineDoorShot shot, SpiritFrame frame, int index, float enter, float remaining,
            CommandBuffer buffer, bool survivor = false)
        {
            float time = frame.motionTime, t = shot.Progress;
            Vector3 at = EchoStations[index];
            bool predicts = t >= ForestDoorEcho.PredictionStart;
            ForestDoorEcho motion = frame.ForestDoorEchoAt(index);
            float imitation = motion.Lane;
            if (predicts) imitation = Mathf.Clamp(imitation+.15f*Mathf.Sin(time*2+index),-1,1);
            at.x += .25f*imitation;
            at.y += .050f*Mathf.Sin(time*4+index) + .41f*shot.Water - .70f*(1-enter) - .36f*(1-remaining);
            Color color = Color.Lerp(Pool,Circuit,Mathf.Repeat(index*.37f,1));
            float opacity = (.36f+.35f*enter)*remaining;
            if (survivor)
            {
                at.x *= .57f;
                at.y += .37f;
                at.z = -1.12f;
                color = Color.Lerp(Pool,Circuit,.58f);
                opacity *= .88f;
            }
            else if (index == 0) opacity *= 1-OpeningStoryboard.Ease(.83f,.93f,t);
            float size = (.62f+.53f*enter)*(survivor ? .87f : .95f);
            var aim = Quaternion.Euler(0,imitation*36,-imitation*11);
            DrawPart(buffer,echo,at,Vector3.one*size,aim,color,.73f+.47f*shot.Water,opacity);
            float raise = motion.Fist ? .14f : .42f+.09f*Mathf.Sin(time*7+index);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 from = at + aim*new Vector3(side*.17f,.77f,0)*size;
                Vector3 to = at + aim*new Vector3(side*raise,.56f+raise,-side*.075f)*size;
                Link(buffer,from,to,.041f,color,opacity*.87f,.78f);
            }
        }

        private void Link(CommandBuffer buffer, Vector3 from, Vector3 to, float thickness, Color color,
            float opacity = 1, float glow = 0)
        {
            Vector3 along = to-from;
            if (along.sqrMagnitude < .0001f) return;
            DrawPart(buffer,arm,(from+to)*.5f,new Vector3(thickness,along.magnitude,thickness),
                Quaternion.FromToRotation(Vector3.up,along),color,glow,opacity);
        }

        private void DrawPart(CommandBuffer buffer, Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation,
            Color tint, float glow = 0, float opacity = 1, bool wave = false, float time = 0, bool softEdge = false)
        {
            if (opacity <= .004f) return;
            properties.SetColor(Tint,tint);
            properties.SetVector(Art,new Vector4(glow,opacity,wave ? 1 : softEdge ? 2 : 0,time));
            buffer.DrawMesh(mesh,Matrix4x4.TRS(position,rotation,scale),material,0,opacity < .999f ? 1 : 0,properties);
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
