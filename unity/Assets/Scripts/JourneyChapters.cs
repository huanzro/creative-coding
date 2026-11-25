using System;

namespace Echoes
{
    public sealed partial class Journey
    {
        private static readonly float[] StarAltitudes = { .18f, .57f, .27f };
        private float starReadyAt;
        private bool bridgeHelpUsed;
        public int Plants { get; private set; }
        public int ForestRoute { get; private set; }
        public RuinPath RuinResult { get; private set; }
        public int StarCount { get; private set; }
        public int StarBits { get; private set; }
        public int Withdrawals { get; private set; }
        public int Mistakes { get; private set; }
        public int ReflectionIndex => 0;
        public bool FastEcho => false;
        public float Beat => 1.38f;
        public float TimingWindow => .53f;
        public float CurrentStarHeight => StarAltitudes[Math.Min(StarCount, 2)];
        public bool RiskySuggestion => StarCount == 1 && (Mistakes & 2) == 0;
        public StarPath PathAt(int index) =>
            (uint)index < 3 ? (StarPath)((StarBits >> (index * 2)) & 3) : StarPath.Unfinished;

        private void StartSection()
        {
            gentleTime = loudTime = openTime = 0;
            bridgeHelpUsed = false;
            if (Lesson >= 0) PracticePrompt();
            else if (Chapter == 0) StartForest();
            else if (Chapter == 1) StartRuins();
            else StartStar();
        }

        private void PracticePrompt()
        {
            string[] prompts = {
                "看精灵捧光。先张手让系统看见你，再握拳半秒并张开，跟它一起放光。",
                "张手轻声点亮近灯，再用舒适音量点亮远灯。键盘可按 Z 和 X。",
                "按 A 看下一处落点，按 B 让精灵借一步跨过石阶。"
            };
            Change(Stage.Tutorial, prompts[Lesson]);
        }

        private void NextLesson()
        {
            Notify(JourneyCue.Gate);
            Lesson++;
            Revision++;
            PracticePrompt();
        }

        private void PracticeTouch(int option)
        {
            if (option == 0)
            {
                Observe();
                if (Lesson == 2 && !bridgeHelpUsed) Message = "石阶上的亮点是系统建议的落点。按 B 试着跨过去。";
                return;
            }
            if (Lesson != 2 || bridgeHelpUsed || !Authorize("路线已亮。再按一次 B 授权跨过石阶。")) return;
            bridgeHelpUsed = true;
            Flight.Assist();
            Signal(FireflyKind.Amber);
            Notify(JourneyCue.Assist);
            Age = 0;
            Message = "第三枚印记点亮了。跟着石阶上的绿芽往下走。";
        }

        private void UpdatePractice(float dt)
        {
            if (Lesson == 0)
            {
                if (Gesture == Hand.Open) openTime += dt;
                Progress = Clamp((openTime + Held) / .90f);
            }
            else if (Lesson == 1)
            {
                if (Voice >= .12f && Voice <= .46f) gentleTime += dt;
                if (Voice >= .53f && Voice <= .92f) loudTime += dt;
                Progress = (Clamp(gentleTime / .68f) + Clamp(loudTime / .68f)) * .5f;
                if (gentleTime >= .68f && loudTime >= .68f) NextLesson();
            }
            else if (bridgeHelpUsed)
            {
                Walk(dt, 1.2f);
                Progress = Clamp(Age / 1.36f);
                if (Progress >= 1) StartOpening();
            }
        }

        private void StartStar()
        {
            starReadyAt = PoseTime;
            if (StarCount == 3)
            {
                Change(Stage.StarsSeal, "星图只差你自己的一拍。握拳半秒，再张开手放出最后一颗星。");
                return;
            }
            string[] locations = { "下方", "中高处", "稍低处" };
            Change(Stage.StarsRoute, $"星图第 {StarCount + 1} 段。张手飞到{locations[StarCount]}的暖光高度，或用 A 看建议、B 授权。");
        }

        private void StarsTouch(int option)
        {
            if (Phase == Stage.StarsSeal)
            {
                if (option == 0) Observe();
                return;
            }
            if (Phase == Stage.StarsDelegated)
            {
                if (option == 0) { Observe(); return; }
                Withdrawals |= 1 << StarCount;
                Revision++;
                Notify(JourneyCue.Withdraw);
                StartStar();
                HintReady = true;
                Message = "授权已收回，这一段还由你决定。可以自己飞，也可以重新授权。";
                return;
            }
            if (option == 0)
            {
                Observe();
                Message = RiskySuggestion ? "机器建议红光侧。它闪了两下，这次判断不太稳；暖光侧是另一条路。"
                    : "建议线通向暖黄光。你可以自己靠近，也可以按 B 让它只接这一段。";
            }
            else if (Authorize(RiskySuggestion ? "先看双闪的建议线。再按 B 授权，执行中可再按 B 收回。"
                : "建议线已展开。再按 B 只授权接这一段。"))
            {
                Notify(JourneyCue.Assist);
                Change(Stage.StarsDelegated, "正在接这一段。完成前再按 B，可以把控制权收回来。");
            }
        }

        private void JoinStar(StarPath path)
        {
            StarBits |= (int)path << (StarCount * 2);
            StarCount++;
            Revision++;
            Signal(FireflyKind.Amber);
            Notify(JourneyCue.Star);
            Flight.Assist();
            StartStar();
        }

        private void UpdateStars(float dt)
        {
            switch (Phase)
            {
                case Stage.StarsRoute:
                    bool aligned = LastRelease > starReadyAt && Gesture == Hand.Open &&
                        Math.Abs(Flight.Height - CurrentStarHeight) < .145f;
                    heldProgress = Math.Max(0, heldProgress + dt * (aligned ? 1 : -.9f));
                    Progress = Clamp(heldProgress / .33f);
                    Walk(dt, .38f);
                    if (Progress >= 1)
                        JoinStar((Mistakes & (1 << StarCount)) != 0 ? StarPath.Corrected : StarPath.Manual);
                    break;
                case Stage.StarsDelegated:
                    Progress = Clamp(Age / 1.54f);
                    Walk(dt, 1.22f);
                    if (Progress < 1) break;
                    if (RiskySuggestion)
                    {
                        Mistakes |= 1 << StarCount;
                        Revision++;
                        Notify(JourneyCue.Misread);
                        Signal(FireflyKind.Scarlet);
                        StartStar();
                        Message = "建议线碰到红光，留下了一段折线。回到暖光，或用 A 看修正后的建议。";
                    }
                    else JoinStar((Mistakes & (1 << StarCount)) != 0 ? StarPath.Corrected : StarPath.Delegated);
                    break;
                case Stage.StarsSeal:
                    Progress = Clamp(Held / .41f);
                    break;
            }
        }
    }
}
