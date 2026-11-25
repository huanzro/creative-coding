using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class RuinEnvelope
        {
            public Transform Root, Gallery;
            public readonly Dictionary<int,int> ClearanceSlots = new Dictionary<int,int>();
            public readonly Dictionary<ulong,Transform> CrossingWalls = new Dictionary<ulong,Transform>();
            public readonly Dictionary<ulong,Transform> CrossingFloors = new Dictionary<ulong,Transform>();
            public readonly Dictionary<int,RuinConnectorView> Branches = new Dictionary<int,RuinConnectorView>();
        }

        private sealed class RuinCrossing
        {
            public readonly int Portal;
            public readonly bool Incoming;
            public readonly List<Vector3> Points = new List<Vector3>();

            public RuinCrossing(int portal, bool incoming)
            {
                Portal=portal;
                Incoming=incoming;
            }
        }

        private readonly struct RuinConnectorView
        {
            public readonly Transform Entrance, Interior;
            public RuinConnectorView(Transform entrance, Transform interior)
            {
                Entrance=entrance;
                Interior=interior;
            }
        }

        private readonly Dictionary<int,RuinEnvelope> ruinEnvelopes = new Dictionary<int,RuinEnvelope>();
        private readonly List<Mesh> ruinSpatialMeshes = new List<Mesh>();
        private RuinPortal ruinLayoutIdentity;
        private float ruinVisualDistance, ruinVisualMouth, ruinVisualArrival;

        internal void SetRuinTransitionDistances(float distance,float mouth,float arrival)
        {
            ruinVisualDistance=distance;
            ruinVisualMouth=mouth;
            ruinVisualArrival=arrival;
        }
        internal static RuinTrackConnection RuinConnection(Journey journey,RuinPortal portal)
        {
            Vector2 section=RuinGallerySection(journey.RuinRoomStyle,RuinTrack.PortalAngle(portal.Direction));
            return new RuinTrackConnection(journey.RuinRoomId,journey.RuinRoomLength,portal,section,
                portal.TargetRoom,journey.RuinRoomLengthFor(portal.TargetRoom));
        }

        internal static RuinRouteFrame RuinPassageStart(Journey journey)
        {
            int room=journey.RuinRoomId;
            float length=journey.RuinRoomLength;
            var track=RuinTrack.Frame(room,length,journey.RuinPassageStartDepth);
            float along=RuinTrack.RouteMetresPerDepth(room,length)*journey.RuinPassageStartForwardSpeed;
            Vector3 momentum=track.Forward*along + track.Right*(2.45f*journey.RuinPassageStartLateralSpeed) +
                Vector3.up*(3.30f*journey.RuinPassageStartVerticalSpeed);
            if(momentum.sqrMagnitude<.025f)
                momentum=track.Forward;
            else if(new Vector2(momentum.x,momentum.z).sqrMagnitude<.09f)
                momentum+=track.Forward*.3f;
            return new RuinRouteFrame(track.Point(2.45f*journey.RuinPassageStartLane,
                .54f+3.30f*journey.RuinPassageStartAltitude),momentum);
        }

        private void DrawRuinEnvelopes(Journey j)
        {
            var first=j.RuinPortalsFor(0);
            RuinPortal identity=first.Count>0 ? first[0] : null;
            if (identity!=ruinLayoutIdentity)
            {
                DisposeRuinEnvelopes();
                ruinLayoutIdentity=identity;
            }
            int current=j.RuinRoomId;
            int next=j.Phase==Stage.RuinTransfer && j.RuinEntrance!=null ? j.RuinEntrance.TargetRoom : -1;
            bool transfer=next>=0;
            bool inside=transfer && ruinVisualDistance>=Mathf.Max(0,ruinVisualMouth-2.5f);
            bool entering=inside && ruinVisualDistance>=Mathf.Max(ruinVisualMouth+3f,ruinVisualArrival-16f);
            EnsureRuinEnvelope(j,current);
            if(transfer) EnsureRuinEnvelope(j,next);
            else if(j.RuinApproach?.TargetRoom>=0) EnsureRuinEnvelope(j,j.RuinApproach.TargetRoom);
            foreach(var pair in ruinEnvelopes)
            {
                bool source=pair.Key==current;
                bool destination=entering && pair.Key==next;
                pair.Value.Root.gameObject.SetActive(source || destination);
                if(!source && !destination) continue;
                var envelope=pair.Value;
                bool passedMouth=source && inside && ruinVisualDistance>ruinVisualMouth+12f;
                envelope.Gallery.gameObject.SetActive(!passedMouth || destination);
                ulong cut=0;
                if(inside && (!destination || ruinVisualDistance<ruinVisualArrival+4.8f) &&
                    envelope.ClearanceSlots.TryGetValue(j.RuinEntrance.Id,out int slot))
                    cut=1UL<<slot;
                foreach(var panel in envelope.CrossingWalls)
                    panel.Value.gameObject.SetActive((panel.Key & cut)==0);
                // A side connector joins above the gallery floor. Keep its landing visible as the camera turns in.
                bool floorReady=destination && ruinVisualDistance>=ruinVisualArrival+
                    (IsVerticalRuinGate(j.RuinEntrance.Direction) ? .85f : -6.2f);
                ulong floorCut=floorReady ? 0 : cut;
                foreach(var panel in envelope.CrossingFloors)
                    panel.Value.gameObject.SetActive((panel.Key & floorCut)==0);
                foreach(var branch in pair.Value.Branches)
                {
                    bool selected=transfer && source && branch.Key==j.RuinEntrance.Id;
                    bool showMouth=j.RuinRetryState==RuinRetryPhase.Inactive &&
                        (destination || source && (!inside || selected));
                    branch.Value.Entrance.gameObject.SetActive(showMouth);
                    branch.Value.Interior.gameObject.SetActive(showMouth && selected && inside);
                }
            }
        }

        private void EnsureRuinEnvelope(Journey j,int room)
        {
            if(ruinEnvelopes.ContainsKey(room)) return;
            int style=j.RuinRoomStyleFor(room);
            float length=j.RuinRoomLengthFor(room);
            var openings=j.RuinPortalsFor(room);
            var created=new RuinEnvelope { Root=Group("Continuous archive gallery "+room,tunnelRoot) };
            ruinEnvelopes.Add(room,created);
            created.Gallery=Group("Gallery masonry and floor",created.Root);
            var shell=new WorldMesh(ruinSpatialMeshes);
            // Group panels by the exact connectors that pass through them.
            var crossingWalls=new Dictionary<ulong,WorldMesh>();
            var crossingFloors=new Dictionary<ulong,WorldMesh>();
            var accents=new WorldMesh(ruinSpatialMeshes);
            Color material=RuinGalleryStoneFor(style,room);
            Color hue=RuinGalleryAccentFor(style,room);
            var clearances=CollectRuinClearances(j,room);
            for(int index=0;index<clearances.Count;index++) created.ClearanceSlots.Add(clearances[index].Portal,index);
            WorldMesh Surface(ulong cut,Dictionary<ulong,WorldMesh> panels)
            {
                if(cut==0) return shell;
                if(!panels.TryGetValue(cut,out var panel))
                    panels.Add(cut,panel=new WorldMesh(ruinSpatialMeshes));
                return panel;
            }
            int segments=Mathf.CeilToInt(length*1.46f);
            const int sides=28;
            for(int step=0;step<segments;step++)
            {
                float z=length*step/segments, z2=length*(step+1f)/segments;
                float depth=(z+z2)*.5f;
                RuinRouteFrame a=RuinTrack.Frame(room,length,z), b=RuinTrack.Frame(room,length,z2);
                for(int side=0;side<sides;side++)
                {
                    float p=side * Mathf.PI*2/sides, q=(side+1f) * Mathf.PI*2/sides;
                    float middle=(p+q)*.5f;
                    if(IsRuinOpening(openings,style,length,depth,middle)) continue;
                    Vector2 u=RuinGallerySection(style,p), v=RuinGallerySection(style,q);
                    Vector3 start=a.Point(u.x,u.y), end=a.Point(v.x,v.y), far=b.Point(u.x,u.y), farEnd=b.Point(v.x,v.y);
                    Vector3 centre=(start+end+far+farEnd)*.25f;
                    ulong cut=RuinClearanceMask(clearances,centre,3.29f,3.88f);
                    float tone=.73f+.14f*Mathf.Sin(step*.56f+side*.58f)+.085f*(step%3);
                    WorldMesh surface=Surface(cut,crossingWalls);
                    surface.Quad(start,far,farEnd,end,WorldMesh.Shade(material,tone));
                    if(step%6==0)
                    {
                        Vector2 rim1=Vector2.Lerp(u,new Vector2(0,3.8f),.045f), rim2=Vector2.Lerp(v,new Vector2(0,3.8f),.045f);
                        RuinBar(surface,a.Point(rim1.x,rim1.y),a.Point(rim2.x,rim2.y),.097f,.15f,
                            WorldMesh.Shade(material,1.46f));
                    }
                }

                float cutStart=RuinLowerFloorOpening(openings,length,z);
                float cutEnd=RuinLowerFloorOpening(openings,length,z2);
                for(int side=-1;side<=1;side+=2)
                {
                    const float floorEdge=4.26f;
                    float laneStart=cutStart*side, laneEnd=cutEnd*side;
                    Vector3 insideA=a.Point(laneStart,-.05f), insideB=b.Point(laneEnd,-.05f);
                    Vector3 outsideA=a.Point(floorEdge*side,-.05f), outsideB=b.Point(floorEdge*side,-.05f);
                    Vector3 centre=(insideA+insideB+outsideA+outsideB)*.25f;
                    ulong floorCut=RuinClearanceMask(clearances,centre,2.38f,2.06f);
                    WorldMesh floor=Surface(floorCut,crossingFloors);
                    Color slab=WorldMesh.Shade(material,.92f+.03f*side+.075f*Mathf.Sin(step*.81f+side));
                    if(side<0) floor.Quad(outsideA,outsideB,insideB,insideA,slab);
                    else floor.Quad(insideA,insideB,outsideB,outsideA,slab);
                    if(step%4!=3 || cutStart>0 || cutEnd>0)
                        RuinBar(accents,a.Point(side*Mathf.Max(1.54f,cutStart+.18f),.004f),
                            b.Point(side*Mathf.Max(1.54f,cutEnd+.18f),.004f),.029f,.025f,hue);
                }
            }
            foreach(RuinPortal portal in openings)
                if(IsVerticalRuinGate(portal.Direction) && portal.TargetRoom>=0)
                    BuildRuinVerticalApron(shell,room,style,length,portal,material);
            RuinPiece("Gallery hull with four-way openings",shell.Finish("Archive hull"),tunnelStone,created.Gallery);
            foreach(var pair in crossingWalls)
                created.CrossingWalls.Add(pair.Key,RuinPiece("Fitted crossing walls",
                    pair.Value.Finish("Archive crossing walls"),tunnelStone,created.Gallery));
            foreach(var pair in crossingFloors)
                created.CrossingFloors.Add(pair.Key,RuinPiece("Fitted landing floors",
                    pair.Value.Finish("Archive crossing floors"),tunnelStone,created.Gallery));
            var strips=RuinPiece("Continuous curb lights",accents.Finish("Track inlay"),glow,created.Gallery).GetComponent<MeshRenderer>();
            Light(strips,Color.white,.69f);

            foreach(RuinPortal portal in openings)
            {
                if(portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit || portal.Direction==RuinDirection.Forward) continue;
                int target=portal.TargetRoom;
                int nextStyle=j.RuinRoomStyleFor(target);
                var profile=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
                var connection=new RuinTrackConnection(room,length,portal,profile,target,j.RuinRoomLengthFor(target));
                created.Branches.Add(portal.Id,BuildRuinConnector(created.Root,connection,portal.Direction,material,RuinGalleryStoneFor(nextStyle,target),
                    hue,RuinGalleryAccentFor(nextStyle,target)));
                if(IsVerticalRuinGate(portal.Direction))
                    BuildRuinApproachLights(created.Root,room,style,length,portal,hue);
            }
        }

        private static List<RuinCrossing> CollectRuinClearances(Journey j,int activeRoom)
        {
            var clearances=new List<RuinCrossing>();
            for (int source=0;source<Journey.RuinRoomCount;source++)
            {
                float length=j.RuinRoomLengthFor(source);
                int style=j.RuinRoomStyleFor(source);
                foreach(RuinPortal portal in j.RuinPortalsFor(source))
                {
                    if(portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit || portal.Direction==RuinDirection.Forward ||
                        source!=activeRoom && portal.TargetRoom!=activeRoom) continue;
                    Vector2 profile=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
                    var path=new RuinTrackConnection(source,length,portal,profile,portal.TargetRoom,j.RuinRoomLengthFor(portal.TargetRoom));
                    // The shell and the tubes share exact positions. A crossing between two of
                    // the arcs is cut out of the masonry rather than hidden by a camera dissolve.
                    bool incoming=source!=activeRoom;
                    var crossing=new RuinCrossing(portal.Id,incoming);
                    clearances.Add(crossing);
                    Vector3 previous=path.Entrance;
                    for(int i=1;i<=44;i++)
                    {
                        // Allow more room as the final shaft widens into the gallery and the camera follows.
                        float progress=(incoming ? .86f : .827f)*i/44;
                        Vector3 next=path.Frame(progress).Position;
                        if(source==activeRoom && IsVerticalRuinGate(portal.Direction) && progress<.13f)
                        {
                            previous=next;
                            continue;
                        }
                        int sweep=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(previous,next)/1.42f));
                        for(int s=1;s<=sweep;s++) crossing.Points.Add(Vector3.Lerp(previous,next,s/(float)sweep));
                        previous=next;
                    }
                }
            }
            return clearances;
        }

        private static ulong RuinClearanceMask(List<RuinCrossing> clearances,Vector3 point,float outgoing,float incoming)
        {
            ulong mask=0;
            for(int i=0;i<clearances.Count;i++)
            {
                var crossing=clearances[i];
                if(InsideRuinClearance(crossing.Points,point,crossing.Incoming ? incoming : outgoing))
                    mask|=1UL<<i;
            }
            return mask;
        }

        private static bool InsideRuinClearance(List<Vector3> centreline,Vector3 point,float radius)
        {
            float threshold=radius*radius;
            foreach(Vector3 center in centreline)
                if((center-point).sqrMagnitude<threshold) return true;
            return false;
        }

        private static Vector2 RuinWorldAperture(RuinDirection direction) => RuinGateRadius(direction)*RuinGateScale(direction);

        private const float RuinUpperCutDepth=2.19f, RuinLowerCutDepth=1.77f;

        private static float RuinLowerFloorOpening(IReadOnlyList<RuinPortal> openings,float length,float depth)
        {
            foreach(RuinPortal portal in openings)
            {
                if(portal.Direction!=RuinDirection.Lower || portal.TargetRoom<0) continue;
                float along=Mathf.Abs(RuinTrack.Offset(depth,portal.Depth,length))/RuinLowerCutDepth;
                if(along<1)
                    return (RuinWorldAperture(portal.Direction).x+.37f)*Mathf.Sqrt(1-along*along);
            }
            return 0;
        }

        private static bool IsRuinOpening(IReadOnlyList<RuinPortal> openings,int style,float length,float depth,float angular)
        {
            foreach(RuinPortal portal in openings)
            {
                if(portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit || portal.Direction==RuinDirection.Forward) continue;
                float distance=Mathf.Abs(RuinTrack.Offset(depth,portal.Depth,length));
                float portalAngle=RuinTrack.PortalAngle(portal.Direction);
                float angle=Mathf.Abs(Mathf.DeltaAngle(angular*Mathf.Rad2Deg,portalAngle*Mathf.Rad2Deg));
                if(!IsVerticalRuinGate(portal.Direction))
                {
                    if(distance<1.55f && angle<40-9*distance/1.55f) return true;
                    continue;
                }
                float along=distance/(portal.Direction==RuinDirection.Upper ? RuinUpperCutDepth : RuinLowerCutDepth);
                if(along>=1 || angle>66) continue;
                float center=RuinGallerySection(style,portalAngle).x;
                float across=(RuinGallerySection(style,angular).x-center)/(RuinWorldAperture(portal.Direction).x+.79f);
                if(along*along+across*across<1) return true;
            }
            return false;
        }

        private static void BuildRuinVerticalApron(WorldMesh shell,int room,int style,float length,RuinPortal portal,Color stone)
        {
            bool upper=portal.Direction==RuinDirection.Upper;
            int corners=upper ? 20 : 8;
            float phase=upper ? 0 : .5f;
            var mouth=RuinTrack.Frame(room,length,portal.Depth);
            var profile=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
            var bore=RuinTrack.PortalForward(mouth,portal);
            var rotation=Quaternion.LookRotation(bore);
            Vector2 span=RuinWorldAperture(portal.Direction)*(upper ? 1.18f : 1.23f);
            Vector3 center=mouth.Point(profile.x,profile.y)-bore*(.20f+.13f*RuinGateScale(portal.Direction));
            Vector3 SkirtPoint(float angle)
            {
                float lengthwise=upper ? -RuinUpperCutDepth-.34f : RuinLowerCutDepth+.27f;
                float width=span.x+(upper ? .92f : .35f);
                var frame=RuinTrack.Frame(room,length,portal.Depth+lengthwise*Mathf.Sin(angle));
                float x=profile.x+width*Mathf.Cos(angle);
                return frame.Point(x,upper ? RuinUpperRoofAt(style,x)-.036f : -.037f);
            }
            for (int i=0;i<corners;i++)
            {
                float turn=(i+phase)*Mathf.PI*2/corners;
                float next=(i+1+phase)*Mathf.PI*2/corners;
                Vector3 inner=center+rotation*GatePoint(turn,span,0);
                Vector3 innerNext=center+rotation*GatePoint(next,span,0);
                Color masonry=WorldMesh.Shade(stone,.77f+.12f*(i%3));
                shell.Quad(SkirtPoint(turn),inner,innerNext,SkirtPoint(next),masonry);
            }
        }

        private static float RuinUpperRoofAt(int style,float x)
        {
            // Each gallery profile rises differently, so seat the vault collar on its own roof.
            float right=0, left=Mathf.PI;
            for(int step=0;step<13;step++)
            {
                float middle=(right+left)*.5f;
                if(RuinGallerySection(style,middle).x>x) right=middle;
                else left=middle;
            }
            return RuinGallerySection(style,(right+left)*.5f).y;
        }

        private void BuildRuinApproachLights(Transform parent,int room,int style,float length,RuinPortal portal,Color accent)
        {
            var inlay=new WorldMesh(ruinSpatialMeshes);
            bool upper=portal.Direction==RuinDirection.Upper;
            Color hue=Color.Lerp(upper ? new Color(.56f,.60f,1) : new Color(.35f,.84f,.86f),accent,.24f);
            Vector2 center=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
            float width=RuinWorldAperture(portal.Direction).x;
            for(int side=-1;side<=1;side+=2)
            {
                for(int mark=0;mark<3;mark++)
                {
                    var frame=RuinTrack.Frame(room,length,portal.Depth-3.35f+mark*.67f);
                    float x=center.x+side*(width+.52f-mark*.38f);
                    float y=upper ? center.y-2.75f+mark*.46f : .052f;
                    Vector3 point=frame.Point(x,y);
                    Vector3 tip=point+frame.Forward*.21f+(upper ? Vector3.up*.21f : Vector3.zero);
                    Vector3 axis=frame.Right*.23f;
                    RuinBar(inlay,point-axis,tip,.065f,.042f,WorldMesh.Shade(hue,.49f+mark*.15f));
                    RuinBar(inlay,tip,point+axis,.065f,.042f,WorldMesh.Shade(hue,.49f+mark*.15f));
                }
            }
            var renderer=RuinPiece(upper ? "Rise toward the vault" : "Descend through the floor",
                inlay.Finish("Shaft approach marks"),glow,parent).GetComponent<MeshRenderer>();
            Light(renderer,Color.white,.80f);
        }

        private RuinConnectorView BuildRuinConnector(Transform parent,RuinTrackConnection path,RuinDirection direction,
            Color stoneFrom,Color stoneTo,Color lightFrom,Color lightTo)
        {
            const int pieces=72, sides=16, visibleSegments=11;
            var firstWall=new WorldMesh(ruinSpatialMeshes);
            var firstTrim=new WorldMesh(ruinSpatialMeshes);
            var innerWall=new WorldMesh(ruinSpatialMeshes);
            var innerTrim=new WorldMesh(ruinSpatialMeshes);
            var mouthRoot=Group("Visible passage entrance",parent);
            var innerRoot=Group("Passage interior",mouthRoot);
            bool vertical=IsVerticalRuinGate(direction);
            Vector2 mouth=vertical ? RuinWorldAperture(direction)*1.09f : new Vector2(2.09f,2.10f);
            Vector2 passage=vertical ? new Vector2(3.50f,3.14f) : new Vector2(3.25f,2.95f);
            for(int i=0;i<pieces;i++)
            {
                WorldMesh wall=i<visibleSegments ? firstWall : innerWall;
                WorldMesh trim=i<visibleSegments ? firstTrim : innerTrim;
                float t=i/(float)pieces*.817f, next=(i+1f)/pieces*.817f;
                Vector2 span=RuinConnectorRadius(t,mouth,passage);
                Vector2 nextSpan=RuinConnectorRadius(next,mouth,passage);
                Color rock=Color.Lerp(stoneFrom,stoneTo,t/.817f);
                Color lit=Color.Lerp(lightFrom,lightTo,t/.817f);
                var a=path.Frame(t);
                var b=path.Frame(next);
                for(int side=0;side<sides;side++)
                {
                    float n=side*Mathf.PI*2/sides, m=(side+1f)*Mathf.PI*2/sides;
                    Vector2 unitN=new Vector2(Mathf.Cos(n),Mathf.Sin(n)), unitM=new Vector2(Mathf.Cos(m),Mathf.Sin(m));
                    Vector2 an=Vector2.Scale(unitN,span), am=Vector2.Scale(unitM,span);
                    Vector2 bn=Vector2.Scale(unitN,nextSpan), bm=Vector2.Scale(unitM,nextSpan);
                    Vector3 p=a.Section(an.x,an.y), q=a.Section(am.x,am.y), r=b.Section(bn.x,bn.y), s=b.Section(bm.x,bm.y);
                    wall.Quad(p,r,s,q,WorldMesh.Shade(rock,.66f+.16f*((side+i/8)%3)+.11f*Mathf.Sin(i*.47f+side*.63f)));
                    if(i%9==0)
                        RuinBar(trim,a.Section(an.x*.94f,an.y*.94f),a.Section(am.x*.94f,am.y*.94f),.045f,.046f,lit);
                    if(side==1 || side==7 || side==10 || side==13)
                        RuinBar(trim,a.Section(an.x*.988f,an.y*.988f),b.Section(bn.x*.988f,bn.y*.988f),.029f,.037f,lit);
                }
            }
            RuinPiece("Mouth masonry",firstWall.Finish("Bending passage entrance"),tunnelStone,mouthRoot);
            var lip=RuinPiece("Mouth ribs",firstTrim.Finish("Entrance inlay"),glow,mouthRoot).GetComponent<MeshRenderer>();
            Light(lip,Color.white,.81f);
            RuinPiece("Curved structural bore",innerWall.Finish("Bending passage interior"),tunnelStone,innerRoot);
            var lines=RuinPiece("Passage ribs",innerTrim.Finish("Transfer inlay"),glow,innerRoot).GetComponent<MeshRenderer>();
            Light(lines,Color.white,.81f);
            return new RuinConnectorView(mouthRoot,innerRoot);
        }

        private static Vector2 RuinConnectorRadius(float t,Vector2 mouth,Vector2 passage)
        {
            float entrance=Mathf.SmoothStep(0,1,t/.14f);
            float opening=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.61f,.817f,t));
            return Vector2.Lerp(mouth,passage,entrance)+new Vector2(1.19f,1.34f)*opening;
        }

        private void DisposeRuinEnvelopes()
        {
            foreach(var room in ruinEnvelopes.Values) if(room.Root!=null) Object.Destroy(room.Root.gameObject);
            foreach(Mesh mesh in ruinSpatialMeshes) if(mesh!=null) Object.Destroy(mesh);
            ruinEnvelopes.Clear();
            ruinSpatialMeshes.Clear();
        }
    }
}
