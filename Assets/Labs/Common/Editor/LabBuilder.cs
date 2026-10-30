using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Labs.Editor
{
    /// <summary>
    /// 실습용 플레이어 빌드(기본 Development). 출력: Builds/&lt;백엔드&gt;/Labs.exe, Release는 Builds/IL2CPP-Release/Labs.exe
    /// 씬: Main + Assets/Labs 아래의 실습 전용 씬 (Build Settings는 바꾸지 않음)
    /// Scripting Backend를 바꿔야 하면 빌드 동안만 바꾸고 원래 값으로 복원한다.
    /// 배치모드: -executeMethod Labs.Editor.LabBuilder.BuildMono
    /// </summary>
    public static class LabBuilder
    {
        const string MainScene = "Assets/Main.unity";

        [MenuItem("Labs/Build/Mono (Development)")]
        public static void BuildMono() => Build(ScriptingImplementation.Mono2x, "Mono");

        // Windows Build Support (IL2CPP) 모듈이 설치되어 있어야 한다
        [MenuItem("Labs/Build/IL2CPP (Development)")]
        public static void BuildIL2CPP() => Build(ScriptingImplementation.IL2CPP, "IL2CPP");

        // 생성 코드·최적화가 Development 빌드와 다른지 비교할 때만 쓴다.
        // ProfilerRecorder 카운터가 동작하지 않으므로 할당 측정 실습에는 쓰지 않는다.
        [MenuItem("Labs/Build/IL2CPP (Release)")]
        public static void BuildIL2CPPRelease() => Build(ScriptingImplementation.IL2CPP, "IL2CPP-Release", BuildOptions.None);

        static void Build(ScriptingImplementation backend, string folder, BuildOptions options = BuildOptions.Development)
        {
            var target = NamedBuildTarget.Standalone;
            var originalBackend = PlayerSettings.GetScriptingBackend(target);

            BuildReport report;
            try
            {
                if (originalBackend != backend)
                {
                    Debug.Log($"[LabBuilder] Scripting Backend {originalBackend} → {backend} (빌드 후 복원)");
                    PlayerSettings.SetScriptingBackend(target, backend);
                }

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = CollectScenes(),
                    locationPathName = $"Builds/{folder}/Labs.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = options,
                });
            }
            finally
            {
                if (PlayerSettings.GetScriptingBackend(target) != originalBackend)
                {
                    PlayerSettings.SetScriptingBackend(target, originalBackend);
                    // 빌드 중 디스크에 저장된 바뀐 값이 남지 않게 복원한 값을 바로 저장한다
                    AssetDatabase.SaveAssets();
                }
            }

            var summary = report.summary;
            Debug.Log($"[LabBuilder] {folder} {summary.result} — {summary.totalTime.TotalSeconds:F0}s, " +
                      $"errors {summary.totalErrors} → {summary.outputPath}");

            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // Main 뒤에 실습 전용 씬(Assets/Labs 아래)을 붙인다. Build Settings 자체는 건드리지 않는다.
        static string[] CollectScenes()
        {
            var scenes = new List<string> { MainScene };
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Labs" }))
                scenes.Add(AssetDatabase.GUIDToAssetPath(guid));
            return scenes.ToArray();
        }
    }
}
