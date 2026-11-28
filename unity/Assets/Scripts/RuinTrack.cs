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
        public RuinRouteFrame(Vector3 position,Vector3 forward,Vector3 right)
        {
            Position=position;
            Forward=forward.normalized;
            Right=(right-Forward*Vector3.Dot(right,Forward)).normalized;
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
        private static readonly GalleryRoute[] PreviousRoutes = new GalleryRoute[Cells.Length];

        public static RuinRouteFrame Frame(int room,float length,float depth) => Route(room,length).Frame(depth);

        public static float RouteMetresPerDepth(int room,float length) => Route(room,length).Metres / length;

        public static float DepthForMetres(int room,float length,float metres) => metres / RouteMetresPerDepth(room,length);

        private static GalleryRoute Route(int room, float length)
        {
            var route = Routes[room];
            if (route == null || route.LogicalLength != length)
                Routes[room] = route = new GalleryRoute(room,length,RuinAtlas.GalleryLengthMultiplier);
            return route;
        }

        internal static RuinRouteFrame PreviousFrame(int room, float length, float depth)
        {
            float scale = RuinAtlas.GalleryLengthMultiplier;
            var route = PreviousRoutes[room];
            if (route == null || route.LogicalLength != length / scale)
                PreviousRoutes[room] = route = new GalleryRoute(room, length / scale, 1);
            return route.Frame(depth / scale);
        }

        private sealed class GalleryRoute
        {
            private readonly GallerySegment[] segments;
            private readonly Quaternion heading;
            private readonly Vector3 centre;
            private readonly float gradePhase;
            public float LogicalLength { get; }
            public float Metres { get; }

            public GalleryRoute(int room,float length,float stretch)
            {
                LogicalLength = length;
                int cell = Cells[room];
                centre = new Vector3((cell%3-1)*Cell*stretch,Elevations[room]*14.2f,(cell/3-1)*Cell*stretch);
                heading = Quaternion.Euler(0,Heading[room],0);
                gradePhase = room * 1.13f;
                Vector2[] plan = Plans[room % Plans.Length];
                float roomScale = RuinAtlas.PreviousGalleryScale(room);
                float scale = Mathf.Clamp(length / (roomScale * stretch) * .378f, 16.3f, 18.8f);
                var corners = BuildCorners(plan, scale * stretch, 10.9f * stretch);
                if (roomScale < 1)
                {
                    // Refit short galleries instead of squeezing their inner walls into tight corners.
                    float target = RouteLength(corners) * roomScale;
                    var compact = new Vector2[8];
                    float oval = .89f + .07f * (room % Plans.Length);
                    for (int i = 0; i < compact.Length; i++)
                    {
                        float angle = (i - .5f) * Mathf.PI * .25f;
                        compact[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * oval);
                    }
                    float unit = target / (2 * Mathf.PI);
                    const float cornerShare = .78f;
                    var test = BuildCorners(compact, unit, unit * cornerShare);
                    unit *= target / RouteLength(test);
                    corners = BuildCorners(compact, unit, unit * cornerShare);
                }
                int count = corners.Length;

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

            private static float RouteLength(GalleryCorner[] corners)
            {
                float length = 0;
                for (int i = 0; i < corners.Length; i++)
                {
                    GalleryCorner at = corners[i], next = corners[(i + 1) % corners.Length];
                    length += Vector2.Distance(at.End, next.Start) + next.Radius * Mathf.Abs(next.Turn);
                }
                return length;
            }

            private static GalleryCorner[] BuildCorners(Vector2[] plan,float scale,float radius)
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
                    setbacks[i]=radius*Mathf.Tan(Mathf.Abs(turns[i])*.5f);
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
        private readonly Vector3 boreEnd;
        private readonly QuinticCurve connector;
        private readonly int target;
        private readonly float targetLength;
        private readonly Vector3 connectorDetour;
        private readonly int source;
        private readonly float sourceLength, sourceMouthDepth;
        private readonly Vector3 sourceInward;
        private readonly System.Func<float,Vector2> sourceSection;
        private readonly RuinPortal portal;
        private readonly Vector2 opening;
        private const float MouthParameter=.22f, BoreParameter=.07f, ArrivalParameter=.81f, ArrivalDepth=-3.7f;
        private const int ApproachSamples=96, BoreSamples=48, CurveSamples=168, MergeSamples=96;
        private const int TravelSamples=ApproachSamples+BoreSamples+CurveSamples+MergeSamples;
        private const int SectionSamples=224;
        private const float LandingHeight=.54f+3.30f*Journey.RuinEntryAltitude;
        private static readonly float[] TravelParameters=BuildTravelParameters();
        private readonly float[] cumulative = new float[TravelSamples+1];
        private Vector3[] sectionRight;
        private float landingRoll;
        private float[] speedLimits;
        private RuinRouteFrame preparedStart;
        private QuinticCurve approach;
        private Vector3 approachDetour;
        private float previousNarrowLength;
        public readonly Vector3 Entrance, OpeningForward;

        public RuinTrackConnection(int room,float length,RuinPortal portal,Vector2 opening,int targetRoom,float tLength,
            System.Func<float,Vector2> gallerySection=null)
        {
            var mouthFrame=RuinTrack.Frame(room,length,portal.Depth);
            source=room;
            sourceLength=length;
            this.portal=portal;
            this.opening=opening;
            sourceMouthDepth=portal.Depth;
            sourceInward=-portal.Lane*mouthFrame.Right;
            if(portal.Direction==RuinDirection.Left || portal.Direction==RuinDirection.Right) sourceSection=gallerySection;
            Entrance=mouthFrame.Point(opening.x,opening.y);
            OpeningForward=RuinTrack.PortalForward(mouthFrame,portal);
            target=targetRoom;
            targetLength=tLength;
            (boreEnd, connector, connectorDetour) = FitConnector(Entrance,OpeningForward,
                RuinTrack.Frame(target,targetLength,ArrivalDepth),portal.Direction);
        }

        private static (Vector3, QuinticCurve, Vector3) FitConnector(Vector3 entrance, Vector3 forward,
            RuinRouteFrame arrival, RuinDirection direction)
        {
            Vector3 boreEnd=entrance+forward*5f;
            Vector3 landing=arrival.Point(0,.42f);
            float gap=Vector3.Distance(boreEnd,landing);
            float span=Mathf.Clamp(gap*.26f,9,36)*.60f;
            float runout=Mathf.Clamp(gap*.20f,8,32)*.60f;
            var curve=new QuinticCurve(boreEnd,forward*span,landing,arrival.Forward*runout);
            float lift=direction==RuinDirection.Left || direction==RuinDirection.Right ? 7.1f : 0;
            return (boreEnd, curve, ChooseDetour(curve,boreEnd,landing,forward,arrival.Forward,lift));
        }

        private static Vector3 ChooseDetour(QuinticCurve curve,Vector3 start,Vector3 end,
            Vector3 departing,Vector3 arriving,float lift)
        {
            Vector3 chord=end-start;
            float gap=chord.magnitude;
            Vector3 heading=chord.normalized;
            Vector3 side=Vector3.Cross(Mathf.Abs(heading.y)>.90f ? Vector3.right : Vector3.up,heading).normalized;
            float reversal=Mathf.Max(
                Mathf.InverseLerp(.04f,-.86f,Vector3.Dot(departing,heading)),
                Mathf.InverseLerp(.04f,-.86f,Vector3.Dot(arriving,heading)));
            var best=Vector3.up*lift;
            if(reversal<.08f) return best;
            float swing=Mathf.Clamp(gap*.20f,9,48)*reversal;
            float score=CurveBendScore(curve,best);
            // A near-reversal needs room to bank into its track and then turn back on arrival.
            for(int direction=-1;direction<=1;direction+=2)
            for(int step=0;step<3;step++)
            {
                var candidate=Vector3.up*lift+side*(direction*swing*(.69f+step*.36f));
                float bend=CurveBendScore(curve,candidate);
                if(bend<score) { best=candidate; score=bend; }
            }
            return best;
        }

        private static float CurveBendScore(QuinticCurve curve,Vector3 detour)
        {
            float worst=0;
            Vector3 previous=curve.Tangent(0).normalized;
            for(int i=1;i<=64;i++)
            {
                float t=i/64f, edge=1-2*t, envelope=4*t*(1-t);
                Vector3 velocity=curve.Tangent(t)+detour*(12*envelope*envelope*edge);
                Vector3 acceleration=curve.Acceleration(t)+detour*(24*envelope*(4*edge*edge-envelope));
                Vector3 direction=velocity.normalized;
                // Include the angle between probes so close reversals cannot hide between samples.
                float bend=Mathf.Max(Curvature(velocity,acceleration),1.8f*(1-Vector3.Dot(previous,direction)));
                worst=Mathf.Max(worst,bend);
                previous=direction;
            }
            return worst;
        }

        private static float Curvature(Vector3 tangent,Vector3 acceleration)
        {
            float speed=tangent.magnitude;
            if(speed<.001f) return 100;
            return Vector3.Cross(tangent,acceleration).magnitude/(speed*speed*speed);
        }

        internal static float SmoothJoin(float t)
        {
            t=Mathf.Clamp01(t);
            return t*t*t*(t*(t*6-15)+10);
        }

        public Vector3 CentreAt(float t)
        {
            t=Mathf.Clamp01(t);
            if(t<=BoreParameter) return Vector3.Lerp(Entrance,boreEnd,t/BoreParameter);
            if(t<ArrivalParameter)
            {
                float u=(t-BoreParameter)/(ArrivalParameter-BoreParameter);
                float envelope=4*u*(1-u);
                return connector.Point(u)+connectorDetour*(envelope*envelope*envelope);
            }
            float arrival=Mathf.InverseLerp(ArrivalParameter,1,t);
            float height=Mathf.Lerp(.42f,LandingHeight,SmoothJoin(arrival));
            return RuinTrack.Frame(target,targetLength,Mathf.Lerp(ArrivalDepth,0,arrival)).Point(0,height);
        }

        private Vector3 ForwardAt(float t)
        {
            if(t<=BoreParameter) return OpeningForward;
            if(t<ArrivalParameter)
            {
                float u=(t-BoreParameter)/(ArrivalParameter-BoreParameter);
                float envelope=4*u*(1-u);
                return (connector.Tangent(u)+connectorDetour*(12*envelope*envelope*(1-2*u))).normalized;
            }
            float arrival=Mathf.InverseLerp(ArrivalParameter,1,t);
            var road=RuinTrack.Frame(target,targetLength,Mathf.Lerp(ArrivalDepth,0,arrival));
            float normalizedHeightChange=30*arrival*arrival*(1-arrival)*(1-arrival);
            float slope=(LandingHeight-.42f)*normalizedHeightChange/
                (-ArrivalDepth*RuinTrack.RouteMetresPerDepth(target,targetLength));
            return (road.Forward+Vector3.up*slope).normalized;
        }

        private void PrepareRoll()
        {
            if(sectionRight!=null) return;
            sectionRight=new Vector3[SectionSamples+1];
            Vector3 forward=OpeningForward;
            sectionRight[0]=Vector3.Cross(Vector3.up,forward).normalized;
            for(int i=1;i<=SectionSamples;i++)
            {
                Vector3 next=ForwardAt(i/(float)SectionSamples);
                sectionRight[i]=Quaternion.FromToRotation(forward,next)*sectionRight[i-1];
                forward=next;
            }
            var exit=RuinTrack.Frame(target,targetLength,0);
            landingRoll=Vector3.SignedAngle(sectionRight[SectionSamples],exit.Right,exit.Forward);
        }

        public RuinRouteFrame Frame(float t)
        {
            t=Mathf.Clamp01(t);
            PrepareRoll();
            Vector3 forward=ForwardAt(t);
            float sample=t*SectionSamples;
            int lower=Mathf.FloorToInt(sample);
            var right=Vector3.Lerp(sectionRight[lower],sectionRight[Mathf.Min(lower+1,SectionSamples)],sample-lower);
            // Transport the cross section around the shaft, then bring the horizon back to the gallery.
            float unwind=SmoothJoin(Mathf.InverseLerp(.46f,1,t));
            return new RuinRouteFrame(CentreAt(t),forward,Quaternion.AngleAxis(unwind*landingRoll,forward)*right);
        }

        private readonly struct QuinticCurve
        {
            private readonly Vector3 a,b,c,d,e,f;
            public QuinticCurve(Vector3 start,Vector3 startHandle,Vector3 end,Vector3 endHandle)
            {
                a=start;
                b=start+startHandle;
                c=b+startHandle;
                f=end;
                e=end-endHandle;
                d=e-endHandle;
            }

            public Vector3 Point(float t)
            {
                float u=1-t, u2=u*u, t2=t*t, u3=u2*u, t3=t2*t;
                return a*(u3*u2)+b*(5*u2*u2*t)+c*(10*u3*t2)+d*(10*u2*t3)+e*(5*u*t2*t2)+f*(t3*t2);
            }

            public Vector3 Tangent(float t)
            {
                float u=1-t, u2=u*u, t2=t*t;
                return 5*((b-a)*(u2*u2)+(c-b)*(4*u2*u*t)+(d-c)*(6*u2*t2)+(e-d)*(4*u*t2*t)+(f-e)*(t2*t2));
            }

            public Vector3 Acceleration(float t)
            {
                float u=1-t, u2=u*u, t2=t*t;
                var ab=b-a;
                var bc=c-b;
                var cd=d-c;
                var de=e-d;
                return 20*((bc-ab)*(u2*u)+(cd-bc)*(3*u2*t)+(de-cd)*(3*u*t2)+(f-e-de)*(t2*t));
            }
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

        internal float PreviousNarrowMetres()
        {
            if (previousNarrowLength > 0) return previousNarrowLength;
            var mouth = RuinTrack.PreviousFrame(source, sourceLength, sourceMouthDepth);
            var arrival = RuinTrack.PreviousFrame(target, targetLength, ArrivalDepth * RuinAtlas.GalleryLengthMultiplier);
            Vector3 entrance = mouth.Point(opening.x, opening.y);
            var (bore, curve, detour) = FitConnector(entrance, RuinTrack.PortalForward(mouth, portal), arrival, portal.Direction);
            previousNarrowLength = Vector3.Distance(entrance, bore);
            Vector3 last = bore;
            for (int i = 1; i <= CurveSamples; i++)
            {
                float t = i / (float)CurveSamples;
                float envelope = 4 * t * (1 - t);
                Vector3 next = curve.Point(t) + detour * (envelope * envelope * envelope);
                previousNarrowLength += Vector3.Distance(last, next);
                last = next;
            }
            return previousNarrowLength;
        }

        private float FoldedMetres(RuinRouteFrame start) =>
            Mathf.Max(0, GalleryMetres(start) - MouthMetres(start) - PreviousNarrowMetres() * RuinAtlas.ConnectorLengthMultiplier);

        public float GameplayMetres(RuinRouteFrame start) => TravelMetres(start) - FoldedMetres(start);

        // The gallery network remains laid out in the overview. In play the dark throat
        // joins the two nearby ends of each passage without showing the distant middle.
        public float PassageSeam(RuinRouteFrame start) => (MouthMetres(start) + GalleryMetres(start) - FoldedMetres(start)) * .5f;

        public float RouteDistance(RuinRouteFrame start, float gameplayDistance) =>
            gameplayDistance >= PassageSeam(start) ? gameplayDistance + FoldedMetres(start) : gameplayDistance;

        public float GameplaySpeedLimit(RuinRouteFrame start, float progress)
        {
            float distance = GameplayMetres(start) * progress;
            float throat = Mathf.Abs(distance - PassageSeam(start));
            float speedLimit = SafeTransferSpeed(start, RouteDistance(start, distance) / TravelMetres(start));
            return Mathf.Lerp(Mathf.Min(speedLimit, 4f), speedLimit, Mathf.InverseLerp(.85f, 3f, throat));
        }

        private void PrepareTravel(RuinRouteFrame start)
        {
            if(cumulative[TravelSamples]>0 && preparedStart.Position==start.Position &&
                preparedStart.Forward==start.Forward) return;

            preparedStart=start;
            speedLimits=null;
            approach=FitApproach(start);
            approachDetour=ContainApproach();
            Vector3 previous=start.Position;
            for(int i=1;i<=TravelSamples;i++)
            {
                float sample=TravelParameters[i];
                Vector3 p=sample<=MouthParameter ? ApproachPoint(sample/MouthParameter,approachDetour) :
                    CentreAt(Mathf.InverseLerp(MouthParameter,1,sample));
                cumulative[i]=cumulative[i-1]+Vector3.Distance(previous,p);
                previous=p;
            }
        }

        private QuinticCurve FitApproach(RuinRouteFrame start)
        {
            float gap=Vector3.Distance(start.Position,Entrance);
            QuinticCurve Fit(float lead,float end) => new QuinticCurve(start.Position,
                start.Forward*Mathf.Clamp(gap*lead,.08f,3.0f),Entrance,OpeningForward*Mathf.Clamp(gap*end,.08f,2.3f));

            QuinticCurve best=Fit(.226f,.172f);
            float score=CurveBendScore(best,Vector3.zero);
            if(score<.26f) return best;
            // Favour the widest curvature that still fits the available turn-in distance.
            for(int lead=0;lead<4;lead++)
            for(int exit=0;exit<3;exit++)
            {
                var option=Fit(.132f+lead*.060f,.138f+exit*.045f);
                float bend=CurveBendScore(option,Vector3.zero);
                if(bend<score) { best=option; score=bend; }
            }
            return best;
        }

        private Vector3 ApproachPoint(float t,Vector3 bow)
        {
            float envelope=4*t*(1-t);
            return approach.Point(t)+bow*(envelope*envelope*envelope);
        }

        private Vector3 ApproachTangent(float t)
        {
            float envelope=4*t*(1-t);
            return approach.Tangent(t)+approachDetour*(12*envelope*envelope*(1-2*t));
        }

        private Vector3 ContainApproach()
        {
            if(sourceSection==null) return Vector3.zero;
            const int sections=52;
            var road=new RuinRouteFrame[sections];
            // Cover the adjustable entrance window and the lead curve's tangent handles.
            for(int i=0;i<sections;i++)
                road[i]=RuinTrack.Frame(source,sourceLength,sourceMouthDepth-9.5f+.28f*i);

            const int crownSamples=48;
            var crown=new Vector2[crownSamples+1];
            for(int i=0;i<=crownSamples;i++)
                crown[i]=sourceSection(Mathf.PI*i/crownSamples);

            float RoofAt(float x)
            {
                int high=crownSamples, low=0;
                while(high-low>1)
                {
                    int mid=(low+high)/2;
                    if(crown[mid].x>x) low=mid;
                    else high=mid;
                }
                return Mathf.Lerp(crown[low].y,crown[high].y,Mathf.InverseLerp(crown[low].x,crown[high].x,x));
            }

            float Clearance(Vector3 bow)
            {
                float clear=float.PositiveInfinity;
                for(int i=1;i<36;i++)
                {
                    Vector3 p=ApproachPoint(i/36f,bow);
                    // The mouth's removable envelope already makes room around the finale.
                    if(Vector3.Distance(p,Entrance)<2.70f) continue;
                    int closest=0;
                    float nearest=float.PositiveInfinity;
                    for(int sample=0;sample<road.Length;sample++)
                    {
                        Vector3 error=p-road[sample].Position;
                        float plan=error.x*error.x+error.z*error.z;
                        if(plan<nearest) { nearest=plan; closest=sample; }
                    }
                    Vector3 local=p-road[closest].Position;
                    float x=Vector3.Dot(local,road[closest].Right);
                    float side=Mathf.Min(crown[0].x-x,x-crown[crownSamples].x);
                    clear=Mathf.Min(clear,Mathf.Min(side,RoofAt(x)-local.y));
                }
                return clear;
            }

            const float headroom=.76f;
            Vector3 best=Vector3.zero;
            float clearBase=Clearance(best);
            if(clearBase>=headroom) return best;

            float Cost(Vector3 bow,float clear)
            {
                float intrusion=Mathf.Max(0,headroom-clear);
                return 24*intrusion+.12f*bow.magnitude+CurveBendScore(approach,bow);
            }

            float cost=Cost(best,clearBase);
            // A smooth bow can widen the bend and clear the roof without changing either join tangent.
            for(int inward=0;inward<=4;inward++)
            for(int drop=0;drop<=3;drop++)
            {
                if(inward==0 && drop==0) continue;
                Vector3 bow=sourceInward*(.95f*inward)-Vector3.up*(.66f*drop);
                float candidate=Cost(bow,Clearance(bow));
                if(candidate<cost) { cost=candidate; best=bow; }
            }
            return best;
        }

        public float SafeTransferSpeed(RuinRouteFrame start,float progress)
        {
            float length=TravelMetres(start);
            if(speedLimits==null)
            {
                int count=Mathf.Max(2,Mathf.CeilToInt(length/.30f));
                float interval=length/count;
                speedLimits=new float[count+1];
                for(int i=0;i<=count;i++)
                {
                    float at=i*interval;
                    float previous=Mathf.Max(0,at-interval*.5f), next=Mathf.Min(length,at+interval*.5f);
                    Vector3 behind=TravelForward(ParameterAt(previous)), ahead=TravelForward(ParameterAt(next));
                    float turn=Mathf.Acos(Mathf.Clamp(Vector3.Dot(behind,ahead),-1,1));
                    float curvature=turn/Mathf.Max(.01f,next-previous);
                    speedLimits[i]=Mathf.Clamp(1.85f/Mathf.Max(.01f,curvature),.85f,18f);
                }

                // The speed limit starts braking before a bend, not on its sharpest part.
                float brake=2*3.7f*interval;
                for(int i=count-1;i>=0;i--)
                    speedLimits[i]=Mathf.Min(speedLimits[i],Mathf.Sqrt(speedLimits[i+1]*speedLimits[i+1]+brake));
            }
            float index=Mathf.Clamp01(progress)*(speedLimits.Length-1);
            int a=Mathf.FloorToInt(index);
            return Mathf.Lerp(speedLimits[a],speedLimits[Mathf.Min(a+1,speedLimits.Length-1)],index-a);
        }

        private Vector3 TravelForward(float t) => t<=MouthParameter ? ApproachTangent(t/MouthParameter).normalized :
            ForwardAt(Mathf.InverseLerp(MouthParameter,1,t));

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
            Append(BoreSamples,Mathf.Lerp(MouthParameter,1,BoreParameter));
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
                float depth=RuinTrack.DepthForMetres(target,targetLength,travelled-length);
                var road=RuinTrack.Frame(target,targetLength,depth);
                return new RuinRouteFrame(road.Point(0,LandingHeight),road.Forward,road.Right);
            }
            return SampleTravel(start,ParameterAt(travelled));
        }

        public float ArrivalRoadDepth(RuinRouteFrame start,float travelled)
        {
            float length=TravelMetres(start);
            if(travelled>=length) return RuinTrack.DepthForMetres(target,targetLength,travelled-length);
            float arrival=ParameterAt(Mathf.Max(0,travelled));
            return Mathf.Lerp(ArrivalDepth,0,Mathf.InverseLerp(Mathf.Lerp(MouthParameter,1,ArrivalParameter),1,arrival));
        }

        private RuinRouteFrame SampleTravel(RuinRouteFrame start,float progress)
        {
            if(progress>=MouthParameter) return Frame(Mathf.InverseLerp(MouthParameter,1,progress));
            float t=Mathf.Clamp01(progress/MouthParameter);
            Vector3 forward=ApproachTangent(t).normalized;
            Vector3 right=Vector3.Lerp(start.Right,Vector3.Cross(Vector3.up,OpeningForward).normalized,SmoothJoin(t));
            return new RuinRouteFrame(ApproachPoint(t,approachDetour),forward,right);
        }
    }
}
