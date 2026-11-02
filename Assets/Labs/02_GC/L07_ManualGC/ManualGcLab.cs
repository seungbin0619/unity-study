using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 02-L06과 같은 조건(EnemyClass LiveCount개를 살려 두고 매 프레임 1 MB를 쓰레기로 만듦)에서
    /// GarbageCollector.GCMode = Manual로 자동 수집을 끄고, 매 프레임 할당 직후 CollectIncremental(slice)를 한 번 부른다.
    /// slice는 플레이어 인자 -slice &lt;나노초&gt;로 정한다. -slice 0이면 Manual로 바꾸지 않고 Enabled(02-L06 기본 조건)로 돈다.
    /// 프레임마다 프레임 시간·할당 루프 시간·CollectIncremental 시간·반환값(남은 작업)·GC 횟수·GC Used를 기록한다.
    /// -interval N: N프레임마다 호출을 시작하고, 반환값이 true(남은 작업)인 동안은 매 프레임 이어 부른다 (기본 1 = 매 프레임).
    /// </summary>
    [Preserve, Lab("02-L07")]
    public sealed class ManualGcLab : ILab
    {
        const int DefaultLiveCount = 4_000_000;
        const ulong DefaultSliceNs = 3_000_000;
        const int ChunkSize = 1024;
        const int ChunksPerFrame = 1024;
        const int WarmupFrames = 60;
        const int SampleFrames = 600;

        // 측정 내내 살아 있어야 하므로 필드에 보관한다
        EnemyClass[] _live;
        byte[] _last;
        ulong _slice;
        int _interval;
        int _frame;
        bool _inProgress;

        readonly double[] _frameMs = new double[SampleFrames];
        readonly double[] _workMs = new double[SampleFrames];
        readonly double[] _collectMs = new double[SampleFrames];
        readonly bool[] _moreWork = new bool[SampleFrames];
        readonly int[] _gcDelta = new int[SampleFrames];
        readonly long[] _usedBytes = new long[SampleFrames];
        int _calls;

        public async Awaitable Run(LabContext ctx)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var liveCount = ReadCount();
            _slice = ReadSlice();
            _interval = ReadInterval();
            var manual = _slice > 0;
            using var metrics = new LabMetrics();
            ctx.Record("-", "slice ns", _slice.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "interval frames", _interval.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());
            ctx.Record("-", "GarbageCollector.isIncremental", GarbageCollector.isIncremental.ToString().ToLowerInvariant());
            ctx.Record("-", "incrementalTimeSliceNanoseconds",
                GarbageCollector.incrementalTimeSliceNanoseconds.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "live count", liveCount.ToString(CultureInfo.InvariantCulture));

            _live = new EnemyClass[liveCount];
            for (var i = 0; i < liveCount; i++) _live[i] = new EnemyClass { Hp = i }; // LAB: 의도적
            GC.Collect();
            for (var i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            ctx.Record("-", "live heap GC Used MB", Mb(metrics.GcUsedBytes));

            // 살아 있는 힙을 만든 뒤에 모드를 바꾼다 (만드는 동안의 수집은 측정 대상이 아님)
            if (manual) GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
            ctx.Record("-", "GCMode", GarbageCollector.GCMode.ToString());

            for (var i = 0; i < WarmupFrames; i++)
            {
                AllocateGarbage();
                if (manual) MaybeCollect(out _);
                await Awaitable.NextFrameAsync();
            }

            var previous = Stopwatch.GetTimestamp();
            var previousGc = GC.CollectionCount(0);
            for (var f = 0; f < SampleFrames; f++)
            {
                var start = Stopwatch.GetTimestamp();
                AllocateGarbage();
                var afterWork = Stopwatch.GetTimestamp();
                _workMs[f] = Ms(afterWork - start);
                if (manual)
                {
                    _moreWork[f] = MaybeCollect(out var called);
                    _collectMs[f] = Ms(Stopwatch.GetTimestamp() - afterWork);
                    if (called) _calls++;
                }

                await Awaitable.NextFrameAsync();

                var now = Stopwatch.GetTimestamp();
                _frameMs[f] = Ms(now - previous);
                previous = now;
                var gc = GC.CollectionCount(0);
                _gcDelta[f] = gc - previousGc;
                previousGc = gc;
                _usedBytes[f] = metrics.GcUsedBytes;
            }

            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;

            for (var f = 0; f < SampleFrames; f++)
                ctx.Record("frame " + f.ToString("D3", CultureInfo.InvariantCulture), "frame ms|work ms|collect ms|more|gc|used MB",
                    F3(_frameMs[f]) + "|" + F3(_workMs[f]) + "|" + F3(_collectMs[f]) + "|" + (_moreWork[f] ? "1" : "0") + "|"
                    + _gcDelta[f].ToString(CultureInfo.InvariantCulture) + "|" + Mb(_usedBytes[f]));

            Summarize(ctx, "frame ms", _frameMs);
            Summarize(ctx, "collect ms", _collectMs);
            var gcTotal = 0;
            foreach (var d in _gcDelta) gcTotal += d;
            var moreFrames = 0;
            foreach (var m in _moreWork) if (m) moreFrames++;
            ctx.Record("summary", "GC count", gcTotal.ToString(CultureInfo.InvariantCulture));
            ctx.Record("summary", "frames with more work", moreFrames.ToString(CultureInfo.InvariantCulture));
            ctx.Record("summary", "CollectIncremental calls", _calls.ToString(CultureInfo.InvariantCulture));
            ctx.Record("summary", "used MB first|last|max", Mb(_usedBytes[0]) + "|" + Mb(_usedBytes[SampleFrames - 1]) + "|" + Mb(Max(_usedBytes)));
            ctx.Record("-", "keep alive", (_live.Length + _last.Length).ToString(CultureInfo.InvariantCulture));
        }

        // N프레임마다 새로 부르고, 진행 중인 수집이 남아 있으면 매 프레임 이어 부른다
        bool MaybeCollect(out bool called)
        {
            called = _inProgress || _frame % _interval == 0;
            _frame++;
            if (!called) return false;
            _inProgress = GarbageCollector.CollectIncremental(_slice);
            return _inProgress;
        }

        void AllocateGarbage()
        {
            for (var i = 0; i < ChunksPerFrame; i++) _last = new byte[ChunkSize]; // LAB: 의도적
        }

        static void Summarize(LabContext ctx, string name, double[] values)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            var sum = 0.0;
            foreach (var v in values) sum += v;
            ctx.Record("summary", name + " median", F3(sorted[sorted.Length / 2]));
            ctx.Record("summary", name + " p99", F3(sorted[(int)(sorted.Length * 0.99)]));
            ctx.Record("summary", name + " max", F3(sorted[sorted.Length - 1]));
            ctx.Record("summary", name + " total", F3(sum));
        }

        static long Max(long[] values)
        {
            var max = long.MinValue;
            foreach (var v in values) if (v > max) max = v;
            return max;
        }

        static ulong ReadSlice()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-slice");
            if (index >= 0 && index + 1 < args.Length && ulong.TryParse(args[index + 1], out var n)) return n;
            return DefaultSliceNs;
        }

        static int ReadInterval()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-interval");
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var n) && n > 0) return n;
            return 1;
        }

        static int ReadCount()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-count");
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var n) && n > 0) return n;
            return DefaultLiveCount;
        }

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        static string F3(double ms) => ms.ToString("F3", CultureInfo.InvariantCulture);
        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
    }
}
