#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    internal static class MacPlayerSmoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void VerifyBundle()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-smoke") < 0) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!Application.isBatchMode || !scene.isLoaded || scene.name != "Journey" ||
                    UnityEngine.Object.FindObjectOfType<EchoesPlayer>() == null)
                    throw new InvalidOperationException("Journey scene with an Echoes player did not load in batch mode");

                if (Resources.Load<Shader>("StarSea/StarTrailInk") == null ||
                    Resources.Load<Shader>("StarSea/FinaleArtwork") == null ||
                    Resources.Load<TextAsset>("StarSea/LightTrails") == null)
                    throw new InvalidOperationException("The Star Sea drawing resources did not load");

                string camera = Path.Combine(Application.streamingAssetsPath, "EchoesCamera");
                foreach (string asset in new[] { "index.html", "index-tip.mjs", "camera.mjs", "core.js",
                    "vendor/hand_landmarker.task", "vendor/wasm/vision_wasm_internal.wasm" })
                {
                    var file = new FileInfo(Path.Combine(camera, asset));
                    if (file.Exists && file.Length > 0) continue;
                    throw new FileNotFoundException("Missing hand camera asset", asset);
                }
                Debug.Log("ECHOES_SMOKE_READY scene=Journey mode=headless-bootstrap");
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Application.Quit(3);
            }
        }
    }
}
#endif
