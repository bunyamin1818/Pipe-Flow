using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PipeFlow.EditorTools
{
    public static class PipeFlowSetup
    {
        private const string MainScene = "Assets/Scenes/Main.unity";
        private const string BundleId = "com.example.pipeflow";

        [MenuItem("Pipe Flow/Prepare Unity Project")]
        public static void Prepare()
        {
            if (!File.Exists(MainScene)) CreateScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            PlayerSettings.productName = "Pipe Flow";
            PlayerSettings.companyName = "Your Studio";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Pipe Flow", "Project is ready. Open Main.unity and press Play. For Android, install Android Build Support through Unity Hub and switch Build Profiles to Android. Replace the example package ID before release.", "OK");
        }

        [MenuItem("Pipe Flow/Create or Repair Main Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .14f, .23f);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("Pipe Flow Game");
            root.AddComponent<PipeFlowGame>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            AssetDatabase.Refresh();
        }

        [MenuItem("Pipe Flow/Build Android Debug APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Android support missing", "Add Android Build Support, SDK & NDK Tools, and OpenJDK in Unity Hub.", "OK");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Select Android", "First select Android in File > Build Profiles and Switch Platform, then run this menu again.", "OK");
                return;
            }
            if (!File.Exists(MainScene)) CreateScene();
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            var request = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Builds/PipeFlow-debug.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(request);
            EditorUtility.DisplayDialog("Android build", report.summary.result == BuildResult.Succeeded
                ? "Debug APK created at Builds/PipeFlow-debug.apk"
                : "Build failed. Open the Unity Console for details.", "OK");
        }
    }
}
