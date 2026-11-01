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

            // 단편화는 깨끗한 힙에서 봐야 하므로 별도 실행(-fragmentation)으로 돌린다
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fragmentation") >= 0)
            {
                await RunFragmentation(ctx, metrics);
                return;
            }

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

            // unmap 가설: 빈 블록이 여러 번의 수집 동안 계속 비어 있으면 OS에 반납되는가
            await CollectRepeatedly(5); // 5번 단계까지 포함해 누적 6회
            await Snapshot(ctx, metrics, "6 Collect x6 total");
            await CollectRepeatedly(6);
            await Snapshot(ctx, metrics, "7 Collect x12 total");
            await CollectRepeatedly(20);
            await Snapshot(ctx, metrics, "8 Collect x32 total");
            var idle = Stopwatch.StartNew();
            while (idle.Elapsed.TotalSeconds < 30) await Awaitable.NextFrameAsync();
            await Snapshot(ctx, metrics, "9 after 30 s idle");
        }

        static async Awaitable CollectRepeatedly(int times)
        {
            for (var i = 0; i < times; i++)
            {
                GC.Collect();
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>
        /// Boehm은 객체를 옮기지 않는다. 빈 공간의 합계가 충분해도 연속된 구멍이 없으면 큰 배열이 들어가지 못하는지 본다.
        /// F: 512 KB 블록 100개 중 홀수 번째만 해제 → 20 MB 할당.
        /// S: 작은 배열 80만 개 중 64개마다 하나만 남김 → 20 MB 할당.
        /// </summary>
        static async Awaitable RunFragmentation(LabContext ctx, LabMetrics metrics)
        {
            const int chunkSize = 512 * 1024;
            const int chunkCount = 100;
            const int requestSize = 20 * 1024 * 1024;
            const int smallCount = 800_000;
            const int smallSize = 32;
            const int keepEvery = 64;

            await Settle();
            GC.Collect();
            await Snapshot(ctx, metrics, "F0 baseline");

            var chunks = new byte[chunkCount][];
            for (var i = 0; i < chunkCount; i++) chunks[i] = new byte[chunkSize]; // LAB: 의도적
            await Snapshot(ctx, metrics, "F1 100 x 512KB held (50 MB)");

            for (var i = 1; i < chunkCount; i += 2) chunks[i] = null;
            GC.Collect();
            await Snapshot(ctx, metrics, "F2 odd chunks freed + Collect (25 MB in 512KB holes)");

            var request = new byte[requestSize]; // LAB: 의도적
            request[requestSize - 1] = 1;
            await Snapshot(ctx, metrics, "F3 new byte[20MB] with 512KB holes");

            request = null;
            Array.Clear(chunks, 0, chunkCount);
            GC.Collect();
            await Snapshot(ctx, metrics, "F4 all freed + Collect");

            request = new byte[requestSize]; // LAB: 의도적 — 구멍이 합쳐진 뒤의 대조군
            request[requestSize - 1] = 1;
            await Snapshot(ctx, metrics, "F5 new byte[20MB] after full free");
            request = null;
            GC.Collect();
            await Snapshot(ctx, metrics, "F6 freed + Collect");

            var small = new byte[smallCount][];
            for (var i = 0; i < smallCount; i++) small[i] = new byte[smallSize]; // LAB: 의도적
            await Snapshot(ctx, metrics, "S1 800k x byte[32] held");

            for (var i = 0; i < smallCount; i++)
                if (i % keepEvery != 0) small[i] = null;
            GC.Collect();
            await Snapshot(ctx, metrics, "S2 keep 1 of 64 + Collect");

            request = new byte[requestSize]; // LAB: 의도적
            request[requestSize - 1] = 1;
            await Snapshot(ctx, metrics, "S3 new byte[20MB] with sparse small survivors");

            request = null;
            small = null;
            GC.Collect();
            await Snapshot(ctx, metrics, "S4 all freed + Collect");
            ctx.Record("-", "keep alive", (chunks.Length + (small?.Length ?? 0)).ToString());
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
