using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using Debug = UnityEngine.Debug;
#endif

namespace Labs.PlayerLoopLabs
{
    /// <summary>편집 모드에서도 Update를 받는 카운터. 마지막 호출 시각으로 최대 간격도 잰다.</summary>
    [ExecuteAlways, AddComponentMenu("")]
    public sealed class EditModeTicker : MonoBehaviour
    {
        public static int Updates;
        public static long LastTicks;
        public static long MaxGapTicks;

        void Update()
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (LastTicks != 0 && now - LastTicks > MaxGapTicks)
                MaxGapTicks = now - LastTicks;
            LastTicks = now;
            Updates++;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 편집 모드 실습. LabRunner(플레이 모드)를 쓰지 않고 EditorApplication.update로 단계를 진행한다.
    /// 측정 중에는 로그를 찍지 않고, 단계가 바뀔 때만 결과를 메모리에 모은다.
    /// </summary>
    public static class EditModeLoopLab
    {
        const string LabId = "03-L10";

        struct Phase
        {
            public string Name;
            public double Seconds;
            public bool Record;
            public Action Tick;
        }

        static Phase[] s_phases;
        static int s_index;
        static long s_phaseStart;
        static int s_editorUpdates;
        static long s_lastEditor;
        static long s_maxEditorGap;
        static int s_activeTicks;
        static GameObject s_go;
        static readonly List<string[]> s_rows = new List<string[]>();

        public static void RunAuto() => Start(new[]
        {
            new Phase { Name = "0 settle", Seconds = 2, Record = false },
            new Phase { Name = "1 idle", Seconds = 10, Record = true },
            new Phase { Name = "2 modify transform each editor tick", Seconds = 5, Record = true, Tick = MoveTicker },
            new Phase { Name = "3 QueuePlayerLoopUpdate each editor tick", Seconds = 5, Record = true, Tick = EditorApplication.QueuePlayerLoopUpdate },
            new Phase { Name = "4 idle again", Seconds = 5, Record = true },
        });

        public static void RunMouse() => Start(new[]
        {
            new Phase { Name = "0 settle", Seconds = 1, Record = false },
            new Phase { Name = "M mouse moving over Scene view", Seconds = 10, Record = true },
        });

        static void MoveTicker()
        {
            if (s_go == null)
                return;
            var p = s_go.transform.position;
            p.x = p.x > 1f ? 0f : p.x + 0.001f;
            s_go.transform.position = p;
        }

        static void Start(Phase[] phases)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[03-L10] 편집 모드에서 실행하세요.");
                return;
            }
            if (s_phases != null)
            {
                Debug.LogWarning("[03-L10] 이미 실행 중입니다.");
                return;
            }

            s_go = new GameObject("EditModeTicker") { hideFlags = HideFlags.HideAndDontSave };
            s_go.AddComponent<EditModeTicker>();

            s_rows.Clear();
            s_phases = phases;
            s_index = -1;
            NextPhase();
            EditorApplication.update += OnEditorUpdate;
            Debug.Log($"[03-L10] 시작 — 약 {Total(phases):0}초. 끝날 때까지 기다리세요.");
        }

        static double Total(Phase[] phases)
        {
            double t = 0;
            foreach (var p in phases) t += p.Seconds;
            return t;
        }

        static void NextPhase()
        {
            s_index++;
            s_phaseStart = Stopwatch.GetTimestamp();
            s_editorUpdates = 0;
            s_lastEditor = 0;
            s_maxEditorGap = 0;
            s_activeTicks = 0;
            EditModeTicker.Updates = 0;
            EditModeTicker.LastTicks = 0;
            EditModeTicker.MaxGapTicks = 0;
        }

        static void OnEditorUpdate()
        {
            var now = Stopwatch.GetTimestamp();
            if (s_lastEditor != 0 && now - s_lastEditor > s_maxEditorGap)
                s_maxEditorGap = now - s_lastEditor;
            s_lastEditor = now;
            s_editorUpdates++;
            if (InternalEditorUtility.isApplicationActive)
                s_activeTicks++;

            var phase = s_phases[s_index];
            phase.Tick?.Invoke();

            var elapsed = (now - s_phaseStart) / (double)Stopwatch.Frequency;
            if (elapsed < phase.Seconds)
                return;

            if (phase.Record)
                RecordPhase(phase.Name, elapsed);

            if (s_index + 1 < s_phases.Length)
            {
                NextPhase();
                return;
            }
            Finish();
        }

        static void RecordPhase(string name, double elapsed)
        {
            var inv = CultureInfo.InvariantCulture;
            var f = (double)Stopwatch.Frequency;
            void Add(string metric, string value) => s_rows.Add(new[] { name, metric, value });

            Add("seconds", elapsed.ToString("0.00", inv));
            Add("ExecuteAlways Update calls", EditModeTicker.Updates.ToString(inv));
            Add("ExecuteAlways Update per second", (EditModeTicker.Updates / elapsed).ToString("0.0", inv));
            Add("ExecuteAlways Update max gap ms", (EditModeTicker.MaxGapTicks * 1000 / f).ToString("0.0", inv));
            Add("EditorApplication.update calls", s_editorUpdates.ToString(inv));
            Add("EditorApplication.update per second", (s_editorUpdates / elapsed).ToString("0.0", inv));
            Add("EditorApplication.update max gap ms", (s_maxEditorGap * 1000 / f).ToString("0.0", inv));
            Add("editor focused ratio", (s_editorUpdates == 0 ? 0 : s_activeTicks / (double)s_editorUpdates).ToString("0.00", inv));
        }

        static void Finish()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (s_go != null)
                UnityEngine.Object.DestroyImmediate(s_go);
            s_go = null;
            s_phases = null;

            var path = WriteCsv();
            Debug.Log($"[03-L10] 완료 — {s_rows.Count}행 → {path}");
        }

        static string WriteCsv()
        {
            var now = DateTime.Now;
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Notes", "results");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{LabId}_Editor_{now:yyyy-MM-dd}.csv");

            var sb = new StringBuilder();
            if (!File.Exists(path))
                sb.AppendLine("run_time,lab,backend,dev_build,unity,condition,metric,value");
            var runTime = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var row in s_rows)
                sb.Append(runTime).Append(',').Append(LabId).Append(",Editor,1,").Append(Application.unityVersion).Append(',')
                  .Append(Escape(row[0])).Append(',').Append(Escape(row[1])).Append(',').Append(Escape(row[2])).AppendLine();
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        static string Escape(string s) =>
            s.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
    }
#endif
}
