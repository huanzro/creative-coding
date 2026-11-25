using System;

namespace Echoes
{
    public sealed partial class Journey
    {
        private const float FlightSafe = .39f;
        private static readonly float[] Gusts = { 2.05f, 4.76f };
        private static readonly float[] Reflections = { 1.03f, 1.53f, 1.12f };
        private static readonly float[] StarAltitudes = { .18f, .57f, .27f };
        private int gustIndex, windMisses, reflectionIndex;
        private float echoDelay, echoTolerance, quietFor, starReadyAt;
        private bool repairReady, usedSecondBeat, bridgeHelpUsed;
        public int Plants { get; private set; }
        public int ForestRoute { get; private set; }
        public RuinPath RuinResult { get; private set; }
        public int StarCount { get; private set; }
        public int StarBits { get; private set; }
        public int Withdrawals { get; private set; }
        public int Mistakes { get; private set; }
        public int ReflectionIndex => reflectionIndex;
        public bool FastEcho => echoTolerance < .35f;
        public float Beat => Phase == Stage.RuinRelease ? echoDelay : Reflections[Math.Min(reflectionIndex, 2)];
        public float TimingWindow => Phase == Stage.RuinRelease ? echoTolerance : .41f;
        public float CurrentStarHeight => StarAltitudes[Math.Min(StarCount, 2)];
        public bool RiskySuggestion => StarCount == 1 && (Mistakes & 2) == 0;
        public float GustWarning => Phase == Stage.ForestWind && gustIndex < Gusts.Length
            ? Clamp((Age - Gusts[gustIndex] + .92f) / .92f) : 0;
        public StarPath PathAt(int index) =>
            (uint)index < 3 ? (StarPath)((StarBits >> (index * 2)) & 3) : StarPath.Unfinished;

