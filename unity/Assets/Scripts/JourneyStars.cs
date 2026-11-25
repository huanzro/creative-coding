/* ARTISTIC RATIONALE
 * The drawing records who made each mark. A recommendation can be inspected,
 * declined, or withdrawn, and silence leaves the unmarked sky intact.
 */
using System;

namespace Echoes
{
    internal static class StarMotifs
    {
        public const int Count = 8, PerGroup = 4, CloseMargin = 4;
        public const float UpperEdge = .38f;
        // These measurements index the original, arc-length sampled paths in StarSea/LightTrails.
        private static readonly float[] Lengths = { 1228.07f, 1525.65f, 1584.92f, 1724.86f, 1798.72f, 1839.72f, 1655.49f, 2885.20f };
        private static readonly int[] UpperShares = { 21, 21, 38, 25, 20, 22, 28, 36 };

        public static float Length(int id) => Lengths[id - 1];
        public static int UpperShare(int id) => UpperShares[id - 1];
        public static int Score(int id) => 100 - (int)Math.Round(Length(id) * .019 + UpperShare(id) * .66);
        public static bool Valid(int id) => id >= 1 && id <= Count;
    }

    public sealed partial class Journey
    {
        public const int StarTrailCount = 3;
        public const float StarHelpStart = .38f, StarHelpEnd = .66f, StarHoldSeconds = .60f;
        public const float StarArrivalSeconds = .62f, StarSettleSeconds = 1.28f, StarVoiceFloor = .08f;

        private int starMotifs, starHelpStarts, starHelpEnds;
        private bool starHelpUsed;
        public int StarCount { get; private set; }
        public int StarBits { get; private set; }
        public int Withdrawals { get; private set; }
        public bool InStars => Chapter == 2 && Lesson < 0;
        public int StarLeft { get; private set; }
        public int StarRight { get; private set; }
        public int StarMotif { get; private set; }
        public float StarProgress { get; private set; }
        public float StarFlow { get; private set; }
        public float StarMachineStart { get; private set; }
        public float StarMachineEnd { get; private set; }
        public bool StarDrawing => Phase == Stage.StarsDrawing || Phase == Stage.StarsDelegated;
        public bool StarCanChoose => Phase == Stage.StarsRoute || Phase == Stage.StarsDrawing && StarProgress == 0;
        private bool StarLoanAvailable => Phase == Stage.StarsDrawing && StarCount == 2 && !starHelpUsed &&
            StarProgress < StarHelpEnd - .035f;
        public bool StarCanBorrow => StarLoanAvailable && StarProgress >= StarHelpStart;
        public bool StarBorrowAhead => StarLoanAvailable && StarProgress >= .29f && StarProgress < StarHelpStart;
        public float StarAuthorization => StarCanBorrow && Gesture == Hand.Fist ? Clamp(Held / StarHoldSeconds) : 0;
        public int StarScoreMargin => StarLeft == 0 ? 0 : Math.Abs(StarMotifs.Score(StarLeft) - StarMotifs.Score(StarRight));
        public int StarRecommendation => StarScoreMargin <= StarMotifs.CloseMargin ? 0 :
            StarMotifs.Score(StarLeft) > StarMotifs.Score(StarRight) ? StarLeft : StarRight;
        public int StarMotifAt(int index) => (uint)index < StarTrailCount ? PackedAt(starMotifs, index, 4) : 0;
        public float StarMachineStartAt(int index) => PackedAt(starHelpStarts, index, 8) / 255f;
        public float StarMachineEndAt(int index) => PackedAt(starHelpEnds, index, 8) / 255f;
        public StarPath PathAt(int index) =>
            (uint)index < StarTrailCount ? (StarPath)((StarBits >> (index * 2)) & 3) : StarPath.Unfinished;

        public string StarInstruction
        {
            get
            {
                if (Phase == Stage.StarsWorkshop) return "你画下的光迹留在星海里。";
                if (Phase == Stage.StarsRest) return "这一笔会留在星海里。";
                if (Gesture == Hand.Missing) return "暂时看不到手。张手回到识别区域后可继续。";
                if (Phase == Stage.StarsRoute) return StarRecommendation == 0
                    ? "两条路线的得分接近。看过依据后，按 A 或 B 选一条。"
                    : "比较两条光路。它们来自两组图案，按 A 或 B 选一条。";
                if (Phase == Stage.StarsDelegated) return "发声带动蓝光前进。握拳即可收回这一段。";
                if (StarCanBorrow && Gesture == Hand.Fist)
                    return StarAuthorization >= 1 ? "张手授权这一小段，仍由你的声音推进。" : "再握拳片刻，可授权一小段蓝光。";
                if (Gesture == Hand.Fist) return "已提笔。张手后发声，接着描画。";
                if (StarCanBorrow) return "可以继续亲手画，也可以握拳片刻再张手，借给系统一小段。";
                if (StarBorrowAhead) return "青蓝虚线标出了可借给系统的一段。沿图案飞到那里再决定。";
                return "张手发声沿光迹前进，安静时停住，握拳提笔。";
            }
        }

