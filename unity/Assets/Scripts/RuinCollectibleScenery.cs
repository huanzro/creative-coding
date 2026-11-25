using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private const float RuinCacheOffset = 3.91f;
        private const float RuinCacheDeckHeight = 1.29f;
        private static readonly Color CacheMarkerTint = new Color(.47f, 1f, .86f);
        private readonly List<RuinNicheView> ruinNiches = new List<RuinNicheView>(7);
        private readonly Dictionary<(int Style, int Side), RuinNicheParts> ruinNicheMeshes =
            new Dictionary<(int Style, int Side), RuinNicheParts>();

        private sealed class RuinNicheView
        {
            public Transform Root;
            public MeshFilter Stone, Fittings, Marker;
            public MeshRenderer StoneRenderer, MarkerRenderer;
        }

        private readonly struct RuinNicheParts
        {
            public readonly Mesh Stone, Fittings, Marker;

            public RuinNicheParts(Mesh stone, Mesh fittings, Mesh marker)
            {
                Stone = stone;
                Fittings = fittings;
                Marker = marker;
            }
        }

        private void DrawRuinNiche(int slot, RuinObject item, RuinRouteFrame frame, Journey journey, float time, float gap)
        {
            if (slot == ruinNiches.Count)
            {
                Transform root = Group("Side archive shelf", tunnelRoot);
                Transform stonework = RuinPiece("Stone shelf plinth", null, tunnelStone, root);
                Transform fittings = RuinPiece("Archive cabinet and lantern case", null, tunnelMetal, root);
                Transform marker = RuinPiece("Archive lantern and path to its shelf", null, glow, root);
                ruinNiches.Add(new RuinNicheView
                {
                    Root = root,
                    Stone = stonework.GetComponent<MeshFilter>(),
                    Fittings = fittings.GetComponent<MeshFilter>(),
                    Marker = marker.GetComponent<MeshFilter>(),
                    StoneRenderer = stonework.GetComponent<MeshRenderer>(),
                    MarkerRenderer = marker.GetComponent<MeshRenderer>()
                });
            }
            RuinNicheView view = ruinNiches[slot];
            int side = item.Lane < 0 ? -1 : 1;
            int style = journey.RuinRoomStyle;
            var key = (style, side);
            if (!ruinNicheMeshes.TryGetValue(key, out RuinNicheParts parts))
            {
                parts = BuildRuinNiche(style, side);
                ruinNicheMeshes.Add(key, parts);
            }
            if (view.Stone.sharedMesh != parts.Stone)
            {
                view.Stone.sharedMesh = parts.Stone;
                view.Fittings.sharedMesh = parts.Fittings;
                view.Marker.sharedMesh = parts.Marker;
            }
            view.Root.gameObject.SetActive(true);
            view.Root.SetLocalPositionAndRotation(frame.Position, frame.Rotation);
            Color roomStone = RuinGalleryStoneFor(style, journey.RuinRoomId);
            Light(view.StoneRenderer, Color.Lerp(Color.white, roomStone, .51f), 0);
            view.MarkerRenderer.enabled = !item.Collected;
            if (item.Collected) return;
            float pulse = .5f + .5f * Mathf.Sin(time * 2.35f + item.Id * .73f);
            float distance = Mathf.Abs(gap);
            float near = 1 - Mathf.SmoothStep(0, 1, (distance - 4) / 11);
            Light(view.MarkerRenderer, CacheMarkerTint, .47f + .29f * near + .17f * pulse + .10f * item.Reveal);
        }

        private static Vector3 RuinCacheDisplay(RuinRouteFrame frame, int side, float reveal)
        {
            // Allow the largest model to bob and turn above the tray inside every gallery shell.
            float lift = .14f * Mathf.SmoothStep(0, 1, reveal);
            return frame.Point(RuinCacheOffset * side, 1.87f + lift);
        }

        private void HideRuinNichesFrom(int slot)
        {
            for (int i = slot; i < ruinNiches.Count; i++)
                ruinNiches[i].Root.gameObject.SetActive(false);
        }

        private RuinNicheParts BuildRuinNiche(int style, int side)
        {
            WorldMesh stone = Geometry(), fittings = Geometry(), markers = Geometry();
            Color slate = new Color(.37f, .41f, .43f);
            Color blackened = new Color(.095f, .135f, .15f);
            Color bronze = new Color(.43f, .34f, .23f);
            Quaternion flat = Quaternion.identity;

            Vector3 Cabinet(float inward, float height, float depth) =>
                new Vector3(side * (RuinCacheOffset - inward), height, depth);

            stone.Box(Cabinet(-.025f, .39f, .055f), new Vector3(.76f, .78f, .98f), flat, slate);
            stone.Box(Cabinet(0, .80f, 0), new Vector3(.71f, .47f, .91f), flat, slate);
            fittings.Box(Cabinet(0, 1.065f, 0), new Vector3(.78f, .125f, 1.06f), flat, bronze);
            fittings.Box(Cabinet(0, RuinCacheDeckHeight - .072f, 0), new Vector3(.86f, .144f, 1.10f), flat, blackened);
            fittings.Box(Cabinet(0, RuinCacheDeckHeight + .012f, 0),
                new Vector3(.62f, .025f, .73f), flat, bronze);
            markers.Box(Cabinet(0, RuinCacheDeckHeight + .031f, -.335f),
                new Vector3(.50f, .015f, .034f), flat, Color.white);

            Vector3 lamp = Cabinet(.20f, 1.09f, -.59f);
            fittings.Box(lamp, new Vector3(.57f, .24f, .195f), flat, blackened);
            markers.Box(lamp + new Vector3(0, .016f, -.104f), new Vector3(.45f, .097f, .032f), flat, Color.white);
            markers.Box(lamp + new Vector3(-side * .292f, .016f, 0),
                new Vector3(.026f, .097f, .141f), flat, Color.white);

            // Follow the shoulder's actual lower profile. This keeps the signs on top of the ground
            // where a low camera can see their lead line even before the cabinet is in view.
            Vector3 OnFloor(float across, float depth, float lift)
            {
                float absoluteCross = across * side;
                return new Vector3(absoluteCross, RuinCacheFloorHeight(style, absoluteCross) + lift, depth);
            }

            for (int step = 0; step < 3; step++)
            {
                float across = 1.88f + .46f * step;
                float depth = -2.54f + .56f * step;
                Vector3 a = OnFloor(across - .13f, depth - .33f, .105f);
                Vector3 b = OnFloor(across + .20f, depth, .105f);
                Vector3 c = OnFloor(across - .13f, depth + .33f, .105f);
                Vector3 nudge = Vector3.down * .017f;
                RuinBar(fittings, a + nudge, b + nudge, .113f, .125f, blackened);
                RuinBar(fittings, b + nudge, c + nudge, .113f, .125f, blackened);
                Vector3 lensTop = Vector3.up * .046f;
                RuinBar(markers, a + lensTop, b + lensTop, .077f, .058f, Color.white);
                RuinBar(markers, b + lensTop, c + lensTop, .077f, .058f, Color.white);
            }

            return new RuinNicheParts(stone.Finish("Side cache pedestal"),
                fittings.Finish("Archive shelf and trail housings"), markers.Finish("Side cache luminous trail"));
        }

        private static float RuinCacheFloorHeight(int style, float x)
        {
            // On the lower hull section x increases with angle from left to right.
            float left = Mathf.PI, right = 2 * Mathf.PI;
            for (int step = 0; step < 14; step++)
            {
                float middle = .5f * (left + right);
                if (RuinGallerySection(style, middle).x < x) left = middle;
                else right = middle;
            }
            return Mathf.Max(-.04f, RuinGallerySection(style, .5f * (left + right)).y);
        }
    }
}
