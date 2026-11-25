namespace Echoes
{
    public sealed partial class Journey
    {
        private bool bridgeHelpUsed;
        public int Plants { get; private set; }
        public int ForestRoute { get; private set; }
        public RuinPath RuinResult { get; private set; }
        public int ReflectionIndex => 0;
        public bool FastEcho => false;
        public float Beat => 1.38f;
        public float TimingWindow => .53f;

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

    }
}
