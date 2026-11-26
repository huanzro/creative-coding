using UnityEngine;

namespace Echoes
{
    internal enum MazeMapCommand { None, Toggle, Route }

    public sealed partial class EchoesView
    {
        private static readonly string[] MazeLevels = { "一层", "二层", "三层" };
        private static readonly Color MapInk = new Color(.035f, .075f, .084f, .98f);
        private static readonly Color MapHall = new Color(.38f, .50f, .51f);
        private static readonly Color MapRamp = new Color(.68f, .72f, .77f);
        private static readonly Color MapPlayer = new Color(.45f, .95f, .83f);
        private static readonly Color MapExit = new Color(1, .77f, .38f);
        private readonly Rect[] mazeMapBounds = new Rect[3];
        private ForestMaze mappedMaze;
        private MazeDirections mazeDirections;
        private ForestMaze guideMaze;
        private MazeDirections guideDirections;
        private int guideFloor = -1;
        private GUIStyle mapTitle, mapLabel, mapMark;
        private GUIStyle guideArrow, guideLabel;

        private void DrawMazeGuide(Journey journey)
        {
            ForestMaze maze = journey.ForestMaze;
            int floor = journey.ForestMazeFloor;
            if (guideMaze != maze || guideFloor != floor)
            {
                guideMaze = maze;
                guideFloor = floor;
                guideDirections = new MazeDirections(maze, floor);
            }
            Vector3 feet = new Vector3(journey.ForestMazeX, journey.ForestMazeY, journey.ForestMazeZ);
            int[] route = guideDirections.RouteFrom(journey.ForestMazeNode, journey.ForestMazeTarget,
                feet.x, feet.y, feet.z, out _);
            ForestMazeNode next = MazeRouteNext(maze, feet, route);
            Vector3 waypoint = new Vector3(next.X, next.Y, next.Z);
            Vector3 beacon = Vector3.MoveTowards(feet, waypoint, 5.2f) + Vector3.up * 3.65f;
            Vector3 viewport = world.ProjectToViewport(beacon);

            bool offscreen = viewport.z <= .05f || viewport.x < .085f || viewport.x > .915f;
            Vector3 delta = waypoint - feet;
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float turn = Mathf.DeltaAngle(journey.ForestMazeHeading, bearing);
            string arrow = !offscreen && Mathf.Abs(turn) < 35f ? "↑" : turn >= 0 ? "→" : "←";
            Vector2 at;
            if (offscreen)
                at = new Vector2(turn >= 0 ? Width - 85 : 85, Height * .55f);
            else
            {
                at = new Vector2(viewport.x * Width, Mathf.Clamp((1 - viewport.y) * Height, 130, 580));
                Vector2 actor = Vector2.Scale(world.ActorScreen, new Vector2(Width, Height));
                if (Mathf.Abs(at.x - actor.x) < 96f)
                    at.y = Mathf.Min(at.y, Mathf.Max(130, actor.y - .80f * world.ActorBody - 49f));
            }

            PrepareInterface();
            if (guideArrow == null)
            {
                guideArrow = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 25, wordWrap = false };
                guideLabel = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = false };
                guideArrow.normal.textColor = guideLabel.normal.textColor = MapPlayer;
            }
            float shimmer = .5f + .5f * Mathf.Sin(journey.PoseTime * 3.1f);
            Paint(new Rect(at.x - 47, at.y - 47, 94, 94), Opacity(MapPlayer, .28f + .19f * shimmer), glow);
            Paint(new Rect(at.x - 20, at.y - 20, 40, 40), Opacity(MapInk, .86f), glow);
            Paint(new Rect(at.x - 24, at.y - 24, 48, 48), Opacity(MapPlayer, .84f), ring);
            LocaleGui.Label(new Rect(at.x - 20, at.y - 20, 40, 40), arrow, guideArrow);
            Paint(new Rect(at.x - 41, at.y + 24, 82, 23), MapInk);
            string destination = floor < MazeLevels.Length - 1 ? "去" + MazeLevels[floor + 1] : "去终点";
            LocaleGui.Label(new Rect(at.x - 41, at.y + 24, 82, 23), destination, guideLabel);
        }

        internal MazeMapCommand DrawMazeMap(Journey journey, bool open, bool showRoute)
        {
            PrepareInterface();
            Matrix4x4 matrix = BeginFrame(StageViewport);
            var command = MazeMapCommand.None;
            string toggle = open ? "收起地图" : "迷宫地图";
            if (Application.platform != RuntimePlatform.WebGLPlayer) toggle += " · F2";
            if (LocaleGui.Button(new Rect(894, 27, 153, 36), toggle, button))
                command = MazeMapCommand.Toggle;

            if (open)
            {
                PrepareMazeMap(journey.ForestMaze);
                Paint(new Rect(822, 74, 429, 564), MapInk);
                LocaleGui.Label(new Rect(840, 86, 201, 30), "根渠迷宫", mapTitle);
                if (LocaleGui.Button(new Rect(1135, 87, 98, 28), showRoute ? "隐藏路线" : "指路到出口", button))
                    command = MazeMapCommand.Route;
                LocaleGui.Label(new Rect(840, 116, 388, 23), "青色箭头是你 · 虚线是上下层坡道", mapLabel);
                DrawMazeFloors(journey, showRoute);
            }
            EndFrame(matrix);
            return command;
        }

        private void PrepareMazeMap(ForestMaze maze)
        {
            if (mapTitle == null)
            {
                mapTitle = new GUIStyle(title) { fontSize = 22 };
                mapLabel = new GUIStyle(body) { fontSize = 15, wordWrap = true };
                mapMark = new GUIStyle(mapLabel) { fontSize = 13, wordWrap = false };
            }
            if (maze == mappedMaze) return;
            mappedMaze = maze;
            mazeDirections = new MazeDirections(maze);
            for (int level = 0; level < mazeMapBounds.Length; level++)
            {
                Vector2 minimum = Vector2.one * float.PositiveInfinity;
                Vector2 maximum = Vector2.one * float.NegativeInfinity;
                foreach (ForestMazeNode node in maze.Nodes)
                {
                    if (node.Level != level) continue;
                    IncludeMazePoint(MapCoordinates(node), ref minimum, ref maximum);
                    foreach (ForestMazeEdge edge in maze.Connections(node.Id))
                    {
                        ForestMazeNode other = maze.Nodes[edge.Other(node.Id)];
                        if (other.Level != level)
                            IncludeMazePoint((MapCoordinates(node) + MapCoordinates(other)) * .5f, ref minimum, ref maximum);
                    }
                }
                mazeMapBounds[level] = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            }
        }

        private void DrawMazeFloors(Journey journey, bool showRoute)
        {
            ForestMaze maze = journey.ForestMaze;
            Vector3 current = new Vector3(journey.ForestMazeX, journey.ForestMazeY, journey.ForestMazeZ);
            int playerFloor = journey.ForestMazeFloor;
            float remaining = 0;
            int[] route = showRoute ? mazeDirections.RouteFrom(journey.ForestMazeNode, journey.ForestMazeTarget,
                current.x, current.y, current.z, out remaining) : null;

            for (int floor = 0; floor < mazeMapBounds.Length; floor++)
            {
                var tile = new Rect(839, 145 + floor * 130, 395, 121);
                bool currentFloor = floor == playerFloor;
                Color surface = currentFloor ? new Color(.092f,.18f,.194f) : new Color(.062f,.13f,.139f);
                Paint(tile, surface);
                LocaleGui.Label(new Rect(tile.x + 11, tile.y + 7, 100, 21),
                    currentFloor ? MazeLevels[floor] + " · 当前" : MazeLevels[floor], mapLabel);
                var drawing = new Rect(tile.x + 99, tile.y + 13, tile.width - 150, tile.height - 26);
                foreach (ForestMazeEdge edge in maze.Edges)
                    DrawMazeMapEdge(edge, maze, journey.ForestMazeNode, journey.ForestMazeTarget, floor, drawing, route);
                DrawMazeEnd(maze.Nodes[maze.Entry], floor, drawing, tile, "入口", MapPlayer);
                DrawMazeEnd(maze.Nodes[maze.RootChamber], floor, drawing, tile, "出口", MapExit);
                if (!currentFloor) continue;
                Vector2 position = ProjectMazePoint(new Vector2(current.x,current.z), floor, drawing);
                if (route != null && route.Length > 0 && maze.Nodes[route[0]].Level == floor)
                    MapLine(position, ProjectMazePoint(MapCoordinates(maze.Nodes[route[0]]),floor,drawing), 2.8f, MapExit);
                DrawMazePlayer(position, journey.ForestMazeHeading);
            }

            bool atExit = journey.ForestMazeNode == maze.RootChamber;
            if (atExit)
                LocaleGui.Label(new Rect(840, 537, 392, 75), "已到古根空地。找两处高低不同的光位，靠近后张手持续发声，让两处都亮起来。", mapLabel);
            else if (route != null)
                LocaleGui.Label(new Rect(840, 537, 392, 75), MazeRouteInstruction(journey, current, route, remaining), mapLabel);
            else
                LocaleGui.Label(new Rect(840, 537, 392, 75), "出口在三层古根空地。到达后，请给上下两处种光。", mapLabel);
        }

        private void DrawMazeMapEdge(ForestMazeEdge edge, ForestMaze maze, int from, int target, int floor, Rect bounds, int[] route)
        {
            ForestMazeNode a = maze.Nodes[edge.A], b = maze.Nodes[edge.B];
            if (a.Level != floor && b.Level != floor) return;
            bool ramp = a.Level != b.Level;
            Vector2 aa = MapCoordinates(a), bb = MapCoordinates(b);
            if (a.Level != floor) aa = (aa+bb)*.5f;
            if (b.Level != floor) bb = (aa+bb)*.5f;
            aa = ProjectMazePoint(aa,floor,bounds);
            bb = ProjectMazePoint(bb,floor,bounds);
            bool current = edge.A == from && edge.B == target || edge.B == from && edge.A == target;
            bool planned = RouteIncludes(route, edge);
            Color color = planned ? MapExit : current ? MapPlayer : ramp ? MapRamp : MapHall;
            float width = planned || current ? 3.1f : 1.7f;
            if (!ramp)
            {
                MapLine(aa,bb,width,color);
                return;
            }
            for (int i = 0; i < 4; i++)
                MapLine(Vector2.Lerp(aa,bb,i*.25f),Vector2.Lerp(aa,bb,i*.25f+.16f),width,color);
            int otherLevel = a.Level == floor ? b.Level : a.Level;
            Vector2 end = a.Level == floor ? bb : aa;
            Paint(new Rect(end.x-2,end.y-2,4,4),color);
            var mark = new Rect(Mathf.Clamp(end.x + 5, bounds.xMin - 12, bounds.xMax + 8), end.y - 10, 44, 20);
            LocaleGui.Label(mark, otherLevel > floor ? "↑" + MazeLevels[otherLevel] : "↓" + MazeLevels[otherLevel], mapMark);
        }

        private void DrawMazeEnd(ForestMazeNode node, int floor, Rect bounds, Rect tile, string text, Color color)
        {
            if (node.Level != floor) return;
            Vector2 spot = ProjectMazePoint(MapCoordinates(node),floor,bounds);
            Paint(new Rect(spot.x-4,spot.y-4,8,8),color);
            LocaleGui.Label(new Rect(Mathf.Clamp(spot.x+9,tile.x+5,tile.xMax-39),
                Mathf.Clamp(spot.y-12,tile.y+2,tile.yMax-20),44,20),text,mapMark);
        }

        private static void DrawMazePlayer(Vector2 at, float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(radians),-Mathf.Cos(radians));
            var across = new Vector2(-forward.y,forward.x);
            Vector2 tip = at + forward * 12;
            Paint(new Rect(at.x-5,at.y-5,10,10),MapInk);
            MapLine(at-forward*3,tip,4.3f,MapPlayer);
            MapLine(tip,at+across*5+forward*6,3.3f,MapPlayer);
            MapLine(tip,at-across*5+forward*6,3.3f,MapPlayer);
        }

        private static string MazeRouteInstruction(Journey journey, Vector3 current, int[] route, float remaining)
        {
            ForestMazeNode next = MazeRouteNext(journey.ForestMaze, current, route);
            Vector2 delta = new Vector2(next.X-current.x,next.Z-current.z);
            float turn = Mathf.DeltaAngle(journey.ForestMazeHeading,
                Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
            string turnLabel = Mathf.Abs(turn) < 14 ? "沿当前朝向前进" :
                (turn < 0 ? "向左转约 " : "向右转约 ") + Mathf.RoundToInt(Mathf.Abs(turn)) + "°";
            return $"沿金线前往出口 · 约 {Mathf.CeilToInt(remaining)} 米\n{turnLabel}，朝向标出的通道。";
        }

        private static ForestMazeNode MazeRouteNext(ForestMaze maze, Vector3 current, int[] route)
        {
            ForestMazeNode first = maze.Nodes[route[0]];
            Vector2 delta = new Vector2(first.X - current.x, first.Z - current.z);
            return route.Length > 1 && delta.sqrMagnitude < 1.5f ? maze.Nodes[route[1]] : first;
        }

        private static bool RouteIncludes(int[] route, ForestMazeEdge edge)
        {
            if (route == null) return false;
            for (int i = 1; i < route.Length; i++)
                if (route[i-1] == edge.A && route[i] == edge.B || route[i-1] == edge.B && route[i] == edge.A) return true;
            return false;
        }

        private static Vector2 MapCoordinates(ForestMazeNode node) => new Vector2(node.X, node.Z);

        private static void IncludeMazePoint(Vector2 point, ref Vector2 minimum, ref Vector2 maximum)
        {
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }

        private Vector2 ProjectMazePoint(Vector2 point, int floor, Rect bounds)
        {
            Rect graph = mazeMapBounds[floor];
            float scale = Mathf.Min(bounds.width / Mathf.Max(1, graph.width), bounds.height / Mathf.Max(1, graph.height));
            Vector2 offset = (point - graph.center) * scale;
            return bounds.center + new Vector2(offset.x,-offset.y);
        }

        private static void MapLine(Vector2 start, Vector2 end, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 delta = end-start;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = RotateAbout(previous,start,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            Paint(new Rect(start.x,start.y-width*.5f,delta.magnitude,width), color);
            GUI.matrix = previous;
        }
    }
}
