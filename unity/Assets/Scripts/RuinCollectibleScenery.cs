using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float RuinNicheLateral = 3.87f;
        private static readonly Color[] RuinClueColors =
        {
            new Color(1f, .46f, .47f), new Color(.87f, .73f, 1f),
            new Color(.45f, .88f, 1f), new Color(.53f, 1f, .78f)
        };

        private readonly List<RuinNicheView> ruinNiches = new List<RuinNicheView>(7);
        private readonly Dictionary<(RuinObjectKind, int), RuinNicheParts> ruinNicheMeshes =
            new Dictionary<(RuinObjectKind, int), RuinNicheParts>();

        private sealed class RuinNicheView
        {
            public Transform Root;
            public MeshFilter Stone, Fittings, Seam;
            public MeshRenderer StoneRenderer, SeamRenderer;
        }

        private readonly struct RuinNicheParts
        {
            public readonly Mesh Stone, Fittings, Seam;

            public RuinNicheParts(Mesh stone, Mesh fittings, Mesh seam)
            {
                Stone = stone;
                Fittings = fittings;
                Seam = seam;
            }
        }

        private void DrawRuinNiche(int slot, RuinObject item, RuinRouteFrame frame, Journey journey, float time, float gap)
        {
            if (slot == ruinNiches.Count)
            {
                Transform root = Group("Recessed side archive", tunnelRoot);
                Transform stonework = RuinPiece("Stone pocket and overhang", null, tunnelStone, root);
                Transform fittings = RuinPiece("Recess and partial screen", null, tunnelMetal, root);
                Transform seam = RuinPiece("Light leaking through the seam", null, glow, root);
                ruinNiches.Add(new RuinNicheView
                {
                    Root = root,
                    Stone = stonework.GetComponent<MeshFilter>(),
                    Fittings = fittings.GetComponent<MeshFilter>(),
                    Seam = seam.GetComponent<MeshFilter>(),
                    StoneRenderer = stonework.GetComponent<MeshRenderer>(),
                    SeamRenderer = seam.GetComponent<MeshRenderer>()
                });
            }
            RuinNicheView view = ruinNiches[slot];
            int side = item.Lane < 0 ? -1 : 1;
            var key = (item.Kind, side);
            if (!ruinNicheMeshes.TryGetValue(key, out RuinNicheParts parts))
            {
                parts = BuildRuinNiche(item.Kind, side);
                ruinNicheMeshes.Add(key, parts);
            }
            if (view.Stone.sharedMesh != parts.Stone)
            {
                view.Stone.sharedMesh = parts.Stone;
                view.Fittings.sharedMesh = parts.Fittings;
                view.Seam.sharedMesh = parts.Seam;
            }
            view.Root.gameObject.SetActive(true);
            view.Root.SetLocalPositionAndRotation(frame.Point(side * RuinNicheLateral, .54f + 3.30f * item.Altitude),
                frame.Rotation);
            view.Root.localScale = new Vector3(1, 1, 1 + .13f * (item.Id % 3));
            Color roomStone = RuinGalleryStoneFor(journey.RuinRoomStyle, journey.RuinRoomId);
            Light(view.StoneRenderer, Color.Lerp(Color.white, roomStone, .56f), 0);
            view.SeamRenderer.enabled = !item.Collected;
            if (item.Collected) return;
            Color roomLight = RuinGalleryAccentFor(journey.RuinRoomStyle, journey.RuinRoomId);
            Color trace = Color.Lerp(RuinClueColors[(int)item.Kind], roomLight, .22f);
            float pulse = .5f + .5f * Mathf.Sin(time * 2.15f + item.Id * .79f);
            float near = 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(gap) - 3.2f) / 7.2f);
            Light(view.SeamRenderer, trace, .17f + near * (.28f + .18f * pulse) + .18f * item.Reveal);
        }

        private void HideRuinNichesFrom(int slot)
        {
            for (int i = slot; i < ruinNiches.Count; i++)
                ruinNiches[i].Root.gameObject.SetActive(false);
        }

        private RuinNicheParts BuildRuinNiche(RuinObjectKind kind, int side)
        {
            WorldMesh stonework = Geometry(), fittings = Geometry(), seam = Geometry();
            Color stoneTone = new Color(.42f, .435f, .46f);
            Color recess = new Color(.069f, .080f, .091f);
            Color alloy = new Color(.34f, .31f, .266f);
            Color edge = new Color(.47f, .42f, .34f);

            void Box(WorldMesh mesh, float x, float y, float z, float w, float h, float d, Color tint,
                float yaw = 0, float tilt = 0)
            {
                mesh.Box(new Vector3(x * side, y, z), new Vector3(w, h, d),
                    Quaternion.Euler(0, yaw * side, tilt * side), tint);
            }

            // Each recess opens diagonally toward the side aisle. The offset screen breaks
            // the line of sight from the centre of the tunnel.
            Box(stonework, .67f, -.11f, .15f, .39f, 1.75f, 1.86f, stoneTone, -9);
            Box(stonework, .06f, -.74f, .09f, 1.59f, .29f, 1.86f, stoneTone, -4);
            Box(stonework, .23f, -.71f, .93f, 1.29f, 1.05f, .35f, stoneTone, 4);
            Box(stonework, 1.10f, -.53f, .43f, 1.12f, .27f, .77f, stoneTone, 7);
            Box(stonework, 1.54f, -.96f, .44f, .24f, 1.05f, .47f, stoneTone);
            Box(fittings, .04f, .005f, .93f, 1.25f, 1.46f, .13f, recess, -4);
            Box(fittings, .06f, -.55f, -.17f, 1.36f, .13f, 1.44f, alloy, -4);
            Box(fittings, -.59f, -.02f, -.70f, .23f, 1.24f, .33f, alloy, -16);
            Box(fittings, -.62f, -.36f, -.85f, .27f, .23f, .19f, edge, -16);
            Box(fittings, -.68f, -.36f, -.947f, .58f, .24f, .095f, recess, -16);
            Box(seam, -.67f, -.322f, -1.002f, .39f, .048f, .024f, Color.white, -16);
            Box(seam, -.77f, -.406f, -1.021f, .18f, .026f, .019f, Color.white, -16);
            Box(seam, -.98f, -.341f, -.905f, .03f, .113f, .126f, Color.white, -16);

            // Different remnants of the archive were used to board up the four kinds of hiding place.
            switch (kind)
            {
                case RuinObjectKind.Heart:
                    Box(stonework, .24f, .68f, .18f, 1.24f, .26f, 1.32f, stoneTone, -6, -9);
                    for (int rib = 0; rib < 3; rib++)
                    {
                        float z = -.65f + rib * .38f;
                        Box(fittings, .33f, .40f, z, .92f, .065f, .14f, alloy, -7, -16);
                        Box(fittings, -.14f, .22f, z, .12f, .32f, .14f, alloy, -7, 38);
                    }
                    break;
                case RuinObjectKind.Brain:
                    Box(stonework, .06f, .70f, .14f, 1.46f, .33f, 1.45f, stoneTone, 4);
                    for (int slate = 0; slate < 4; slate++)
                        Box(fittings, -.65f, .55f - slate * .14f, -.70f + slate * .057f,
                            .43f, .097f, .16f, alloy, -24, 6);
                    break;
                case RuinObjectKind.Phone:
                    Box(stonework, .22f, .59f, .20f, 1.33f, .22f, 1.58f, stoneTone, -3);
                    Box(fittings, -.65f, .06f, -.77f, .39f, .89f, .12f, recess, -23, 8);
                    Box(fittings, -.65f, .47f, -.80f, .40f, .082f, .15f, edge, -23, 8);
                    Box(fittings, -.52f, -.04f, -.86f, .042f, .21f, .061f, alloy, -23, 8);
                    break;
                default:
                    Box(stonework, .15f, .71f, .17f, 1.43f, .34f, 1.65f, stoneTone, 7);
                    Box(fittings, -.64f, .39f, -.66f, .46f, .45f, .16f, recess, -17, 8);
                    for (int vent = 0; vent < 4; vent++)
                        Box(fittings, -.80f + .092f * vent, .40f + .015f * vent, -.747f + .028f * vent,
                            .041f, .27f, .026f, alloy, -17, 8);
                    break;
            }
            return new RuinNicheParts(stonework.Finish("Archive side recess stonework"),
                fittings.Finish("Archive side recess baffles"), seam.Finish("Archive casing status seam"));
        }
    }
}
