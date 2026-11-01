using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 50 MB byte[] 하나를 할당·해제하며 단계마다 GC Used / Reserved를 기록한다.
    /// 값은 세 경로로 읽는다: ProfilerRecorder("GC Used/Reserved Memory"), Profiler.GetMonoUsed/HeapSizeLong, GC.GetTotalMemory.
    /// 단계마다 몇 프레임을 넘긴 뒤 읽는다 (ProfilerRecorder 값이 프레임 단위로 갱신되므로).
    /// </summary>
    [Preserve, Lab("02-L04")]
    public sealed class UsedVsReservedLab : ILab
    {
        const int Size = 50 * 1024 * 1024;
        const int SettleFrames = 5;
        const double WaitSeconds = 10;

        byte[] _big;

        public async Awaitable Run(LabContext ctx)
        {
            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());

            await Settle();
            GC.Collect();
            await Snapshot(ctx, metrics, "0 baseline (after Collect)");

            _big = new byte[Size]; // LAB: 의도적
            _big[Size - 1] = 1;
            await Snapshot(ctx, metrics, "1 after new byte[50MB]");

            _big = null;
            await Snapshot(ctx, metrics, "2a ref dropped, no Collect");

            GC.Collect();
            await Snapshot(ctx, metrics, "2b after GC.Collect()");

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < WaitSeconds) await Awaitable.NextFrameAsync();
            await Snapshot(ctx, metrics, "3 after 10 s idle");

            _big = new byte[Size]; // LAB: 의도적 — 해제된 공간을 다시 쓰는가
            _big[Size - 1] = 1;
            await Snapshot(ctx, metrics, "4 re-allocate byte[50MB]");

            _big = null;
            GC.Collect();
            await Snapshot(ctx, metrics, "5 drop + Collect again");
        }

        static async Awaitable Settle()
        {
            for (var i = 0; i < SettleFrames; i++) await Awaitable.NextFrameAsync();
        }

        static async Awaitable Snapshot(LabContext ctx, LabMetrics metrics, string phase)
        {
            await Settle();
            ctx.Record(phase, "recorder GC Used MB", Mb(metrics.GcUsedBytes));
            ctx.Record(phase, "recorder GC Reserved MB", Mb(metrics.GcReservedBytes));
            ctx.Record(phase, "Profiler.GetMonoUsedSizeLong MB", Mb(Profiler.GetMonoUsedSizeLong()));
            ctx.Record(phase, "Profiler.GetMonoHeapSizeLong MB", Mb(Profiler.GetMonoHeapSizeLong()));
            ctx.Record(phase, "GC.GetTotalMemory(false) MB", Mb(GC.GetTotalMemory(false)));
            ctx.Record(phase, "GC.CollectionCount(0)", GC.CollectionCount(0).ToString());
        }

        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }
}