        private static int PackedAt(int bits, int index, int size) => (bits >> (index * size)) & ((1 << size) - 1);
        private static int QuantizeStar(float point) => (int)Math.Round(point * 255);

        private void ResetStarHistory()
        {
            StarCount = StarBits = Withdrawals = starMotifs = starHelpStarts = starHelpEnds = 0;
            StarLeft = StarRight = 0;
            ClearStarTrace();
        }

        private void ClearStarTrace()
        {
            StarMotif = 0;
            StarProgress = StarFlow = StarMachineStart = StarMachineEnd = 0;
            starHelpUsed = false;
        }

        private void StartStar()
        {
            ClearStarTrace();
            StarLeft = StarRight = 0;
            if (StarCount >= StarTrailCount)
            {
                Change(Stage.StarsWorkshop, "");
                return;
            }
            PickStarPair();
            Change(Stage.StarsRoute, "");
        }

        private void PickStarPair()
        {
            // Reservoir sampling keeps the eligible pairs equally likely without allocating a list.
            int choices = 0;
            for (int a = 1; a <= StarMotifs.PerGroup; a++)
            for (int b = StarMotifs.PerGroup + 1; b <= StarMotifs.Count; b++)
            {
                if (StarWasDrawn(a) || StarWasDrawn(b)) continue;
                int margin = Math.Abs(StarMotifs.Score(a) - StarMotifs.Score(b));
                if (StarCount == 0 && margin <= StarMotifs.CloseMargin ||
                    StarCount == 1 && margin > StarMotifs.CloseMargin) continue;
                if (forestRandom.Next(++choices) != 0) continue;
                StarLeft = a;
                StarRight = b;
            }
        }

        private bool StarWasDrawn(int id)
        {
            for (int i = 0; i < StarCount; i++)
                if (StarMotifAt(i) == id) return true;
            return false;
        }

        private bool StarsTouch(int option)
        {
            if (!StarCanChoose) return false;
            int motif = option == 0 ? StarLeft : StarRight;
            if (!StarMotifs.Valid(motif) || motif == StarMotif) return false;
            StarMotif = motif;
            Change(Stage.StarsDrawing, "");
            Notify(JourneyCue.Observe);
            Revision++;
            return true;
        }

        private void SetStarHand(Hand previous, Hand next)
        {
            if (next != Hand.Open)
            {
                StarFlow = Voice = 0;
                if (Phase != Stage.StarsDelegated) return;
                Withdrawals |= 1 << StarCount;
                Change(Stage.StarsDrawing, "");
                Age = StarArrivalSeconds;
                Progress = StarProgress;
                Notify(JourneyCue.Withdraw);
                Revision++;
            }
            else if (previous == Hand.Fist && Held >= StarHoldSeconds && StarCanBorrow)
            {
                starHelpUsed = true;
                StarMachineStart = StarMachineEnd = StarProgress;
                Change(Stage.StarsDelegated, "");
                Progress = StarProgress;
                Notify(JourneyCue.Assist);
                Revision++;
            }
        }

        private void UpdateStars(float dt)
        {
            if (Phase == Stage.StarsRest)
            {
                if (Age >= StarSettleSeconds)
                {
                    StartStar();
                    Revision++;
                }
                return;
            }
            bool delegated = Phase == Stage.StarsDelegated;
            float previousFlow = StarFlow;
            StarFlow = StarDrawing && Gesture == Hand.Open && Voice > StarVoiceFloor &&
                (delegated || Age >= StarArrivalSeconds) ? Clamp((Voice - StarVoiceFloor) / (1 - StarVoiceFloor)) : 0;
            if (StarFlow == 0) return;
            if (previousFlow < .64f && StarFlow >= .64f && PoseTime - LastSpin >= 2.15f)
                LastSpin = LastRelease = PoseTime;
            float previous = StarProgress;
            float distance = (34 + 244 * StarFlow) * dt;
            StarProgress = Math.Min(1, previous + distance / StarMotifs.Length(StarMotif));
            Progress = StarProgress;
            Distance += distance / 180;
            if (delegated)
            {
                StarMachineEnd = Math.Min(StarHelpEnd, StarProgress);
                if (StarProgress >= StarHelpEnd)
                {
                    Change(Stage.StarsDrawing, "");
                    Age = StarArrivalSeconds;
                    Progress = StarProgress;
                    Revision++;
                }
            }
            if ((int)(StarProgress * 5) != (int)(previous * 5)) Revision++;
            if (StarProgress >= 1) FinishStar();
        }

        private void FinishStar()
        {
            StarPath path = StarMachineEnd > StarMachineStart ? StarPath.Delegated : StarPath.Manual;
            StarBits |= (int)path << (StarCount * 2);
            starMotifs |= StarMotif << (StarCount * 4);
            starHelpStarts |= QuantizeStar(StarMachineStart) << (StarCount * 8);
            starHelpEnds |= QuantizeStar(StarMachineEnd) << (StarCount * 8);
            StarCount++;
            ClearStarTrace();
            StarLeft = StarRight = 0;
            Change(Stage.StarsRest, "");
            Notify(JourneyCue.Star);
            Revision++;
        }

