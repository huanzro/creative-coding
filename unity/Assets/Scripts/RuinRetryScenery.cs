using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const int RetryShutterCount = 12;
        private const int RetryRingCount = 5;
        private Transform retrySet;
        private Transform retryInscription;
        private Transform[] retrySlabs;
        private MeshRenderer[] retryVeins, retryRings, retryTravelMarks, retryExitTrails;
        private MeshRenderer retryAperture, retryImprint, retryPrediction, retryAttempt, retryHeart, retryBrain;
        private Transform retryGhost, retryGhostLeft, retryGhostRight;
        private MeshRenderer[] retryGhostParts;

        private static readonly Color RetryIce = new Color(.58f, .81f, 1);
        private static readonly Color RetryBrass = new Color(1, .73f, .43f);
        private static readonly Color RetryEmber = new Color(1, .31f, .38f);

        private void BuildRuinRetry()
        {
            retrySet = Group("The wall that remembers", roomShell);
            retrySlabs = new Transform[RetryShutterCount];
            retryVeins = new MeshRenderer[RetryShutterCount];
            Color front = new Color(.18f, .28f, .34f), edge = new Color(.41f, .46f, .48f);
            for (int i = 0; i < RetryShutterCount/2; i++)
            {
                var slabs = Geometry();
                var circuit = Geometry();
                float height = .78f + i * .085f;
                slabs.Box(Vector3.zero, new Vector3(5.95f, height, .62f), Quaternion.identity, front);
                slabs.Box(new Vector3(.10f, height*.31f, -.31f), new Vector3(5.60f, .08f, .06f), Quaternion.identity, edge);
                slabs.Box(new Vector3(.10f, -height*.40f, -.31f), new Vector3(5.71f, .035f, .08f), Quaternion.identity, edge);

                for (int slot = 0; slot < 5; slot++)
                {
                    float x = -.28f - slot * .48f - i * .23f;
                    var a = new Vector3(x, .01f, -.362f);
                    var b = a + new Vector3(-.17f - (i % 2) * .13f, .13f, 0);
                    RuinBar(circuit, a, b, .018f, .022f, Color.white);
                    RuinBar(circuit, b, b + Vector3.left * (.12f + .06f * (slot % 3)), .018f, .022f, Color.white);
                    circuit.Box(a, Vector3.one*.034f, Quaternion.Euler(0,0,45), Color.white);
                }
                Mesh metal = slabs.Finish("Segmented memory shutter");
                Mesh inlay = circuit.Finish("Engraved shutter circuits");
                for (int side = 0; side < 2; side++)
                {
                    int slot = i * 2 + side;
                    retrySlabs[slot] = Group("Shutter hinge", retrySet);
                    RuinPiece("Shutter case", metal, tunnelMetal, retrySlabs[slot]);
                    var vein = RuinPiece("Embedded circuit", inlay, glow, retrySlabs[slot]);
                    if (side != 0) vein.localPosition = Vector3.right * 3.10f;
                    retryVeins[slot] = vein.GetComponent<MeshRenderer>();
                }
            }
            retryInscription = BuildFindYourself(retrySet, false);
            retryInscription.localPosition = new Vector3(0, 2.65f, -.57f);
            retryInscription.localRotation = Quaternion.identity;
            retryInscription.localScale = Vector3.one * 1.04f;

            var beam = Geometry();
            beam.Box(Vector3.zero, new Vector3(4.3f, 2.9f, .052f), Quaternion.identity, Color.white);
            retryAperture = RuinPiece("Exit light collapsing into a scanline", beam.Finish("Rectangular white aperture"),
                glow, retrySet).GetComponent<MeshRenderer>();
            retryRings = new MeshRenderer[RetryRingCount];
            for (int i = 0; i < retryRings.Length; i++)
            {
                var rim = Geometry();
                RetryRing(rim, 1.45f + i * .12f, 1.71f + i * .11f, .018f + i * .004f);
                retryRings[i] = RuinPiece("Threshold concentric iris", rim.Finish("Open aperture rim"), glow, retrySet)
                    .GetComponent<MeshRenderer>();
            }

            retryHeart = MakeRetryRemnant(retrySet, true);
            retryBrain = MakeRetryRemnant(retrySet, false);
            retryImprint = BuildRetryTrail(retrySet, "Previously traversed trace", true);
            retryPrediction = BuildRetryTrail(retrySet, "System's projected route", false);
            retryAttempt = BuildRetryTrail(retrySet, "Trace from the most recent attempt", false);
            BuildRetryGhost();
            retryExitTrails = new[] {
                BuildRetryExitTrail(RuinRetryResolution.Guided),
                BuildRetryExitTrail(RuinRetryResolution.Takeover),
                BuildRetryExitTrail(RuinRetryResolution.Remembered)
            };
            retryTravelMarks = new MeshRenderer[7];
            for (int i = 0; i < retryTravelMarks.Length; i++)
            {
                var symbol = Geometry();
                RuinBar(symbol, new Vector3(-.23f, 0, -.13f), new Vector3(0, 0, .16f), .033f, .027f, Color.white);
                RuinBar(symbol, new Vector3(0, 0, .16f), new Vector3(.23f, 0, -.13f), .033f, .027f, Color.white);
                retryTravelMarks[i] = RuinPiece("Wayfinding chevron", symbol.Finish("Projected navigation arrow"), glow, retrySet)
                    .GetComponent<MeshRenderer>();
            }
        }

        private void BuildRetryGhost()
        {
            retryGhost = Group("Recorded silhouette", retrySet);
            retryGhostLeft = Group("Recorded left arm", retryGhost);
            retryGhostRight = Group("Recorded right arm", retryGhost);
            var outline = Geometry();
            outline.Rock(new Vector3(0, .76f, .004f), new Vector3(.115f,.139f,.108f), Color.white, .4f, 5, 14, .025f);
            for (int i = 0; i < 12; i++)
            {
                float a = i*Mathf.PI/6, b = a + Mathf.PI/6;
                Vector3 high = new Vector3(Mathf.Sin(a)*.137f,.61f,Mathf.Cos(a)*.081f);
                Vector3 low = new Vector3(Mathf.Sin(a)*.271f,.25f,Mathf.Cos(a)*.197f-.056f);
                Vector3 next = new Vector3(Mathf.Sin(b)*.271f,.25f,Mathf.Cos(b)*.197f-.056f);
                RuinBar(outline,high,low,.014f,.016f,Color.white);
                RuinBar(outline,low,next,.014f,.016f,Color.white);
            }
            for (int side = -1; side <= 1; side += 2)
                RuinBar(outline,new Vector3(side*.12f,.30f,0),new Vector3(side*.16f,.07f,-.035f),.055f,.051f,Color.white);
            var arm = Geometry();
            RuinBar(arm,Vector3.zero,new Vector3(.18f,-.103f,.013f),.039f,.041f,Color.white);
            RuinBar(arm,new Vector3(.18f,-.103f,.013f),new Vector3(.26f,-.176f,.073f),.032f,.032f,Color.white);
            Mesh armMesh = arm.Finish("Recorded articulated arms");
            retryGhostLeft.localPosition = new Vector3(-.12f,.57f,0);
            retryGhostRight.localPosition = new Vector3(.12f,.57f,0);
            retryGhostLeft.localScale = new Vector3(-1,1,1);
            retryGhostParts = new[] {
                RuinPiece("Cloaked trace",outline.Finish("Glowing wireframe of earlier self"),glow,retryGhost).GetComponent<MeshRenderer>(),
                RuinPiece("Left recorded joint",armMesh,glow,retryGhostLeft).GetComponent<MeshRenderer>(),
                RuinPiece("Right recorded joint",armMesh,glow,retryGhostRight).GetComponent<MeshRenderer>()
            };
        }

        private static void RetryRing(WorldMesh mesh, float width, float height, float outline)
        {
            for (int i = 0; i < 28; i++)
            {
                float a = i * (Mathf.PI / 14);
                float b = (i + 1) * (Mathf.PI / 14);
                var start = new Vector3(Mathf.Sin(a) * width, Mathf.Cos(a) * height, 0);
                var end = new Vector3(Mathf.Sin(b) * width, Mathf.Cos(b) * height, 0);
                RuinBar(mesh, start, end, outline, outline, Color.white);
            }
        }

        private MeshRenderer MakeRetryRemnant(Transform parent, bool heart)
        {
            var trace = Geometry();
            Vector3 previous = RetryRemnantPoint(0, heart);
            for (int i = 1; i <= 64; i++)
            {
                Vector3 next = RetryRemnantPoint(i * Mathf.PI / 32, heart);
                RuinBar(trace, previous, next, .025f, .025f, Color.white);
                if (i % 4 == 0)
                {
                    var spoke = Vector3.Lerp(next, Vector3.zero, heart ? .89f : .76f);
                    spoke.z = Mathf.Sin(i * .90f) * .13f;
                    RuinBar(trace, next, Vector3.Lerp(next, spoke, .56f), .008f, .009f, Color.white);
                }
                previous = next;
            }
            return RuinPiece(heart ? "Heart rhythm projected in the wall" : "Brain trace projected in the wall",
                trace.Finish("Organic signal remnant"), glow, parent).GetComponent<MeshRenderer>();
        }

        private static Vector3 RetryRemnantPoint(float angle, bool heart)
        {
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            if (heart)
                return new Vector3(.65f*s*s*s, .044f*(13*c - 5*Mathf.Cos(2*angle) - 2*Mathf.Cos(3*angle) - Mathf.Cos(4*angle)), 0);
            float fold = .76f + .062f*Mathf.Cos(9*angle) + .025f*Mathf.Cos(16*angle);
            return new Vector3(s*fold, c*fold*.82f - Mathf.Pow(Mathf.Abs(c),12)*.18f, .075f*Mathf.Sin(angle*4));
        }

        private MeshRenderer BuildRetryTrail(Transform parent, string name, bool footpath)
        {
            var trail = Geometry();
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f, x = Mathf.SmoothStep(.04f, 2.20f, t), z = Mathf.Lerp(-9.25f, -.85f, t);
                if (footpath)
                {
                    var place = new Vector3(x + ((i & 1) == 0 ? -.15f : .15f), .043f, z);
                    trail.Rock(place, new Vector3(.080f, .015f, .167f), Color.white, i*.174f, 3, 7, .035f);
                    trail.Rock(place - Vector3.forward*.15f, new Vector3(.056f, .012f, .058f), Color.white, i*.34f, 3, 6, .035f);
                }
                else
                {
                    float next = (i+1)/12f;
                    RuinBar(trail, new Vector3(x,.06f,z),
                        new Vector3(Mathf.SmoothStep(.04f,2.20f,next), .06f, z + .38f), .033f, .021f, Color.white);
                }
            }
            return RuinPiece(name, trail.Finish(name), glow, parent).GetComponent<MeshRenderer>();
        }

        private MeshRenderer BuildRetryExitTrail(RuinRetryResolution mode)
        {
            var trail = Geometry();
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f, b = (i + 1) / 24f;
                bool guided = mode == RuinRetryResolution.Guided ||
                    (mode == RuinRetryResolution.Takeover && a < .39f);
                Vector3 start = RetryExitPoint(a, mode), end = RetryExitPoint(b, mode);
                RuinBar(trail, start, end, guided ? .043f : .056f, .025f, guided ? RetryIce : RetryBrass);
                if (mode == RuinRetryResolution.Remembered && i % 2 == 0)
                {
                    float difference = .82f * (1-a);
                    start.x -= difference;
                    end.x -= difference;
                    RuinBar(trail, start, end, .032f, .020f, WorldMesh.Shade(RetryIce,.55f));
                }
            }
            return RuinPiece("Recorded decision at the exit", trail.Finish("Run inscription"), glow, retrySet)
                .GetComponent<MeshRenderer>();
        }

        private static Vector3 RetryExitPoint(float t, RuinRetryResolution mode)
        {
            float x = mode == RuinRetryResolution.Guided ? -.97f :
                mode == RuinRetryResolution.Takeover ? Mathf.SmoothStep(-1.40f,1.19f,Mathf.InverseLerp(.25f,.65f,t)) :
                1.21f + Mathf.Sin(t*12)*.125f*(1-t);
            return new Vector3(x, .071f, Mathf.Lerp(-1.76f,10.76f,t));
        }

        private void DrawRuinRetry(Journey journey, float time)
        {
            RuinRetryPhase phase = journey.RuinRetryState;
            if (phase == RuinRetryPhase.Inactive)
            {
                if (retrySet != null) retrySet.gameObject.SetActive(false);
                return;
            }
            if (retrySet == null) BuildRuinRetry();
            retrySet.gameObject.SetActive(true);
            var route=RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRetryWallDepth-.18f);
            retrySet.localPosition=route.Position;
            retrySet.localRotation=route.Rotation;
            float age = journey.Age;
            float progress = journey.Progress;
            bool impact = phase == RuinRetryPhase.Impact;
            bool guided = phase == RuinRetryPhase.Guided;
            bool trace = phase == RuinRetryPhase.Trace;
            bool fault = phase == RuinRetryPhase.Fault;
            bool listen = phase == RuinRetryPhase.Listen;
            bool release = phase == RuinRetryPhase.Release;
            bool departure = phase == RuinRetryPhase.Departure;
            float panelGap = guided ? 1.12f*OpeningStoryboard.Ease(.02f,.34f,progress) : 0;
            if (departure) panelGap = Mathf.Lerp(1.12f,4.60f, OpeningStoryboard.Ease(0,.76f,progress));
            for (int i = 0; i < retrySlabs.Length; i++)
            {
                float side = (i & 1) == 0 ? -1 : 1;
                float band = i / 2;
                float stagger = .018f * Mathf.Abs(2-band);
                float close = impact ? OpeningStoryboard.Ease(.17f+stagger,.42f+stagger,age) : 1;
                var slab = retrySlabs[i];
                slab.localPosition = new Vector3(side*(3.005f + 3.12f*(1-close) + panelGap),
                    .51f + .96f * band, .10f + .41f*(1-close));
                slab.localRotation = Quaternion.Euler(0, side*(18*(1-close) + 12*panelGap),0);
                Color hue = listen || release ? Color.Lerp(RetryIce,RetryEmber,journey.RuinRetryPulse) : RetryIce;
                Light(retryVeins[i], hue, .21f + .31f * close + .17f * Mathf.Sin(age*4.4f-band*.97f));
            }

            bool review = phase == RuinRetryPhase.Review;
            bool choosing = phase == RuinRetryPhase.Choice;
            retryInscription.gameObject.SetActive(review || choosing);
            if (review || choosing)
                retryInscription.localScale = Vector3.one * Mathf.Lerp(.76f,1.51f,review ? OpeningStoryboard.Ease(.08f,.64f,age) : 1);
            retryAperture.gameObject.SetActive(impact || guided || departure);
            float white = impact ? 1-OpeningStoryboard.Ease(.42f,.67f,age) : departure ? OpeningStoryboard.Ease(.11f,.72f,progress) : .22f;
            float beamHeight = impact ? Mathf.Lerp(.016f,1,1-OpeningStoryboard.Ease(.12f,.38f,age)) : 1;
            retryAperture.transform.localPosition = new Vector3(0, 2.05f, impact ? -.43f : .62f);
            retryAperture.transform.localScale = new Vector3(impact ? 1 : .53f, beamHeight, 1);
            Color apertureTint = departure ? new Color(.46f,.71f,.84f) : impact ? Color.white : RetryIce;
            Light(retryAperture, apertureTint, .04f + white*(departure ? .36f : .79f));
            for (int i = 0; i < retryRings.Length; i++)
            {
                var renderer = retryRings[i];
                float knock = impact ? OpeningStoryboard.Window(age,.42f,.50f,.56f,.92f) : 0;
                renderer.gameObject.SetActive(guided || departure || listen || release || knock > 0);
                renderer.transform.localPosition = new Vector3(0,1.99f,(departure ? .34f : -.53f) + i*.22f);
                renderer.transform.localScale = Vector3.one * (1 - i*.059f + journey.RuinRetryPulse*.055f);
                Light(renderer, listen || release ? RetryEmber : RetryIce,
                    listen || release ? (.26f + .48f*journey.RuinRetryPulse) / (i+1) : (.86f+white*.29f)/(i+1));
                if (knock <= 0) continue;
                renderer.transform.localPosition = new Vector3(journey.RuinLane*2.45f,
                    .61f+journey.Flight.Height*3.30f,-.64f-i*.012f);
                renderer.transform.localScale = Vector3.one*(.11f+OpeningStoryboard.Ease(.42f,.88f,age)*(.93f+i*.11f));
                Light(renderer, Color.Lerp(Color.white,RetryIce,i*.17f),knock*.80f/(i+1));
            }

            int lane = journey.RuinRetryTraceLane < 0 ? -1 : 1;
            bool replay = review || choosing || trace || fault || listen || release;
            retryImprint.gameObject.SetActive(replay);
            retryPrediction.gameObject.SetActive(review || choosing || trace || fault || guided);
            retryImprint.transform.localScale = new Vector3(lane, 1, 1);
            retryPrediction.transform.localScale = new Vector3(-lane, 1, 1);
            float imprint = review ? OpeningStoryboard.Ease(.19f,1.03f,age) : 1;
            if (trace) imprint *= .12f + .88f*journey.RuinRetryTraceLight;
            Light(retryImprint, RetryBrass, .10f+imprint*(.52f+.16f*Mathf.Sin(time*3.6f)));
            Light(retryPrediction, RetryIce, .61f+.17f*Mathf.Sin(time*4.45f));
            retryAttempt.gameObject.SetActive((fault || trace && age < 2.0f) && journey.RuinRetryLastAttemptLane != 0);
            retryAttempt.transform.localPosition = new Vector3(0,.018f,.31f);
            retryAttempt.transform.localScale = new Vector3(journey.RuinRetryLastAttemptLane < 0 ? -.79f : .79f,1,1);
            Light(retryAttempt,RetryEmber,fault ? .74f : .64f*(1-OpeningStoryboard.Ease(.78f,1.94f,age)));
            retryHeart.gameObject.SetActive(replay || guided);
            retryBrain.gameObject.SetActive(review || choosing || trace || fault);
            float pulse = listen || release ? journey.RuinRetryPulse : Mathf.Pow(.5f+.5f*Mathf.Sin(time*3.1f),5);
            retryHeart.transform.localPosition = new Vector3(lane*(guided ? .92f : 1.42f),1.98f,-.69f);
            retryHeart.transform.localScale = Vector3.one*(.69f+ .16f*pulse);
            retryBrain.transform.localPosition = new Vector3(lane*-1.53f,2.39f,-.72f);
            retryBrain.transform.localEulerAngles = new Vector3(0,Mathf.Sin(time*.9f)*19,0);
            Light(retryHeart,RetryEmber,.38f+pulse*.72f);
            Light(retryBrain,RetryBrass,.51f);
            PoseRetryGhost(journey, phase, lane, time);
            for (int i = 0; i < retryExitTrails.Length; i++)
            {
                var result = retryExitTrails[i];
                bool shown = departure && (int)journey.RuinRetryDisplayOutcome == i + 1;
                result.gameObject.SetActive(shown);
                if (!shown) continue;
                result.transform.localScale = new Vector3(lane,1,1);
                Light(result,Color.white,.45f+.28f*OpeningStoryboard.Ease(.12f,.58f,progress));
            }
            for (int i = 0; i < retryTravelMarks.Length; i++)
            {
                var mark = retryTravelMarks[i];
                mark.gameObject.SetActive(guided);
                float phaseMark = Mathf.Repeat(time*.52f + i*.148f,1);
                mark.transform.localPosition = new Vector3(-lane * (phaseMark*.68f + Mathf.Sin(phaseMark*Mathf.PI)*.37f),
                    .091f, Mathf.Lerp(-8.3f,5.7f,phaseMark));
                Light(mark,RetryIce,.69f*Mathf.Sin(phaseMark*Mathf.PI));
            }
        }

        private void PoseRetryGhost(Journey journey, RuinRetryPhase phase, int lane, float time)
        {
            bool review = phase == RuinRetryPhase.Review;
            bool trace = phase == RuinRetryPhase.Trace;
            bool visible = review || trace && journey.Age < 1.34f;
            retryGhost.gameObject.SetActive(visible);
            if (!visible) return;
            float run = OpeningStoryboard.Ease(.12f,review ? 1.16f : .91f,journey.Age);
            float up = journey.RuinRetryRecord == RuinRetryRecordKind.Upper ? .75f :
                journey.RuinRetryRecord == RuinRetryRecordKind.Lower ? -.29f : 0;
            bool heartPutDown = journey.RuinRetryRecord == RuinRetryRecordKind.HeartPutDown;
            bool brainPutDown = journey.RuinRetryRecord == RuinRetryRecordKind.BrainPutDown;
            float deposit = heartPutDown || brainPutDown ? OpeningStoryboard.Ease(.78f,1.14f,journey.Age) : 0;
            retryGhost.localPosition = new Vector3(lane*Mathf.Lerp(.33f,1.96f,run),
                .14f+run*(.19f+up)+.022f*Mathf.Sin(time*8), Mathf.Lerp(-4.76f,-.96f,run));
            retryGhost.localEulerAngles = new Vector3(29*deposit, lane*34*run,-lane*14*run);
            retryGhostLeft.localEulerAngles = new Vector3(-20*Mathf.Cos(time*8),0,31);
            retryGhostRight.localEulerAngles = new Vector3(29*Mathf.Cos(time*8)*(1-deposit),-57*deposit,-30);
            if (heartPutDown)
                retryHeart.transform.localPosition = Vector3.Lerp(retryHeart.transform.localPosition,
                    retryGhost.localPosition + new Vector3(.31f,.32f-deposit*.18f,-.20f), run);
            if (brainPutDown)
                retryBrain.transform.localPosition = Vector3.Lerp(retryBrain.transform.localPosition,
                    retryGhost.localPosition + new Vector3(.31f,.32f-deposit*.18f,-.20f), run);
            float strength = trace ? OpeningStoryboard.Window(journey.Age,0,.2f,.87f,1.34f) :
                OpeningStoryboard.Ease(0,.28f,journey.Age);
            foreach (var piece in retryGhostParts)
                Light(piece, RetryBrass, strength*(.38f + .14f*Mathf.Sin(time*27)));
        }
    }
}
