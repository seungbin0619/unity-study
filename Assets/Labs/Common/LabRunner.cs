using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Labs
{
    /// <summary>실습 하나. 프레임을 넘길 때는 Awaitable.NextFrameAsync() 등을 await한다.</summary>
    public interface ILab
    {
        Awaitable Run(LabContext ctx);
    }

    /// <summary>실습 ID 등록. 리플렉션으로만 생성되므로 클래스에 [Preserve]도 같이 붙인다.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class LabAttribute : Attribute
    {
        public readonly string Id;
        public LabAttribute(string id) => Id = id;
    }

    /// <summary>실습이 결과 행을 쌓는 곳. 측정 구간 밖에서만 호출한다.</summary>
    public sealed class LabContext
    {
        public readonly string LabId;
        internal readonly List<string[]> Rows = new List<string[]>();

        internal LabContext(string labId) => LabId = labId;

        public void Record(string condition, string metric, string value) =>
            Rows.Add(new[] { condition, metric, value });
    }

    /// <summary>
    /// Main 씬에 배치하지 않고 스스로 부트스트랩한다. 씬에 GameObject를 만들지 않는다.
    /// 실습 ID: 플레이어는 -lab 인자, 에디터는 LabMenu가 SessionState에 넣은 값.
    /// 실행 후 결과를 Notes/results/&lt;ID&gt;_&lt;백엔드&gt;_&lt;날짜&gt;.csv에 덧붙이고 종료한다.
    /// </summary>
    public static class LabRunner
    {
        public const string EditorLabIdKey = "Labs.LabId";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var labId = ReadLabId();
            if (string.IsNullOrEmpty(labId))
                return;

            _ = RunLab(labId); // 아무도 await하지 않으므로 예외는 RunLab 안에서 처리한다
        }

        static string ReadLabId()
        {
#if UNITY_EDITOR
            var id = UnityEditor.SessionState.GetString(EditorLabIdKey, "");
            UnityEditor.SessionState.EraseString(EditorLabIdKey); // 평소 플레이 모드에서는 실습을 돌리지 않는다
            return id;
#else
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-lab")
                    return args[i + 1];
            return null;
#endif
        }

        static async Awaitable RunLab(string labId)
        {
            var lab = CreateLab(labId);
            if (lab == null)
            {
                Debug.LogError($"[Lab] 등록되지 않은 실습 ID: {labId}");
                Exit(1);
                return;
            }

            var ctx = new LabContext(labId);
            try
            {
                await lab.Run(ctx);
            }
            catch (Exception e)
            {
                // 실습이 예외로 끝나도 플레이어가 멈춰 있지 않게 종료한다
                Debug.LogException(e);
                Exit(1);
                return;
            }

            var path = WriteCsv(ctx);
            Debug.Log($"[Lab] {labId} 완료 — {ctx.Rows.Count}행 → {path}");
            Exit(0);
        }

        static ILab CreateLab(string labId)
        {
            foreach (var type in typeof(LabRunner).Assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<LabAttribute>();
                if (attr != null && attr.Id == labId && typeof(ILab).IsAssignableFrom(type))
                    return (ILab)Activator.CreateInstance(type);
            }
            return null;
        }

        public static string Backend
        {
            get
            {
#if UNITY_EDITOR
                return "Editor";
#elif ENABLE_IL2CPP
                return "IL2CPP";
#else
                return "Mono";
#endif
            }
        }

        static string ResultDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Notes", "results");
#else
            // -logFile을 지정했으면 로그 옆에, 아니면 persistentDataPath에 쓴다
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-logFile")
                    return Path.GetDirectoryName(Path.GetFullPath(args[i + 1]));
            return Application.persistentDataPath;
#endif
        }

        static string WriteCsv(LabContext ctx)
        {
            var now = DateTime.Now;
            var dir = ResultDirectory();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{ctx.LabId}_{Backend}_{now:yyyy-MM-dd}.csv");

            var sb = new StringBuilder();
            if (!File.Exists(path))
                sb.AppendLine("run_time,lab,backend,dev_build,unity,condition,metric,value");

            var runTime = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var row in ctx.Rows)
            {
                sb.Append(runTime).Append(',')
                  .Append(ctx.LabId).Append(',')
                  .Append(Backend).Append(',')
                  .Append(Debug.isDebugBuild ? "1" : "0").Append(',')
                  .Append(Application.unityVersion).Append(',')
                  .Append(Escape(row[0])).Append(',')
                  .Append(Escape(row[1])).Append(',')
                  .Append(Escape(row[2])).AppendLine();
            }

            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        static string Escape(string s) =>
            s.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";

        static void Exit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit(code);
#endif
        }
    }
}
