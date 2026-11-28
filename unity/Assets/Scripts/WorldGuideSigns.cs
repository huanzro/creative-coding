/* ARTISTIC RATIONALE
 * Trail signs introduce the rules where the player can use them. Wood, brass and short inscriptions
 * keep the guidance in the forest and the archive, with the machine route still inviting exploration.
 */
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private sealed class GuideSign
        {
            public Transform Root;
            public TextMesh Lettering;
            public MeshRenderer LettersRenderer;
            public Vector2 Size;
            public Vector3 LettersCenter;
        }

        private static readonly string[,] GuideCopy =
        {
            { "收集黄色萤火\n它们会一路跟随你", "Collect yellow fireflies\nThey travel with you" },
            { "躲避红色萤火\n碰到会失去一只黄萤", "Avoid red fireflies\nA hit costs one yellow light" },
            { "小心藏着的怪物\n树冠有蛛网，地底有食人花", "Beware of hidden creatures\nWebs above, hungry flowers below" },
            { "黄色萤火耗尽\n你会回到森林起点", "Run out of yellow lights\nand return to the forest entrance" },
            { "← 根渠迷宫\n机械林地 →", "← Root Maze\nMachine Grove →" },
            { "走出常规\n放出眼光", "Step beyond habit\nand look further" },
            { "机器真的都是对的吗？", "Are machines always right?" },
            { "一扇门挡住了去路\n想办法过去", "A gate blocks the path\nFind a way past it" },
            { "前方是三层根渠迷宫\n入内后地图会显示 10 秒", "Three levels of the Root Maze\nThe map opens for 10 seconds" },
            { "给上、下两个光位种光\n靠近光位，张手持续发声", "Plant at both the high and low sites\nOpen your hand and sustain a sound" },
            { "在心脏中央种光\n靠近光位，张手持续发声", "Plant a light in the machine's heart\nOpen your hand and sustain a sound" },
            { "收集藏品，换取金币\n心脏 3 · 大脑 4\n手机 5 · 电脑 6", "Collect items worth coins\nHeart 3 · Brain 4\nPhone 5 · Computer 6" },
            { "留意发光的宝箱\n靠近后按 B 或鼠标右键打开", "Look for glowing chests\nGet close and press B or right-click" }
        };

        private readonly GuideSign[] guideSigns = new GuideSign[GuideCopy.GetLength(0)];
        private Font guideFont;

        public void DrawGuideSigns(Journey journey, bool forest, bool ruins)
        {
            if (forest)
            {
                bool path = journey.Branch == ForestBranch.Shared;
                SetForestSign(journey, 0, 7, -2.52f, path);
                SetForestSign(journey, 1, 13, 2.48f, path);
                SetForestSign(journey, 2, 22, -2.48f, path);
                SetForestSign(journey, 3, 30, 2.48f, path);
                SetForestSign(journey, 4, 108.5f, 0, path, true);
                bool right = journey.Branch == ForestBranch.Right;
                SetForestSign(journey, 5, 121, 1.05f, right);
                SetForestSign(journey, 6, 145, -2.2f, right);
                SetForestSign(journey, 7, Journey.MachineDoorDepth - 4.2f, 2.1f, right);
                SetForestSign(journey, 8, Journey.MazeEntranceDepth - 2.45f, -1.35f,
                    journey.Branch == ForestBranch.Left && !journey.ForestMazeActive);
                SetForestSign(journey, 10, Journey.MachineHeartDepth + 2.2f, 1.63f,
                    right && journey.Phase == Stage.ForestPlanting);
                bool atRoots = journey.AtMazeRoots && journey.Phase == Stage.ForestPlanting;
                if (atRoots)
                {
                    Quaternion heading = Quaternion.Euler(0, journey.ForestMaze.RootHeading, 0);
                    SetGuideSign(9, MazeRootCenter(journey) + heading * new Vector3(-1.52f, 0, 2.24f), heading, false);
                }
                else HideGuideSign(9);
            }
            else
                for (int i = 0; i < 11; i++) HideGuideSign(i);

            bool entrance = ruins && journey.RuinRoomId == 0 && journey.Phase == Stage.RuinExplore;
            SetRuinGuide(11, 2.05f, -2.18f, journey, entrance);
            SetRuinGuide(12, 4.1f, 2.13f, journey, entrance);
        }

        private void SetForestSign(Journey journey, int index, float depth, float side, bool show, bool fork = false)
        {
            if (!show) { HideGuideSign(index); return; }
            float grade = (journey.ForestCenterAt(depth + .1f) - journey.ForestCenterAt(depth - .1f)) / .2f;
            Quaternion heading = Quaternion.Euler(0, Mathf.Atan(grade) * Mathf.Rad2Deg, 0);
            Vector3 at = ForestPoint(journey, 0, depth, 0) - Vector3.up * .54f;
            at.x = journey.ForestCenterAt(depth);
            at += heading * Vector3.right * side;
            if (journey.Branch == ForestBranch.Right && index == 10) at.y = ForestPoint(journey, 0, Journey.MachineHeartDepth, 0).y - .54f;
            SetGuideSign(index, at, heading * Quaternion.Euler(0, side * 7, 0), fork);
        }

        private void SetRuinGuide(int index, float depth, float side, Journey journey, bool show)
        {
            if (!show) { HideGuideSign(index); return; }
            var road = RuinTrack.Frame(journey.RuinRoomId, journey.RuinRoomLength, depth);
            SetGuideSign(index, road.Point(side, 0) + Vector3.up * RuinY, road.Rotation, false);
        }

        private void HideGuideSign(int index)
        {
            if (guideSigns[index] != null) guideSigns[index].Root.gameObject.SetActive(false);
        }

        private void SetGuideSign(int index, Vector3 at, Quaternion heading, bool fork)
        {
            Vector2 size = index == 11 ? new Vector2(2.72f, 1.56f) : new Vector2(2.64f, 1.05f);
            GuideSign sign = guideSigns[index] ?? (guideSigns[index] = BuildGuideSign(index, size, fork));
            sign.Root.SetPositionAndRotation(at, heading);
            sign.Root.gameObject.SetActive(true);
            string copy = GuideCopy[index, EchoesLocale.English ? 1 : 0];
            if (sign.Lettering.text == copy) return;
            guideFont.RequestCharactersInTexture(copy, sign.Lettering.fontSize);
            sign.Lettering.text = copy;
            Bounds measured = sign.LettersRenderer.localBounds;
            float fit = Mathf.Min((sign.Size.x - .40f) / Mathf.Max(.001f, measured.size.x),
                (sign.Size.y - .26f) / Mathf.Max(.001f, measured.size.y));
            float scale = Mathf.Min(1.35f, fit);
            sign.Lettering.transform.localScale = Vector3.one * scale;
            sign.Lettering.transform.localPosition = sign.LettersCenter - measured.center * scale;
        }

        private GuideSign BuildGuideSign(int index, Vector2 size, bool fork)
        {
            var node = Group("Trail guide " + index, lowerWorld.transform);
            float panelY = fork ? 3.24f : index == 5 ? 2.12f : 1.76f;
            var panel = new WorldMesh(meshes);
            Color board = new Color(.16f, .105f, .067f);
            Color brass = new Color(1f, .73f, .38f);
            Vector3 face = new Vector3(0, panelY, 0);
            panel.Box(face, new Vector3(size.x, size.y, .126f), Quaternion.identity, board);
            foreach (int side in new[] { -1, 1 })
            {
                float stake = size.x * (fork ? .46f : .33f) * side;
                panel.Box(new Vector3(stake, panelY * .43f, .062f), new Vector3(.085f, panelY * 1.04f, .13f), Quaternion.identity, board);
                panel.Box(face + new Vector3(0, side * (size.y * .5f - .055f), -.068f), new Vector3(size.x - .09f, .024f, .015f), Quaternion.identity, brass);
                panel.Box(face + new Vector3(side * (size.x * .5f - .056f), 0, -.068f), new Vector3(.021f, size.y - .10f, .014f), Quaternion.identity, brass);
            }
            Light(RuinPiece("Wood and brass guide", panel.Finish("Guide plaque"), glow, node).GetComponent<MeshRenderer>(),
                Color.white, .98f);

            var textNode = Group("Trail guide lettering", node);
            textNode.localPosition = face + new Vector3(0, 0, -.082f);
            var lettering = textNode.gameObject.AddComponent<TextMesh>();
            if (guideFont == null)
                guideFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "PingFang SC", "Arial" }, 58);
            lettering.font = guideFont;
            lettering.fontSize = 58;
            lettering.characterSize = .093f;
            lettering.anchor = TextAnchor.MiddleCenter;
            lettering.alignment = TextAlignment.Center;
            lettering.color = new Color(1f, .96f, .82f);
            var renderer = textNode.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = guideFont.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return new GuideSign { Root = node, Lettering = lettering, LettersRenderer = renderer, Size = size,
                LettersCenter = textNode.localPosition };
        }
    }
}
