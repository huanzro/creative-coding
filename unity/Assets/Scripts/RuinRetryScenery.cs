using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private Transform ruinWall, ruinWallLeft, ruinWallRight;
        private MeshRenderer ruinWallLights, ruinWallSeam, ruinWallFloor;

        private void BuildRuinWall()
        {
            ruinWall = Group("Archive impact and outside threshold", roomShell);
            ruinWallLeft = Group("Left retracting shutter", ruinWall);
            ruinWallRight = Group("Right retracting shutter", ruinWall);
            Color metal = new Color(.16f,.25f,.35f), brightMetal = new Color(.34f,.43f,.50f);
            Color brass = new Color(.54f,.39f,.22f);
            var panels = Geometry();
            var rim = Geometry();
            var channels = Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                var leaf = Geometry();
                Transform hinge = side < 0 ? ruinWallLeft : ruinWallRight;
                for (int level = 0; level < 7; level++)
                {
                    float x = side*(2.01f + .10f*Mathf.Sin(level*.99f));
                    float y = .44f + .78f*level;
                    leaf.Box(new Vector3(x,y,0), new Vector3(4.12f,.764f,.61f), Quaternion.identity, metal);
                    leaf.Box(new Vector3(x+side*.16f,y+.246f,-.33f),new Vector3(3.74f,.094f,.068f),Quaternion.identity,brightMetal);
                    for (int band = 0; band < 3; band++)
                    {
                        Vector3 start = new Vector3(side*(.23f+.38f*band),y-.18f+band*.13f,-.373f);
                        Vector3 kink = start+new Vector3(side*.43f,.19f,0);
                        RuinBar(leaf,start,kink,.021f,.017f,brass);
                        RuinBar(leaf,kink,kink+Vector3.right*side*(.4f+.34f*band),.021f,.017f,brass);
                    }
                }
                RuinPiece("Machined stone segments",leaf.Finish("Layered kinetic door leaf"),tunnelMetal,hinge);
                panels.Box(new Vector3(side*4.06f,2.7f,.19f),new Vector3(1.9f,5.75f,1.68f),Quaternion.identity,metal);
                panels.Box(new Vector3(side*3.36f,2.67f,-.68f),new Vector3(.63f,5.53f,.42f),Quaternion.identity,brightMetal);
                panels.Box(new Vector3(side*2.80f,1.71f,-.22f),new Vector3(.78f,3.42f,1.36f),Quaternion.identity,metal);
                for (int tooth = 0; tooth < 12; tooth++)
                {
                    float y = .25f + tooth*.285f;
                    rim.Box(new Vector3(side*(2.50f+.026f*tooth),y,-.96f),new Vector3(.060f,.152f,.037f),
                        Quaternion.Euler(0,0,side*12),Color.white);
                }
                RuinBar(channels,new Vector3(side*.87f,.045f,-5.0f),new Vector3(side*.87f,.045f,6.2f),.047f,.031f,Color.white);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = new Vector3(side*2.86f,3.39f,0);
                Vector3 arch = new Vector3(side*1.98f,4.41f,0);
                Vector3 crown = new Vector3(side*.76f,4.80f,0);
                RuinBar(panels,shoulder,arch,.83f,1.54f,brightMetal);
                RuinBar(panels,arch,crown,.69f,1.54f,metal);
                RuinBar(panels,crown,new Vector3(0,4.80f,0),.68f,1.54f,metal);
                RuinBar(rim,shoulder-Vector3.forward*.91f,arch-Vector3.forward*.91f,.046f,.040f,Color.white);
                RuinBar(rim,arch-Vector3.forward*.91f,crown-Vector3.forward*.91f,.046f,.040f,Color.white);
            }
            panels.Box(new Vector3(0,-.11f,1.4f),new Vector3(5.9f,.25f,9.8f),Quaternion.identity,metal);
            RuinPiece("Portal jamb and tread",panels.Finish("Load-bearing threshold"),tunnelMetal,ruinWall);
            ruinWallLights = RuinPiece("Readout lights",rim.Finish("Threshold inlay"),glow,ruinWall).GetComponent<MeshRenderer>();
            ruinWallFloor = RuinPiece("Chamber from this point",channels.Finish("Threshold guidance rails"),glow,ruinWall)
                .GetComponent<MeshRenderer>();

            var split = Geometry();
            Vector3 previous = new Vector3(0,.08f,-.376f);
            for (int section = 1; section <= 21; section++)
            {
                Vector3 next = new Vector3(Mathf.Sin(section*5.36f)*.067f,.08f+section*.244f,-.377f);
                RuinBar(split,previous,next,.026f,.042f,Color.white);
                previous = next;
            }
            ruinWallSeam = RuinPiece("Hairline light revealed by impact",split.Finish("Staggered wall seam"),glow,ruinWall)
                .GetComponent<MeshRenderer>();
        }

        private void DrawRuinRetry(Journey journey, float time)
        {
            RuinRetryPhase phase = journey.RuinRetryState;
            bool active = phase == RuinRetryPhase.Impact || phase == RuinRetryPhase.Gate;
            if (!active)
            {
                if (ruinWall != null) ruinWall.gameObject.SetActive(false);
                return;
            }
            if (ruinWall == null) BuildRuinWall();
            ruinWall.gameObject.SetActive(true);
            var route=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRetryWallDepth-.18f);
            ruinWall.localPosition = route.Position;
            ruinWall.localRotation = Quaternion.LookRotation(new Vector3(route.Forward.x,0,route.Forward.z));
            float age = journey.Age;
            float open = phase == RuinRetryPhase.Gate ? OpeningStoryboard.Ease(.21f,1.25f,age) : 0;
            ruinWallLeft.localPosition = new Vector3(-4.10f*open,0,.29f*open);
            ruinWallRight.localPosition = new Vector3(4.10f*open,0,.29f*open);
            float hit = phase == RuinRetryPhase.Impact ? OpeningStoryboard.Window(age,.32f,.44f,.51f,1.17f) : 0;
            Color ice = new Color(.47f,.84f,1);
            Light(ruinWallLights,ice,.21f+.72f*open+.91f*hit);
            Light(ruinWallFloor,ice,.28f+.77f*open);
            ruinWallSeam.gameObject.SetActive(open<.27f);
            Light(ruinWallSeam,Color.Lerp(ice,new Color(1,.78f,.43f),hit*.46f), .37f + hit*1.58f + .12f*Mathf.Sin(time*5));
        }
    }
}
