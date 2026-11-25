using System;
using System.Collections.Generic;

namespace Echoes
{
    public enum ForestFeatureKind { Amber, Scarlet, Web, Maw, Plant }
    public enum ForestTrapKind { None, Web, Maw }

    public sealed class ForestFeature
    {
        public int Id { get; }
        public ForestFeatureKind Kind { get; }
        public int Lane { get; }
        public float Depth { get; }
        public float Altitude { get; }
        public bool Collected { get; internal set; }

        internal ForestFeature(int id, ForestFeatureKind kind, int lane, float depth, float altitude)
        {
            Id = id;
            Kind = kind;
            Lane = lane;
            Depth = depth;
            Altitude = altitude;
        }
    }

    public sealed partial class Journey
    {
        private const int PlantCount = 3;
        private const float PlantTime = .56f;
        private const float RescueDuration = 2.4f;
        private const float RestartDuration = 3.2f;
        public const float ForestTransitionDuration = 5.2f;
        private static readonly float[] PlantDepths = { 25f, 54f, 84f };

        private readonly Random forestRandom;
        private readonly List<ForestFeature> forestFeatures = new List<ForestFeature>();
        private readonly ForestFeature[] forestSites = new ForestFeature[PlantCount];
        private ForestFeature forestRenewal;
        private int forestSeed, nextForestFeatureId, savedForestLights, savedForestLane;
        private float forestPlantTime;

        public bool InForest => Chapter == 0 && Lesson < 0 && !IsOpening;
        private bool IsForestSequence => Phase == Stage.ForestTrapped || Phase == Stage.ForestTransition;
        public bool ForestAirborne => InForest && (Flight.Height > .001f || Flight.Speed > .01f);
        public float ForestLane { get; private set; }
        public int ForestTargetLane { get; private set; }
        public float ForestSteering { get; private set; }
        public int ForestLights { get; private set; }
        public int ForestPlantsRequired => PlantCount;
        public IReadOnlyList<ForestFeature> ForestFeatures => forestFeatures;
        public ForestTrapKind ForestTrap { get; private set; }
        public float ForestTrapAltitude { get; private set; }
        public float ForestTrapDepth { get; private set; }
        public int ForestTrapLane { get; private set; }
        public bool ForestHasRescue { get; private set; }
        public float ForestPlantCharge => Clamp(forestPlantTime / PlantTime);
        public int ForestPlantTargetId => Phase == Stage.ForestPlanting ? forestSites[Plants].Id : -1;
        public float ForestWindFactor => !ForestAirborne ? 0 :
            Clamp((Flight.Height - .20f) / .64f) * (IsForestSequence ? .35f : 1f);
        public float GustWarning => ForestWindFactor;

        private void ResetForestHistory()
        {
            forestSeed = forestRandom.Next();
            forestFeatures.Clear();
            savedForestLights = savedForestLane = 0;
        }

        private ForestFeature AddForestFeature(ForestFeatureKind kind, int lane, float depth, float altitude)
        {
            var feature = new ForestFeature(++nextForestFeatureId, kind, lane, depth, altitude);
            forestFeatures.Add(feature);
            return feature;
        }

        private static float RandomOffset(Random random, float radius) => (float)(random.NextDouble() * 2 - 1) * radius;

        private void BuildForest()
        {
            var random = new Random(forestSeed);
            int side = random.Next(2) == 0 ? -1 : 1;
            forestFeatures.Clear();
            forestRenewal = null;
            nextForestFeatureId = 0;
            for (int section = 0; section < PlantCount; section++)
            {
                float start = section == 0 ? 0 : PlantDepths[section - 1];
                int entryLane = section == 0 ? 0 : forestSites[section - 1].Lane;
                int siteLane = section == 0 ? 0 : section == 1 ? side : -side;
                AddForestFeature(ForestFeatureKind.Amber, entryLane, start + 3.3f, .52f);
                AddForestFeature(ForestFeatureKind.Amber, random.Next(-1, 2), start + 7.4f + RandomOffset(random, .6f),
                    .22f + (float)random.NextDouble() * .49f);
                AddForestFeature(ForestFeatureKind.Scarlet, random.Next(-1, 2), start + 10.3f + RandomOffset(random, .6f),
                    .21f + (float)random.NextDouble() * .46f);
                AddForestFeature(ForestFeatureKind.Web, random.Next(-1, 2), start + 13.0f + RandomOffset(random, 1.2f), .66f);
                AddForestFeature(ForestFeatureKind.Amber, random.Next(-1, 2), start + 15.8f + RandomOffset(random, .7f),
                    .27f + (float)random.NextDouble() * .42f);
                AddForestFeature(ForestFeatureKind.Scarlet, random.Next(-1, 2), start + 18.3f + RandomOffset(random, .6f),
                    .30f + (float)random.NextDouble() * .39f);
                AddForestFeature(ForestFeatureKind.Maw, random.Next(-1, 2), start + 21.0f + RandomOffset(random, .9f), .06f);
                AddForestFeature(ForestFeatureKind.Amber, siteLane, PlantDepths[section] - 1.8f, .30f);
                forestSites[section] = AddForestFeature(ForestFeatureKind.Plant, siteLane, PlantDepths[section], .06f);
                forestSites[section].Collected = section < Plants;
            }
        }

