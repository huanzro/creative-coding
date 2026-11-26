using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private const string FireflyRangeKey = "EchoesUnity.Forest.FireflyRange";
        private static readonly string[] ForestDebugLabels =
        {
            "食人花", "蜘蛛", "迷宫终点", "右路一号入口 · 齿轮", "右路二号入口 · 机器人",
            "右路三号入口 · 预测池", "右路心脏"
        };
        private bool forestDebugSettings;
        private ForestDebugPoint? forestDebugPoint;

        private void DrawForestDebugSettings(GUIStyle label, GUIStyle button)
        {
            if (LocaleGui.Button(new Rect(354, 12, 148, 31), "返回游戏设置", button))
            {
                forestDebugSettings = false;
                return;
            }
            LocaleGui.Label(new Rect(20, 14, 315, 28), "森林调试断点", label);
            LocaleGui.Label(new Rect(20, 53, 484, 40), "点击立即跳转。按 R 重跑本断点，点「恢复」读取正常存档。", label);
            for (int i = 0; i < ForestDebugLabels.Length; i++)
                if (LocaleGui.Button(new Rect(20, 98 + 49 * i, 482, 37), ForestDebugLabels[i], button))
                    StartForestDebugPoint((ForestDebugPoint)i);
        }

        private void StartForestDebugPoint(ForestDebugPoint point)
        {
            if (!journey.JumpToForestDebugPoint(point)) return;
            forestDebugPoint = point;
            savedRevision = journey.Revision;
            paused = settings = mazeMapOpen = mazeMapRoute = false;
            System.Array.Clear(physicalTouchDown, 0, physicalTouchDown.Length);
            SaveTuning();
        }
    }
}
