using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class RuinEnvelope
        {
            public Transform Root;
            public readonly Dictionary<int,Transform> Branches = new Dictionary<int,Transform>();
        }

        private readonly Dictionary<int,RuinEnvelope> ruinEnvelopes = new Dictionary<int,RuinEnvelope>();
        private readonly List<Mesh> ruinSpatialMeshes = new List<Mesh>();
        private RuinPortal ruinLayoutIdentity;
        internal static RuinTrackConnection RuinConnection(Journey journey,RuinPortal portal)
        {
            Vector2 section=RuinGallerySection(journey.RuinRoomStyle,RuinTrack.PortalAngle(portal.Direction));
            return new RuinTrackConnection(journey.RuinRoomId,journey.RuinRoomLength,portal,section,
                portal.TargetRoom,journey.RuinRoomLengthFor(portal.TargetRoom));
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
            EnsureRuinEnvelope(j,current);
            if(next>=0) EnsureRuinEnvelope(j,next);
            foreach(var pair in ruinEnvelopes)
            {
                bool active=pair.Key==current || pair.Key==next;
                pair.Value.Root.gameObject.SetActive(active);
                if(!active) continue;
                foreach(var branch in pair.Value.Branches)
                    branch.Value.gameObject.SetActive(pair.Key==current && j.RuinRetryState==RuinRetryPhase.Inactive &&
                        (j.Phase!=Stage.RuinTransfer || j.Progress<.18f || branch.Key==j.RuinEntrance.Id));
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
            var shell=new WorldMesh(ruinSpatialMeshes);
            var accents=new WorldMesh(ruinSpatialMeshes);
            Color material=RuinGalleryStoneFor(style,room);
            Color hue=RuinGalleryAccentFor(style,room);
            List<Vector3> clearances=CollectRuinClearances(j,room);
            int segments=Mathf.CeilToInt(length*1.46f);
            const int sides=28;
            for(int step=0;step<segments;step++)
            {
                float z=length*step/segments, z2=length*(step+1f)/segments;
                float depth=(z+z2)*.5f;
                RuinRouteFrame a=RuinTrack.Frame(room,length,z), b=RuinTrack.Frame(room,length,z2);
                float seam=RuinTrack.Offset(depth,0,length);
                bool arrivals=seam > -7.35f && seam < -3.12f;
                for(int side=0;side<sides;side++)
                {
                    float p=side * Mathf.PI*2/sides, q=(side+1f) * Mathf.PI*2/sides;
                    float middle=(p+q)*.5f;
                    if(arrivals && Mathf.Sin(middle)>-.48f || IsRuinOpening(openings,length,depth,middle)) continue;
                    Vector2 u=RuinGallerySection(style,p), v=RuinGallerySection(style,q);
                    Vector3 start=a.Point(u.x,u.y), end=a.Point(v.x,v.y), far=b.Point(u.x,u.y), farEnd=b.Point(v.x,v.y);
                    if(InsideRuinClearance(clearances,(start+end+far+farEnd)*.25f,3.29f)) continue;
                    float tone=.73f+.14f*Mathf.Sin(step*.56f+side*.58f)+.085f*(step%3);
                    shell.Quad(start,far,farEnd,end,WorldMesh.Shade(material,tone));
                    if(step%6==0 && !arrivals)
                    {
                        Vector2 rim1=Vector2.Lerp(u,new Vector2(0,3.8f),.045f), rim2=Vector2.Lerp(v,new Vector2(0,3.8f),.045f);
                        RuinBar(shell,a.Point(rim1.x,rim1.y),a.Point(rim2.x,rim2.y),.097f,.15f,
                            WorldMesh.Shade(material,1.46f));
                    }
                }

                int floorLanes=3;
                for(int lane=0;lane<floorLanes;lane++)
                {
                    float left= (lane-floorLanes*.5f)*2.85f, right=left+2.82f;
                    if(lane==1 && IsRuinOpening(openings,length,depth,Mathf.PI*1.5f)) continue;
                    if(InsideRuinClearance(clearances,(a.Point((left+right)*.5f,0)+b.Point((left+right)*.5f,0))*.5f,2.38f)) continue;
                    Color slab=WorldMesh.Shade(material,.86f+.10f*(lane%2)+.075f*Mathf.Sin(step*.81f+lane));
                    shell.Quad(a.Point(left,-.05f),b.Point(left,-.05f),b.Point(right,-.05f),a.Point(right,-.05f),slab);
                }
                for(int side=-1;side<=1;side+=2)
                    if(step%4!=3)
                        RuinBar(accents,a.Point(side*1.54f,.004f),b.Point(side*1.54f,.004f),.025f,.023f,hue);
            }
            RuinPiece("Seamed closed loop with four-way openings",shell.Finish("Bent archive hull"),tunnelStone,created.Root);
            var strips=RuinPiece("Continuous curb lights",accents.Finish("Track inlay"),glow,created.Root).GetComponent<MeshRenderer>();
            Light(strips,Color.white,.69f);

            foreach(RuinPortal portal in openings)
            {
                if(portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit || portal.Direction==RuinDirection.Forward) continue;
                int target=portal.TargetRoom;
                int nextStyle=j.RuinRoomStyleFor(target);
                var profile=RuinGallerySection(style,RuinTrack.PortalAngle(portal.Direction));
                var connection=new RuinTrackConnection(room,length,portal,profile,target,j.RuinRoomLengthFor(target));
                created.Branches.Add(portal.Id,BuildRuinConnector(created.Root,connection,material,RuinGalleryStoneFor(nextStyle,target),
                    hue,RuinGalleryAccentFor(nextStyle,target)));
            }
        }

        private static List<Vector3> CollectRuinClearances(Journey j,int activeRoom)
        {
            var samples=new List<Vector3>();
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
                    Vector3 previous=path.Entrance;
                    for(int i=1;i<=44;i++)
                    {
                        Vector3 next=path.Frame(.827f*i/44).Position;
                        int sweep=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(previous,next)/1.42f));
                        for(int s=1;s<=sweep;s++) samples.Add(Vector3.Lerp(previous,next,s/(float)sweep));
                        previous=next;
                    }
                }
            }
            return samples;
        }

        private static bool InsideRuinClearance(List<Vector3> centreline,Vector3 point,float radius)
        {
            float threshold=radius*radius;
            foreach(Vector3 center in centreline)
                if((center-point).sqrMagnitude<threshold) return true;
            return false;
        }

        private static bool IsRuinOpening(IReadOnlyList<RuinPortal> openings,float length,float depth,float angular)
        {
            foreach(RuinPortal portal in openings)
            {
                if(portal.TargetRoom<0 || portal.Direction==RuinDirection.Exit || portal.Direction==RuinDirection.Forward) continue;
                float distance=Mathf.Abs(RuinTrack.Offset(depth,portal.Depth,length));
                float angle=Mathf.Abs(Mathf.DeltaAngle(angular*Mathf.Rad2Deg,RuinTrack.PortalAngle(portal.Direction)*Mathf.Rad2Deg));
                if(distance<1.55f && angle<40-9*distance/1.55f) return true;
            }
            return false;
        }

        private Transform BuildRuinConnector(Transform parent,RuinTrackConnection path,Color stoneFrom,Color stoneTo,Color lightFrom,Color lightTo)
        {
            const int pieces=72, sides=16;
            var wall=new WorldMesh(ruinSpatialMeshes);
            var trim=new WorldMesh(ruinSpatialMeshes);
            var node=Group("Sloping passage connected to adjacent gallery",parent);
            for(int i=0;i<pieces;i++)
            {
                float t=i/(float)pieces*.817f, next=(i+1f)/pieces*.817f;
                float entrance=Mathf.SmoothStep(0,1,t/.14f);
                float opening=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.61f,.817f,t));
                float width=Mathf.Lerp(2.09f,3.25f,entrance)+opening*1.19f;
                float height=Mathf.Lerp(2.10f,2.95f,entrance)+opening*1.34f;
                Color rock=Color.Lerp(stoneFrom,stoneTo,t/.817f);
                Color lit=Color.Lerp(lightFrom,lightTo,t/.817f);
                var a=path.Frame(t);
                var b=path.Frame(next);
                for(int side=0;side<sides;side++)
                {
                    float n=side*Mathf.PI*2/sides, m=(side+1f)*Mathf.PI*2/sides;
                    float nx=Mathf.Cos(n)*width, ny=Mathf.Sin(n)*height;
                    float mx=Mathf.Cos(m)*width, my=Mathf.Sin(m)*height;
                    Vector3 p=a.Section(nx,ny), q=a.Section(mx,my), r=b.Section(nx,ny), s=b.Section(mx,my);
                    wall.Quad(p,r,s,q,WorldMesh.Shade(rock,.66f+.16f*((side+i/8)%3)+.11f*Mathf.Sin(i*.47f+side*.63f)));
                    if(i%9==0)
                        RuinBar(trim,a.Section(nx*.94f,ny*.94f),a.Section(mx*.94f,my*.94f),.045f,.046f,lit);
                    if(side==1 || side==7 || side==10 || side==13)
                        RuinBar(trim,a.Section(nx*.988f,ny*.988f),b.Section(nx*.988f,ny*.988f),.029f,.037f,lit);
                }
            }
            RuinPiece("Curved structural bore",wall.Finish("Bending passage interior"),tunnelStone,node);
            var lines=RuinPiece("Animated depth framing",trim.Finish("Transfer inlay"),glow,node).GetComponent<MeshRenderer>();
            Light(lines,Color.white,.81f);
            return node;
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