        private void StartForest()
        {
            BuildForest();
            Distance = Plants == 0 ? 0 : PlantDepths[Plants - 1];
            ForestLights = savedForestLights;
            ForestLane = ForestTargetLane = savedForestLane;
            ForestSteering = forestPlantTime = 0;
            ForestTrap = ForestTrapKind.None;
            ForestHasRescue = false;
            Flight.Reset(0);
            if (Plants == PlantCount)
            {
                Flight.Reset(ForestRoute == 2 ? .55f : 0);
                BeginForestTransition(ForestRoute == 2 ? ForestTrapKind.Web : ForestTrapKind.Maw);
                return;
            }
            Change(Stage.ForestExplore, Plants == 0
                ? "握拳后张手起飞。A 左、B 右，发声加速。收集黄萤，避开红萤、蛛网和食人花。"
                : "已种下的根光留在林里。起飞，沿着下一处灯塔前行。");
        }

        private void ForestTouch(int option)
        {
            ForestTargetLane = Math.Max(-1, Math.Min(1, ForestTargetLane + (option == 0 ? -1 : 1)));
        }

        private void UpdateForest(float dt)
        {
            if (Phase == Stage.ForestTransition)
            {
                Progress = Clamp(Age / ForestTransitionDuration);
                if (Progress >= 1) Enter(1, false, false);
                return;
            }
            if (Phase == Stage.ForestTrapped)
            {
                UpdateForestCapture();
                return;
            }

            var site = forestSites[Plants];
            if (ForestAirborne)
            {
                float oldLane = ForestLane;
                ForestLane += (ForestTargetLane - ForestLane) * (1 - (float)Math.Exp(-dt * 5.6f));
                ForestSteering = Math.Max(-1, Math.Min(1, (ForestLane - oldLane) / (dt * 4.6f)));
                Distance = Math.Min(site.Depth, Distance + (4.35f + 4.15f * Voice) * dt);
            }
            else ForestSteering = 0;

            CollectForestContacts();
            if (Phase == Stage.ForestTrapped) return;

            if (Phase == Stage.ForestExplore)
            {
                Progress = Clamp(Distance / PlantDepths[PlantCount - 1]);
                if (Distance < site.Depth) return;
                Change(Stage.ForestPlanting, "沿 A / B 选到亮起的种光位。张手，轻声把一束黄萤送进根脉。");
            }
            PlantForestLight(dt, site);
        }

        private void CollectForestContacts()
        {
            foreach (var feature in forestFeatures)
            {
                if (feature.Collected || feature.Kind == ForestFeatureKind.Plant ||
                    Math.Abs(feature.Depth - Distance) > 1.02f || Math.Abs(feature.Lane - ForestLane) > .33f)
                    continue;

                if (feature.Kind == ForestFeatureKind.Amber)
                {
                    if (Math.Abs(Flight.Height - feature.Altitude) > .19f) continue;
                    feature.Collected = true;
                    ForestLights++;
                    Notify(JourneyCue.ForestAmber);
                    Signal(FireflyKind.Amber);
                    Message = Phase == Stage.ForestPlanting
                        ? "黄萤已归队。对齐种光位，张手轻声把光送进根脉。"
                        : "黄萤跟上了你。它可以种光，也可以救你脱困。";
                }
                else if (feature.Kind == ForestFeatureKind.Scarlet)
                {
                    if (Math.Abs(Flight.Height - feature.Altitude) > .14f) continue;
                    feature.Collected = true;
                    Signal(FireflyKind.Scarlet);
                    Notify(JourneyCue.ForestScarlet);
                    Flight.Deflect();
                    Message = "撞到了红萤，飞行被打断。换一条道，或从它上方掠过。";
                }
                else
                {
                    if ((feature.Kind == ForestFeatureKind.Web && Flight.Height < .52f) ||
                        (feature.Kind == ForestFeatureKind.Maw && Flight.Height > .20f)) continue;
                    feature.Collected = true;
                    BeginForestCapture(feature);
                    return;
                }
            }
        }

