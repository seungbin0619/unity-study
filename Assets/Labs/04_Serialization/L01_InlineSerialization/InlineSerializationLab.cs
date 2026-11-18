using System;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
#endif

namespace Labs.SerializationLabs
{
    /// <summary>두 Holder에 같은 값을 넣고 상태를 기록하는 공용 코드.</summary>
    static class SerializationProbe
    {
        public static void Fill(out Item a, out Item b, out Item c, out Item d)
        {
            var shared = new Item { value = 1 };
            a = shared;
            b = shared; // 같은 인스턴스를 두 필드가 공유
            c = new Special { value = 2, bonus = 5 }; // 선언 타입은 Item
            d = null;
        }

        public static string Describe(Item x)
        {
            if (x is null) return "null";
            return x is Special s
                ? $"Special(value={s.value} bonus={s.bonus})"
                : $"{x.GetType().Name}(value={x.value})";
        }

        /// <param name="mutate">true면 a.value = 99 후 b를 다시 읽어 공유 여부를 값으로도 확인한다 (객체를 바꾸므로 원본에는 쓰지 않는다).</param>
        public static void Report(Action<string, string, string> record, string cond,
            Item a, Item b, Item c, Item d, bool mutate)
        {
            record(cond, "a", Describe(a));
            record(cond, "b", Describe(b));
            record(cond, "ReferenceEquals(a, b)", LabEval.Eval(() => ReferenceEquals(a, b)));
            record(cond, "c", Describe(c));
            record(cond, "d", Describe(d));
            if (mutate && a != null && b != null)
            {
                a.value = 99;
                record(cond, "b.value after a.value = 99", b.value.ToString());
            }
        }
    }

