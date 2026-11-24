using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Echoes
{
    public static class EchoesBuild
    {
        private const string ScenePath = "Assets/Scenes/Journey.unity";
        private static readonly string Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string Website = Path.GetFullPath(Path.Combine(Project, "..", "dist"));
        private static readonly string[] Artwork = {
            "chapter1-0.png", "chapter1-1.png", "chapter1-2.png", "chapter2-0.png",
            "chapter3-0.png", "forest-sylph-long-dress.png"
        };
        private static readonly string[] BrowserModules = {
            "audio-input.js", "core.js", "volume-core.js", "exhibit-game.js",
            "exhibit-serial.js", "physical-output.js", "interaction-controls.js", "input-profiles.js"
        };

        [InitializeOnLoadMethod]
        private static void PrepareAtLaunch()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += () => Prepare(false);
        }

        [MenuItem("Echoes of Light/Prepare Original Art and Scene")]
        public static void Prepare() => Prepare(true);

        private static void Prepare(bool openScene)
        {
            string resourceFolder = Path.Combine(Application.dataPath, "Resources", "OriginalArt");
            if (!Directory.Exists(resourceFolder))
            {
                Directory.CreateDirectory(resourceFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            foreach (string name in Artwork)
            {
                string from = Path.Combine(Website, "assets", name);
                string to = Path.Combine(resourceFolder, name);
                if (!File.Exists(from))
                    throw new FileNotFoundException("Keep unity beside the repository's dist folder.", from);
                if (File.Exists(to) && File.GetLastWriteTimeUtc(to) == File.GetLastWriteTimeUtc(from)) continue;
                File.Copy(from, to, true);
                File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(from));
                string path = "Assets/Resources/OriginalArt/" + name;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            if (!File.Exists(Path.Combine(Project, ScenePath)))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                var player = new GameObject("EchoesPlayer");
                EditorSceneManager.MoveGameObjectToScene(player, scene);
                player.AddComponent<EchoesPlayer>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save the Echoes scene.");
                if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
            }
            if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (openScene && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Echoes of Light/Build Web Preview")]
        public static void Web()
        {
            Prepare(false);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Add WebGL Build Support for this Unity Editor in Unity Hub.");
            PlayerSettings.productName = "Echoes of Light";
            PlayerSettings.companyName = "Creative Coding";
            PlayerSettings.WebGL.template = "PROJECT:Echoes";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            string output = Path.Combine(Project, "Builds", "Web");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity Web build failed: " + report.summary.result);

            string browser = Path.Combine(output, "browser");
            Directory.CreateDirectory(browser);
            foreach (string name in BrowserModules)
                File.Copy(Path.Combine(Website, name), Path.Combine(browser, name), true);
            CopyDirectory(Path.Combine(Website, "vendor"), Path.Combine(browser, "vendor"));
            Debug.Log("Echoes Web build: " + output);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string name in Directory.GetFiles(source))
                File.Copy(name, Path.Combine(destination, Path.GetFileName(name)), true);
            foreach (string folder in Directory.GetDirectories(source))
                CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
        }
    }
}
