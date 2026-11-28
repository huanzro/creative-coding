using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private bool mazeMapOpen, mazeMapRoute;
        private ForestMaze previewedMaze;
        private float mazePreviewRemaining;

        private void UpdateMazeMap(bool keyboard)
        {
            if (!paused) journey.AdvanceMazeFloorClock(Time.unscaledDeltaTime);
            if (!journey.ForestMazeActive)
            {
                mazeMapOpen = mazeMapRoute = false;
                previewedMaze = null;
                mazePreviewRemaining = 0;
                return;
            }
            if (journey.ForestMazeWalkArea && previewedMaze != journey.ForestMaze)
            {
                previewedMaze = journey.ForestMaze;
                mazePreviewRemaining = 10;
            }
            else if (!paused) mazePreviewRemaining = Mathf.Max(0, mazePreviewRemaining - Time.unscaledDeltaTime);
            if (keyboard && !settings && !characterOnly && Input.GetKeyDown(KeyCode.F2))
                ToggleMazeMap();
        }

        private void DrawMazeMapControls()
        {
            var command = view.DrawMazeMap(journey, mazeMapOpen, mazeMapRoute, mazePreviewRemaining);
            if (command == MazeMapCommand.Toggle) ToggleMazeMap();
            if (command == MazeMapCommand.Route) mazeMapRoute = !mazeMapRoute;
        }

        private void ToggleMazeMap()
        {
            mazeMapOpen = !mazeMapOpen && mazePreviewRemaining <= 0;
            mazePreviewRemaining = 0;
        }
    }
}
