using UnityEngine;

namespace Echoes
{
    internal static class WorldCameraPath
    {
        private readonly struct Mark
        {
            public readonly float time, lens;
            public readonly Vector3 eye, aim;

            public Mark(float time, Vector3 eye, Vector3 aim, float lens)
            { this.time=time; this.eye=eye; this.aim=aim; this.lens=lens; }
        }

        private static readonly Mark[] Route = {
            new Mark(0,new Vector3(-2.46f,1.73f,-7.5f),new Vector3(.8f,1.18f,11.5f),49),
            new Mark(2.5f,new Vector3(-1.74f,1.58f,-2.9f),new Vector3(.4f,1.14f,13.0f),47),
            new Mark(4.6f,new Vector3(-1.32f,1.47f,3.0f),new Vector3(.32f,1.29f,13.7f),44),
            new Mark(6.98f,new Vector3(-.72f,1.38f,7.75f),new Vector3(.16f,1.04f,14.5f),43),
            new Mark(7.70f,new Vector3(-.32f,1.31f,9.88f),new Vector3(.21f,-1.42f,15.3f),48),
            new Mark(8.72f,new Vector3(.12f,-2.52f,13.52f),new Vector3(.46f,-9.41f,18.95f),59),
            new Mark(10.50f,new Vector3(.47f,-8.55f,13.94f),new Vector3(1.12f,-12.35f,25.1f),55),
            new Mark(11.88f,new Vector3(-.23f,-11.48f,14.11f),new Vector3(.37f,-8.30f,25.9f),51),
            new Mark(13.62f,new Vector3(-.33f,-9.12f,13.67f),new Vector3(.07f,-.93f,16.33f),52),
            new Mark(15.35f,new Vector3(-.12f,-2.95f,13.70f),new Vector3(.19f,3.30f,15.58f),58),
            new Mark(16.86f,new Vector3(.20f,1.80f,13.75f),new Vector3(.55f,4.01f,21.2f),52),
            new Mark(18.62f,new Vector3(.78f,4.82f,16.90f),new Vector3(1.72f,10.98f,27.67f),48),
            new Mark(20.24f,new Vector3(.37f,12.6f,18.5f),new Vector3(2.23f,24.3f,33.1f),55),
            new Mark(22.45f,new Vector3(-2.53f,29.20f,18.48f),new Vector3(2.47f,34.5f,41),54),
            new Mark(24.56f,new Vector3(-1.89f,32.25f,22.21f),new Vector3(3.39f,34.03f,42.53f),49),
            new Mark(26.88f,new Vector3(-.88f,33.90f,24.70f),new Vector3(3.59f,34.23f,41.50f),44),
            new Mark(27.62f,new Vector3(.06f,32.84f,25.40f),new Vector3(3.2f,22.19f,37.56f),57),
            new Mark(29.52f,new Vector3(.29f,13.90f,23.60f),new Vector3(.95f,4.20f,39.88f),63),
            new Mark(30.78f,new Vector3(-.19f,3.82f,21.55f),new Vector3(.32f,1.02f,33.8f),55),
            new Mark(31.43f,new Vector3(.28f,1.86f,22.39f),new Vector3(.90f,.67f,36.06f),49),
            new Mark(32.13f,new Vector3(-.48f,1.95f,22.72f),new Vector3(.60f,.84f,36.7f),48),
            new Mark(34.62f,new Vector3(-.75f,1.78f,21.78f),new Vector3(-.35f,1.06f,37.75f),48),
            new Mark(37.6f,new Vector3(-1.27f,1.71f,21.98f),new Vector3(-1.09f,.97f,37.18f),49)
        };

        public static void Evaluate(float t, out Vector3 eye, out Vector3 aim, out float lens)
        {
            int i=1;
            while(i<Route.Length-1 && t>Route[i].time) i++;
            Mark a=Route[i-1], b=Route[i], p=Route[Mathf.Max(0,i-2)], q=Route[Mathf.Min(i+1,Route.Length-1)];
            float span=b.time-a.time;
            float u=Mathf.Clamp01((t-a.time)/span);
            eye=Interpolate(a.eye,b.eye,(b.eye-p.eye)*(.63f*span/(b.time-p.time)),
                (q.eye-a.eye)*(.63f*span/(q.time-a.time)),u);
            aim=Interpolate(a.aim,b.aim,(b.aim-p.aim)*(.63f*span/(b.time-p.time)),
                (q.aim-a.aim)*(.63f*span/(q.time-a.time)),u);
            lens=Mathf.SmoothStep(a.lens,b.lens,u);

            float strike=OpeningStoryboard.Window(t,31.37f,31.46f,31.58f,32.09f);
            eye+=new Vector3(Mathf.Sin(t*38),Mathf.Cos(t*45),0)*(.105f*strike);
        }

        private static Vector3 Interpolate(Vector3 a, Vector3 b, Vector3 incoming, Vector3 outgoing, float u)
        {
            float square=u*u,cube=square*u;
            return a*(2*cube-3*square+1)+incoming*(cube-2*square+u)+b*(-2*cube+3*square)+outgoing*(cube-square);
        }
    }
}
