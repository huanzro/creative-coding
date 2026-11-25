using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private bool mazeMapOpen, mazeMapRoute;

        private void UpdateMazeMap(bool keyboard)
        {
            if (!paused) journey.AdvanceMazeFloorClock(Time.unscaledDeltaTime);
            if (!journey.ForestMazeActive)
            {
                mazeMapOpen = mazeMapRoute = false;
                return;
            }
            if (keyboard && !settings && !characterOnly && Input.GetKeyDown(KeyCode.F2))
                mazeMapOpen = !mazeMapOpen;
        }

        private void DrawMazeMapControls()
        {
            var command = view.DrawMazeMap(journey, mazeMapOpen, mazeMapRoute);
            if (command == MazeMapCommand.Toggle) mazeMapOpen = !mazeMapOpen;
            if (command == MazeMapCommand.Route) mazeMapRoute = !mazeMapRoute;
        }
    }
}
