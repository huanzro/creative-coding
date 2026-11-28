/* ARTISTIC RATIONALE
 * Crossing the wall reveals the archive as a machine that can be inspected. Its load-bearing
 * layers follow the galleries and branches the player has just travelled. The same descent path
 * that delivers the machine frames the two ways out, with different colours and spatial rhythms.
 */
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        internal static readonly Vector3 RuinFrameworkOrigin = new Vector3(0,420,0);
        internal static readonly Vector3 RuinStarLanding = new Vector3(0,2.48f,22.8f);
        internal static readonly Vector3 StarEntrySight = new Vector3(3.85f,3.8f,18.7f);
        internal static readonly Vector3 StarEntryCamera = new Vector3(-1.7f,StarY+3.3f,9.3f);
        private static readonly Vector3 BlueprintCentre = new Vector3(1.7f,-2.8f,27.8f);
        private static readonly Color BlueprintSteel = new Color(.23f,.39f,.50f);
        private static readonly Color BlueprintIce = new Color(.37f,.82f,1);
        private static readonly Color BlueprintGold = new Color(1,.67f,.34f);

        private Transform frameworkRoot, frameworkBlueprint, frameworkSky, frameworkExit, frameworkStaticShell;
        private MeshRenderer frameworkTraces, frameworkCool, frameworkWarm;
        private MeshRenderer[] frameworkSignals;
        private readonly List<Mesh> frameworkMeshes = new List<Mesh>();
        private readonly List<MeshRenderer> frameworkFadeSurfaces = new List<MeshRenderer>();
        private readonly MaterialPropertyBlock frameworkProperties = new MaterialPropertyBlock();
        private int frameworkSeed, builtFrameworkRooms;
        private float frameworkPreviousFade;

        internal static bool IsRuinFramework(RuinRetryPhase phase) => phase >= RuinRetryPhase.Gate;

        internal static bool IsRuinDetached(Journey journey) =>
            journey.RuinRetryState > RuinRetryPhase.Gate ||
            journey.RuinRetryState == RuinRetryPhase.Gate && journey.Age >= 2.66f;

        internal static bool IsRuinExit(RuinRetryPhase phase) =>
            phase == RuinRetryPhase.Accepted || phase == RuinRetryPhase.Declined;

        internal static void RuinFrameworkPlacement(Journey journey, bool detached, out Vector3 origin, out Quaternion rotation)
        {
            if (detached)
            {
                origin = RuinFrameworkOrigin;
                rotation = Quaternion.identity;
                return;
            }
            var wall = RuinTrack.Frame(journey.RuinRoomId,journey.RuinRoomLength,journey.RuinRetryWallDepth-.18f);
            rotation = Quaternion.LookRotation(new Vector3(wall.Forward.x,0,wall.Forward.z));
            origin = wall.Position + Vector3.up*RuinY + rotation*Vector3.forward*5.12f;
        }

        internal static Vector3 RuinFrameworkActor(Journey journey)
        {
            float age = journey.Age;
            switch (journey.RuinRetryState)
            {
                case RuinRetryPhase.Gate:
                    return new Vector3(0,0,Mathf.Lerp(-7.48f,0,OpeningStoryboard.Ease(.81f,2.98f,age)));
                case RuinRetryPhase.Accepted:
                    return ExitGroveActor(age, true);
                case RuinRetryPhase.Declined:
                    return ExitGroveActor(age, false);
                default: return Vector3.zero;
            }
        }

        private static Vector3 ExitGroveActor(float age, bool accepted)
        {
            float stride=OpeningStoryboard.Ease(accepted ? 3.01f : 2.88f, 9.85f, age);
            float turn=OpeningStoryboard.Window(age, 2.52f, 4.19f, 5.04f, 7.94f);
            return new Vector3((accepted ? -.72f : -2.38f) * turn + .91f * stride,
                2.28f * stride + .73f * Mathf.Sin(stride * Mathf.PI), 27.9f * stride);
        }

        private void BuildRuinFramework()
        {
            frameworkRoot = Group("Behind the archive walls",root.transform);
            frameworkRoot.gameObject.SetActive(false);
            frameworkStaticShell = Group("Fixed inspection architecture",frameworkRoot);
            var backing = Geometry();
            backing.Box(Vector3.zero,Vector3.one*2,Quaternion.identity,Color.white);
            frameworkSky = RuinPiece("Sky around the suspended archive",backing.Finish("Archive exterior sky"),sky,frameworkRoot);
            frameworkSky.localScale = Vector3.one*210;

            var deck = Geometry();
            var markings = Geometry();
            Color plates = new Color(.19f,.28f,.38f), bevel = new Color(.32f,.43f,.51f);
            for (int row = 0; row < 8; row++)
            {
                float z = -4.7f + row*2.18f;
                float width = row < 4 ? 8.4f : Mathf.Lerp(8.4f,3.2f,(row-4)/3f);
                deck.Box(new Vector3(0,-.23f,z),new Vector3(width,.42f,2.11f),Quaternion.identity,plates);
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = side*(width*.5f-.22f);
                    deck.Box(new Vector3(edge,-.12f,z),new Vector3(.31f,.42f,2.11f),Quaternion.identity,bevel);
                    for (int line = 0; line < 3; line++)
                        markings.Box(new Vector3(edge-side*.19f,.008f,z-.78f+line*.71f),
                            new Vector3(.050f,.018f,.21f),Quaternion.identity,WorldMesh.Shade(BlueprintIce,.46f+line*.16f));
                    Vector3 support = new Vector3(edge,-.33f,z-.75f);
                    Vector3 bottom = new Vector3(edge*.56f,-3.0f,z+.61f);
                    RuinBar(deck,support,bottom,.16f,.13f,bevel);
                }
            }
            for (int lane = -1; lane <= 1; lane += 2)
            {
                RuinBar(markings,new Vector3(lane*.87f,.018f,-5.6f),new Vector3(lane*.87f,.018f,8.2f),
                    .032f,.018f,BlueprintIce);
                RuinBar(deck,new Vector3(lane*3.94f,-.25f,-5.8f),new Vector3(lane*3.94f,-.25f,8.5f),
                    .33f,.20f,bevel);
            }
            RuinPiece("Suspended inspection deck",deck.Finish("Segmented inspection deck"),stone,frameworkStaticShell);
            frameworkTraces=RuinPiece("Walkway rails and edge tick marks",markings.Finish("Illuminated deck routing"),glow,frameworkStaticShell)
                .GetComponent<MeshRenderer>();

            BuildFrameworkLoom();
            BuildRuinExitLights();
            frameworkSignals = new MeshRenderer[16];
            for (int i=0; i<frameworkSignals.Length; i++)
                frameworkSignals[i]=RuinPiece("Traffic in the exposed rails",beaconMesh,glow,frameworkRoot)
                    .GetComponent<MeshRenderer>();
        }

        private void BuildFrameworkLoom()
        {
            var scaffolding = Geometry();
            var lines = Geometry();
            var distant = Geometry();
            Quaternion standing = Quaternion.Euler(0,0,0);
            Vector3 hub = new Vector3(0,32f,31);
            for (int tier = 0; tier < 7; tier++)
            {
                float radius=13.8f+tier*2.04f;
                Vector3 center=hub+Vector3.up*(tier*4.1f-12.3f);
                Quaternion angle=Quaternion.Euler(79+tier*3.1f,9+tier*11,4-tier*2.7f);
                Ring(scaffolding,center,radius,.18f,BlueprintSteel,angle,62,1);
                Ring(lines,center,radius+.25f,.043f,tier%2==0?BlueprintIce:BlueprintGold,angle,71,.78f);
                Ring(lines,center,radius-.38f,.022f,BlueprintIce,angle,44,.69f);
                for (int side=-1; side<=1; side+=2)
                {
                    Vector3 arm=center+new Vector3(side*(radius-2.7f),0,3.7f);
                    Vector3 anchor=new Vector3(side*(51+5*tier),-29f-tier*6f,63f+tier*7f);
                    RuinBar(scaffolding,arm,anchor,.21f,.19f,BlueprintSteel);
                    RuinBar(distant,arm+Vector3.up*.22f,anchor+Vector3.up*.22f,.048f,.047f,BlueprintIce*.62f);
                }
            }
            for (int side=-1;side<=1;side+=2)
            {
                Vector3 pivot = new Vector3(side*32,8,8);
                RuinBar(scaffolding,pivot+Vector3.down*52,pivot+Vector3.up*28,.51f,.51f,BlueprintSteel);
                RuinBar(lines,pivot+Vector3.down*51+Vector3.right*.28f,pivot+Vector3.up*27+Vector3.right*.28f,
                    .040f,.058f,BlueprintIce);
                Ring(distant,new Vector3(side*57,-37,47),29,.10f,BlueprintIce*.67f,standing,76,.86f);
            }
            RuinPiece("Overhead archive load bearing rings",scaffolding.Finish("Shaft cradle and tension members"),stone,frameworkStaticShell);
            Light(RuinPiece("Loading rings and cables",lines.Finish("Shaft rim lights"),glow,frameworkStaticShell)
                .GetComponent<MeshRenderer>(),Color.white,.37f);
            Light(RuinPiece("Distant shaft supports through the void",distant.Finish("Tension cables and distant ring"),glow,frameworkStaticShell)
                .GetComponent<MeshRenderer>(),Color.white,.25f);
        }

        private void BuildRuinExitLights()
        {
            frameworkExit = Group("Two openings toward the Star Sea",frameworkRoot);
            var cool = Geometry();
            var warm = Geometry();
            for (int rib = 0; rib < 11; rib++)
            {
                float z = 7.8f+rib*1.51f;
                float iceRadius = 1.86f + .13f*rib;
                Vector3 coolCentre=new Vector3(-.55f,1.72f+.09f*rib,z);
                Ring(cool,coolCentre,iceRadius,.036f,BlueprintIce,Quaternion.identity,28,.89f);
                Ring(cool,coolCentre+new Vector3(0,0,.23f),iceRadius+.25f,.023f,BlueprintIce,Quaternion.Euler(0,0,22),24,.62f);

                float t = rib/10f;
                Vector3 start = new Vector3(-3.0f-1.65f*Mathf.Sin(t*Mathf.PI),.09f+2.09f*t,1.5f+z*.86f);
                Vector3 step = start + new Vector3(-.12f,.015f,1.24f);
                RuinBar(warm,start-Vector3.right*.85f,step-Vector3.right*.85f,.031f,.037f,BlueprintGold);
                RuinBar(warm,start+Vector3.right*.85f,step+Vector3.right*.85f,.031f,.037f,BlueprintGold);
                RuinBar(warm,start-Vector3.right*.85f,start+Vector3.right*.85f,.019f,.034f,BlueprintGold*.61f);
            }
            frameworkCool = RuinPiece("Cold inspection apertures",cool.Finish("Token exit ring corridor"),glow,frameworkExit)
                .GetComponent<MeshRenderer>();
            frameworkWarm = RuinPiece("Warm path under its own light",warm.Finish("Independent amber ladder of light"),glow,frameworkExit)
                .GetComponent<MeshRenderer>();
        }

        private static Vector3 BlueprintPoint(Vector3 actual) =>
            BlueprintCentre + new Vector3(actual.x*.80f/RuinAtlas.GalleryLengthMultiplier,actual.y*.89f,
                actual.z*.79f/RuinAtlas.GalleryLengthMultiplier);

        private void ClearRuinBlueprint()
        {
            if (frameworkBlueprint != null)
            {
                frameworkBlueprint.gameObject.SetActive(false);
                Object.Destroy(frameworkBlueprint.gameObject);
                frameworkBlueprint=null;
            }
            foreach (Mesh mesh in frameworkMeshes) Object.Destroy(mesh);
            frameworkMeshes.Clear();
            frameworkFadeSurfaces.Clear();
            frameworkPreviousFade=-1;
        }

        private void PrepareRuinBlueprint(Journey journey)
        {
            if (frameworkBlueprint == null || frameworkSeed != journey.RuinVisualSeed)
            {
                ClearRuinBlueprint();
                frameworkSeed = journey.RuinVisualSeed;
                builtFrameworkRooms = 0;
                frameworkBlueprint=Group("The sixteen explored gallery shells",frameworkRoot);
                frameworkFadeSurfaces.AddRange(frameworkStaticShell.GetComponentsInChildren<MeshRenderer>(true));
            }
            int batch = Mathf.Min(RuinAtlas.RoomCount,builtFrameworkRooms+2);
            for (;builtFrameworkRooms<batch;builtFrameworkRooms++)
            {
                BuildFrameworkRoom(journey,builtFrameworkRooms);
                var gallery=frameworkBlueprint.GetChild(frameworkBlueprint.childCount-1);
                frameworkFadeSurfaces.AddRange(gallery.GetComponentsInChildren<MeshRenderer>(true));
            }
        }

        private void BuildFrameworkRoom(Journey journey, int room)
        {
            int style = journey.RuinRoomStyleFor(room);
            float length = journey.RuinRoomLengthFor(room);
            Color hue = Color.Lerp(BlueprintIce,RuinGalleryAccentFor(style,room),.58f);
            const int ribs = 52, sides = 10;
            var metal = new WorldMesh(frameworkMeshes);
            var trace = new WorldMesh(frameworkMeshes);
            var rail = new WorldMesh(frameworkMeshes);
            var prior = new Vector3[sides];
            for (int rib = 0; rib <= ribs; rib++)
            {
                float depth = rib * length/ribs;
                RuinRouteFrame road = RuinTrack.Frame(room,length,depth);
                for (int vertex = 0; vertex < sides; vertex++)
                {
                    Vector2 section = RuinGallerySection(style,vertex*2*Mathf.PI/sides);
                    Vector3 next=BlueprintPoint(road.Section(section.x,section.y));
                    if (rib>0)
                    {
                        if (vertex%2==0)
                            RuinBar(rail,prior[vertex],next,.086f,.069f,BlueprintSteel);
                        if (vertex==0 || vertex==3 || vertex==7)
                            RuinBar(trace,prior[vertex]+Vector3.up*.055f,next+Vector3.up*.055f,.027f,.026f,
                                WorldMesh.Shade(hue,vertex==0?.73f:.53f));
                    }
                    prior[vertex] = next;
                }
                if (rib>0 && rib%3==1)
                {
                    for (int edge=0;edge<sides;edge++)
                    {
                        RuinBar(metal,prior[edge],prior[(edge+1)%sides],.167f,.118f,BlueprintSteel);
                        if (edge%3==0)
                            trace.Box(prior[edge],Vector3.one*.128f,road.Rotation,hue);
                    }
                }
            }
            foreach (RuinPortal portal in journey.RuinPortalsFor(room))
            {
                if (portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit) continue;
                Vector2 opening=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
                var link = new RuinTrackConnection(room,length,portal,opening,portal.TargetRoom,
                    journey.RuinRoomLengthFor(portal.TargetRoom),angle=>RuinGallerySection(style,angle));
                BuildFrameworkConnector(metal,trace,link,hue);
            }
            Transform gallery=Group("Exposed gallery " + (room+1),frameworkBlueprint);
            RuinPiece("Transverse bent sections",metal.Finish("Bent sections and branch hinges"),stone,gallery);
            RuinPiece("Longitudinal support rails",rail.Finish("Archive longitudinal rails"),stone,gallery);
            Light(RuinPiece("Live trace of each tube and junction",trace.Finish("Inlaid tube and shaft markings"),glow,gallery)
                .GetComponent<MeshRenderer>(),Color.white,.44f);
        }

        private static void BuildFrameworkConnector(WorldMesh cases, WorldMesh lights, RuinTrackConnection link, Color accent)
        {
            const int count = 32;
            Vector3 previous = Vector3.zero;
            for (int i=0; i<=count; i++)
            {
                float t = i/(float)count*.88f;
                RuinRouteFrame frame=link.Frame(t);
                Vector3 centre=BlueprintPoint(frame.Position);
                if (i>0)
                {
                    RuinBar(cases,previous,centre,.096f,.099f,BlueprintSteel);
                    RuinBar(lights,previous+Vector3.up*.11f,centre+Vector3.up*.11f,.041f,.039f,accent);
                }
                if (i%4==0)
                    Ring(cases,centre,1.13f,.073f,BlueprintSteel,frame.Rotation,8,1);
                previous=centre;
            }
        }

        internal void DrawRuinFramework(Journey journey, float time, bool detached, Vector3 actor, int cast)
        {
            RuinRetryPhase phase=journey.RuinRetryState;
            bool active=IsRuinFramework(phase);
            bool leaving=IsRuinExit(phase);
            SetRuinStarPreview(leaving,journey.Age);
            DrawRuinBroker(journey,cast,RuinFrameworkOrigin,actor-Vector3.up*.54f,Vector3.forward);
            if (!active && phase!=RuinRetryPhase.Impact)
            {
                if (frameworkRoot!=null) frameworkRoot.gameObject.SetActive(false);
                return;
            }

            if (frameworkRoot==null) BuildRuinFramework();
            PrepareRuinBlueprint(journey);
            frameworkRoot.gameObject.SetActive(active);
            if (!active) return;
            RuinFrameworkPlacement(journey,detached,out Vector3 origin,out Quaternion facing);
            frameworkRoot.SetPositionAndRotation(origin,facing);
            frameworkSky.gameObject.SetActive(detached);
            bool exchange=phase==RuinRetryPhase.Accepted;
            float fade=leaving ? OpeningStoryboard.Ease(4.16f,4.97f,journey.Age) : 0;
            float open = leaving ? OpeningStoryboard.Ease(exchange?2.52f:2.03f,3.4f,journey.Age) : 0;
            frameworkExit.gameObject.SetActive(open>.004f && fade<.998f);
            Light(frameworkTraces,Color.white,.46f+.13f*Mathf.Sin(time*1.22f),stageLight:1-.96f*fade);
            if (!Mathf.Approximately(fade,frameworkPreviousFade))
            {
                frameworkPreviousFade=fade;
                foreach (MeshRenderer item in frameworkFadeSurfaces)
                {
                    item.GetPropertyBlock(frameworkProperties);
                    frameworkProperties.SetFloat(StageLight,1-.96f*fade);
                    item.SetPropertyBlock(frameworkProperties);
                }
            }
            frameworkBlueprint.gameObject.SetActive(fade<.998f);
            frameworkStaticShell.gameObject.SetActive(fade<.998f);
            frameworkCool.gameObject.SetActive(exchange);
            frameworkWarm.gameObject.SetActive(!exchange);
            Light(exchange?frameworkCool:frameworkWarm,Color.white,.76f*open*(1-.66f*fade),stageLight:1-fade);
            for (int i=0; i<frameworkSignals.Length; i++)
            {
                int room=(i*7)%RuinAtlas.RoomCount;
                float length=journey.RuinRoomLengthFor(room);
                float distance=Mathf.Repeat(time*(1.3f+.16f*(i%4)) + i*2.29f,length);
                var track=RuinTrack.Frame(room,length,distance);
                MeshRenderer signal=frameworkSignals[i];
                signal.transform.localPosition=BlueprintPoint(track.Point(i%2==0?3.37f:-3.37f,5.41f));
                signal.transform.localRotation=track.Rotation;
                signal.transform.localScale=new Vector3(.094f,.085f,.48f);
                signal.gameObject.SetActive(fade<.996f);
                Light(signal,i%3==0?BlueprintGold:BlueprintIce,1.21f*(1-.61f*fade),stageLight:1-fade);
            }
        }
    }
}
