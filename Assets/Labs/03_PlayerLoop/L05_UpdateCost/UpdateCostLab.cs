using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 객체 1만 개 갱신 비용: 스크립트마다 Update / 빈 Update / Update 없음 / 매니저 루프 / 커스텀 PlayerLoop 시스템.
    /// PlayerLoop Update 단계의 ScriptRunBehaviourUpdate 앞뒤에 타이머 시스템을 끼우고, 그 사이에 커스텀 시스템을 둔다.
    /// 측정 구간 = ScriptRunBehaviourUpdate + 커스텀 시스템 (Awaitable 재개는 ScriptRunDelayedDynamicFrameRate라 구간 밖).
    /// </summary>
    [Preserve, Lab("03-L05")]
    public sealed class UpdateCostLab : ILab
    {
        /// <summary>프레임마다 Begin 타이머가 한 번 정한다. 객체마다 Time.deltaTime(경계 호출)을 읽지 않게 하려는 것.</summary>
        public static float Dt;

        const int Count = 10000;
        const int Warmup = 30;
        const int Frames = 300;

        // 측정 버퍼 (측정 구간 안에서 할당하지 않도록 미리 잡아 둠)
        static readonly long[] s_regionTicks = new long[Frames];
        static readonly long[] s_frameTicks = new long[Frames];
        static int s_sampleCount;
        static bool s_recording;
        static long s_begin;
        static long s_lastBegin;

        // C 조건에서만 채운다
        static PlainTicker[] s_systemTargets;

        struct TimerBegin { }
        struct TimerEnd { }
        struct CustomTickSystem { }

        enum Kind { Baseline, Update, EmptyUpdate, NoUpdate, ManagerPlain, ManagerBehaviour, CustomSystem }

        public async Awaitable Run(LabContext ctx)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            ctx.Record("-", "count", Count.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "frames", Frames.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "Application.isBatchMode", Application.isBatchMode.ToString().ToLowerInvariant());
            ctx.Record("-", "Stopwatch.Frequency", Stopwatch.Frequency.ToString(CultureInfo.InvariantCulture));

            var original = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(Instrument(original));
            using var metrics = new LabMetrics();
            try
            {
                var conditions = new[]
                {
                    ("0 baseline", Kind.Baseline),
                    ("A Update x10000", Kind.Update),
                    ("A0 empty Update x10000", Kind.EmptyUpdate),
                    ("N no Update x10000", Kind.NoUpdate),
                    ("B manager -> plain x10000", Kind.ManagerPlain),
                    ("B2 manager -> MonoBehaviour.Tick x10000", Kind.ManagerBehaviour),
                    ("C PlayerLoop system -> plain x10000", Kind.CustomSystem),
                    // 순서 효과 확인용으로 기준선과 A를 한 번 더
                    ("0 baseline (again)", Kind.Baseline),
                    ("A Update x10000 (again)", Kind.Update),
                };

                foreach (var (name, kind) in conditions)
                {
                    var root = new GameObject("UpdateCostRoot");
                    Setup(kind, root);
                    for (var i = 0; i < Warmup; i++)
                        await Awaitable.NextFrameAsync();

                    s_sampleCount = 0;
                    s_recording = true;
                    long allocBytes = 0;
                    var allocFrames = 0;
                    while (s_sampleCount < Frames)
                    {
                        await Awaitable.NextFrameAsync();
                        if (metrics.Valid)
                        {
                            allocBytes += metrics.LastFrameGcAllocBytes;
                            allocFrames++;
                        }
                    }
                    s_recording = false;

                    Summarize(ctx, name, allocFrames > 0 ? allocBytes / (double)allocFrames : -1);

                    s_systemTargets = null;
                    Object.Destroy(root);
                    // 조건 사이 정리: 파괴 반영 + 그 사이 할당을 GC
                    for (var i = 0; i < 5; i++)
                        await Awaitable.NextFrameAsync();
                    GC.Collect();
                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                s_recording = false;
                s_systemTargets = null;
                PlayerLoop.SetPlayerLoop(original);
            }
        }

        /// <summary>GameObject 1만 개를 root 아래에 만든다 (Destroy(root) 한 번으로 정리).</summary>
        static void Setup(Kind kind, GameObject root)
        {
            switch (kind)
            {
                case Kind.Baseline:
                    return;
                case Kind.Update:
                    Spawn<UpdateTicker>(root);
                    return;
                case Kind.EmptyUpdate:
                    Spawn<EmptyUpdateTicker>(root);
                    return;
                case Kind.NoUpdate:
                    Spawn<NoUpdateTicker>(root);
                    return;
                case Kind.ManagerPlain:
                    root.AddComponent<TickManager>().Plain = CreatePlain();
                    return;
                case Kind.ManagerBehaviour:
                    root.AddComponent<TickManager>().Behaviours = Spawn<ManualTicker>(root);
                    return;
                case Kind.CustomSystem:
                    s_systemTargets = CreatePlain();
                    return;
            }
        }

        static T[] Spawn<T>(GameObject root) where T : Component
        {
            var result = new T[Count];
            var parent = root.transform;
            for (var i = 0; i < Count; i++)
            {
                var go = new GameObject();
                go.transform.SetParent(parent, false);
                result[i] = go.AddComponent<T>();
            }
            return result;
        }

        static PlainTicker[] CreatePlain()
        {
            var result = new PlainTicker[Count];
            for (var i = 0; i < Count; i++)
                result[i] = new PlainTicker();
            return result;
        }

        /// <summary>Update 단계: ... TimerBegin, ScriptRunBehaviourUpdate, CustomTickSystem, TimerEnd ...</summary>
        static PlayerLoopSystem Instrument(PlayerLoopSystem original)
        {
            var root = original;
            var phases = (PlayerLoopSystem[])root.subSystemList.Clone();
            var found = false;
            for (var p = 0; p < phases.Length; p++)
            {
                if (phases[p].type != typeof(UnityEngine.PlayerLoop.Update))
                    continue;
                var phase = phases[p];
                var children = phase.subSystemList;
                var list = new PlayerLoopSystem[children.Length + 3];
                var n = 0;
                foreach (var child in children)
                {
                    if (child.type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))
                    {
                        list[n++] = new PlayerLoopSystem { type = typeof(TimerBegin), updateDelegate = OnBegin };
                        list[n++] = child;
                        list[n++] = new PlayerLoopSystem { type = typeof(CustomTickSystem), updateDelegate = OnCustomTick };
                        list[n++] = new PlayerLoopSystem { type = typeof(TimerEnd), updateDelegate = OnEnd };
                        found = true;
                    }
                    else
                    {
                        list[n++] = child;
                    }
                }
                phase.subSystemList = list;
                phases[p] = phase;
            }
            if (!found)
                throw new InvalidOperationException("ScriptRunBehaviourUpdate를 찾지 못함");
            root.subSystemList = phases;
            return root;
        }

        static void OnBegin()
        {
            Dt = Time.deltaTime;
            var now = Stopwatch.GetTimestamp();
            if (s_recording && s_sampleCount < Frames)
                s_frameTicks[s_sampleCount] = now - s_lastBegin;
            s_lastBegin = now;
            s_begin = Stopwatch.GetTimestamp();
        }

        static void OnCustomTick()
        {
            var targets = s_systemTargets;
            if (targets == null)
                return;
            for (var i = 0; i < targets.Length; i++)
                targets[i].Tick();
        }

        static void OnEnd()
        {
            var elapsed = Stopwatch.GetTimestamp() - s_begin;
            if (s_recording && s_sampleCount < Frames)
                s_regionTicks[s_sampleCount++] = elapsed;
        }

        static void Summarize(LabContext ctx, string name, double allocPerFrame)
        {
            var region = ToMs(s_regionTicks);
            var frame = ToMs(s_frameTicks);
            ctx.Record(name, "region ms avg", F(Avg(region)));
            ctx.Record(name, "region ms median", F(Median(region)));
            ctx.Record(name, "region ms min", F(region[0]));
            ctx.Record(name, "region ms max", F(region[region.Length - 1]));
            ctx.Record(name, "region ns per object (median)", F(Median(region) * 1e6 / Count));
            ctx.Record(name, "frame ms median", F(Median(frame)));
            ctx.Record(name, "GC alloc bytes per frame avg", allocPerFrame < 0 ? "-" : F(allocPerFrame));
        }

        /// <summary>정렬된 ms 배열.</summary>
        static double[] ToMs(long[] ticks)
        {
            var ms = new double[ticks.Length];
            for (var i = 0; i < ticks.Length; i++)
                ms[i] = ticks[i] * 1000.0 / Stopwatch.Frequency;
            Array.Sort(ms);
            return ms;
        }

        static double Avg(double[] v)
        {
            double sum = 0;
            foreach (var x in v) sum += x;
            return sum / v.Length;
        }

        static double Median(double[] sorted) => sorted[sorted.Length / 2];

        static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