        private void RetryStar()
        {
            if (!StarDrawing) return;
            int motif = StarMotif;
            ClearStarTrace();
            StarMotif = motif;
            Withdrawals &= ~(1 << StarCount);
            Held = Charge = Voice = 0;
            Change(Stage.StarsDrawing, "");
            Revision++;
        }

        private static bool UsesLegacyStarCheckpoint(JourneyCheckpoint checkpoint)
        {
            if (checkpoint.version < 8) return true;
            // Both branches wrote v8, and the Ruins branch wrote v9 before the Star fields existed.
            return checkpoint.version < 10 && checkpoint.starPaths != 0 &&
                checkpoint.starMotifs == 0 && checkpoint.starHelpStarts == 0 && checkpoint.starHelpEnds == 0 &&
                checkpoint.starLeft == 0 && checkpoint.starRight == 0 && checkpoint.starMotif == 0 &&
                checkpoint.starProgress == 0 && checkpoint.starMachineStart == 0 && checkpoint.starMachineEnd == 0 &&
                !checkpoint.starHelpUsed;
        }

        private static bool ValidStarCheckpoint(JourneyCheckpoint checkpoint)
        {
            if (UsesLegacyStarCheckpoint(checkpoint)) return true;
            if ((checkpoint.starMotifs & ~0xFFF) != 0 || (checkpoint.starHelpStarts & ~0xFFFFFF) != 0 ||
                (checkpoint.starHelpEnds & ~0xFFFFFF) != 0 || !(checkpoint.starProgress >= 0 && checkpoint.starProgress < 1) ||
                !(checkpoint.starMachineStart >= 0 && checkpoint.starMachineStart <= checkpoint.starMachineEnd &&
                checkpoint.starMachineEnd <= checkpoint.starProgress && checkpoint.starMachineEnd <= StarHelpEnd)) return false;
            int ids = 0;
            bool empty = false;
            for (int i = 0; i < StarTrailCount; i++)
            {
                int id = PackedAt(checkpoint.starMotifs, i, 4);
                int first = PackedAt(checkpoint.starHelpStarts, i, 8);
                int last = PackedAt(checkpoint.starHelpEnds, i, 8);
                bool drawn = ((checkpoint.starPaths >> (i * 2)) & 3) != 0;
                if (drawn != (id != 0) || (drawn && (empty || !StarMotifs.Valid(id) || (ids & (1 << id)) != 0)) ||
                    first > last || last > QuantizeStar(StarHelpEnd) || (!drawn && last != 0)) return false;
                empty |= !drawn;
                if (drawn) ids |= 1 << id;
            }
            int a = checkpoint.starLeft, b = checkpoint.starRight;
            if (a == 0 && b == 0)
                return checkpoint.starMotif == 0 && checkpoint.starProgress == 0;
            return checkpoint.chapter == 2 && empty && a >= 1 && a <= StarMotifs.PerGroup &&
                b > StarMotifs.PerGroup && b <= StarMotifs.Count && (ids & ((1 << a) | (1 << b))) == 0 &&
                (checkpoint.starMotif == a || checkpoint.starMotif == b || checkpoint.starMotif == 0 && checkpoint.starProgress == 0);
        }

        private void RestoreStars(JourneyCheckpoint checkpoint)
        {
            StarBits = checkpoint.starPaths;
            Withdrawals = checkpoint.withdrawals & 7;
            while (StarCount < StarTrailCount && PathAt(StarCount) != StarPath.Unfinished) StarCount++;
            if (UsesLegacyStarCheckpoint(checkpoint))
            {
                // Give old completed routes stable shapes while keeping their recorded provenance.
                StarBits &= (1 << (StarCount * 2)) - 1;
                for (int i = 0; i < StarCount; i++)
                {
                    starMotifs |= (i * 2 + 1) << (i * 4);
                    if (PathAt(i) != StarPath.Delegated) continue;
                    starHelpStarts |= QuantizeStar(StarHelpStart) << (i * 8);
                    starHelpEnds |= QuantizeStar(StarHelpEnd) << (i * 8);
                }
                StartStar();
                return;
            }
            starMotifs = checkpoint.starMotifs;
            starHelpStarts = checkpoint.starHelpStarts;
            starHelpEnds = checkpoint.starHelpEnds;
            StartStar();
            if (checkpoint.starLeft == 0) return;
            StarLeft = checkpoint.starLeft;
            StarRight = checkpoint.starRight;
            StarMotif = checkpoint.starMotif;
            if (StarMotif == 0) return;
            StarProgress = checkpoint.starProgress;
            StarMachineStart = checkpoint.starMachineStart;
            StarMachineEnd = checkpoint.starMachineEnd;
            starHelpUsed = checkpoint.starHelpUsed;
            Change(Stage.StarsDrawing, "");
            Progress = StarProgress;
        }
    }
}
