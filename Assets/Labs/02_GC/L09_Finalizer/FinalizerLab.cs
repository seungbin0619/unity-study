using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>02-L09 파이널라이저 없는 객체.</summary>
    public sealed class PlainObject
    {
        public readonly byte[] Data = new byte[1024];
    }

    /// <summary>02-L09 파이널라이저 있는 객체. 본문은 실행 횟수만 센다.</summary>
    public sealed class FinalizableObject
    {
        public static int FinalizedCount;
        public readonly byte[] Data = new byte[1024];

        ~FinalizableObject() => Interlocked.Increment(ref FinalizedCount);
    }

    /// <summary>
    /// 그룹(Plain / Finalizable)마다 객체 10만 개를 만들고 참조를 모두 끊은 뒤 GC.Collect()를 Collects회 부른다.
    /// Collect 사이에는 1프레임만 기다리고 WaitForPendingFinalizers는 부르지 않는다.
    /// 매 Collect 직후 GC.GetTotalMemory(false)·소요 시간, 1프레임 뒤 GC Used·실행된 파이널라이저 수·
    /// 표본 객체의 짧은/긴 WeakReference 생존 수를 기록한다. c0은 참조를 끊은 직후(Collect 전).
    /// 이후 다 회수될 때까지 같은 방식으로 Collect를 이어 가며 횟수·시간을 재고(drain),
    /// 마지막으로 Collect → WaitForPendingFinalizers → Collect 조합을 따로 잰다(Finalizable+Wait).
    /// </summary>
    [Preserve, Lab("02-L09")]
    public sealed class FinalizerLab : ILab
    {
        const int Count = 100_000;
        const int SampleEvery = 100;
        const int Collects = 5;
        const int Trials = 3;
        const int DrainLimit = 2000;

        // async 메서드의 지역 변수는 상태 머신 필드가 되므로 객체는 필드에만 두고 지운다
        object[] _objects;
        WeakReference[] _short;
        WeakReference[] _long;

        public async Awaitable Run(LabContext ctx)
        {
            // -frameRate N: Collect 사이 프레임 간격을 늘린다 (기본 -1 = 제한 없음)
            var frameRate = ReadFrameRate();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameRate;
            using var metrics = new LabMetrics();
            ctx.Record("-", "targetFrameRate", frameRate.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());
            ctx.Record("-", "GarbageCollector.isIncremental", GarbageCollector.isIncremental.ToString().ToLowerInvariant());
            ctx.Record("-", "count", Count.ToString(CultureInfo.InvariantCulture));

            // 워밍업: 두 그룹을 한 번씩 돌려 JIT·최초 초기화를 배제한다 (기록하지 않음)
            foreach (var finalizable in new[] { false, true })
            {
                Spawn(finalizable);
                _objects = null;
                await Settle();
            }

            for (var t = 0; t < Trials; t++)
            {
                // 순서 효과를 줄이려고 시행마다 그룹 순서를 바꾼다
                var order = t % 2 == 0 ? new[] { false, true } : new[] { true, false };
                foreach (var finalizable in order)
                    await RunGroup(ctx, metrics, finalizable, t + 1);
            }
            for (var t = 0; t < Trials; t++)
                await RunWait(ctx, metrics, t + 1);
        }

        async Awaitable RunGroup(LabContext ctx, LabMetrics metrics, bool finalizable, int trial)
        {
            var condition = (finalizable ? "Finalizable" : "Plain") + " t" + trial.ToString(CultureInfo.InvariantCulture);
            await Settle();
            ctx.Record(condition, "baseline GC Used MB", Mb(metrics.GcUsedBytes));
            ctx.Record(condition, "baseline GetTotalMemory MB", Mb(GC.GetTotalMemory(false)));

            var finalizedBefore = Volatile.Read(ref FinalizableObject.FinalizedCount);
            Spawn(finalizable);
            await Awaitable.NextFrameAsync();
            ctx.Record(condition, "spawned GC Used MB", Mb(metrics.GcUsedBytes));

            _objects = null;
            await Awaitable.NextFrameAsync();
            RecordState(ctx, condition, "c0", metrics, finalizedBefore);

            for (var c = 1; c <= Collects; c++)
            {
                var start = Stopwatch.GetTimestamp();
                GC.Collect();
                var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                var total = GC.GetTotalMemory(false);
                await Awaitable.NextFrameAsync();

                var step = "c" + c.ToString(CultureInfo.InvariantCulture);
                ctx.Record(condition, step + " collect ms", ms.ToString("F2", CultureInfo.InvariantCulture));
                ctx.Record(condition, step + " GetTotalMemory MB", Mb(total));
                RecordState(ctx, condition, step, metrics, finalizedBefore);
            }

            // 다 회수될 때까지 같은 방식(Collect → 1프레임)을 이어 간다
            var drainStart = Stopwatch.GetTimestamp();
            // 끝 판정: 파이널라이저가 모두 돌았고, 긴 약한 참조가 모두 죽었을 때 (없으면 다 돈 뒤 Collect 1회 더)
            var drained = Collects;
            var collectsAfterFinalized = 0;
            while (drained < DrainLimit)
            {
                var allFinalized = Volatile.Read(ref FinalizableObject.FinalizedCount) - finalizedBefore >= (finalizable ? Count : 0);
                if (allFinalized && (_long != null ? AllDead(_long) : !finalizable || collectsAfterFinalized >= 1)) break;
                if (allFinalized) collectsAfterFinalized++;
                GC.Collect();
                drained++;
                await Awaitable.NextFrameAsync();
            }
            var drainMs = (Stopwatch.GetTimestamp() - drainStart) * 1000.0 / Stopwatch.Frequency;
            ctx.Record(condition, "drain total collects", drained.ToString(CultureInfo.InvariantCulture));
            ctx.Record(condition, "drain ms after c5", drainMs.ToString("F0", CultureInfo.InvariantCulture));
            RecordState(ctx, condition, "drained", metrics, finalizedBefore);

            _short = null;
            _long = null;
        }

        // Collect → WaitForPendingFinalizers → Collect: 대기 시간으로 파이널라이저 스레드 처리량을 잰다
        async Awaitable RunWait(LabContext ctx, LabMetrics metrics, int trial)
        {
            var condition = "Finalizable+Wait t" + trial.ToString(CultureInfo.InvariantCulture);
            await Settle();
            ctx.Record(condition, "baseline GC Used MB", Mb(metrics.GcUsedBytes));
            var finalizedBefore = Volatile.Read(ref FinalizableObject.FinalizedCount);
            Spawn(true);
            _objects = null;
            await Awaitable.NextFrameAsync();

            var t0 = Stopwatch.GetTimestamp();
            GC.Collect();
            var t1 = Stopwatch.GetTimestamp();
            GC.WaitForPendingFinalizers();
            var t2 = Stopwatch.GetTimestamp();
            ctx.Record(condition, "after wait finalized",
                (Volatile.Read(ref FinalizableObject.FinalizedCount) - finalizedBefore).ToString(CultureInfo.InvariantCulture));
            GC.Collect();
            var t3 = Stopwatch.GetTimestamp();
            await Awaitable.NextFrameAsync();

            ctx.Record(condition, "collect1 ms", Ms(t0, t1));
            ctx.Record(condition, "WaitForPendingFinalizers ms", Ms(t1, t2));
            ctx.Record(condition, "collect2 ms", Ms(t2, t3));
            RecordState(ctx, condition, "after collect2", metrics, finalizedBefore);

            GC.Collect();
            await Awaitable.NextFrameAsync();
            RecordState(ctx, condition, "after collect3", metrics, finalizedBefore);
            _short = null;
            _long = null;
        }

        static bool AllDead(WeakReference[] refs)
        {
            foreach (var r in refs) if (r.IsAlive) return false;
            return true;
        }

        static string Ms(long from, long to) =>
            ((to - from) * 1000.0 / Stopwatch.Frequency).ToString("F2", CultureInfo.InvariantCulture);

        void RecordState(LabContext ctx, string condition, string step, LabMetrics metrics, int finalizedBefore)
        {
            int shortAlive = 0, longAlive = _long == null ? -1 : 0;
            for (var i = 0; i < _short.Length; i++)
            {
                if (_short[i].IsAlive) shortAlive++;
                if (_long != null && _long[i].IsAlive) longAlive++;
            }
            var finalized = Volatile.Read(ref FinalizableObject.FinalizedCount) - finalizedBefore;
            ctx.Record(condition, step + " GC Used MB", Mb(metrics.GcUsedBytes));
            ctx.Record(condition, step + " finalized", finalized.ToString(CultureInfo.InvariantCulture));
            ctx.Record(condition, step + " weak short|long alive",
                shortAlive.ToString(CultureInfo.InvariantCulture) + "|" + longAlive.ToString(CultureInfo.InvariantCulture));
        }

        // 생성 루프의 지역 변수가 Run의 스택·레지스터에 남지 않게 별도 프레임에서 만든다
        [MethodImpl(MethodImplOptions.NoInlining)]
        void Spawn(bool finalizable)
        {
            _objects = new object[Count];
            _short = new WeakReference[Count / SampleEvery];
            // IL2CPP는 trackResurrection: true를 지원하지 않는다 (NotSupportedException) — 긴 약한 참조는 Mono에서만 잰다
            _long = LabRunner.Backend == "IL2CPP" ? null : new WeakReference[Count / SampleEvery];
            for (var i = 0; i < Count; i++)
            {
                object o = finalizable ? new FinalizableObject() : new PlainObject(); // LAB: 의도적
                _objects[i] = o;
                if (i % SampleEvery != 0) continue;
                _short[i / SampleEvery] = new WeakReference(o, false);
                if (_long != null) _long[i / SampleEvery] = new WeakReference(o, true);
            }
        }

        static async Awaitable Settle()
        {
            for (var i = 0; i < 3; i++) await Awaitable.NextFrameAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var i = 0; i < 2; i++) await Awaitable.NextFrameAsync();
        }

        static int ReadFrameRate()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-frameRate");
            return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;
        }

        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
    }
}
