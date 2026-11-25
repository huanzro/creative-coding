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
        private const float Cell=53;
        private const float Scale=2.5f;
        private static readonly int[] Cells={4,3,5,1,7,0,6,8,2,7,1,3,5,0,6,8};
        private static readonly int[] Elevations={0,0,0,1,0,-1,-2,-1,2,-2,3,-1,1,1,-3,-3};
        private static readonly float[] Heading={0,185,177,69,282,85,257,223,109,188,246,351,65,164,348,169};

        public static RuinRouteFrame Frame(int room,float length,float depth)
        {
            int cell=Cells[room];
            Vector3 centre=new Vector3((cell%3-1)*Cell,Elevations[room]*14.2f,(cell/3-1)*Cell);
            float angle=Mathf.Repeat(depth/length,1)*2*Mathf.PI;
            float radius=length*Scale/(2*Mathf.PI);
            float bend=.12f + .02f*(room%4);
            Quaternion heading=Quaternion.Euler(0,Heading[room],0);
            Vector3 offset=new Vector3(radius*(Mathf.Cos(angle)+bend*Mathf.Cos(2*angle)),0,
                radius*(.94f*Mathf.Sin(angle)+.07f*Mathf.Sin(3*angle)));
            Vector3 tangent=new Vector3(-Mathf.Sin(angle)-2*bend*Mathf.Sin(2*angle),0,
                .94f*Mathf.Cos(angle)+.21f*Mathf.Cos(3*angle));
            return new RuinRouteFrame(centre+heading*offset,heading*tangent);
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
        private readonly Vector3 a, b, c, d, entry, entryTangent;
        private readonly Vector3 branch;
        private readonly int target;
        private readonly float targetLength;
        private const int TravelSamples=160;
        private float[] cumulative;
        public readonly Vector3 Entrance, OpeningForward;

        public RuinTrackConnection(int room,float length,RuinPortal portal,Vector2 opening,int targetRoom,float tLength)
        {
            var mouthFrame=RuinTrack.Frame(room,length,portal.Depth);
            Entrance=mouthFrame.Point(opening.x,opening.y);
            OpeningForward=RuinTrack.PortalForward(mouthFrame,portal);
            branch=OpeningForward;
            target=targetRoom;
            targetLength=tLength;
            var arrival=RuinTrack.Frame(target,targetLength,-3.7f);
            entry=arrival.Point(0,.42f);
            entryTangent=arrival.Forward;
            a=Entrance+branch*5.0f;
            float gap=Vector3.Distance(a,entry);
            float span=Mathf.Clamp(gap*.26f,9,36);
            float bypass=portal.Direction==RuinDirection.Left || portal.Direction==RuinDirection.Right ? 16 : 0;
            b=a+branch*span+Vector3.up*bypass;
            d=entry;
            c=d-entryTangent*Mathf.Clamp(gap*.20f,8,32);
        }

        private static Vector3 Cubic(Vector3 p,Vector3 q,Vector3 r,Vector3 s,float t)
        {
            float inv=1-t;
            return inv*inv*inv*p + 3*inv*inv*t*q + 3*inv*t*t*r + t*t*t*s;
        }

        private Vector3 Along(float t)
        {
            if(t<=.07f) return Vector3.Lerp(Entrance,a,t/.07f);
            if(t<.81f) return Cubic(a,b,c,d,(t-.07f)/.74f);
            float arrival=(t-.81f)/.19f;
            return RuinTrack.Frame(target,targetLength,Mathf.Lerp(-3.7f,0,arrival)).Point(0,Mathf.Lerp(.42f,.54f+3.30f*.072f,arrival));
        }

        public RuinRouteFrame Frame(float t)
        {
            t=Mathf.Clamp01(t);
            Vector3 tangent=t==1 ? RuinTrack.Frame(target,targetLength,0).Forward :
                t>=.81f ? RuinTrack.Frame(target,targetLength,Mathf.Lerp(-3.7f,0,(t-.81f)/.19f)).Forward :
                Along(Mathf.Min(1,t+.001f))-Along(Mathf.Max(0,t-.001f));
            return new RuinRouteFrame(Along(t),tangent);
        }

        public RuinRouteFrame Travel(RuinRouteFrame start,float progress,float distanceOffset=0)
        {
            if(cumulative==null)
            {
                cumulative=new float[TravelSamples+1];
                Vector3 previous=start.Position;
                for(int i=1;i<=TravelSamples;i++)
                {
                    Vector3 p=SampleTravel(start,i/(float)TravelSamples).Position;
                    cumulative[i]=cumulative[i-1]+Vector3.Distance(previous,p);
                    previous=p;
                }
            }
            float easing=Mathf.Lerp(progress,Mathf.SmoothStep(0,1,progress),.72f);
            float length=cumulative[TravelSamples];
            float travelled=easing*length+distanceOffset;
            if(travelled<0) return new RuinRouteFrame(start.Position+start.Forward*travelled,start.Forward);
            if(travelled>length)
            {
                var end=Frame(1);
                return new RuinRouteFrame(end.Position+end.Forward*(travelled-length),end.Forward);
            }
            int iHi=System.Array.BinarySearch(cumulative,travelled);
            if(iHi>=0) return SampleTravel(start,iHi/(float)TravelSamples);
            iHi=Mathf.Clamp(~iHi,1,TravelSamples);
            float fraction=Mathf.InverseLerp(cumulative[iHi-1],cumulative[iHi],travelled);
            return SampleTravel(start,(iHi-1+fraction)/TravelSamples);
        }

        private RuinRouteFrame SampleTravel(RuinRouteFrame start,float progress)
        {
            if(progress>=.22f) return Frame((progress-.22f)/.78f);
            float t=Mathf.Clamp01(progress/.22f);
            Vector3 p=start.Position;
            Vector3 q=p+start.Forward*Mathf.Clamp(Vector3.Distance(p,Entrance)*.35f,.6f,2.4f);
            Vector3 r=Entrance-OpeningForward*1.75f;
            float v=1-t;
            Vector3 tangent= v*v*(q-p) + 2*v*t*(r-q) + t*t*(Entrance-r);
            return new RuinRouteFrame(Cubic(p,q,r,Entrance,t),tangent);
        }
    }
}
