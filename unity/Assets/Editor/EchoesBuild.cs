using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    public static class EchoesBuild
    {
        private const string ScenePath = "Assets/Scenes/Journey.unity";
        private static readonly string Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        internal static readonly string Website = Path.GetFullPath(Path.Combine(Project, "..", "dist"));
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
                // Unity starts a fresh project with an untitled scene. It cannot
                // add another scene until that scene is saved or replaced.
                bool hasUntitledScene = false;
                for (int index = 0; index < SceneManager.sceneCount; index++)
                    hasUntitledScene |= string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path);
                bool replace = Application.isBatchMode || openScene || hasUntitledScene;
                if (replace && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replace ? NewSceneMode.Single : NewSceneMode.Additive);
                var player = new GameObject("EchoesPlayer");
                if (player.scene != scene) SceneManager.MoveGameObjectToScene(player, scene);
                player.AddComponent<EchoesPlayer>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save the Echoes scene.");
                if (!replace) EditorSceneManager.CloseScene(scene, true);
            }
            if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (openScene && SceneManager.GetActiveScene().path != ScenePath &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
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

        [MenuItem("Echoes of Light/Build Windows Preview")]
        public static void Windows() => BuildWindows(false);

        [MenuItem("Echoes of Light/Build Windows Debug")]
        public static void WindowsDebug() => BuildWindows(true);

        public static void WindowsRelease()
        {
            BuildWindows(false);
            BuildWindows(true);
        }

        private static void BuildWindows(bool development)
        {
            Prepare(false);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support for this Unity Editor.");
            ConfigureDesktop();
            string output = Path.Combine(Project, "Builds", development ? "WindowsDebug" : "Windows", "Echoes of Light.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity Windows build failed: " + report.summary.result);
            Debug.Log("Echoes Windows build: " + output);
        }

        [MenuItem("Echoes of Light/Build macOS Universal")]
        public static void Mac()
        {
            const BuildTarget target = BuildTarget.StandaloneOSX;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install Mac Build Support (Mono) for this Unity Editor.");

            Prepare(false);
            ConfigureDesktop();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "org.creativecoding.echoesoflight");
            PlayerSettings.iOS.microphoneUsageDescription = "Echoes of Light uses the microphone locally to control flight speed.";
            string platform = BuildPipeline.GetBuildTargetName(target);
            EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", OSArchitecture.x64ARM64.ToString().ToLowerInvariant());
            EditorUserBuildSettings.SetPlatformSettings(platform, "CreateXcodeProject", "false");

            string output = Path.Combine(Project, "Builds", "macOS", "Echoes of Light.app");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = output,
                targetGroup = BuildTargetGroup.Standalone,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity macOS build failed: " + report.summary.result);
            Debug.Log("Echoes macOS build: " + output);
        }

        private static void ConfigureDesktop()
        {
            PlayerSettings.productName = "Echoes of Light";
            PlayerSettings.companyName = "Creative Coding";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
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

    internal sealed class DesktopCameraBuildAssets : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            BuildTarget target = context.BuildPlayerOptions.target;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64 &&
                target != BuildTarget.StandaloneOSX) return;

            context.AddAdditionalPathToStreamingAssets(Path.Combine(EchoesBuild.Website, "core.js"), "EchoesCamera/core.js");
            context.AddAdditionalPathToStreamingAssets(Path.Combine(EchoesBuild.Website, "vendor"), "EchoesCamera/vendor");
        }
    }
}
