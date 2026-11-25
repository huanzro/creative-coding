using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float MazeStart = 25, MazeLane = Journey.ForkLaneWidth;
        private static readonly Color PollutedBark = new Color(.23f, .29f, .24f);
        private static readonly Color WetConcrete = new Color(.33f, .40f, .41f);
        private static readonly Color Rust = new Color(.46f, .23f, .13f);
        private static readonly Color Effluent = new Color(.34f, .62f, .45f);
        private static readonly Color PaleVein = new Color(.41f, .65f, .63f);
        private static readonly Color OchreMark = new Color(.79f, .61f, .27f);
        private static readonly int CurrentTime = Shader.PropertyToID("_CurrentTime");

        private readonly List<Mesh> mazeMeshes = new List<Mesh>();
        private readonly MeshRenderer[] mazeRootSignals = new MeshRenderer[2];
        private readonly List<MeshRenderer> mazeBeacons = new List<MeshRenderer>();
        private Transform forkGround, mazeArchitecture, mazeEntryGate;
        private MeshFilter forkSoil;
        private ForestMaze mazeLayout, forkSoilLayout;
        private Material mazeWater;

        private const float RoomRadius = ForestMaze.RoomRadius;
        private const float PassageHalf = ForestMaze.PassageHalf;

        private sealed class MazeFabric
        {
            public readonly WorldMesh Concrete, Iron, Bark, Foliage, Mineral, Water, Ground;

            public MazeFabric(List<Mesh> meshes)
            {
                Concrete = new WorldMesh(meshes);
                Iron = new WorldMesh(meshes);
                Bark = new WorldMesh(meshes);
                Foliage = new WorldMesh(meshes);
                Mineral = new WorldMesh(meshes);
                Water = new WorldMesh(meshes);
                Ground = new WorldMesh(meshes);
            }
        }

        public void DrawMaze(Journey journey, bool forest, float time)
        {
            bool seeFork = forest && journey.Distance >= 69f;
            if (seeFork)
            {
                if (forkGround == null) BuildForkLandscape();
                if (forkSoilLayout != journey.ForestForkMaze) BuildForkTerrain(journey.ForestForkMaze);
            }
            if (forkGround != null) forkGround.gameObject.SetActive(seeFork);

            ForestMaze layout = forest && journey.Distance >= 88 && (journey.Branch!=ForestBranch.Right || journey.Distance<142)
                ? journey.ForestForkMaze : null;
            if (layout != null && layout != mazeLayout)
            {
                ClearMazeLayout();
                BuildPollutedMaze(layout);
                mazeLayout = layout;
            }
            bool mazeVisible = layout != null && journey.Distance >= 99;
            if (mazeArchitecture != null) mazeArchitecture.gameObject.SetActive(mazeVisible);
            if (!mazeVisible) return;

            if (mazeEntryGate != null)
                mazeEntryGate.localPosition = Vector3.up * ((1 - journey.ForestMazeGateClosure) * (ForestMaze.WallHeight + .4f));
            if (mazeWater != null) mazeWater.SetFloat(CurrentTime, time);
            for (int i = 0; i < mazeBeacons.Count; i++)
                Light(mazeBeacons[i], PaleVein, .68f + .18f * Mathf.Sin(time * 1.7f + i * 1.39f));
            bool low = false, high = false;
            foreach (ForestFeature feature in journey.ForestFeatures)
            {
                if (feature.Kind != ForestFeatureKind.Plant || !feature.Collected) continue;
                if (feature.Altitude > .4f) high = true;
                else low = true;
            }
            for (int i = 0; i < mazeRootSignals.Length; i++)
            {
                bool planted = i == 0 ? low : high;
                Light(mazeRootSignals[i], planted ? Warm : PaleVein,
                    planted ? .98f + .17f * Mathf.Sin(time * 2.8f + i) : .15f);
            }
        }

        private void ClearMazeLayout()
        {
            if (mazeArchitecture != null)
            {
                mazeArchitecture.gameObject.SetActive(false);
                Object.Destroy(mazeArchitecture.gameObject);
            }
            foreach (Mesh mesh in mazeMeshes) Object.Destroy(mesh);
            mazeMeshes.Clear();
            mazeBeacons.Clear();
            mazeArchitecture = null;
            mazeEntryGate = null;
            mazeLayout = null;
        }

        private void BuildForkTerrain(ForestMaze maze)
        {
            var terrain = Geometry();
            const int columns = 86, rows = 206;
            var points = new Vector3[columns+1,rows+1];
            for (int row=0;row<=rows;row++)
            for (int column=0;column<=columns;column++)
            {
                float x = (column-43)*.86f, z = 138+row*.48f;
                float toLeft = Mathf.Abs(x-Journey.ForkTrackX(ForestBranch.Left,z-MazeStart));
                float shoulder = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.72f,4.65f,toLeft));
                float natural = Ground(x,z);
                float apron = Mathf.Lerp(MazeApronFloor(x,z)-.018f*Mathf.InverseLerp(138,142,z),Mathf.Min(natural,.20f),
                    Mathf.SmoothStep(0,1,Mathf.InverseLerp(149,151.4f,z)));
                float ground = RecessedMazeGround(maze,x,z,Mathf.Lerp(natural,apron,shoulder));
                points[column,row] = new Vector3(x,ground,z);
            }
            terrain.Surface(columns,rows,(column,row)=>points[column,row], p =>
            {
                float depth = p.z-MazeStart;
                float left = Mathf.Abs(p.x-Journey.ForkTrackX(ForestBranch.Left,depth));
                float right = Mathf.Abs(p.x-Journey.ForkTrackX(ForestBranch.Right,depth));
                float track = Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.75f,4.26f,Mathf.Min(left,right)));
                float original = ForestTrackMoss(p.x,p.z);
                float shoulder = Mathf.SmoothStep(0,1,Mathf.InverseLerp(138,141.1f,p.z));
                Color clay = new Color(.27f, .232f, .164f);
                Color routeTint = left<right ? new Color(.26f,.32f,.271f) : new Color(.33f,.298f,.224f);
                Color moss = new Color(.177f, .246f, .168f);
                Color bed = Color.Lerp(Color.Lerp(clay,moss,original),Color.Lerp(routeTint,moss,track),shoulder);
                return WorldMesh.Shade(bed,.66f+.55f*Mathf.PerlinNoise(p.x*.64f+9,p.z*.54f+9));
            }, p => p.z >= 204 && p.x > 5 && p.x < 17);
            const string name = "Continuous ground under both roads";
            if (forkSoil == null) forkSoil = MazePiece(name,terrain,soil,forkGround).GetComponent<MeshFilter>();
            else
            {
                Mesh previous = forkSoil.sharedMesh;
                forkSoil.sharedMesh = terrain.Finish(name);
                meshes.Remove(previous);
                Object.Destroy(previous);
            }
            forkSoilLayout = maze;
        }

        private static float RecessedMazeGround(ForestMaze maze, float x, float z, float ground)
        {
            // The apron remains intact even on layouts with a lower-floor room farther west
            // than the entrance. Clear the walkways and the terrain grid triangles at their edges.
            if (x>-7) return ground;
            float fromApron = Mathf.Abs(x-Journey.ForkTrackX(ForestBranch.Left,z-MazeStart));
            float clearance = z>=149 ? 1 : Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.42f,4.65f,fromApron));
            if (clearance<=0) return ground;
            float original = ground;
            foreach (ForestMazeNode room in maze.Nodes)
            {
                float floor = room.Y-.22f;
                if (floor>=ground) continue;
                float d = Vector2.Distance(new Vector2(x,z),new Vector2(room.X,room.Z));
                ground = Mathf.Lerp(floor,ground,Mathf.SmoothStep(0,1,Mathf.InverseLerp(RoomRadius+1,RoomRadius+2.55f,d)));
            }
            foreach (ForestMazeEdge edge in maze.Edges)
            {
                ForestMazeNode a = maze.Nodes[edge.A], b = maze.Nodes[edge.B];
                if (Mathf.Min(a.Y,b.Y)-.22f>=ground) continue;
                Vector2 span = new Vector2(b.X-a.X,b.Z-a.Z);
                Vector2 offset = new Vector2(x-a.X,z-a.Z);
                float t = Mathf.Clamp01(Vector2.Dot(offset,span)/span.sqrMagnitude);
                float floor = Mathf.Lerp(a.Y,b.Y,t)-.22f;
                if (floor>=ground) continue;
                float d = (offset-span*t).magnitude;
                ground = Mathf.Lerp(floor,ground,Mathf.SmoothStep(0,1,Mathf.InverseLerp(PassageHalf+1,PassageHalf+2.35f,d)));
            }
            return Mathf.Lerp(original,ground,clearance);
        }

        private void BuildForkLandscape()
        {
            forkGround = Group("Single forest Y junction and both approaches", root.transform);

            var roots = Geometry();
            var foliage = Geometry();
            var rocks = Geometry();
            var rightMetal = Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                ForestBranch branch = side<0 ? ForestBranch.Left : ForestBranch.Right;
                int shoulders = side<0 ? 5 : 17;
                for (int i=0; i<shoulders; i++)
                {
                    float depth = 113.5f+i*2.57f;
                    float offset = side*(6.45f+.95f*Mathf.Sin(i*2.43f+side));
                    if (i%3==0) Tree(roots,foliage,ForkShoulder(branch,depth,offset,-.08f),10+i%4,.73f+i*.019f);
                    Fern(foliage,ForkShoulder(branch,depth+.42f,offset-side*1.01f,0),.66f+i%4*.16f,i*.68f);
                    if (i%2==0) rocks.Rock(ForkShoulder(branch,depth-.57f,offset-side*1.1f,.03f),
                        new Vector3(.53f,.32f,.59f),PollutedBark,i*.67f,4,8,.18f);
                }
            }

            Vector3 dividingTree = Landscape(-.32f,146.7f,-.08f);
            Tree(roots,foliage,dividingTree,11.1f,1.27f);
            Roots(roots,dividingTree,Landscape(-.52f,142.6f,-.07f),.51f,.47f,0);
            Roots(roots,dividingTree,Landscape(2.62f,149.8f,-.06f),.78f,.71f,2.4f);
            Roots(roots,dividingTree,Landscape(-3.05f,150.6f,-.12f),.68f,.83f,3.5f);
            for (int i=0; i<16; i++)
            {
                float z = 141.8f+i*.61f;
                float width = Mathf.SmoothStep(.36f,3.3f,Mathf.InverseLerp(141.8f,150,z));
                float x = width*Mathf.Sin(i*3.63f);
                Fern(foliage,Landscape(x,z,-.025f),.47f+.29f*i/16f,i*1.68f);
                if (i%3==0) rocks.Rock(Landscape(x+.13f,z,.14f),new Vector3(.46f,.35f,.61f),PollutedBark,i*.79f,4,8,.20f);
            }

            for (int i=0; i<15; i++)
            {
                float depth = 117.8f+i*1.15f;
                for (int side=-1; side<=1; side+=2)
                {
                    float offset = side*Mathf.Lerp(2.65f,3.45f,i/14f);
                    Vector3 a = ForkShoulder(ForestBranch.Right,depth,offset,.038f);
                    if (i%3==0)
                        rightMetal.Box(a+Vector3.up*.09f,new Vector3(.45f,.17f,.62f),Quaternion.Euler(0,side*(7+i),0),
                            Color.Lerp(PollutedBark,Rust,.33f+i*.039f));
                    else if (i>3)
                        rightMetal.Box(a,new Vector3(.16f,.045f,.80f),Quaternion.identity,WorldMesh.Shade(WetConcrete,.63f+i*.012f));
                }
            }
            MazePiece("Root island and continuous outer banks",roots,wood,forkGround);
            MazePiece("Ferns defining both road saddles",foliage,leaves,forkGround);
            MazePiece("Fieldstones shaped around the junction",rocks,stone,forkGround);
            MazePiece("First traces of the machine under the right road",rightMetal,stone,forkGround);
            BuildMazeApron();
        }

        private static Vector3 ForkShoulder(ForestBranch branch, float depth, float offset, float rise)
        {
            float x = Journey.ForkTrackX(branch,depth), z = MazeStart+depth;
            float grade = (Journey.ForkTrackX(branch,depth+.26f)-Journey.ForkTrackX(branch,depth-.26f))/.52f;
            float reach = offset/Mathf.Sqrt(1+grade*grade);
            x += reach;
            z -= grade*reach;
            float lower = branch==ForestBranch.Left ? MazeApronFloor(x,z) : Ground(x,z);
            return new Vector3(x,lower+rise,z);
        }

        internal static float MazeApronFloor(float x, float z)
        {
            float intoChannel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(138.4f, 149, z));
            return Mathf.Lerp(Ground(x, z), .45f, intoChannel);
        }

        private void BuildMazeApron()
        {
            var bank = Geometry();
            var roots = Geometry();
            var greenery = Geometry();
            var water = Geometry();
            var hardware = Geometry();
            Vector3 At(float z, float offset, float rise)
                => ForkShoulder(ForestBranch.Left,z-MazeStart,offset,rise);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    float z = 138.7f + i * 1.22f;
                    float progress = Mathf.InverseLerp(138.7f,148.5f,z);
                    float offset = side * Mathf.Lerp(3.05f,2.35f,progress);
                    Vector3 here = At(z, offset, 0);
                    float height = Mathf.Lerp(.24f,1.30f,progress);
                    bank.Rock(here + Vector3.up * height * .29f,
                        new Vector3(.58f,height, .79f), Color.Lerp(PollutedBark,WetConcrete,progress), i+side*.64f,4,8,.18f);
                    Fern(greenery, At(z-.21f,offset+side*.58f,-.06f),Mathf.Lerp(1.18f,.48f,progress),side*i*1.8f);
                    if (i % 2 == 0)
                        Roots(roots,At(z-1.18f,offset+side*.64f,.46f+progress),here+Vector3.up*.055f,
                            .30f+.18f*progress,.36f,i+side);
                    if (i < 2) continue;
                    Vector3 a = At(z-.8f,offset-side*.27f,.031f), b=At(z+.43f,offset-side*.27f,.031f);
                    MazeFloor(water,a,b,Vector3.right,.26f,Color.Lerp(new Color(.10f,.22f,.178f), new Color(.23f,.46f,.339f),progress));
                }
            }

            for (int i=0; i<3; i++)
            {
                float z = 144.4f+i*1.65f;
                float crown = 6.35f-i*.46f;
                Vector3 a=At(z+.1f,-3.08f,.10f), b=At(z+.22f,3.08f,.16f);
                Vector3 high = At(z+.65f,-.45f,crown);
                roots.Tube(new[] { a,At(z-.4f,-2.75f,3.4f),high,At(z+.1f,2.52f,4.3f),b },
                    new[] { .76f,.64f,.39f,.45f,.57f },11,PollutedBark);
            }
            Vector3 pipe = At(145.7f,-3.10f,1.37f);
            SpillPipe(hardware,hardware, pipe+new Vector3(-1.4f,1.2f,2.22f),pipe,.34f);
            MazePiece("Flagstones on the drainage shoulders",bank,stone,forkGround);
            MazePiece("Tangled canopy over the approach",roots,wood,forkGround);
            MazePiece("Thinning fern bank",greenery,leaves,forkGround);
            MazePiece("Runoff gathering at the path edges",water,glow,forkGround,.14f);
            MazePiece("Exposed runoff pipe",hardware,stone,forkGround);
        }

        private void BuildPollutedMaze(ForestMaze maze)
        {
            mazeArchitecture = Group("Generated drainage levels and root archive", root.transform);
            if (mazeWater == null)
            {
                Shader shader = Resources.Load<Shader>("ForestRunoff");
                if (shader != null && shader.isSupported) mazeWater = new Material(shader) { name = "Maze runoff and surface sheen" };
            }
            var fabric = new MazeFabric(mazeMeshes);
            var ports = new List<MazePort>[maze.Nodes.Count];
            for (int i = 0; i < ports.Length; i++) ports[i] = new List<MazePort>(4);
            foreach (ForestMazeEdge edge in maze.Edges)
            {
                ForestMazeNode from = maze.Nodes[edge.A], to = maze.Nodes[edge.B];
                Vector3 a = MazeNodePosition(from), b = MazeNodePosition(to);
                Vector3 heading = MazeHeading(a,b);
                float reach = Vector3.Dot(b-a,heading);
                float grade = (b.y-a.y)/reach;
                ports[edge.A].Add(new MazePort(heading,grade,reach,edge));
                ports[edge.B].Add(new MazePort(-heading,-grade,reach,edge));
                BuildMazePassage(fabric,maze,edge,a,b,from.Level,to.Level,edge.A*16.3f+edge.B*5.3f);
            }
            // The expedition forks on natural ground before entering the built maze.
            ports[maze.Entry].Add(new MazePort(Vector3.back,0,0,null));
            foreach (ForestMazeNode node in maze.Nodes)
                BuildMazeRoom(fabric,maze,node,ports[node.Id]);
            BuildMazeEntryGate(MazeNodePosition(maze.Nodes[maze.Entry]));

            ForestMazeNode end = maze.Nodes[maze.RootChamber];
            BuildMazeBasins(fabric,MazeNodePosition(end));
            BuildMazeBackdrop(fabric,maze);

            MazePiece("Ground beyond the drainage works", fabric.Ground, soil, mazeArchitecture);
            MazePiece("Layered concrete rooms and connected walkways", fabric.Concrete, stone, mazeArchitecture);
            MazePiece("Inspection rails, grate joints and corroded pipes", fabric.Iron, stone, mazeArchitecture);
            MazePiece("Roots binding the three levels", fabric.Bark, wood, mazeArchitecture);
            MazePiece("Dead canopy over the flooded works", fabric.Foliage, leaves, mazeArchitecture);
            MazePiece("Mineral growths and eroded deposits", fabric.Mineral, glow, mazeArchitecture, .20f);
            if (mazeWater != null) MazePiece("Runoff films below the inspection routes", fabric.Water, mazeWater, mazeArchitecture);
        }

        private readonly struct MazePort
        {
            public readonly Vector3 Heading;
            public readonly float Grade;
            public readonly float Length;
            public readonly ForestMazeEdge Edge;

            public MazePort(Vector3 heading,float grade,float length,ForestMazeEdge edge)
            {
                Heading = heading;
                Grade = grade;
                Length = length;
                Edge = edge;
            }
        }

        private static Vector3 MazeNodePosition(ForestMazeNode node) => new Vector3(node.X,node.Y,node.Z);

        private static Vector3 MazeHeading(Vector3 from, Vector3 to)
        {
            Vector3 heading = to-from;
            heading.y = 0;
            return heading.sqrMagnitude < .001f ? Vector3.forward : heading.normalized;
        }

        private void BuildMazePassage(MazeFabric fabric,ForestMaze maze,ForestMazeEdge passage,Vector3 start,Vector3 finish,
            int firstLevel,int lastLevel,float seed)
        {
            Vector3 heading = MazeHeading(start,finish);
            Vector3 cross = Vector3.Cross(Vector3.up,heading);
            Quaternion orientation = Quaternion.LookRotation(heading);
            bool rising = firstLevel != lastLevel;
            Vector3 a = start + heading * (RoomRadius-.64f), b = finish - heading * (RoomRadius-.64f);
            Vector3 span = b-a;
            float run = Vector3.Dot(span,heading);
            if (run < .15f) return;
            // Keep the deck height exactly on the movement graph's centreline.
            a.y = .014f + Mathf.Lerp(start.y,finish.y,Vector3.Dot(a-start,heading)/Vector3.Dot(finish-start,heading));
            b.y = .014f + Mathf.Lerp(start.y,finish.y,Vector3.Dot(b-start,heading)/Vector3.Dot(finish-start,heading));
            int sections = Mathf.Max(1,Mathf.CeilToInt(run/1.35f));
            int level = Mathf.Min(firstLevel,lastLevel);
            Color deck = level == 0 ? new Color(.265f,.303f,.235f) : level == 1
                ? new Color(.304f,.325f,.327f) : new Color(.373f,.336f,.229f);

            for (int i = 0; i < sections; i++)
            {
                Vector3 p = Vector3.Lerp(a,b,i/(float)sections), q = Vector3.Lerp(a,b,(i+1)/(float)sections);
                Vector3 center = (p+q)*.5f;
                Color stoneTint = WorldMesh.Shade(deck,.85f+.24f*Mathf.PerlinNoise(seed*.1f,i*.38f));
                MazeDeck(fabric.Concrete,p,q,cross,PassageHalf,.27f,stoneTint);

                if (level == 0)
                {
                    Vector3 gutter = cross * (i % 13 < 7 ? -1.57f : 1.57f);
                    MazeFloor(fabric.Water,p+gutter+Vector3.up*.017f,q+gutter+Vector3.up*.017f,
                        cross,.62f, new Color(.113f,.21f,.168f,.78f));
                }
                if (level == 1 || rising)
                {
                    fabric.Iron.Box(center-Vector3.up*.005f,new Vector3(4.43f,.046f,.074f),
                        orientation,WorldMesh.Shade(Rust,.62f+.18f*Mathf.Sin(i*1.71f+seed)));
                    if (i%3 == 0 && rising)
                        fabric.Iron.Box(center+Vector3.up*.043f,new Vector3(4.10f,.035f,.38f),orientation,
                            WorldMesh.Shade(OchreMark,.62f+.11f*Mathf.Cos(i)));
                }

                for (int sign=-1; sign<=1; sign+=2)
                {
                    Vector3 outward = cross*sign;
                    Vector3 offset = cross*PassageHalf*sign;
                    float wall = MazeWallHeight(level,rising,i,sections,sign);
                    float wallRoom = Mathf.Max(wall,level == 0 ? .82f : 1.18f);
                    if (!MazeObstructionClear(maze,passage,p+offset,.43f,wallRoom) ||
                        !MazeObstructionClear(maze,passage,center+offset,.43f,wallRoom) ||
                        !MazeObstructionClear(maze,passage,q+offset,.43f,wallRoom)) continue;
                    MazeRetainer(fabric.Concrete,p+offset,q+offset,outward,
                        wall, WorldMesh.Shade(WetConcrete,(level == 0 ? .68f : .80f)+.16f*Mathf.Sin(i*2.17f+seed+sign)));
                    if (level == 0 && !rising)
                    {
                        Vector3 vein = center+offset-outward*.056f+Vector3.up*.57f;
                        fabric.Concrete.Box(vein,new Vector3(.10f,.19f,.88f),orientation,
                            WorldMesh.Shade(Effluent,.44f+.17f*Mathf.Sin(i*.31f+seed)));
                    }
                    if (i%4 == 1)
                    {
                        Vector3 foot = center+offset;
                        fabric.Iron.Box(foot+Vector3.up*.58f,new Vector3(.16f,1.35f,.17f),
                            orientation*Quaternion.Euler(0,0,sign*3.0f),Rust);
                        if (level == 0 && wall > 1.5f && MazeObstructionClear(maze,passage,foot,.78f,wall+.98f))
                            Roots(fabric.Bark,foot+cross*.09f*sign,foot+heading*1.21f+Vector3.up*(wall+.76f),
                                .32f,.33f,seed+i+sign);
                        if (i%8 == 1 && !rising)
                            DepositedReeds(fabric.Mineral,foot-cross*sign*.18f,seed+i);
                    }
                    if (level>0 || i%6<4)
                    {
                        Vector3 railOffset = offset-outward*.12f+Vector3.up*(level == 0 ? .72f : 1.09f);
                        Vector3 pRail = p+railOffset, qRail = q+railOffset;
                        fabric.Iron.Tube(new[] {pRail,qRail},new[] {.044f,.044f},7,Rust);
                    }
                }
                if (i%8==3)
                {
                    // Supports under the deck make crossing another floor read as a real overlap.
                    for (int sign=-1; sign<=1; sign+=2)
                    {
                        Vector3 support = center+cross*sign*2.33f;
                        float height = center.y+.62f;
                        if (!MazeObstructionClear(maze,passage,support-Vector3.up*height,.42f,height)) continue;
                        fabric.Concrete.Box(support-Vector3.up*(height*.5f),new Vector3(.42f,height,.58f),
                            orientation,WorldMesh.Shade(WetConcrete,.69f));
                    }
                }
            }
            if (run>=8 && !rising)
            {
                Vector3 landmark = Vector3.Lerp(a,b,.53f);
                int side = Mathf.Sin(seed) < 0 ? -1 : 1;
                if (level == 0)
                {
                    Vector3 rim = landmark+cross*PassageHalf*side+Vector3.up*.76f;
                    Vector3 neck = rim+cross*side*2.40f+Vector3.up*1.72f;
                    if (MazeObstructionClear(maze,passage,rim,.40f,.38f) &&
                        MazeObstructionClear(maze,passage,(rim+neck)*.5f,.46f,.52f) &&
                        MazeObstructionClear(maze,passage,neck,.46f,.40f))
                    {
                        SpillPipe(fabric.Iron,fabric.Mineral,neck,rim,.32f);
                        mazeBeacons.Add(MazeLamp(mazeArchitecture,rim-Vector3.up*.56f,.095f));
                    }
                }
                else if (level == 2)
                {
                    Vector3 foot = landmark+cross*(PassageHalf+1.1f)*side-Vector3.up*.38f;
                    if (MazeObstructionClear(maze,passage,foot,1.05f,4.5f) &&
                        MazeObstructionClear(maze,passage,foot+heading*2,.91f,4.5f))
                        Roots(fabric.Bark,foot,foot+Vector3.up*4.4f+heading*3.8f,.52f,.48f,seed);
                }
            }
            if (run>=11 && level==0 && !rising)
            {
                Vector3 center=Vector3.Lerp(a,b,.71f);
                Vector3 from=center-cross*2.7f, to=center+cross*2.7f;
                if (MazeObstructionClear(maze,passage,from,.78f,5.1f) && MazeObstructionClear(maze,passage,to,.80f,5.1f))
                {
                    Roots(fabric.Bark,from-Vector3.up*.20f,from+Vector3.up*4.95f,.34f,.35f,seed);
                    Roots(fabric.Bark,to+Vector3.up*.40f,to+Vector3.up*4.95f,.40f,.34f,seed+2);
                    SpillPipe(fabric.Iron,fabric.Mineral,from+Vector3.up*4.8f,to+Vector3.up*4.7f,.22f);
                }
            }
        }

        private static bool MazeObstructionClear(ForestMaze maze, ForestMazeEdge owner, Vector3 anchor, float padding, float height)
        {
            float half = PassageHalf+padding;
            foreach (ForestMazeEdge other in maze.Edges)
            {
                if (other == owner) continue;
                ForestMazeNode a = maze.Nodes[other.A], b = maze.Nodes[other.B];
                if (Mathf.Max(a.Y,b.Y)+4.55f<anchor.y || Mathf.Min(a.Y,b.Y)>anchor.y+height+.16f) continue;
                Vector2 axis = new Vector2(b.X-a.X,b.Z-a.Z);
                Vector2 offset = new Vector2(anchor.x-a.X,anchor.z-a.Z);
                float t = Mathf.Clamp01(Vector2.Dot(offset,axis)/axis.sqrMagnitude);
                if ((offset-axis*t).sqrMagnitude>=half*half) continue;
                float floor = Mathf.Lerp(a.Y,b.Y,t);
                if (anchor.y<=floor+4.55f && anchor.y+height>=floor-.16f) return false;
            }
            return true;
        }

        private static float MazeWallHeight(int level, bool rising, int index, int sections, int side)
        {
            if (rising) return .57f;
            if (level == 0) return 2.31f + .55f*Mathf.Sin(index*.27f+side*1.39f);
            if (level == 1) return index==0 || index==sections-1 ? 2.08f : .90f+.27f*Mathf.Sin(index*.47f+side);
            return .67f+.22f*Mathf.Sin(index*.39f+side*2.39f);
        }

        private static void MazeFloor(WorldMesh mesh, Vector3 start, Vector3 finish, Vector3 side, float width, Color color)
        {
            Vector3 edge = side*width;
            mesh.Quad(start-edge,finish-edge,finish+edge,start+edge,color);
        }

        private static void MazeDeck(WorldMesh mesh, Vector3 start, Vector3 finish, Vector3 side,float width,float depth,Color color)
        {
            MazeFloor(mesh,start,finish,side,width,color);
            Vector3 offset = side*width, down = Vector3.down*depth;
            Color underside = WorldMesh.Shade(color,.65f);
            mesh.Quad(start-offset+down,finish-offset+down,finish-offset,start-offset,underside);
            mesh.Quad(finish+offset+down,start+offset+down,start+offset,finish+offset,underside);
            mesh.Quad(start-offset+down,start+offset+down,finish+offset+down,finish-offset+down,underside);
        }

        private static void MazeRetainer(WorldMesh mesh,Vector3 start,Vector3 finish,Vector3 outward,float height,Color color)
        {
            Vector3 up = Vector3.up*height;
            Vector3 outside=outward*.28f;
            bool front=Vector3.Dot(Vector3.Cross(up,finish-start),outward)<0;
            if (front)
            {
                mesh.Quad(start,start+up,finish+up,finish,color);
                mesh.Quad(finish+outside,finish+up+outside,start+up+outside,start+outside,WorldMesh.Shade(color,.82f));
            }
            else
            {
                mesh.Quad(finish,finish+up,start+up,start,color);
                mesh.Quad(start+outside,start+up+outside,finish+up+outside,finish+outside,WorldMesh.Shade(color,.82f));
            }
            if (Vector3.Dot(Vector3.Cross(outside,finish-start),Vector3.up)>0)
                mesh.Quad(start+up,start+up+outside,finish+up+outside,finish+up,WorldMesh.Shade(color,1.14f));
            else mesh.Quad(finish+up,finish+up+outside,start+up+outside,start+up,WorldMesh.Shade(color,1.14f));
        }

        private void BuildMazeRoom(MazeFabric fabric, ForestMaze maze, ForestMazeNode node, List<MazePort> ports)
        {
            Vector3 center = MazeNodePosition(node);
            bool terminal = node.Kind == ForestMazeNodeKind.DeadEnd;
            bool basin = node.Kind == ForestMazeNodeKind.RootChamber;
            float radius = basin ? RoomRadius+.42f : RoomRadius;
            const int sides = 32;
            float height = basin || terminal ? 3.25f : node.Level==0 ? 2.72f : 1.76f;
            for (int i=0; i<sides; i++)
            {
                Vector3 d0 = MazeRadial(i,sides), d1 = MazeRadial(i+1,sides);
                Vector3 p = MazeRoomPoint(center,d0*radius,ports), q = MazeRoomPoint(center,d1*radius,ports);
                Color panel = WorldMesh.Shade(WetConcrete,.78f+.17f*Mathf.PerlinNoise(node.Id*.32f,i*.25f));
                // The last chamber uses soil so the closing shot can open a real shaft.
                (basin ? fabric.Ground : fabric.Concrete).Triangle(center,p,q,panel);
                fabric.Concrete.Quad(p,p-Vector3.up*.24f,q-Vector3.up*.24f,q,WorldMesh.Shade(panel,.73f));

                Vector3 direction = (d0+d1).normalized;
                if (PortOpen(direction,radius,ports) || !MazeObstructionClear(maze,null,(p+q)*.5f,.22f,height)) continue;
                MazeRetainer(fabric.Concrete,p,q,direction,
                    height+.18f*Mathf.Sin(i*1.19f+node.Id), WorldMesh.Shade(panel,.84f));
                if (i%7==0)
                {
                    Vector3 foot=(p+q)*.5f+direction*.12f;
                    Roots(fabric.Bark,foot,foot+Vector3.up*(height+.60f)+MazeRadial(i+2,sides)*.66f,
                        .38f,.28f,node.Id*.63f+i);
                    if (i%14==0) DepositedReeds(fabric.Mineral,foot-direction*.47f,node.Id*.43f);
                }
            }

            for (int i=0;i<ports.Count;i++)
            {
                MazePort port = ports[i];
                Vector3 heading = port.Heading, tangent = Vector3.Cross(Vector3.up,heading);
                Quaternion rotation = Quaternion.LookRotation(heading);
                float inset = radius-.57f;
                Vector3 lip = center+heading*inset+Vector3.up*(port.Grade*inset);
                Vector3 jamb = tangent*(PassageHalf+.04f);
                if (node.Kind==ForestMazeNodeKind.Junction)
                {
                    for (float next=inset; next<Mathf.Min(port.Length-3.40f,inset+8);next+=.62f)
                    {
                        Vector3 candidate = center+heading*next+Vector3.up*(port.Grade*next);
                        if (!MazeObstructionClear(maze,port.Edge,candidate-jamb,.22f,2.56f) ||
                            !MazeObstructionClear(maze,port.Edge,candidate+jamb,.22f,2.56f)) continue;
                        lip = candidate;
                        break;
                    }
                }
                for (int side=-1;side<=1;side+=2)
                {
                    Vector3 post = lip+jamb*side;
                    if (!MazeObstructionClear(maze,port.Edge,post,.22f,2.56f)) continue;
                    fabric.Iron.Box(post+Vector3.up*1.28f,new Vector3(.24f,2.56f,.32f),rotation,
                        WorldMesh.Shade(Rust,.89f + node.Level*.12f));
                    if (i==0 || node.Kind==ForestMazeNodeKind.Junction)
                        mazeBeacons.Add(MazeLamp(mazeArchitecture,post+Vector3.up*1.87f-heading*.09f,.114f));
                }
                if (node.Kind==ForestMazeNodeKind.Entry)
                {
                    Vector3 left=lip-tangent*(PassageHalf+.16f), right=lip+tangent*(PassageHalf+.16f);
                    fabric.Bark.Tube(new[] {left,left+Vector3.up*3.84f,left+tangent*.47f+Vector3.up*5.27f,
                            right-tangent*.56f+Vector3.up*5.41f,right+Vector3.up*4.12f,right},
                        new[] {.35f,.28f,.23f,.24f,.26f,.32f},9,PollutedBark);
                }
                else if (basin)
                {
                    Vector3 left=lip-tangent*(PassageHalf+.03f), right=lip+tangent*(PassageHalf+.03f);
                    fabric.Bark.Tube(new[] {left,right-tangent*.80f+Vector3.up*4.55f,right+Vector3.up*.94f},
                        new[] {.24f,.26f,.28f},9,PollutedBark);
                }
            }

            if (terminal && ports.Count>0)
            {
                Vector3 facing = -ports[0].Heading, tangent=Vector3.Cross(Vector3.up,facing);
                Quaternion turn = Quaternion.LookRotation(facing);
                Vector3 back = center + facing * ForestMaze.WallCenterRadius;
                fabric.Concrete.Box(back + Vector3.up * (ForestMaze.WallHeight / 2),
                    new Vector3(PassageHalf * 2, ForestMaze.WallHeight, ForestMaze.WallThickness),
                    turn, WorldMesh.Shade(WetConcrete,.67f));
                for (int i=0; i<5; i++)
                {
                    float lateral = (i-2)*.76f;
                    Vector3 panel = back + tangent * lateral - facing * .22f + Vector3.up * (ForestMaze.WallHeight / 2);
                    fabric.Concrete.Box(panel, new Vector3(.67f, ForestMaze.WallHeight - .35f, .018f),
                        turn, WorldMesh.Shade(WetConcrete,.71f+i*.045f));
                }
                WarningPlate(fabric.Iron,back+tangent*1.57f-facing*.6f,turn.eulerAngles.y,.66f);
            }

            // Put readable environmental landmarks in the closed sectors, away from traversal.
            int index = (node.Id*13+5)%sides;
            for (int attempts=0; attempts<sides; attempts++,index=(index+3)%sides)
            {
                Vector3 side = MazeRadial(index,sides);
                if (PortOpen(side,radius,ports)) continue;
                Vector3 fixture = center+side*(node.Level<=1 ? radius-.19f : radius+.09f);
                if (!MazeObstructionClear(maze,null,fixture,node.Level<=1 ? .48f : .75f,3.8f)) continue;
                if (node.Level <= 1)
                    MazeDrum(fabric.Iron,fabric.Mineral,fixture,Mathf.Sin(node.Id*.24f)*.21f,node.Id*.79f);
                else
                    Roots(fabric.Bark,fixture-Vector3.up*.35f,fixture+Vector3.up*3.75f+side*.45f,
                        .46f,.29f,node.Id);
                break;
            }
        }

        private void BuildMazeEntryGate(Vector3 center)
        {
            mazeEntryGate = Group("Weathered return floodgate", mazeArchitecture);
            Vector3 floor = center + Vector3.back * ForestMaze.WallCenterRadius;
            var door = new WorldMesh(mazeMeshes);
            float height = ForestMaze.WallHeight;
            door.Box(floor + Vector3.up * (height / 2),
                new Vector3(2 * PassageHalf + .24f, height, ForestMaze.WallThickness),
                Quaternion.identity, WorldMesh.Shade(WetConcrete,.65f));
            for (int bay = -3; bay <= 3; bay++)
            {
                Vector3 strap = floor + new Vector3(bay * .68f, height / 2, ForestMaze.WallThickness / 2 - .03f);
                door.Box(strap,new Vector3(.36f,height-.20f,.06f),Quaternion.identity,
                    WorldMesh.Shade(Rust,.74f + (bay + 3) * .054f));
            }
            for (int joint = 1; joint <= 3; joint++)
            {
                Vector3 rail = floor + new Vector3(0,joint * height / 4,ForestMaze.WallThickness / 2 - .045f);
                door.Box(rail,new Vector3(2 * PassageHalf + .20f,.16f,.09f),Quaternion.identity,
                    WorldMesh.Shade(WetConcrete,.79f));
            }
            WarningPlate(door, floor + new Vector3(-1.05f,.75f,ForestMaze.WallThickness / 2 + .025f), 180, .74f);
            MazePiece("Corroded panels sliding across the return vestibule",door,stone,mazeEntryGate);
        }

        private static Vector3 MazeRadial(int index,int sides)
        {
            float angle = index*(Mathf.PI*2/sides);
            return new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
        }

        private static Vector3 MazeRoomPoint(Vector3 center,Vector3 offset,List<MazePort> ports)
        {
            foreach (MazePort port in ports)
            {
                if (Mathf.Abs(port.Grade)<.001f) continue;
                float forward = Vector3.Dot(port.Heading,offset);
                if (forward<=0) continue;
                float across = Mathf.Abs(Vector3.Cross(port.Heading,offset).y);
                float continuation = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(PassageHalf-.40f,RoomRadius,across));
                offset.y += forward*port.Grade*continuation;
            }
            return center+offset;
        }

        private static bool PortOpen(Vector3 direction,float radius,List<MazePort> ports)
        {
            foreach (MazePort port in ports)
                if (Vector3.Dot(direction,port.Heading)>0 && Mathf.Abs(Vector3.Cross(direction,port.Heading).y)*radius<PassageHalf+.28f)
                    return true;
            return false;
        }

        private void BuildMazeBasins(MazeFabric fabric,Vector3 chamber)
        {
            for (int i=0;i<mazeRootSignals.Length;i++)
            {
                float side=i==0?-1:1;
                Vector3 rootAt=chamber+new Vector3(side*MazeLane,i==0?.09f:3.02f,0);
                for(int segment=0;segment<8;segment++)
                {
                    Vector3 radial = MazeRadial(segment,8);
                    Vector3 lip=rootAt+radial*.56f;
                    fabric.Concrete.Rock(lip,new Vector3(.25f,.17f,.29f),WorldMesh.Shade(WetConcrete,.96f),
                        segment*.83f,4,8,.21f);
                    Roots(fabric.Bark,lip+Vector3.down*.12f,rootAt+radial*.94f+Vector3.down*(i==1?.69f:.04f),
                        .103f,.11f,segment*.39f);
                }
                if (i==1)
                {
                    Roots(fabric.Bark,chamber+new Vector3(3.67f,.17f,1.10f),rootAt+Vector3.down*.211f,
                        .56f,.44f,9f);
                    Roots(fabric.Bark,chamber+new Vector3(3.03f,.12f,-1.81f),rootAt+Vector3.down*.243f,
                        .42f,.28f,13f);
                }
                var veins=new WorldMesh(mazeMeshes);
                for (int line=0;line<4;line++)
                {
                    var tip=rootAt+new Vector3(side*(.53f+line*.24f),i==0?.68f:1.25f,-.42f+line*.28f);
                    Roots(veins,rootAt+Vector3.up*.12f,tip,.031f,.17f,4*line,Color.white);
                }
                mazeRootSignals[i]=MazePiece("Expandable veins of the planting basin",veins,glow,mazeArchitecture);
            }
        }

        private void BuildMazeBackdrop(MazeFabric fabric,ForestMaze maze)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (ForestMazeNode node in maze.Nodes)
            {
                minX=Mathf.Min(minX,node.X);
                maxX=Mathf.Max(maxX,node.X);
                minZ=Mathf.Min(minZ,node.Z);
                maxZ=Mathf.Max(maxZ,node.Z);
            }
            float left=minX-12, right=Mathf.Min(-13,maxX+8), back=minZ-10, front=maxZ+17;
            fabric.Ground.Surface(38,40,(x,z)=>
            {
                float px=Mathf.Lerp(left,right,x/38f), pz=Mathf.Lerp(back,front,z/40f);
                float shoulder=Mathf.Clamp01((left+6-px)*.25f)+(pz>maxZ+4?.38f*(pz-maxZ-4):0);
                return new Vector3(px,-1.1f+Mathf.PerlinNoise(px*.09f+4,pz*.09f+3)*.75f+shoulder,pz);
            }, p=>WorldMesh.Shade(new Color(.219f,.264f,.190f),
                .62f+.62f*Mathf.PerlinNoise(p.x*.73f,p.z*.56f)),null);
            for (int i=0;i<22;i++)
            {
                float t=i/21f;
                float x=Mathf.Lerp(left+2,right-3,t), z=maxZ+6.2f+1.27f*Mathf.Sin(i*2.25f);
                Vector3 foot=new Vector3(x,-.35f,z);
                if (i%2==0) DeadCanopy(fabric.Bark,fabric.Foliage,foot,8.2f+i%4*1.53f,.51f+i%3*.19f,i*.641f);
                fabric.Concrete.Rock(foot,new Vector3(2.4f,1.28f+i%4*.66f,2.3f),
                    WorldMesh.Shade(WetConcrete,.60f+i%3*.07f),i*.73f,5,9,.25f);
            }
            for (int i=0;i<16;i++)
            {
                float z=Mathf.Lerp(back+3,front-1,i/15f);
                Vector3 foot=new Vector3(left+2.2f+.39f*Mathf.Sin(i*.62f),.17f,z);
                if(i%2==0) DeadCanopy(fabric.Bark,fabric.Foliage,foot,10+i%4,.75f,i*.92f);
                fabric.Concrete.Rock(foot,new Vector3(1.79f,1.87f,2.15f),WetConcrete,i*.916f,5,9,.23f);
            }
        }

        private static Vector3 Landscape(float x, float z, float rise) => new Vector3(x, Ground(x, z) + rise, z);

        private MeshRenderer MazePiece(string name, WorldMesh mesh, Material material, Transform parent, float light = 0)
        {
            MeshRenderer renderer = Piece(name, mesh.Finish(name), material, parent, Vector3.zero).GetComponent<MeshRenderer>();
            if (light > 0) Light(renderer, Color.white, light);
            return renderer;
        }

        private MeshRenderer MazeLamp(Transform parent, Vector3 point, float radius)
        {
            MeshRenderer lamp = Lamp("Seepage on the damaged pipe", point, radius, Effluent, .25f);
            lamp.transform.SetParent(parent, false);
            lamp.transform.localScale = new Vector3(radius * 1.4f, radius * .26f, radius * .76f);
            return lamp;
        }

        private static void Roots(WorldMesh mesh, Vector3 start, Vector3 end, float thick, float sway, float seed,
            Color? tone = null)
        {
            var spine = new Vector3[9];
            var radii = new float[spine.Length];
            Vector3 side = Vector3.Cross(end - start, Vector3.up).normalized;
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            for (int i = 0; i < spine.Length; i++)
            {
                float t = i / (float)(spine.Length - 1);
                float lip = Mathf.Sin(t * Mathf.PI);
                spine[i] = Vector3.Lerp(start, end, t) + side * (lip * sway * Mathf.Sin(t * 6.5f + seed)) +
                    Vector3.up * (lip * sway * .66f);
                radii[i] = thick * Mathf.Lerp(1.09f, .12f, t) * (1 + .22f * Mathf.Sin(t * 8.8f + seed));
            }
            mesh.Tube(spine, radii, 9, tone ?? PollutedBark);
        }

        private static void SpillPipe(WorldMesh metal, WorldMesh trim, Vector3 first, Vector3 mouth, float radius)
        {
            Vector3 elbow = Vector3.Lerp(first, mouth, .56f) + Vector3.up * .37f;
            Vector3 rim = mouth - (mouth - elbow).normalized * .09f;
            metal.Tube(new[] { first, elbow, rim }, new[] { radius * .90f, radius, radius * .98f }, 14, WetConcrete);
            Quaternion facing = Quaternion.LookRotation(mouth - elbow);
            Ring(metal, mouth, radius, .104f, Rust, facing, 22, 1);
            Ring(metal, elbow, radius, .071f, Rust, facing, 20, 1);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Vector3 bolt = mouth + facing * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, .045f);
                metal.Rock(bolt, new Vector3(.071f, .069f, .081f), WetConcrete, a, 3, 5, .12f);
            }
            Ring(trim, mouth + facing * new Vector3(0, -.06f, .03f), radius * .86f, .022f, Effluent,
                facing * Quaternion.Euler(0, 0, 192), 14, .47f);
        }

        private static void MazeDrum(WorldMesh shell, WorldMesh mark, Vector3 bottom, float tilt, float seed)
        {
            Quaternion rotation = Quaternion.Euler(tilt * 36, seed * 16.2f, tilt * -17);
            Vector3 At(float x, float y, float z) => bottom + rotation * new Vector3(x, y, z);
            shell.Tube(new[] { At(0,0,0), At(0,.18f,0), At(0,.75f,0), At(0,.93f,0) },
                new[] { .26f, .30f, .30f, .25f }, 13, Rust);
            Quaternion top = rotation * Quaternion.Euler(90, 0, 0);
            Ring(shell, At(0,.15f,0), .292f, .034f, WetConcrete, top, 16, 1);
            Ring(shell, At(0,.82f,0), .295f, .038f, WetConcrete, top, 16, 1);
            shell.Rock(At(0,.9f,0), new Vector3(.25f, .054f, .244f), WetConcrete, seed, 3, 10, .06f);
            // Three sectors and a center mark read as a faded contamination stamp on the barrel.
            Vector3 center = At(0, .51f, -.319f);
            mark.Rock(center, new Vector3(.034f, .034f, .011f), OchreMark, seed, 3, 7);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3 + .31f;
                Vector3 Point(float r, float angle) => center + rotation * new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), -.003f);
                mark.Quad(Point(.051f, a - .43f), Point(.139f, a - .41f), Point(.139f, a + .42f), Point(.051f, a + .42f), OchreMark);
            }
        }

        private static void WarningPlate(WorldMesh metal, Vector3 foot, float angle, float scale)
        {
            Quaternion heading = Quaternion.Euler(0, angle, -9);
            Vector3 Point(float x, float y, float z) => foot + heading * new Vector3(x, y, z) * scale;
            metal.Box(Point(0,.83f,0), new Vector3(.105f, 1.79f, .11f) * scale, heading, Rust);
            metal.Box(Point(0,1.74f,0), new Vector3(1.16f, .91f, .10f) * scale, heading, Rust);
            metal.Quad(Point(-.52f,1.39f,-.056f), Point(.53f,1.32f,-.056f), Point(.52f,2.12f,-.056f),
                Point(-.46f,2.15f,-.056f), OchreMark);
            Vector3 Stamp(float radius, float radians) => Point(radius * Mathf.Cos(radians), 1.73f + radius * Mathf.Sin(radians), -.061f);
            Color stamp = new Color(.10f, .12f, .14f);
            for (int i = 0; i < 3; i++)
            {
                float turn = .5236f + i * 2.0944f;
                metal.Quad(Stamp(.113f, turn - .44f), Stamp(.298f, turn - .46f), Stamp(.311f, turn + .48f),
                    Stamp(.11f, turn + .45f), stamp);
            }
            metal.Rock(Point(0,1.73f,-.066f), new Vector3(.071f, .070f, .015f) * scale, stamp, 3.2f, 3, 8);
        }

        private static void DeadCanopy(WorldMesh woodMesh, WorldMesh leavesMesh, Vector3 bottom, float height, float thickness, float seed)
        {
            Vector3 fork = bottom + new Vector3(Mathf.Sin(seed) * .64f, height * .66f, .24f);
            Roots(woodMesh, bottom, bottom + new Vector3(Mathf.Sin(seed) * 1.13f, height, .79f), thickness, .68f, seed);
            for (int i = 0; i < 6; i++)
            {
                float angle = seed + i * 2.39996f;
                var bend = new Vector3(Mathf.Cos(angle), .33f + .2f * Mathf.Sin(i * 2.11f), Mathf.Sin(angle));
                Vector3 elbow = fork + bend * 1.25f + Vector3.up * (i * .18f);
                Vector3 end = fork + bend * (2.42f + .18f * i) + Vector3.up * (i*.32f);
                Roots(woodMesh, fork + Vector3.up * (.23f * i), end, thickness * .38f, .31f, seed + i);
                Roots(woodMesh, elbow, end + Vector3.up * .6f - Vector3.forward * .33f, thickness * .14f, .28f, seed - i);
                if (i % 2 == 0)
                    leavesMesh.Leaf(elbow, end + bend * .18f, Vector3.Cross(bend, Vector3.up), .63f,
                        new Color(.25f, .32f, .19f));
            }
        }

        private static void DepositedReeds(WorldMesh mesh, Vector3 root, float seed)
        {
            for (int i = 0; i < 5; i++)
            {
                float turn = seed + i * 2.39996f;
                var blade = new Vector3(Mathf.Cos(turn), .3f + .38f * Mathf.Cos(i * 2.43f), Mathf.Sin(turn));
                Vector3 middle = root + blade * .19f + Vector3.up * .18f;
                Vector3 end = root + blade * .36f + Vector3.up * .31f;
                mesh.Tube(new[] { root, middle, end }, new[] { .014f, .017f, .0055f }, 4,
                    Color.Lerp(PaleVein, Effluent, .23f + .72f * Mathf.Abs(Mathf.Sin(turn))));
                mesh.Rock(middle, new Vector3(.036f, .051f, .034f), Effluent, turn, 3, 6, .31f);
            }
        }

        public void DisposeMaze()
        {
            ClearMazeLayout();
            if (mazeWater != null) Object.Destroy(mazeWater);
        }
    }
}