        private void StartSection()
        {
            gentleTime = loudTime = openTime = 0;
            bridgeHelpUsed = false;
            reflectionIndex = gustIndex = windMisses = 0;
            repairReady = usedSecondBeat = false;
            quietFor = 0;
            echoDelay = 1.38f;
            echoTolerance = .53f;
            if (Lesson >= 0) PracticePrompt();
            else if (Chapter == 0)
            {
                if (Plants >= 2) CrossForest();
                else if (Plants > 0) StartPlanting("第一处根光已经留下。到低处，张手轻声种下第二枚。");
                else Change(Stage.ForestRoute, "低处根系可以种光。张手轻声进入林间，或按 B 查看上方风廊。");
            }
            else if (Chapter == 1)
            {
                if (RuinResult != RuinPath.Unfinished)
                    Change(Stage.RuinCrossing, "门后的桥已对齐。");
                else Change(Stage.RuinGather, "第一闸。握拳把光留住，直到门边的琥珀纹亮起。");
            }
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

        private void ForestTouch(int option)
        {
            if (option == 0)
            {
                Observe();
                if (Phase == Stage.ForestRoute)
                    Message = "两处发暗的根系在低处。张手轻声进林间，或按 B 借道风廊。";
                else if (Phase == Stage.ForestPlant)
                    Message = Plants == 0 ? "从第一根亮起的根脉，可以看到后面的两处歇脚点。" :
                        "第一处已经保留。下一束暖光会沿第二根根脉点亮石桥。";
                else if (Phase == Stage.ForestWind)
                    Message = "风来之前草会伏低，红萤闪两下。看到信号就握拳护光。";
                return;
            }
            if (Phase == Stage.ForestRoute)
            {
                if (!Authorize("高处风廊已亮起。再按 B 借风走捷径，或张手轻声走低处。")) return;
                ForestRoute = 2;
                Flight.Assist();
                Notify(JourneyCue.Assist);
                Change(Stage.ForestWind, "进入风廊。两阵风会吹散暖光，红萤闪动时握拳。");
            }
            else if (Phase == Stage.ForestCrossing && !bridgeHelpUsed &&
                Authorize("光桥已显示。再按 B 帮精灵跨越前面一段。"))
            {
                bridgeHelpUsed = true;
                Flight.Assist();
                Notify(JourneyCue.Assist);
            }
        }

        private void StartPlanting(string message)
        {
            ForestRoute = 1;
            Change(Stage.ForestPlant, message);
        }

        private void CrossForest()
        {
            bridgeHelpUsed = false;
            Change(Stage.ForestCrossing, ForestRoute == 2
                ? "风停了，保留下来的黄光沿高处铺桥。"
                : "两根光脉已连通。沿着被点亮的石桥前行，A 可照见落点。");
        }

        private void UpdateForest(float dt)
        {
            bool gentle = Gesture == Hand.Open && Voice >= .12f && Voice <= .46f;
            switch (Phase)
            {
                case Stage.ForestRoute:
                    heldProgress = gentle ? heldProgress + dt : 0;
                    Progress = Clamp(heldProgress / .5f);
                    if (Progress >= 1) StartPlanting("张手、轻声，等精灵滑到低处。让黄萤沿根脉落地。");
                    break;
                case Stage.ForestPlant:
                    bool planting = gentle && Flight.Height <= FlightSafe;
                    heldProgress = Math.Max(0, heldProgress + dt * (planting ? 1 : -.55f));
                    Progress = Clamp(heldProgress / (Plants == 0 ? 1.37f : 1.72f));
                    Walk(dt, planting ? 1.85f : .14f);
                    if (Progress < 1) break;
                    Plants++;
                    Revision++;
                    Signal(FireflyKind.Amber);
                    Notify(JourneyCue.Plant);
                    if (Plants == 2) CrossForest();
                    else Change(Stage.ForestPlant, "第一束光已经扎根。沿着亮起的根，再种一束。");
                    break;
                case Stage.ForestWind:
                    Walk(dt, 2.55f);
                    Progress = Clamp(Age / 6.15f);
                    if (gustIndex < Gusts.Length && Age >= Gusts[gustIndex])
                    {
                        gustIndex++;
                        if (Gesture == Hand.Fist)
                        {
                            Signal(FireflyKind.Amber);
                            Message = gustIndex == 1 ? "第一阵风过去了，张手再次出发。" : "两阵风都已经掠过。";
                        }
                        else
                        {
                            windMisses++;
                            Signal(FireflyKind.Scarlet);
                            Flight.Deflect();
                            Message = "一束光被吹散了。风廊结束后可以回低处补种。";
                        }
                        Notify(JourneyCue.Gust);
                    }
                    if (Progress < 1) break;
                    Plants = Math.Max(Plants, 2 - windMisses);
                    Revision++;
                    if (Plants >= 2) CrossForest();
                    else StartPlanting("回到低处补种。已经留下的根光不必再种。");
                    break;
                case Stage.ForestCrossing:
                    Walk(dt, bridgeHelpUsed ? 3.8f : 2.4f);
                    heldProgress += dt * (bridgeHelpUsed ? 1.7f : 1);
                    Progress = Clamp(heldProgress / 3.55f);
                    if (Progress >= 1) Complete();
                    break;
            }
            if (Phase == Stage.ForestCrossing && Gesture != Hand.Missing)
                Fireflies.Step(dt, Voice, Flight.Height, Gesture, IsObserving);
        }

        private void RuinTouch(int option)
        {
            if (option == 0)
            {
                Observe();
                switch (Phase)
                {
                    case Stage.RuinMisread:
                        BeginRepair();
                        break;
                    case Stage.RuinSide:
                        Message = "观察亮环的缺口。第二束比其他两束晚一拍，光线碰到缺口时张手。";
                        break;
                    case Stage.RuinRepair:
                        Message = "你的动作是张手，系统记成了握拳。让它先听到安静，再握拳、张手纠正。";
                        break;
                    default:
                        Message = "第一闸看手势和声音，第二闸看松手的节拍。B 可以借过当前这一步。";
                        break;
                }
                return;
            }
            if (Phase == Stage.RuinCrossing || Phase == Stage.RuinSide ||
                !Authorize("已显示系统建议。再按 B，只代过当前这一步。")) return;
            Notify(JourneyCue.Assist);
            switch (Phase)
            {
                case Stage.RuinGather:
                case Stage.RuinVoice:
                    BeginRelease(false);
                    break;
                case Stage.RuinRelease:
                    BeginMisread();
                    break;
                case Stage.RuinMisread:
                case Stage.RuinRepair:
                    CrossRuin(RuinPath.Assisted);
                    break;
            }
        }

        private void BeginRelease(bool fast)
        {
            echoDelay = fast ? 1.03f : 1.40f;
            echoTolerance = fast ? .29f : .53f;
            usedSecondBeat = false;
            Change(Stage.RuinRelease, fast
                ? "第一闸通过。走窄窗，等第二闸的短回声落到标记，再张手。"
                : "第一闸通过。等第二闸的光条走到标记，再张手。");
        }

        private void CheckRelease()
        {
            if (Math.Abs(Age - echoDelay) <= echoTolerance)
            {
                BeginMisread();
                return;
            }
            MissRelease();
        }

        private void MissRelease()
        {
            if (!usedSecondBeat)
            {
                usedSecondBeat = true;
                echoDelay = 1.45f;
                echoTolerance = .66f;
                Age = Progress = 0;
                Message = "第一闸还亮着。第二闸给你一拍宽窗，重新握拳，在光条标记处张手。";
                return;
            }
            Change(Stage.RuinRelease, "第二闸重新响起。握稳，等光到标记再松手。");
        }

        private void BeginMisread()
        {
            Change(Stage.RuinMisread, "你张开了手，门却记成握拳。A 回看并修正。再握稳会走进误读打开的回廊。");
            Notify(JourneyCue.Misread);
            Signal(FireflyKind.Scarlet);
        }

        private void BeginRepair()
        {
            quietFor = 0;
            repairReady = false;
            Change(Stage.RuinRepair, "青影是张手，红影是系统记下的握拳。先安静片刻，再握拳、张手重做。");
            HintReady = true;
        }

        private void CheckReflection()
        {
            if (Math.Abs(Age - Beat) > .41f || Held < .32f)
            {
                Age = Progress = 0;
                Message = "这一束尚未对齐，前面的光仍在。重新握拳，等光到缺口再张手。";
                return;
            }
            reflectionIndex++;
            Signal(FireflyKind.Amber);
            if (reflectionIndex >= Reflections.Length) CrossRuin(RuinPath.Arcade);
            else
            {
                Notify(JourneyCue.Gate);
                Age = Progress = 0;
                Message = reflectionIndex == 1 ? "第一束已进石槽。第二束会晚一拍。" : "还剩最后一束。它回到了较短的节拍。";
            }
        }

        private void CrossRuin(RuinPath result)
        {
            RuinResult = result;
            Notify(result == RuinPath.Assisted ? JourneyCue.Assist : JourneyCue.Correct);
            Signal(FireflyKind.Amber);
            Revision++;
            Change(Stage.RuinCrossing, result == RuinPath.Arcade
                ? "三束反光合在一起，回廊留下另一份记录。绕回主桥。"
                : "门记下了这次选择。石板在脚下合拢，准备越桥。");
        }

        private void UpdateRuins(float dt)
        {
            switch (Phase)
            {
                case Stage.RuinGather:
                    Progress = Clamp(Held / .55f);
                    if (Progress >= 1)
                        Change(Stage.RuinVoice, "握稳，等琥珀音窗亮起再发声。Z 走宽窗，X 走窄窗。");
                    break;
                case Stage.RuinVoice:
                    Progress = Clamp(Age / 2.28f);
                    if (Gesture != Hand.Fist || Age >= 2.28f)
                        Change(Stage.RuinGather, "琥珀音窗合上了。握稳再等下一次，已经学会的动作会保留。");
                    else if (Age >= .35f && Voice >= .14f && Voice <= .95f)
                    {
                        Notify(JourneyCue.Gate);
                        BeginRelease(Voice >= .56f);
                    }
                    break;
                case Stage.RuinRelease:
                    Progress = Clamp(Age / (echoDelay + echoTolerance + .53f));
                    if (Age > echoDelay + echoTolerance + .28f && Gesture == Hand.Fist) MissRelease();
                    break;
                case Stage.RuinMisread:
                    Progress = Clamp(Held / .65f);
                    if (Progress >= 1)
                    {
                        reflectionIndex = 0;
                        Change(Stage.RuinSide, "旁门打开了。重新握拳，等第一束光到缺口再张手。");
                        Held = Charge = 0;
                        Walk(dt, 1);
                    }
                    break;
                case Stage.RuinRepair:
                    if (!repairReady)
                    {
                        quietFor = Voice <= .1f ? quietFor + dt : 0;
                        Progress = Clamp(quietFor / .67f);
                        if (Progress >= 1)
                        {
                            repairReady = true;
                            Message = "静音已被系统听见。握拳半秒，张手纠正上一拍。";
                        }
                    }
                    break;
                case Stage.RuinSide:
                    Progress = Clamp(Age / (Beat + TimingWindow + .48f));
                    if (Age > Beat + TimingWindow + .48f) Age = 0;
                    Walk(dt, .85f);
                    break;
                case Stage.RuinCrossing:
                    Walk(dt, 3.25f);
                    Progress = Clamp(Age / 4.4f);
                    if (Progress >= 1) Complete();
                    break;
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
