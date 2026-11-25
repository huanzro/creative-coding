using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float MazeCenter = -11;
        private const float MazeLane = 2.1f;
        private const float MazeStart = 25;
        private static readonly Color PollutedBark = new Color(.23f, .29f, .24f);
        private static readonly Color WetConcrete = new Color(.33f, .40f, .41f);
        private static readonly Color Rust = new Color(.46f, .23f, .13f);
        private static readonly Color Effluent = new Color(.34f, .62f, .45f);
        private static readonly Color PaleVein = new Color(.41f, .65f, .63f);
        private static readonly Color OchreMark = new Color(.79f, .61f, .27f);
        private static readonly int CurrentTime = Shader.PropertyToID("_CurrentTime");
        private static readonly int ReflectedFrame = Shader.PropertyToID("_ReflectionCenter");
        private readonly MeshRenderer[] mazeRootSignals = new MeshRenderer[2];
        private readonly MeshRenderer[] contaminationLights = new MeshRenderer[4];
        private Transform forkGround, mazeArchitecture;
        private Material mazeWater;

        public void DrawMaze(Journey journey, bool forest, float time)
        {
            if (!forest)
            {
                if (forkGround != null) forkGround.gameObject.SetActive(false);
                if (mazeArchitecture != null) mazeArchitecture.gameObject.SetActive(false);
                return;
            }
            bool seeFork = journey.Distance >= 69;
            if (seeFork && forkGround == null) BuildForkLandscape();
            if (forkGround != null) forkGround.gameObject.SetActive(seeFork);
            bool seeLeft = journey.Distance >= 88 && journey.Branch != ForestBranch.Right;
            if (seeLeft && mazeArchitecture == null) BuildPollutedMaze();
            if (mazeArchitecture == null) return;
            mazeArchitecture.gameObject.SetActive(seeLeft);
            if (!seeLeft) return;

            if (mazeWater != null) mazeWater.SetFloat(CurrentTime, time);
            for (int i = 0; i < contaminationLights.Length; i++)
                Light(contaminationLights[i], Effluent, .20f + .08f * Mathf.Sin(time * 1.5f + i * 1.8f));
            for (int i = 0; i < mazeRootSignals.Length; i++)
            {
                bool planted = false;
                foreach (ForestFeature feature in journey.ForestFeatures)
                {
                    if (feature.Kind != ForestFeatureKind.Plant || !feature.Collected) continue;
                    if ((i == 1) == (feature.Altitude > .4f)) planted = true;
                }
                Light(mazeRootSignals[i], planted ? Warm : PaleVein,
                    planted ? .63f + .10f * Mathf.Sin(time * 3.1f + i) : .085f);
            }
        }

        private void BuildForkLandscape()
        {
            forkGround = Group("Forest fork and continued terrain", root.transform);
            var terrain = Geometry();
            terrain.Surface(80, 110, (x, z) =>
            {
                float px = (x - 40) * .9f;
                float pz = 138 + z * .9f;
                return new Vector3(px, Ground(px, pz), pz);
            }, p =>
            {
                float opening = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(137, 149, p.z));
                float distance = Mathf.Abs(Mathf.Abs(p.x) - 11 * opening);
                float track = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.6f, 4.55f, distance));
                Color clay = new Color(.27f, .232f, .164f);
                Color moss = new Color(.177f, .246f, .168f);
                return WorldMesh.Shade(Color.Lerp(clay, moss, track),
                    .66f + .55f * Mathf.PerlinNoise(p.x * .64f + 9, p.z * .54f + 9));
            }, p => p.z >= 204 && p.x > 5 && p.x < 17);
            MazePiece("Soil extending through both paths", terrain, soil, forkGround);

            var trail = Geometry();
            var roots = Geometry();
            var foliage = Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                trail.Surface(12, 37, (x, z) =>
                {
                    float depth = 105 + z * .57f;
                    float fork = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(112, 124, depth));
                    float width = Mathf.Lerp(.50f, 2.12f, fork);
                    float px = side * (1.65f + 9.35f * fork) + (x / 6f - 1) * width;
                    return Landscape(px, MazeStart + depth, .013f);
                }, p => WorldMesh.Shade(side < 0 ? new Color(.24f, .30f, .27f) : new Color(.34f, .30f, .23f),
                    .78f + .22f * Mathf.PerlinNoise(p.x * 4.4f, p.z * 2.1f)), null);
                for (int i = 0; i < 12; i++)
                {
                    float z = 139 + i * 2.16f;
                    float x = side * (17.8f + Mathf.Sin(i * 4.8f + side) * 1.9f);
                    if (i % 3 == 0) Tree(roots, foliage, Landscape(x, z, -.08f), 10 + i % 4, .65f + i * .024f);
                    Fern(foliage, Landscape(x + side * .9f, z + .42f, 0), .8f + i % 4 * .18f, i * .68f);
                }
            }
            Vector3 bark = Landscape(-.55f, 145.8f, -.08f);
            Tree(roots, foliage, bark, 9.4f, 1.45f);
            Roots(roots, bark, Landscape(-4.3f, 139, -.06f), 1.26f, 1.3f, 0);
            Roots(roots, bark, Landscape(4.2f, 140.2f, -.06f), .87f, 1.34f, 2.4f);
            MazePiece("Diverging wet and dry tracks", trail, stone, forkGround);
            MazePiece("Weathered roots between both tracks", roots, wood, forkGround);
            MazePiece("Trees framing the fork", foliage, leaves, forkGround);
        }

        private void BuildPollutedMaze()
        {
            mazeArchitecture = Group("Contaminated root maze", root.transform);
            Shader shader = Resources.Load<Shader>("ForestRunoff");
            if (shader != null && shader.isSupported) mazeWater = new Material(shader) { name = "Oil and runoff" };
            WorldMesh barriers = Geometry(), fibers = Geometry(), trees = Geometry(), deposits = Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 28; i++)
                {
                    float z = 150 + i * 2.5f;
                    float x = MazeCenter + side * (6.8f + .65f * Mathf.Sin(i * 2.04f + .3f));
                    Vector3 ground = Landscape(x, z, -.09f);
                    float bank = .99f + .55f * Mathf.Sin(i * 1.84f + side);
                    barriers.Rock(ground, new Vector3(1.04f, bank, 1.7f),
                        WorldMesh.Shade(WetConcrete, .72f + .24f * Mathf.Sin(i * .71f)), i * .91f, 5, 9, .35f);
                    if (i % 4 == 0)
                    {
                        if (i < 10) Tree(fibers, trees, ground, 8.9f + i % 5, .62f + i % 3 * .17f);
                        else DeadCanopy(fibers, trees, ground, 7.4f + i % 5, .68f + i % 3 * .16f, i * .83f);
                    }
                    if (i % 3 != 0) continue;
                    Roots(fibers, ground, Landscape(x - side * .83f, z + 5.8f, -.28f),
                        .69f, .93f, i + side);
                    for (int vein = 0; vein < 3; vein++)
                    {
                        Vector3 rootAt = Landscape(x - side * (1.43f + .18f * vein), z + .6f + vein * .88f, .08f);
                        DepositedReeds(deposits, rootAt, i * .73f + vein * 3.2f);
                    }
                }
            }
            for (int i = 0; i < 19; i++)
            {
                float z = 166 + i * 3.32f;
                float x = MazeCenter + 9.2f + .44f * Mathf.Sin(i * 1.25f);
                Vector3 patch = Landscape(x, z, .86f);
                barriers.Rock(patch, new Vector3(1.26f, 2.08f + .63f * Mathf.Sin(i * 1.23f), 2.45f),
                    WorldMesh.Shade(WetConcrete, .67f + .09f * (i % 4)), i * .57f, 5, 10, .31f);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = MazeCenter + (i - 5.5f) * 1.33f, z = 224.0f + Mathf.Sin(i * .84f) * 2.26f;
                barriers.Rock(Landscape(x, z, 1.08f), new Vector3(1.72f, 2.97f + .75f * Mathf.Cos(i * 1.7f), 2.25f),
                    WorldMesh.Shade(WetConcrete, .62f + .075f * (i % 5)), i * .95f, 5, 10, .28f);
            }
            MazePiece("Water damaged retaining banks", barriers, stone, mazeArchitecture);
            MazePiece("Bare branches and coiled bank roots", fibers, wood, mazeArchitecture);
            MazePiece("Degraded canopy", trees, leaves, mazeArchitecture);
            MazePiece("Mineral deposits on deformed wetland grass", deposits, glow, mazeArchitecture, .13f);
            BuildDoubleRoots(155);
            BuildEchoWell(169);
            BuildCrossingBridges(184);
            BuildCanopyRoutes(199);
            BuildFiltrationClearing(213);
        }

        private void BuildDoubleRoots(float z)
        {
            Transform hall = Group("Double roots and ruptured outfall", mazeArchitecture);
            var bank = Geometry();
            var roots = Geometry();
            var details = Geometry();
            Vector3 low = Landscape(MazeCenter - MazeLane, z + .18f, 0);
            Vector3 hollow = Landscape(MazeCenter + MazeLane, z + 1.28f, 0);
            RootPortal(roots, low, 1.19f, 4.38f, .35f, -.22f);
            RootPortal(roots, hollow, 1.18f, 4.24f, .52f, .41f);
            Roots(roots, Landscape(MazeCenter, z + 2.08f, -.12f),
                Landscape(MazeCenter + .12f, z + 4.3f, 5.85f), .85f, 1.1f, 13);
            Roots(roots, Landscape(MazeCenter - 5.17f, z - 5.8f, .18f),
                Landscape(MazeCenter - 3.65f, z + 3.9f, 3.0f), .97f, 1.12f, 9.8f);
            SpillPipe(bank, details, Landscape(MazeCenter - 4.47f, z - 4.25f, 2.1f),
                Landscape(MazeCenter - 3.57f, z - 1.6f, .58f), .54f);
            MazeDrum(bank, details, Landscape(MazeCenter - 4.42f, z - 3.1f, .02f), -.14f, 21);
            MazeDrum(bank, details, Landscape(MazeCenter + 4.43f, z - 2.1f, .02f), .1f, 4);
            WarningPlate(bank, Landscape(MazeCenter + 3.90f, z + .29f, 0), 12, .63f);
            Stain(roots, Landscape(MazeCenter - 3.24f, z - 1.2f, .18f),
                Landscape(MazeCenter - 1.17f, z + 4.45f, .22f), .34f, 9.1f);
            Puddle(hall, MazeCenter - 1.75f, z - 4.6f, 3.1f, 5.1f, false);
            Puddle(hall, MazeCenter + 3.3f, z - 2.5f, 1.12f, 2.3f, false);
            MazePiece("Barrels, drainage concrete and outfall", bank, stone, hall);
            MazePiece("Ash crust on the two root mouths", roots, wood, hall);
            MazePiece("Faded radiological warning plates", details, glow, hall, .09f);
            contaminationLights[0] = MazeLamp(hall, Landscape(MazeCenter - 3.58f, z - 1.62f, .61f), .13f);
        }

        private void BuildEchoWell(float z)
        {
            Transform well = Group("Echo well and overhead opening", mazeArchitecture);
            var wall = Geometry();
            var roots = Geometry();
            var ribs = Geometry();
            Vector3 window = Landscape(MazeCenter + MazeLane, z + .4f, 2.65f);
            RootPortal(roots, window, 1.31f, 3.28f, .49f, .81f);
            Roots(roots, Landscape(MazeCenter - 4.70f, z + 1.24f, .12f),
                Landscape(MazeCenter - .08f, z + 3.65f, 5.60f), .98f, .72f, 9.8f);
            Roots(roots, Landscape(MazeCenter + 4.82f, z + 1.5f, -.24f),
                Landscape(MazeCenter + 3.59f, z + 1.8f, 8.9f), .79f, .55f, 7.4f);
            for (int i = 0; i < 5; i++)
            {
                float x = MazeCenter - 3.85f + i * 1.12f;
                float y = .8f + .24f * Mathf.Sin(i * 1.91f);
                wall.Rock(Landscape(x, z + 1.5f, y), new Vector3(.94f, 1.30f, 1.03f),
                    WorldMesh.Shade(WetConcrete, .75f + i * .043f), i * .58f, 5, 10);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 step = Landscape(MazeCenter + MazeLane, z + .65f + i * .35f, .33f + .17f * i);
                wall.Box(step, new Vector3(2.48f - .17f * i, .70f + .30f * i, .68f),
                    Quaternion.Euler(-4, Mathf.Sin(i * 1.41f) * 5, i * 2.1f),
                    WorldMesh.Shade(WetConcrete, .74f + .064f * i));
            }
            Roots(ribs, window + new Vector3(-.97f, .05f, .02f), window + new Vector3(-.89f, 2.1f, .22f),
                .045f, .13f, 6.71f, PaleVein);
            SpillPipe(wall, ribs, Landscape(MazeCenter + 3.89f, z + 3.2f, 6.59f),
                Landscape(MazeCenter + 4.17f, z + 1.65f, 5.52f), .52f);
            WarningPlate(wall, Landscape(MazeCenter - 2.67f, z - .28f, .05f), -14, .44f);
            Roots(roots, Landscape(MazeCenter + 4.18f, z + 2.18f, .05f),
                Landscape(MazeCenter + 4.14f, z + 1.62f, 5.48f), .20f, .11f, 11.2f, Effluent);
            Stain(roots, window + new Vector3(1.1f, -.09f, -.11f),
                window + new Vector3(.43f, 2.53f, 1.02f), .07f, 8);
            Puddle(well, MazeCenter + 1.0f, z - 5.4f, 3.7f, 5.63f, true);
            MazePiece("Collapsed intake masonry", wall, stone, well);
            MazePiece("Layered roots framing the climb", roots, wood, well);
            MazePiece("Corroded intake fittings", ribs, glow, well, .11f);
            contaminationLights[1] = MazeLamp(well, Landscape(MazeCenter + 4.14f, z + 1.86f, .81f), .15f);
        }

        private void BuildCrossingBridges(float z)
        {
            Transform crossing = Group("Abandoned inspection bridges", mazeArchitecture);
            var bridge = Geometry();
            var rust = Geometry();
            var roots = Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    float distance = z - 6.0f + i * .93f;
                    float horizontal = MazeCenter + side * MazeLane;
                    float height = side < 0 ? 1.49f + i * .12f : 1.91f + .05f * Mathf.Sin(i);
                    Vector3 plank = Landscape(horizontal, distance, height);
                    bridge.Box(plank, new Vector3(2.10f, .18f, .91f), Quaternion.Euler(side * -3, i * .82f, 0),
                        WorldMesh.Shade(WetConcrete, .67f + .24f * Mathf.Sin(i * 2.1f) + .12f * i / 9));
                    for (int bank = -1; bank <= 1; bank += 2)
                    {
                        if (i % 2 == 0 && (side < 0 || i < 6))
                            rust.Box(plank + new Vector3(bank * 1.05f, .27f, 0), new Vector3(.083f, .87f, .11f),
                                Quaternion.Euler(0, 0, bank * 6.5f), Rust);
                    }
                }
                Vector3 origin = Landscape(MazeCenter + side * MazeLane, z - 6.3f, 1.56f);
                Vector3 destination = Landscape(MazeCenter + side * MazeLane, z + 2.3f, side < 0 ? 2.49f : 1.90f);
                for (int bank = -1; bank <= 1; bank += 2)
                    rust.Tube(new[] { origin + Vector3.right * bank, (origin + destination) * .5f + Vector3.right * bank,
                        destination + Vector3.right * bank }, new[] { .066f, .072f, .047f }, 6, Rust);
            }
            Vector3 closed = Landscape(MazeCenter + MazeLane, z + 2.1f, .48f);
            RootPortal(roots, closed, 1.24f, 4.82f, .47f, 1.45f);
            for (int i = 0; i < 5; i++)
                Roots(roots, closed + new Vector3(-1.29f, i * .83f, .65f),
                    closed + new Vector3(1.53f, i * .62f + .64f, .01f), .245f, .53f, i * .9f + 7.2f);
            for (int i = 0; i < 3; i++)
                bridge.Box(Landscape(MazeCenter - MazeLane + (i - 1) * .61f, z + .30f, .92f),
                    new Vector3(.69f, 1.83f, .48f), Quaternion.Euler(5, i * 4.44f, (i - 1) * 6), WetConcrete);
            MazeDrum(bridge, rust, Landscape(MazeCenter + 4.35f, z - 5.1f, .02f), -.67f, 7.1f);
            WarningPlate(rust, Landscape(MazeCenter + 3.62f, z - .17f, 1.13f), -21, .46f);
            Roots(roots, Landscape(MazeCenter - 4.23f, z - 3.04f, .0f),
                Landscape(MazeCenter - 3.65f, z + 4.6f, 2.34f), .72f, 1.01f, 13.9f);
            Stain(roots, Landscape(MazeCenter - 3.8f, z - 2.4f, 1.35f),
                Landscape(MazeCenter - 2.78f, z + 3.2f, 2.31f), .095f, 10);
            Puddle(crossing, MazeCenter - .7f, z - 2.4f, 3.86f, 4.06f, false);
            MazePiece("Overlapping, eroded footbridges", bridge, stone, crossing);
            MazePiece("Rusted hand rail and discarded barrel", rust, stone, crossing);
            MazePiece("Root blockage of the east bridge", roots, wood, crossing);
            contaminationLights[2] = MazeLamp(crossing, Landscape(MazeCenter + 4.4f, z - 4.7f, .68f), .13f);
        }

        private void BuildCanopyRoutes(float z)
        {
            Transform canopy = Group("Upper canopy and descending outlet", mazeArchitecture);
            var roots = Geometry();
            var stonework = Geometry();
            var accretions = Geometry();
            Vector3 drop = Landscape(MazeCenter + MazeLane, z + .31f, -.13f);
            RootPortal(roots, drop, 1.23f, 3.93f, .42f, .11f);
            Roots(roots, Landscape(MazeCenter + 4.52f, z - 2.41f, 4.31f),
                Landscape(MazeCenter - 4.81f, z + 2.81f, 5.96f), .57f, .36f, 7.6f);
            Roots(roots, Landscape(MazeCenter + 4.8f, z + 2.31f, 2.69f),
                Landscape(MazeCenter - 3.72f, z + 6.38f, 4.62f), .44f, .58f, 5.4f);
            Roots(roots, Landscape(MazeCenter - 4.48f, z + 1.40f, 5.83f),
                Landscape(MazeCenter - 1.36f, z + 1.81f, 3.32f), .82f, .78f, 1.72f);
            SpillPipe(stonework, accretions, Landscape(MazeCenter - 4.30f, z + 4.82f, 7.58f),
                Landscape(MazeCenter - 3.42f, z + 3.1f, 5.64f), .66f);
            Stain(roots, Landscape(MazeCenter + 2.42f, z - 1.63f, 3.31f),
                Landscape(MazeCenter + 3.42f, z + 4.91f, 1.0f), .095f, 15);
            for (int i = 0; i < 13; i++)
            {
                float x = MazeCenter - 4.65f + i % 4 * .39f;
                float y = 5.63f + .66f * Mathf.Sin(i * 2.41f);
                float depth = z + i * .42f;
                Vector3 branch = Landscape(x, depth, y);
                accretions.Tube(new[] { branch, branch + new Vector3(.20f, -.44f, .17f),
                    branch + new Vector3(.11f, -.83f, .30f) }, new[] { .044f, .035f, .005f }, 5, PaleVein);
            }
            MazePiece("Canopy outlet and overgrown three-way junction", roots, wood, canopy);
            MazePiece("Broken evaporation ducts", stonework, stone, canopy);
            MazePiece("Leached deposits hanging from the canopy", accretions, glow, canopy, .12f);
            contaminationLights[3] = MazeLamp(canopy, Landscape(MazeCenter - 3.43f, z + 3.1f, 5.64f), .16f);
        }

        private void BuildFiltrationClearing(float z)
        {
            Transform court = Group("Filtration clearing with two living beds", mazeArchitecture);
            var supports = Geometry();
            var ceramic = Geometry();
            Vector3 lower = Landscape(MazeCenter - MazeLane, z, .32f);
            Vector3 upper = Landscape(MazeCenter + MazeLane, z, 3.30f);
            Roots(supports, Landscape(MazeCenter + 4.93f, z + 3.26f, -.17f), upper + new Vector3(.22f, -.83f, .09f),
                .86f, .61f, 8.86f);
            Roots(supports, Landscape(MazeCenter + 3.91f, z + 5.49f, 1.65f), upper + new Vector3(.1f, -.23f, .71f),
                .47f, .42f, 7.26f);
            Roots(supports, Landscape(MazeCenter - 4.42f, z + 6.6f, .13f),
                Landscape(MazeCenter - 4.89f, z - 1.41f, -.12f), .71f, 1.18f, 11.4f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * (Mathf.PI * 2 / 12);
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                ceramic.Rock(lower + radial * .98f - Vector3.up * .25f, new Vector3(.31f, .24f, .31f),
                    WorldMesh.Shade(WetConcrete, .86f + .15f * Mathf.Sin(i * 2)), i * .81f, 5, 8, .24f);
                Roots(supports, upper + radial * .29f - Vector3.up * .46f,
                    upper + radial * (.67f + .08f * Mathf.Sin(i)) - Vector3.up * .36f, .14f, .22f, i * 1.76f);
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 start = i == 0 ? lower : upper;
                var veins = Geometry();
                for (int line = 0; line < 4; line++)
                {
                    float sign = i == 0 ? -1 : 1;
                    Vector3 end = Landscape(MazeCenter + sign * (4.15f + line * .31f), z + 2.25f + line * .75f,
                        i == 0 ? .22f : 1.11f + line * .14f);
                    Roots(veins, start + new Vector3(sign * .27f, -.02f, .63f), end, .017f, .23f, 3 * line, Color.white);
                }
                mazeRootSignals[i] = MazePiece("Plant light carried along root veins", veins, glow, court);
            }
            MazeDrum(ceramic, ceramic, Landscape(MazeCenter + 4.11f, z - 3.65f, .04f), .17f, 11.4f);
            for (int i = 0; i < 6; i++)
                ceramic.Box(Landscape(MazeCenter - 5.32f + i * .092f, z + 3.1f + i * .71f, .56f),
                    new Vector3(.72f, .98f, .32f), Quaternion.Euler(-7, i * 4, 8), WetConcrete);
            Puddle(court, MazeCenter - 3.5f, z + 2.35f, 1.65f, 3.52f, false);
            MazePiece("Roots lifting the upper planting bed", supports, wood, court);
            MazePiece("Broken containment and the lower root basin", ceramic, stone, court);
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

        private static void RootPortal(WorldMesh mesh, Vector3 floor, float halfWidth, float height, float radius, float seed)
        {
            var spine = new Vector3[19];
            var radii = new float[spine.Length];
            for (int i = 0; i < spine.Length; i++)
            {
                float t = i / (float)(spine.Length - 1), a = t * Mathf.PI;
                spine[i] = floor + new Vector3(Mathf.Cos(a) * (halfWidth + .23f), Mathf.Pow(Mathf.Max(Mathf.Sin(a), 0), .72f) * height,
                    Mathf.Sin(a * 2.2f + seed) * .59f);
                radii[i] = radius * (.90f + .28f * Mathf.Cos(a * 6.2f + seed));
            }
            mesh.Tube(spine, radii, 11, PollutedBark, .82f);
            Roots(mesh, floor + new Vector3(-halfWidth - .22f, -.1f, -.35f),
                floor + new Vector3(-halfWidth - .47f, height * .62f, .75f), radius * .68f, .35f, seed + .6f);
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

        private static void Stain(WorldMesh mesh, Vector3 source, Vector3 sink, float spread, float seed)
        {
            for (int i = 0; i < 5; i++)
            {
                float separation = (i - 2) * spread * .42f;
                var offset = new Vector3(separation, i * .019f, -separation * .38f);
                Roots(mesh, source + offset, sink + offset * (1.9f + .48f * Mathf.Sin(i * 5)), .026f + .02f * Mathf.Sin(i),
                    .21f + spread, i + seed, WorldMesh.Shade(Effluent, .68f + i * .11f));
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

        private void Puddle(Transform parent, float x, float z, float width, float length, bool reflect)
        {
            if (mazeWater == null) return;
            var pond = Geometry();
            pond.Surface(24, 27, (column, row) =>
            {
                float px = x + (column / 12f - 1) * width;
                float pz = z + (row / 13.5f - 1) * length;
                return Landscape(px, pz, .026f);
            }, p =>
            {
                float dx = (p.x-x) / width, dz = (p.z-z) / length;
                float margin = 1 - Mathf.Sqrt(dx * dx + dz * dz);
                Color water = Color.Lerp(new Color(.10f, .18f, .17f), new Color(.26f, .35f, .21f),
                    Mathf.PerlinNoise(p.x*.46f, p.z*.68f));
                water.a = Mathf.Clamp01(margin * 6);
                return water;
            }, p => new Vector2((p.x - x) / width, (p.z - z) / length).sqrMagnitude > .97f);
            MeshRenderer surface = MazePiece("Contaminated shallow water", pond, mazeWater, parent);
            if (!reflect) return;
            var reflected = new MaterialPropertyBlock();
            reflected.SetVector(ReflectedFrame, new Vector4(MazeCenter + MazeLane, z + .2f, 1.07f, 1));
            surface.SetPropertyBlock(reflected);
        }

        public void DisposeMaze()
        {
            if (mazeWater != null) Object.Destroy(mazeWater);
        }
    }
}
