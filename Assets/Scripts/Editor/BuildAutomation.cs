using System;
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

        [MenuItem("Tap Defense/Configure Mobile Settings")]
        public static void ConfigureMobile()
        {
            PlayerSettings.companyName = "Prototype Studio"; PlayerSettings.productName = "点击防线";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.prototype.tapdefense");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.prototype.tapdefense");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            AssetDatabase.SaveAssets();
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

        [MenuItem("Tap Defense/Export iOS Xcode Project")]
        public static void ExportIOS()
        {
            ConfigureMobile(); ValidateProject();
            Build(BuildTarget.iOS, "Builds/iOS");
        }
        [MenuItem("Tap Defense/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureMobile(); ValidateProject();
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/TapDefense.apk");
        }
        private static void Build(BuildTarget target, string output)
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"..",output));
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target)) throw new BuildFailedException("Install the Unity Hub build-support module for " + target);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene }, locationPathName = output, target = target, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Build failed: " + report.summary.result);
            Debug.Log("Build succeeded: " + Path.GetFullPath(output));
        }
    }
    public sealed class ValidateBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { BuildAutomation.ValidateProject(); }
    }
}
