using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float StarReach = 38f;
        private readonly Transform[] starReaches = new Transform[4];
        private Transform starFlight;
        private int starReach = -1;
        internal static Quaternion StarHeading => Quaternion.LookRotation(StarEntrySight);

        internal static Vector3 StarCameraAt(float distance) =>
            StarEntryCamera + StarHeading * (Vector3.forward * Mathf.Repeat(distance, StarReach));

        public void DrawStarFlight(bool visible, float distance)
        {
            if (!visible)
            {
                if (starFlight != null && starFlight.gameObject.activeSelf) starFlight.gameObject.SetActive(false);
                return;
            }
            if (starFlight == null) BuildStarFlight();
            if (!starFlight.gameObject.activeSelf) starFlight.gameObject.SetActive(true);
            int reach = Mathf.FloorToInt(distance / StarReach);
            if (reach != starReach)
            {
                starReach = reach;
                for (int offset = -1; offset <= 2; offset++)
                {
                    int index = (reach + offset + starReaches.Length) % starReaches.Length;
                    starReaches[index].localPosition = Vector3.forward * (offset * StarReach);
                }
            }
            // Keep the prologue's distant islands and rings as a slow parallax layer.
            float distantTravel = 68 * (1 - Mathf.Exp(-distance / 68));
            starBackdrop.localPosition = StarHeading * (Vector3.forward * (Mathf.Repeat(distance, StarReach) - distantTravel));
        }

        private void BuildStarFlight()
        {
            starFlight = Group("Star Sea route", root.transform);
            starFlight.localPosition = StarEntryCamera;
            starFlight.localRotation = StarHeading;
            var random = new System.Random(2646);
            float Between(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
            var basalt = new Color(.25f, .29f, .43f);
            var slate = new Color(.38f, .41f, .55f);
            Color silver = new Color(.44f, .58f, .86f), honey = new Color(.92f, .55f, .28f);
            Color ember = new Color(.80f, .33f, .30f);
            for (int i = 0; i < starReaches.Length; i++)
            {
                Transform range = Group("Floating prologue fragments " + (i + 1), starFlight);
                starReaches[i] = range;
                var islands = Geometry();
                var roots = Geometry();
                var lights = Geometry();
                for (int j = 0; j < 3; j++)
                {
                    float side = ((i + j) & 1) == 0 ? -1 : 1;
                    float z = 11.8f + j * 10.8f + Between(-1.25f, 1.25f);
                    var shelf = new Vector3(side * Between(5.4f, 8.0f), Between(-4.9f, -2.6f), z);
                    float width = Between(1.35f, 2.5f);
                    islands.Rock(shelf, new Vector3(width, .55f, 1.52f), basalt, i + j * 2.9f, 5, 11, .28f);
                    islands.Rock(shelf + Vector3.down * 1.13f + Vector3.forward * .19f,
                        new Vector3(width * .53f, 1.44f, .92f), WorldMesh.Shade(basalt, .66f), i * 2.6f, 5, 9, .31f);
                    if (j != 1)
                    {
                        Arch(islands, shelf.x, shelf.y + .24f, shelf.z + .23f, .81f, .60f, .24f, .37f, slate, 11);
                        Vector3 root = shelf + new Vector3(-side * 1.23f, .26f, .28f);
                        Roots(roots, root, shelf + new Vector3(.15f * side, 1.42f, .60f), .19f, .38f, i * 5.3f + j);
                        Vector3 pearl = shelf + new Vector3(side * .24f, 1.31f, -.04f);
                        lights.Rock(pearl, new Vector3(.090f, .137f, .089f), j == 0 ? honey : ember, j * 3f, 5, 9, .08f);
                        Ring(lights, pearl, .37f, .010f, j == 0 ? honey : ember, Quaternion.Euler(60, 10 + i * 16, 22), 34, .78f);
                    }
                    else
                    {
                        for (int s = 0; s < 4; s++)
                        {
                            Vector3 step = shelf + new Vector3(side * (s * .46f - .57f), .50f + s * .17f, s * .47f - .53f);
                            islands.Box(step, new Vector3(.82f, .18f, .54f), Quaternion.Euler(0, 14 * side, 3 * s), slate);
                        }
                        Vector3 arc = shelf + new Vector3(-side * .57f, 1.66f, .20f);
                        Ring(lights, arc, 1.51f, .020f, silver,
                            Quaternion.Euler(17 + i * 7, 27 * side, 22 * side), 58, .74f);
                        Ring(lights, arc, 1.90f, .012f, Color.Lerp(silver, honey, .33f),
                            Quaternion.Euler(34, 49 * side, -8 * side), 58, .64f);
                    }
                }
                for (int mote = 0; mote < 34; mote++)
                {
                    float side = (mote & 1) == 0 ? -1 : 1;
                    Vector3 at = new Vector3(side * Between(3.2f, 12.7f), Between(-4.1f, 4.7f), Between(2.4f, StarReach - 1));
                    float scale = Between(.016f, .061f);
                    lights.Rock(at, new Vector3(scale, scale * Between(.74f, 1.65f), scale),
                        Color.Lerp(silver, honey, mote % 7 == 0 ? .58f : .06f), mote * 2.1f, 3, 5, .07f);
                }
                PlaceStarBackdrop(range, "Basalt islands and ruin arches", islands, stone);
                PlaceStarBackdrop(range, "Forest roots on suspended stone", roots, wood);
                Light(PlaceStarBackdrop(range, "Prologue embers and orbit fragments", lights, glow), Color.white, .26f);
            }
        }

        private MeshRenderer PlaceStarBackdrop(Transform parent, string name, WorldMesh mesh, Material material)
        {
            var renderer = Piece(name, mesh.Finish(name), material, parent, Vector3.zero).GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
