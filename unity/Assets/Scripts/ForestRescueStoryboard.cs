using UnityEngine;

namespace Echoes
{
    internal static class ForestRescueStoryboard
    {
        public const float Departure = 2.04f;
        public const float Break = 6.80f;
        public const float MawFirstContact = 3.72f;
        public const float MawSecondContact = 5.10f;

        private static readonly Vector3 FlyOrigin = new Vector3(.25f,.29f,-.09f);
        private static readonly Vector3 MawLip = new Vector3(.51f,-.08f,-.27f);
        private static readonly Vector3 MawSeam = new Vector3(.42f,-.10f,-.38f);
        private static readonly Vector3 MawWedge = new Vector3(.15f,-.08f,-.43f);

        internal struct Performance
        {
            public Vector3 fly;
            public float bright, bind, tight, pull, effort, free, balance, returnLook, splinters;
            public float upperJaw, lowerJaw, mouthPulse, pry, pop, watch;
        }

        public static Performance Sample(float time, bool web) => web ? SampleWeb(time) : SampleMaw(time);

        private static Performance SampleWeb(float time)
        {
            Vector3 origin = FlyOrigin;
            Vector3 first = new Vector3(.68f,.39f,.12f);
            Vector3 recoil = new Vector3(.14f,-.02f,-.16f);
            Vector3 far = new Vector3(.86f,.62f,.22f);
            Vector3 knot = new Vector3(.55f,.20f,.02f);
            Vector3 fall = knot + new Vector3(.11f,-.45f,.08f);
            Vector3 fly = origin;
            if (time >= Departure)
            {
                fly = Vector3.Lerp(origin, first, Ease(Departure,3.26f,time));
                fly += new Vector3(0,.17f,-.08f)*OpeningStoryboard.Window(time,2.10f,2.81f,2.89f,3.26f);
                fly = Vector3.Lerp(fly,recoil,Ease(3.28f,3.96f,time));
                fly = Vector3.Lerp(fly,far,Ease(4.15f,5.44f,time));
                fly = Vector3.Lerp(fly,knot,Ease(5.49f,6.39f,time));
                fly = Vector3.Lerp(fly,fall,Ease(Break,7.88f,time));
                fly += Vector3.right*(Mathf.Sin(time*26)*.035f*OpeningStoryboard.Window(time,5.55f,5.95f,6.65f,7.29f));
            }

            float bind=Ease(.08f,1.35f,time);
            float strain=OpeningStoryboard.Window(time,1.05f,1.42f,1.56f,2.44f)
                + .73f*OpeningStoryboard.Window(time,3.31f,3.67f,3.84f,4.83f)
                + .61f*OpeningStoryboard.Window(time,5.82f,6.35f,6.76f,7.47f);
            float free=Ease(6.43f,8.46f,time);
            float glow=(.52f + .22f*OpeningStoryboard.Window(time,2.90f,3.17f,3.27f,3.51f)
                + .41f*OpeningStoryboard.Window(time,5.52f,6.26f,6.74f,7.02f))*(1-Ease(6.93f,7.88f,time));
            return new Performance {
                fly=fly, bright=glow*Ease(Departure,2.32f,time), bind=bind,
                tight=bind*(1-free), pull=OpeningStoryboard.Window(time,6.43f,7.18f,7.32f,8.46f),
                effort=Mathf.Clamp01(strain)*(1-Ease(7.4f,8.72f,time)), free=free,
                balance=OpeningStoryboard.Window(time,7.55f,8.26f,8.56f,9.38f),
                returnLook=OpeningStoryboard.Window(time,8.47f,8.85f,8.98f,9.39f),
                splinters=OpeningStoryboard.Window(time,6.76f,6.92f,7.30f,7.96f)
            };
        }

        private static Performance SampleMaw(float time)
        {
            // The light circles the rim, tests the outer petal, and then wedges into the seam.
            Vector3 fly = Arc(FlyOrigin,new Vector3(1.07f,.44f,-.51f),MawLip,Ease(Departure,MawFirstContact,time));
            if (time > MawFirstContact)
            {
                Vector3 recoil = new Vector3(.86f,.10f,-.50f);
                fly = Arc(MawLip,new Vector3(.66f,.06f,-.63f),recoil,Ease(MawFirstContact,4.28f,time));
                fly = Arc(fly,new Vector3(.69f,-.26f,-.66f),MawSeam,Ease(4.28f,MawSecondContact,time));
                fly = Arc(fly,new Vector3(.34f,-.08f,-.58f),MawWedge,Ease(5.35f,6.10f,time));
                float drain = Ease(Break,7.94f,time);
                fly = Arc(fly,MawWedge+new Vector3(.10f,.10f,-.15f),new Vector3(.25f,-.57f,-.51f),drain);
            }

            float first = Impact(MawFirstContact,time);
            float second = Impact(MawSecondContact,time);
            float release = Impact(Break,time);
            float pulse = Mathf.Max(first*.72f,Mathf.Max(second*.86f,release));
            float pry = OpeningStoryboard.Window(time,5.35f,6.06f,6.86f,7.22f);
            float upper = Mathf.Clamp01(.16f*first+.21f*Ease(5.43f,6.16f,time)+.79f*Ease(6.46f,7.13f,time));
            float lower = Mathf.Clamp01(.08f*first+.39f*Ease(5.09f,5.70f,time)+.61f*Ease(6.38f,7.03f,time));
            float bind = Ease(.11f,1.05f,time);
            float free = Ease(Break,7.65f,time);
            float flare = Ease(Departure,2.34f,time)*(1-Ease(6.89f,7.94f,time));
            float effort = Mathf.Clamp01(.72f*OpeningStoryboard.Window(time,.65f,1.28f,1.66f,2.24f)
                + .58f*first+.38f*second+.71f*pry)*(1-free);
            return new Performance {
                fly=fly, bright=flare*Mathf.Clamp01(.55f+.42f*pulse+.13f*pry), bind=bind,
                tight=bind*(1-.45f*upper-.55f*lower), pull=pry, effort=effort, free=free,
                balance=OpeningStoryboard.Window(time,7.19f,7.79f,8.26f,9.17f),
                returnLook=OpeningStoryboard.Window(time,8.22f,8.68f,8.96f,9.38f),
                splinters=OpeningStoryboard.Window(time,6.78f,7.03f,7.49f,8.13f),
                upperJaw=upper, lowerJaw=lower, mouthPulse=pulse, pry=pry,
                pop=OpeningStoryboard.Window(time,Break,7.06f,7.18f,7.73f),
                watch=OpeningStoryboard.Window(time,2.16f,2.64f,6.45f,7.13f)
            };
        }

        public static Vector3 MawContactPosition(float time) => time < 4.50f ? MawLip : time < 5.96f ? MawSeam : MawWedge;

        public static Vector3 MawEscapeOffset(float time)
        {
            float slide = Ease(6.88f,7.53f,time)*(1-Ease(8.38f,9.31f,time));
            float hop = OpeningStoryboard.Window(time,6.92f,7.44f,7.64f,8.46f);
            return new Vector3(-.53f*slide,.42f*hop,-.28f*slide);
        }

        private static Vector3 Arc(Vector3 from,Vector3 control,Vector3 to,float t) =>
            Vector3.Lerp(Vector3.Lerp(from,control,t),Vector3.Lerp(control,to,t),t);

        private static float Impact(float contact,float time) =>
            OpeningStoryboard.Window(time,contact-.065f,contact,contact+.08f,contact+.55f);

        private static float Ease(float a,float b,float time) => OpeningStoryboard.Ease(a,b,time);
    }
}