    /// <summary>
    /// Instantiate(= 직렬화 → 역직렬화 복제) 뒤 공유 참조·다형성·null이 어떻게 되는지,
    /// 일반 [Serializable] 필드(InlineHolder)와 [SerializeReference](RefHolder)를 비교한다.
    /// </summary>
    [Preserve, Lab("04-L01")]
    public sealed class InlineSerializationLab : ILab
    {
        public async Awaitable Run(LabContext ctx)
        {
            await Awaitable.NextFrameAsync();
            void Rec(string c, string m, string v) => ctx.Record(c, m, v);

            // --- 일반 [Serializable] ---
            var inl = new GameObject("L01 Inline").AddComponent<InlineHolder>();
            SerializationProbe.Fill(out inl.a, out inl.b, out inl.c, out inl.d);
            SerializationProbe.Report(Rec, "Inline/original before", inl.a, inl.b, inl.c, inl.d, false);

            var inlCopy = Object.Instantiate(inl);
            SerializationProbe.Report(Rec, "Inline/original after Instantiate", inl.a, inl.b, inl.c, inl.d, false);
            ctx.Record("Inline/copy", "ReferenceEquals(original.a, copy.a)", LabEval.Eval(() => ReferenceEquals(inl.a, inlCopy.a)));
            SerializationProbe.Report(Rec, "Inline/copy", inlCopy.a, inlCopy.b, inlCopy.c, inlCopy.d, true);

            ctx.Record("Inline/original", "JsonUtility.ToJson", JsonUtility.ToJson(inl));
            ctx.Record("Inline/original after ToJson", "d", SerializationProbe.Describe(inl.d));

            // --- [SerializeReference] ---
            var rf = new GameObject("L01 Ref").AddComponent<RefHolder>();
            SerializationProbe.Fill(out rf.a, out rf.b, out rf.c, out rf.d);
            SerializationProbe.Report(Rec, "Ref/original before", rf.a, rf.b, rf.c, rf.d, false);

            var rfCopy = Object.Instantiate(rf);
            SerializationProbe.Report(Rec, "Ref/original after Instantiate", rf.a, rf.b, rf.c, rf.d, false);
            ctx.Record("Ref/copy", "ReferenceEquals(original.a, copy.a)", LabEval.Eval(() => ReferenceEquals(rf.a, rfCopy.a)));
            SerializationProbe.Report(Rec, "Ref/copy", rfCopy.a, rfCopy.b, rfCopy.c, rfCopy.d, true);

            ctx.Record("Ref/original", "JsonUtility.ToJson", JsonUtility.ToJson(rf));

            Object.Destroy(inl.gameObject);
            Object.Destroy(inlCopy.gameObject);
            Object.Destroy(rf.gameObject);
            Object.Destroy(rfCopy.gameObject);
            await Awaitable.NextFrameAsync();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 04-L01b (에디터 편집 모드): 숨긴 GameObject에 두 Holder를 만들고 도메인 리로드를 요청한다.
    /// 리로드 뒤 새 어셈블리의 객체가 직렬화 데이터로 다시 채워진 상태를 기록한다.
    /// </summary>
    public static class DomainReloadProbe
    {
        const string LabId = "04-L01b";
        const string PendingKey = "Labs.04-L01b.pending";
        const string ObjectName = "L01b Holder";

        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[04-L01b] 편집 모드에서 실행하세요.");
                return;
            }

            var rows = new List<string[]>();
            void Rec(string c, string m, string v) => rows.Add(new[] { c, m, v });

            var go = EditorUtility.CreateGameObjectWithHideFlags(ObjectName, HideFlags.HideAndDontSave);
            var inl = go.AddComponent<InlineHolder>();
            var rf = go.AddComponent<RefHolder>();
            SerializationProbe.Fill(out inl.a, out inl.b, out inl.c, out inl.d);
            SerializationProbe.Fill(out rf.a, out rf.b, out rf.c, out rf.d);
            inl.nonSerialized = 7;
            rf.nonSerialized = 7;

            SerializationProbe.Report(Rec, "Inline/before reload", inl.a, inl.b, inl.c, inl.d, false);
            Rec("Inline/before reload", "nonSerialized", inl.nonSerialized.ToString());
            SerializationProbe.Report(Rec, "Ref/before reload", rf.a, rf.b, rf.c, rf.d, false);
            Rec("Ref/before reload", "nonSerialized", rf.nonSerialized.ToString());
            WriteCsv(rows);

            SessionState.SetBool(PendingKey, true);
            EditorUtility.RequestScriptReload();
        }

        [InitializeOnLoadMethod]
        static void AfterReload()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            SessionState.EraseBool(PendingKey);
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            var rows = new List<string[]>();
            void Rec(string c, string m, string v) => rows.Add(new[] { c, m, v });

            GameObject go = null;
            foreach (var h in Resources.FindObjectsOfTypeAll<InlineHolder>())
                if (h.name == ObjectName) { go = h.gameObject; break; }

            if (go == null)
            {
                Rec("after reload", "holder", "not found");
            }
            else
            {
                var inl = go.GetComponent<InlineHolder>();
                var rf = go.GetComponent<RefHolder>();
                SerializationProbe.Report(Rec, "Inline/after reload", inl.a, inl.b, inl.c, inl.d, true);
                Rec("Inline/after reload", "nonSerialized", inl.nonSerialized.ToString());
                SerializationProbe.Report(Rec, "Ref/after reload", rf.a, rf.b, rf.c, rf.d, true);
                Rec("Ref/after reload", "nonSerialized", rf.nonSerialized.ToString());
                Object.DestroyImmediate(go);
            }

            var path = WriteCsv(rows);
            Debug.Log($"[04-L01b] 완료 — {rows.Count}행 → {path}");
        }

        static string WriteCsv(List<string[]> rows)
        {
            var now = DateTime.Now;
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Notes", "results");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{LabId}_Editor_{now:yyyy-MM-dd}.csv");

            var sb = new StringBuilder();
            if (!File.Exists(path))
                sb.AppendLine("run_time,lab,backend,dev_build,unity,condition,metric,value");
            var runTime = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var row in rows)
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
