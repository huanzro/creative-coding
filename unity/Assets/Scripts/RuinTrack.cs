using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal readonly struct RuinRouteFrame
    {
        public readonly Vector3 Position, Forward, Right;
        public RuinRouteFrame(Vector3 position, Vector3 forward)
        {
            Position=position;
            Forward=forward.normalized;
            Right=Vector3.Cross(Mathf.Abs(Forward.y)>.985f ? Vector3.forward : Vector3.up, Forward).normalized;
        }
        public Quaternion Rotation => Quaternion.LookRotation(Forward, Vector3.Cross(Forward,Right));
        public Vector3 Point(float lateral,float height) => Position + Right*lateral + Vector3.up*height;
        public Vector3 Section(float across,float above) => Position + Right*across + Vector3.Cross(Forward,Right)*above;
    }

    internal static class RuinTrack
    {
        // One authored district occupies the centre. Other galleries use the surrounding grid
        // and multiple elevations, so crossing a side branch really leaves the original hull.
        private const float Cell=57;
        private static readonly int[] Cells={4,3,5,1,7,0,6,8,2,7,1,3,5,0,6,8};
        private static readonly int[] Elevations={0,0,0,1,0,-1,-2,-1,2,-2,3,-1,1,1,-3,-3};
        private static readonly float[] Heading={0,185,177,69,282,85,257,223,109,188,246,351,65,164,348,169};
        // The first edge passes the entrance. Each plan has a distinct long sightline and a dogleg
        // that hides the return, with enough room inside the bends for the full gallery cross-section.
        private static readonly Vector2[][] Plans =
        {
            new[] { new Vector2(1,-1.18f), new Vector2(1,1.13f), new Vector2(-.35f,1.16f),
                new Vector2(-1.18f,.57f), new Vector2(-.86f,-.19f), new Vector2(-1.19f,-1.10f), new Vector2(.10f,-1.23f) },
            new[] { new Vector2(1.09f,-1.01f), new Vector2(1.09f,1.04f), new Vector2(.27f,1.18f),
                new Vector2(-1.10f,.83f), new Vector2(-1.10f,-.87f), new Vector2(-.34f,-1.17f), new Vector2(.15f,-1) },
            new[] { new Vector2(1.14f,-.98f), new Vector2(1.14f,.86f), new Vector2(.18f,1.22f),
                new Vector2(-1.13f,.98f), new Vector2(-.92f,.24f), new Vector2(-1.12f,-.49f),
                new Vector2(-.61f,-1.13f), new Vector2(.22f,-1.17f) },
            new[] { new Vector2(.96f,-1.20f), new Vector2(.96f,.66f), new Vector2(.62f,1.17f), new Vector2(-.66f,1.17f),
                new Vector2(-1.17f,.22f), new Vector2(-1.13f,-.89f), new Vector2(-.34f,-1.18f), new Vector2(.19f,-1.11f) }
        };
        private static readonly GalleryRoute[] Routes = new GalleryRoute[Cells.Length];

        public static RuinRouteFrame Frame(int room,float length,float depth) => Route(room,length).Frame(depth);

        public static float RouteMetresPerDepth(int room,float length) => Route(room,length).Metres / length;

        public static float DepthForMetres(int room,float length,float metres) => metres / RouteMetresPerDepth(room,length);

        private static GalleryRoute Route(int room, float length)
        {
            var route = Routes[room];
            if (route == null || route.LogicalLength != length)
                Routes[room] = route = new GalleryRoute(room,length);
            return route;
        }

        private sealed class GalleryRoute
        {
            private readonly GallerySegment[] segments;
            private readonly Quaternion heading;
            private readonly Vector3 centre;
            private readonly float gradePhase;
            public float LogicalLength { get; }
            public float Metres { get; }

            public GalleryRoute(int room,float length)
            {
                LogicalLength = length;
                int cell = Cells[room];
                centre = new Vector3((cell%3-1)*Cell,Elevations[room]*14.2f,(cell/3-1)*Cell);
                heading = Quaternion.Euler(0,Heading[room],0);
                gradePhase = room * 1.13f;
                Vector2[] plan = Plans[room % Plans.Length];
                int count = plan.Length;
                float scale = Mathf.Clamp(length*.378f,16.3f,18.8f);
                var corners = BuildCorners(plan,scale);

                var parts = new List<GallerySegment>(count*2+1);
                Vector2 seam = Vector2.Lerp(corners[0].End,corners[1].Start,.42f);
                Vector2 lastPosition = seam;
                float distance = 0;
                for (int n = 1; n <= count; n++)
                {
                    GalleryCorner corner = corners[n%count];
                    var straight = GallerySegment.Straight(lastPosition,corner.Start,distance);
                    var bend = GallerySegment.Arc(corner,straight.EndDistance);
                    parts.Add(straight);
                    parts.Add(bend);
                    lastPosition = corner.End;
                    distance = bend.EndDistance;
                }
                var closing = GallerySegment.Straight(lastPosition,seam,distance);
                parts.Add(closing);
                segments = parts.ToArray();
                Metres = closing.EndDistance;
            }

            private static GalleryCorner[] BuildCorners(Vector2[] plan,float scale)
            {
                int count=plan.Length;
                var directions=new Vector2[count];
                var distances=new float[count];
                var turns=new float[count];
                var setbacks=new float[count];
                for(int i=0;i<count;i++)
                {
                    Vector2 edge=(plan[(i+1)%count]-plan[i])*scale;
                    directions[i]=edge.normalized;
                    distances[i]=edge.magnitude;
                }
                for(int i=0;i<count;i++)
                {
                    Vector2 incoming=directions[(i+count-1)%count], outgoing=directions[i];
                    turns[i]=Mathf.Atan2(incoming.x*outgoing.y-incoming.y*outgoing.x,Vector2.Dot(incoming,outgoing));
                    setbacks[i]=10.9f*Mathf.Tan(Mathf.Abs(turns[i])*.5f);
                }
                // Adjacent bends share the available straight. A shallow turn leaves more
                // room for its neighbour so a short edge does not force a pinched corner.
                for(int i=0;i<count;i++)
                {
                    int next=(i+1)%count;
                    float available=Mathf.Min(1,.94f*distances[i]/(setbacks[i]+setbacks[next]));
                    setbacks[i]*=available;
                    setbacks[next]*=available;
                }
                var corners=new GalleryCorner[count];
                for(int i=0;i<count;i++)
                    corners[i]=new GalleryCorner(plan[i]*scale,directions[(i+count-1)%count],directions[i],turns[i],setbacks[i]);
                return corners;
            }

            public RuinRouteFrame Frame(float depth)
            {
                float progress = Mathf.Repeat(depth / LogicalLength,1);
                float distance = progress * Metres;
                int index = 0;
                while (index < segments.Length-1 && distance > segments[index].EndDistance) index++;
                segments[index].Sample(distance,out Vector2 position,out Vector2 forward);

                float angle = progress * (2*Mathf.PI);
                float slopeWave = angle + gradePhase;
                float height = .59f*(Mathf.Sin(slopeWave)-Mathf.Sin(gradePhase)) + .23f*Mathf.Sin(2*angle);
                float grade = (2*Mathf.PI/Metres)*(.59f*Mathf.Cos(slopeWave) + .46f*Mathf.Cos(2*angle));
                return new RuinRouteFrame(centre + heading*new Vector3(position.x,height,position.y),
                    heading*new Vector3(forward.x,grade,forward.y));
            }
        }

        private readonly struct GalleryCorner
        {
            public readonly Vector2 Start, End, Heading;
            public readonly float Turn, Radius;

            public GalleryCorner(Vector2 vertex,Vector2 incoming,Vector2 outgoing,float turn,float setback)
            {
                Heading = incoming;
                Turn = turn;
                Radius = setback/Mathf.Tan(Mathf.Abs(turn)*.5f);
                Start = vertex-Heading*setback;
                End = vertex+outgoing*setback;
            }
        }

        private readonly struct GallerySegment
        {
            private readonly Vector2 start, forward, pivot;
            private readonly float distance, turn;
            public readonly float EndDistance;

            private GallerySegment(Vector2 start,Vector2 forward,Vector2 pivot,float distance,float length,float turn)
            {
                this.start=start;
                this.forward=forward;
                this.pivot=pivot;
                this.distance=distance;
                this.turn=turn;
                EndDistance=distance+length;
            }

            public static GallerySegment Straight(Vector2 start,Vector2 end,float distance)
            {
                Vector2 span=end-start;
                return new GallerySegment(start,span.normalized,default,distance,span.magnitude,0);
            }

            public static GallerySegment Arc(GalleryCorner corner,float distance)
            {
                Vector2 normal=new Vector2(-corner.Heading.y,corner.Heading.x);
                Vector2 pivot=corner.Start+normal*(Mathf.Sign(corner.Turn)*corner.Radius);
                return new GallerySegment(corner.Start,corner.Heading,pivot,distance,Mathf.Abs(corner.Turn)*corner.Radius,corner.Turn);
            }

            public void Sample(float metres,out Vector2 at,out Vector2 heading)
            {
                float along = metres-distance;
                if (turn == 0)
                {
                    at=start+forward*along;
                    heading=forward;
                    return;
                }
                float angle=turn*along/(EndDistance-distance);
                float c=Mathf.Cos(angle), s=Mathf.Sin(angle);
                Vector2 arm=start-pivot;
                at=pivot+new Vector2(arm.x*c-arm.y*s,arm.x*s+arm.y*c);
                heading=new Vector2(forward.x*c-forward.y*s,forward.x*s+forward.y*c);
            }
        }

        public static float Offset(float depth,float origin,float length) => Mathf.Repeat(depth-origin+length*.5f,length)-length*.5f;

        public static float PortalAngle(RuinDirection direction)
        {
            switch(direction)
            {
                case RuinDirection.Left: return Mathf.PI;
                case RuinDirection.Upper: return Mathf.PI*.5f;
                case RuinDirection.Lower: return Mathf.PI*1.5f;
                default: return 0;
            }
        }

        public static Vector3 PortalForward(RuinRouteFrame frame,RuinPortal portal)
        {
            float variant=((portal.FrameStyle%5)-2)*.105f;
            switch(portal.Direction)
            {
                case RuinDirection.Left: return (frame.Forward*.40f-frame.Right+Vector3.up*(variant+.12f)).normalized;
                case RuinDirection.Right: return (frame.Forward*.57f+frame.Right+Vector3.up*(variant-.08f)).normalized;
                case RuinDirection.Upper: return (frame.Forward*.51f+frame.Right*(variant+.14f)+Vector3.up).normalized;
                default: return (frame.Forward*.56f+frame.Right*(variant-.17f)-Vector3.up).normalized;
            }
        }
    }

    internal sealed class RuinTrackConnection
    {
        private readonly Vector3 a, b, c, d;
        private readonly int target;
        private readonly float targetLength;
        private readonly float overpassHeight;
        private const float MouthParameter=.22f, ArrivalParameter=.81f, ArrivalDepth=-3.7f;
        private const int ApproachSamples=72, BoreSamples=48, CurveSamples=168, MergeSamples=96;
        private const int TravelSamples=ApproachSamples+BoreSamples+CurveSamples+MergeSamples;
        private static readonly float[] TravelParameters=BuildTravelParameters();
        private readonly float[] cumulative = new float[TravelSamples+1];
        private RuinRouteFrame preparedStart;
        public readonly Vector3 Entrance, OpeningForward;

        public RuinTrackConnection(int room,float length,RuinPortal portal,Vector2 opening,int targetRoom,float tLength)
        {
            var mouthFrame=RuinTrack.Frame(room,length,portal.Depth);
            Entrance=mouthFrame.Point(opening.x,opening.y);
            OpeningForward=RuinTrack.PortalForward(mouthFrame,portal);
            target=targetRoom;
            targetLength=tLength;
            var arrival=RuinTrack.Frame(target,targetLength,ArrivalDepth);
            a=Entrance+OpeningForward*5f;
            d=arrival.Point(0,.42f);
            float gap=Vector3.Distance(a,d);
            float span=Mathf.Clamp(gap*.26f,9,36);
            overpassHeight=portal.Direction==RuinDirection.Left || portal.Direction==RuinDirection.Right ? 16 : 0;
            b=a+OpeningForward*span;
            c=d-arrival.Forward*Mathf.Clamp(gap*.20f,8,32);
        }

        private static Vector3 Cubic(Vector3 p,Vector3 q,Vector3 r,Vector3 s,float t)
        {
            float inv=1-t;
            return inv*inv*inv*p + 3*inv*inv*t*q + 3*inv*t*t*r + t*t*t*s;
        }

        private Vector3 Along(float t)
        {
            if(t<=.07f) return Vector3.Lerp(Entrance,a,t/.07f);
            if(t<ArrivalParameter)
            {
                float u=(t-.07f)/(ArrivalParameter-.07f);
                // Ease the overpass onto the passage tangent before it climbs over the gallery.
                float lift=overpassHeight*3*u*(1-u)*(1-u)*Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.24f));
                return Cubic(a,b,c,d,u)+Vector3.up*lift;
            }
            float arrival=Mathf.InverseLerp(ArrivalParameter,1,t);
            float height=Mathf.SmoothStep(.42f,.54f+3.30f*Journey.RuinEntryAltitude,arrival);
            return RuinTrack.Frame(target,targetLength,Mathf.Lerp(ArrivalDepth,0,arrival)).Point(0,height);
        }

        public RuinRouteFrame Frame(float t)
        {
            t=Mathf.Clamp01(t);
            Vector3 tangent=t==1 ? RuinTrack.Frame(target,targetLength,0).Forward :
                Along(Mathf.Min(1,t+.0005f))-Along(Mathf.Max(0,t-.0005f));
            return new RuinRouteFrame(Along(t),tangent);
        }

        public float TravelMetres(RuinRouteFrame start)
        {
            PrepareTravel(start);
            return cumulative[TravelSamples];
        }

        public float MouthMetres(RuinRouteFrame start)
        {
            PrepareTravel(start);
            return cumulative[ApproachSamples];
        }

        public float GalleryMetres(RuinRouteFrame start)
        {
            PrepareTravel(start);
            return cumulative[ApproachSamples+BoreSamples+CurveSamples];
        }

        private void PrepareTravel(RuinRouteFrame start)
        {
            if(cumulative[TravelSamples]>0 && preparedStart.Position==start.Position &&
                preparedStart.Forward==start.Forward) return;

            preparedStart=start;
            Vector3 previous=start.Position;
            for(int i=1;i<=TravelSamples;i++)
            {
                Vector3 p=SampleTravel(start,TravelParameters[i]).Position;
                cumulative[i]=cumulative[i-1]+Vector3.Distance(previous,p);
                previous=p;
            }
        }

        private static float[] BuildTravelParameters()
        {
            // Sample the exact joins. Starting a new piece can change its parameter speed
            // even though its physical heading and distance should remain continuous.
            var result=new float[TravelSamples+1];
            int index=0;
            float from=0;
            void Append(int count,float to)
            {
                for(int i=1;i<=count;i++)
                    result[++index]=Mathf.Lerp(from,to,i/(float)count);
                from=to;
            }
            Append(ApproachSamples,MouthParameter);
            Append(BoreSamples,Mathf.Lerp(MouthParameter,1,.07f));
            Append(CurveSamples,Mathf.Lerp(MouthParameter,1,ArrivalParameter));
            Append(MergeSamples,1);
            return result;
        }

        private float ParameterAt(float travelled)
        {
            int boundary=System.Array.BinarySearch(cumulative,travelled);
            if(boundary>=0) return TravelParameters[boundary];
            boundary=Mathf.Clamp(~boundary,1,TravelSamples);
            float fraction=Mathf.InverseLerp(cumulative[boundary-1],cumulative[boundary],travelled);
            return Mathf.Lerp(TravelParameters[boundary-1],TravelParameters[boundary],fraction);
        }

        public RuinRouteFrame Travel(RuinRouteFrame start,float progress,float distanceOffset=0)
        {
            float length=TravelMetres(start);
            float travelled=Mathf.Clamp01(progress)*length+distanceOffset;
            if(travelled<0) return new RuinRouteFrame(start.Position+start.Forward*travelled,start.Forward);
            if(travelled>length)
            {
                var end=Frame(1);
                return new RuinRouteFrame(end.Position+end.Forward*(travelled-length),end.Forward);
            }
            return SampleTravel(start,ParameterAt(travelled));
        }

        public float ArrivalRoadDepth(RuinRouteFrame start,float travelled)
        {
            float arrival=ParameterAt(travelled);
            return Mathf.Lerp(ArrivalDepth,0,Mathf.InverseLerp(Mathf.Lerp(MouthParameter,1,ArrivalParameter),1,arrival));
        }

        private RuinRouteFrame SampleTravel(RuinRouteFrame start,float progress)
        {
            if(progress>=MouthParameter) return Frame(Mathf.InverseLerp(MouthParameter,1,progress));
            float t=Mathf.Clamp01(progress/MouthParameter);
            Vector3 p=start.Position;
            float bend=Vector3.Distance(p,Entrance);
            // Fit the handles to the starting gap so late turns do not overshoot the mouth.
            Vector3 q=p+start.Forward*Mathf.Clamp(bend*.38f,.09f,4.25f);
            Vector3 r=Entrance-OpeningForward*Mathf.Clamp(bend*.29f,.07f,2.05f);
            float v=1-t;
            Vector3 tangent= v*v*(q-p) + 2*v*t*(r-q) + t*t*(Entrance-r);
            return new RuinRouteFrame(Cubic(p,q,r,Entrance,t),tangent);
        }
    }
}