        private void BeginForestCapture(ForestFeature source)
        {
            ForestTrap = source.Kind == ForestFeatureKind.Web ? ForestTrapKind.Web : ForestTrapKind.Maw;
            ForestTrapAltitude = Flight.Height;
            ForestTrapDepth = source.Depth;
            ForestTrapLane = source.Lane;
            ForestHasRescue = ForestLights > 0;
            ForestSteering = 0;
            Flight.Reset(ForestTrapAltitude);
            string caught = ForestTrap == ForestTrapKind.Web ? "蛛丝缠住了披风。" : "食人花合起了花瓣。";
            Change(Stage.ForestTrapped, caught + (ForestHasRescue ? "等一束黄萤飞来解救。" : "没有黄萤能来解救，即将重新进入森林。"));
            Notify(JourneyCue.ForestCaptured);
        }

        private void UpdateForestCapture()
        {
            Progress = Clamp(Age / (ForestHasRescue ? RescueDuration : RestartDuration));
            if (Progress < 1) return;
            if (!ForestHasRescue)
            {
                Enter(0, false, true);
                Message = "森林重新排列了。先收集一束黄萤，再尝试从蛛网或花瓣旁飞过。";
                Notify(JourneyCue.ForestRestart);
                return;
            }
            ForestLights--;
            ForestTrap = ForestTrapKind.None;
            ForestHasRescue = false;
            Signal(FireflyKind.Amber);
            Notify(JourneyCue.ForestRescued);
            Change(Distance >= forestSites[Plants].Depth ? Stage.ForestPlanting : Stage.ForestExplore,
                "黄萤帮你脱困，消耗了一束光。可以继续飞了。");
        }

        private void PlantForestLight(float dt, ForestFeature site)
        {
            if (ForestLights == 0 && (forestRenewal == null || forestRenewal.Collected))
            {
                forestRenewal = AddForestFeature(ForestFeatureKind.Amber, site.Lane, site.Depth, .10f);
                Message = "这处根脉没有光。降到亮位的同一侧，等绕回来的黄萤靠近。";
            }

            bool aligned = ForestLights > 0 && Math.Abs(site.Lane - ForestLane) < .31f &&
                Gesture == Hand.Open && Voice >= .12f && Voice <= .50f;
            forestPlantTime = Math.Max(0, forestPlantTime + dt * (aligned ? 1 : -.95f));
            Progress = ForestPlantCharge;
            if (forestPlantTime < PlantTime) return;

            ForestLights--;
            Plants++;
            site.Collected = true;
            forestRenewal = null;
            savedForestLights = ForestLights;
            savedForestLane = site.Lane;
            forestPlantTime = 0;
            Notify(JourneyCue.Plant);
            Signal(FireflyKind.Amber);
            Revision++;
            if (Plants == PlantCount)
            {
                BeginForestTransition(Flight.Height >= .43f ? ForestTrapKind.Web : ForestTrapKind.Maw);
                return;
            }
            Change(Stage.ForestExplore, Plants == PlantCount - 1
                ? "第二束根光亮了。最后一处可掠过种光，也可以停在根边种下。"
                : "第一束根光扎下了。沿着下一处灯塔，换道收集黄萤。");
        }

        private void BeginForestTransition(ForestTrapKind kind)
        {
            ForestRoute = kind == ForestTrapKind.Web ? 2 : 1;
            ForestTrap = kind;
            ForestTrapAltitude = Flight.Height;
            ForestTrapDepth = Distance;
            ForestTrapLane = forestSites[PlantCount - 1].Lane;
            ForestSteering = 0;
            ForestHasRescue = false;
            Flight.Reset(ForestTrapAltitude);
            Change(Stage.ForestTransition, kind == ForestTrapKind.Web
                ? "第三束光唤醒了树冠。上方有东西拉紧了蛛丝。"
                : "第三束光沿着根脉渗进地底。根下的花瓣动了。");
        }
    }
}
