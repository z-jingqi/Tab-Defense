using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TapDefense.Editor
{
    public static class BuildAutomation
    {
        public const string MainScene = "Assets/Scenes/Main.unity";
        public const string ConfigPath = "Assets/Data/GameConfig.asset";

        [MenuItem("Tap Defense/Open Main Scene")]
        public static void OpenMain()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MainScene); }

        // Shared presentation settings only. Release identity, versions, signing and SDK
        // requirements stay in Player Settings and are never replaced by a build command.
        [MenuItem("Tap Defense/Configure Mobile Settings")]
        public static void ConfigureMobile()
        {
            ConfigurePortrait();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tap Defense/Configure/WebGL Baseline")]
        public static void ConfigureWebGL()
        {
            ConfigurePortrait();
            ConfigureManagedCode(NamedBuildTarget.WebGL);
            // Unity's Web default is WebGL2. Do not opt into experimental WebGPU.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tap Defense/Configure/iOS")]
        public static void ConfigureIOS()
        {
            ConfigurePortrait();
            ConfigureManagedCode(NamedBuildTarget.iOS);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tap Defense/Configure/Android")]
        public static void ConfigureAndroid()
        {
            ConfigurePortrait();
            ConfigureManagedCode(NamedBuildTarget.Android);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePortrait()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        private static void ConfigureManagedCode(NamedBuildTarget target)
        {
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
        }

        [MenuItem("Tap Defense/Validate Project")]
        public static void ValidateProject()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (!config) throw new BuildFailedException("GameConfig asset is missing.");
            string error = config.ValidateData();
            if (error != null) throw new BuildFailedException(error);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene)) throw new BuildFailedException("Main scene is missing.");
            foreach (string name in new[] { "Enemy", "PulseEffect", "DamageNumber", "Crystal" })
                if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"))
                    throw new BuildFailedException("Missing prefab: " + name);
            Debug.Log("Tap Defense: survival configuration, sprites, scene and prefabs validated.");
        }

        [MenuItem("Tap Defense/Export WebGL Baseline")]
        public static void ExportWebGL()
        {
            PrepareBuild(BuildTarget.WebGL);
            ConfigureWebGL();
            Build(BuildTarget.WebGL, "Builds/WebGL");
            Debug.Log("WebGL baseline exported. WeChat conversion, SDK configuration and device validation are separate steps.");
        }

        [MenuItem("Tap Defense/Export iOS Xcode Project")]
        public static void ExportIOS()
        {
            PrepareBuild(BuildTarget.iOS);
            ConfigureIOS();
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        [MenuItem("Tap Defense/Build Android APK")]
        public static void BuildAndroid()
        {
            PrepareBuild(BuildTarget.Android);
            ConfigureAndroid();
            bool previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                Build(BuildTarget.Android, "Builds/Android/TapDefense.apk");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            }
        }

        private static void PrepareBuild(BuildTarget target)
        {
            // Check prerequisites before changing project settings or writing build output.
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException("Install the Unity Hub build-support module for " + target);
            ValidateProject();
        }

        private static void Build(BuildTarget target, string output)
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Build failed: " + report.summary.result);
            Debug.Log("Build succeeded: " + output);
        }
    }
    public sealed class ValidateBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { BuildAutomation.ValidateProject(); }
    }
}
