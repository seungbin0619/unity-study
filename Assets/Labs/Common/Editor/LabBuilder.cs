using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Labs.Editor
{
    /// <summary>
    /// 실습용 Development 플레이어 빌드. 출력: Builds/&lt;백엔드&gt;/Labs.exe
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

        static void Build(ScriptingImplementation backend, string folder)
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
                    scenes = new[] { MainScene },
                    locationPathName = $"Builds/{folder}/Labs.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                if (PlayerSettings.GetScriptingBackend(target) != originalBackend)
                    PlayerSettings.SetScriptingBackend(target, originalBackend);
            }

            var summary = report.summary;
            Debug.Log($"[LabBuilder] {folder} {summary.result} — {summary.totalTime.TotalSeconds:F0}s, " +
                      $"errors {summary.totalErrors} → {summary.outputPath}");

            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
