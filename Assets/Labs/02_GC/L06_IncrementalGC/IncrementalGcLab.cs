using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 살아 있는 힙(EnemyClass LiveCount개)을 둔 채 매 프레임 1 MB(byte[1024] × 1024)를 쓰레기로 만들고,
    /// 프레임마다 프레임 시간·할당 루프 시간·GC 횟수·GC Used를 기록한다.
    /// GC.Collect()는 부르지 않는다 — 할당이 유발하는 자동 수집을 본다.
    /// Incremental GC On/Off는 빌드 설정이므로 빌드를 바꿔 같은 실습을 돌린다.
    /// 살아 있는 객체 수는 플레이어 인자 -count N으로 바꿀 수 있다.
    /// -mode midlived: 매 프레임의 1 MB를 바로 버리지 않고 MidLivedFrames 동안 보관한다 (중간 수명 객체).
    /// -mode mutate: 매 프레임 _live 원소 SwapsPerFrame쌍을 맞바꾼다 (장수 객체는 그대로, dirty 페이지만 늘어남).
    /// </summary>
    [Preserve, Lab("02-L06")]
    public sealed class IncrementalGcLab : ILab
    {
        const int DefaultLiveCount = 4_000_000;
        const int ChunkSize = 1024;
        const int ChunksPerFrame = 1024;
        const int WarmupFrames = 60;
        const int SampleFrames = 600;
        const int MidLivedFrames = 30;
        const int SwapsPerFrame = 10_000;

        // 측정 내내 살아 있어야 하므로 필드에 보관한다
        EnemyClass[] _live;
        byte[] _last;
        readonly byte[][][] _midLived = new byte[MidLivedFrames][][];
        string _mode;
        int _frame;
        uint _rng = 2463534242;

        readonly double[] _frameMs = new double[SampleFrames];
        readonly double[] _workMs = new double[SampleFrames];
        readonly int[] _gcDelta = new int[SampleFrames];
        readonly long[] _usedBytes = new long[SampleFrames];

        public async Awaitable Run(LabContext ctx)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var liveCount = ReadCount();
            _mode = ReadArg("-mode") ?? "baseline";
            using var metrics = new LabMetrics();
            ctx.Record("-", "mode", _mode);
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());
            ctx.Record("-", "GarbageCollector.isIncremental", UnityEngine.Scripting.GarbageCollector.isIncremental.ToString().ToLowerInvariant());
            ctx.Record("-", "incrementalTimeSliceNanoseconds",
                UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "live count", liveCount.ToString(CultureInfo.InvariantCulture));

            _live = new EnemyClass[liveCount];
            for (var i = 0; i < liveCount; i++) _live[i] = new EnemyClass { Hp = i }; // LAB: 의도적
            GC.Collect();
            for (var i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            ctx.Record("-", "live heap GC Used MB", Mb(metrics.GcUsedBytes));

            for (var i = 0; i < WarmupFrames; i++)
            {
                AllocateGarbage();
                await Awaitable.NextFrameAsync();
            }

            var previous = Stopwatch.GetTimestamp();
            var previousGc = GC.CollectionCount(0);
            for (var f = 0; f < SampleFrames; f++)
            {
                var start = Stopwatch.GetTimestamp();
                AllocateGarbage();
                _workMs[f] = Ms(Stopwatch.GetTimestamp() - start);

                await Awaitable.NextFrameAsync();

                var now = Stopwatch.GetTimestamp();
                _frameMs[f] = Ms(now - previous);
                previous = now;
                var gc = GC.CollectionCount(0);
                _gcDelta[f] = gc - previousGc;
                previousGc = gc;
                _usedBytes[f] = metrics.GcUsedBytes;
            }

            for (var f = 0; f < SampleFrames; f++)
                ctx.Record("frame " + f.ToString("D3", CultureInfo.InvariantCulture), "frame ms|work ms|gc|used MB",
                    F3(_frameMs[f]) + "|" + F3(_workMs[f]) + "|" + _gcDelta[f].ToString(CultureInfo.InvariantCulture) + "|" + Mb(_usedBytes[f]));

            Summarize(ctx, "frame ms", _frameMs);
            Summarize(ctx, "work ms", _workMs);
            var gcTotal = 0;
            foreach (var d in _gcDelta) gcTotal += d;
            ctx.Record("summary", "GC count", gcTotal.ToString(CultureInfo.InvariantCulture));
            ctx.Record("-", "keep alive", (_live.Length + _last.Length).ToString(CultureInfo.InvariantCulture));
        }

        void AllocateGarbage()
        {
            if (_mode == "midlived")
            {
                // MidLivedFrames 전에 만든 묶음을 버리고 새 묶음을 보관한다 → 각 byte[]는 30프레임 산다
                var batch = new byte[ChunksPerFrame][]; // LAB: 의도적
                for (var i = 0; i < ChunksPerFrame; i++) batch[i] = new byte[ChunkSize]; // LAB: 의도적
                _midLived[_frame % MidLivedFrames] = batch;
                _last = batch[0];
            }
            else
            {
                for (var i = 0; i < ChunksPerFrame; i++) _last = new byte[ChunkSize]; // LAB: 의도적
            }

            if (_mode == "mutate")
            {
                // 배열 전체에 흩어진 위치를 맞바꾼다: 살아 있는 객체 집합은 그대로, 쓰기(배리어)만 발생
                var n = (uint)_live.Length;
                for (var k = 0; k < SwapsPerFrame; k++)
                {
                    var a = (int)(NextRandom() % n);
                    var b = (int)(NextRandom() % n);
                    (_live[a], _live[b]) = (_live[b], _live[a]);
                }
            }
            _frame++;
        }

        uint NextRandom()
        {
            // xorshift32: 할당 없는 난수
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return _rng;
        }

        static string ReadArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static void Summarize(LabContext ctx, string name, double[] values)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            var median = sorted[sorted.Length / 2];
            var over = 0;
            foreach (var v in values) if (v > median * 2) over++;
            ctx.Record("summary", name + " median", F3(median));
            ctx.Record("summary", name + " p99", F3(sorted[(int)(sorted.Length * 0.99)]));
            ctx.Record("summary", name + " max", F3(sorted[sorted.Length - 1]));
            ctx.Record("summary", name + " frames > 2x median", over.ToString(CultureInfo.InvariantCulture));
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
