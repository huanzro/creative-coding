using UnityEngine;

namespace Echoes
{
    internal static class ForestRescueStoryboard
    {
        public const float Departure = 2.04f;
        public const float Break = 6.80f;

        internal struct Performance
        {
            public Vector3 fly;
            public float bright, bind, tight, pull, effort, free, balance, returnLook, splinters;
        }

        public static Performance Sample(float time, bool web)
        {
            Vector3 origin = new Vector3(.25f,.29f,-.09f);
            Vector3 first = web ? new Vector3(.68f,.39f,.12f) : new Vector3(.58f,-.29f,.28f);
            Vector3 recoil = web ? new Vector3(.14f,-.02f,-.16f) : new Vector3(-.07f,.04f,-.21f);
            Vector3 far = web ? new Vector3(.86f,.62f,.22f) : new Vector3(.74f,-.39f,.29f);
            Vector3 knot = web ? new Vector3(.55f,.20f,.02f) : new Vector3(.46f,-.40f,.30f);
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

        private static float Ease(float a,float b,float time) => OpeningStoryboard.Ease(a,b,time);
    }
}
