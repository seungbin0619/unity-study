using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 프레임마다 객체 BatchSize개를 "꺼내고" 직전 프레임의 BatchSize개를 "돌려놓는" 작업을 두 방식으로 비교한다.
    /// A: Instantiate / Destroy, B: 미리 만든 풀에서 SetActive(true / false).
    /// 프레임마다 작업 시간(Stopwatch), 직전 프레임의 GC 할당, GC 횟수를 기록한다.
    /// </summary>
    [Preserve, Lab("02-L03")]
    public sealed class PoolingLab : ILab
    {
        const int BatchSize = 100;
        const int WarmupFrames = 30;
        const int SampleFrames = 600;

        GameObject _template;
        readonly GameObject[] _current = new GameObject[BatchSize];
        readonly GameObject[] _previous = new GameObject[BatchSize];
        GameObject[] _pool;
        int _poolTop;

        public async Awaitable Run(LabContext ctx)
        {
            _template = new GameObject("Template");
            _template.AddComponent<BoxCollider>();
            _template.AddComponent<PooledProbe>();

            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());

            await Measure(ctx, metrics, "A Instantiate/Destroy", InstantiateDestroyStep);
            ClearBatches(destroy: true);

            _pool = new GameObject[BatchSize * 2]; // 꺼낸 것 + 직전 프레임 것
            for (var i = 0; i < _pool.Length; i++)
            {
                var go = Object.Instantiate(_template);
                go.SetActive(false);
                _pool[i] = go;
            }
            _poolTop = _pool.Length;
            await Measure(ctx, metrics, "B Pool SetActive", PoolStep);
            ClearBatches(destroy: false);

            foreach (var go in _pool) Object.Destroy(go);
            Object.Destroy(_template);
        }

        void InstantiateDestroyStep()
        {
            for (var i = 0; i < BatchSize; i++)
            {
                if (_previous[i] != null) Object.Destroy(_previous[i]);
                _previous[i] = _current[i];
                _current[i] = Object.Instantiate(_template);
            }
        }

        void PoolStep()
        {
            for (var i = 0; i < BatchSize; i++)
            {
                var old = _previous[i];
                if (old != null)
                {
                    old.SetActive(false);
                    _pool[_poolTop++] = old;
                }
                _previous[i] = _current[i];
                var go = _pool[--_poolTop];
                go.SetActive(true);
                _current[i] = go;
            }
        }

        void ClearBatches(bool destroy)
        {
            for (var i = 0; i < BatchSize; i++)
            {
                foreach (var go in new[] { _current[i], _previous[i] })
                {
                    if (go == null) continue;
                    if (destroy) Object.Destroy(go);
                    else
                    {
                        go.SetActive(false);
                        _pool[_poolTop++] = go;
                    }
                }
                _current[i] = null;
                _previous[i] = null;
            }
        }

        static async Awaitable Measure(LabContext ctx, LabMetrics metrics, string name, Action step)
        {
            var allocBytes = new long[SampleFrames];
            var allocCounts = new long[SampleFrames];
            var workMs = new double[SampleFrames];
            var gcPerFrame = new int[SampleFrames];
            var stopwatch = new Stopwatch();

            await Awaitable.NextFrameAsync();
            var gcStart = 0;
            long usedStart = 0;
            for (var frame = -WarmupFrames; frame <= SampleFrames; frame++)
            {
                await Awaitable.NextFrameAsync();

                // 프레임 카운터는 직전 프레임(= 직전 step) 값
                if (frame > 0)
                {
                    allocBytes[frame - 1] = metrics.LastFrameGcAllocBytes;
                    allocCounts[frame - 1] = metrics.LastFrameGcAllocCount;
                }
                if (frame == SampleFrames) break;

                var gcBefore = GC.CollectionCount(0);
                if (frame == 0)
                {
                    gcStart = gcBefore;
                    usedStart = metrics.GcUsedBytes;
                }

                stopwatch.Restart();
                step();
                stopwatch.Stop();

                if (frame < 0) continue;
                workMs[frame] = stopwatch.Elapsed.TotalMilliseconds;
                gcPerFrame[frame] = GC.CollectionCount(0) - gcBefore;
            }

            var gcTotal = GC.CollectionCount(0) - gcStart;
            var usedEnd = metrics.GcUsedBytes;
            Summarize(ctx, name, allocBytes, allocCounts, workMs, gcPerFrame, gcTotal, usedStart, usedEnd);
        }

        static void Summarize(LabContext ctx, string name, long[] bytes, long[] counts, double[] ms, int[] gcPerFrame,
            int gcTotal, long usedStart, long usedEnd)
        {
            var inv = CultureInfo.InvariantCulture;
            var n = bytes.Length;
            long sumBytes = 0, maxBytes = 0, sumCounts = 0;
            double sumMs = 0, maxMs = 0, sumMsGc = 0, sumMsNoGc = 0;
            int framesWithGc = 0;
            for (var i = 0; i < n; i++)
            {
                sumBytes += bytes[i];
                maxBytes = Math.Max(maxBytes, bytes[i]);
                sumCounts += counts[i];
                sumMs += ms[i];
                maxMs = Math.Max(maxMs, ms[i]);
                if (gcPerFrame[i] > 0)
                {
                    framesWithGc++;
                    sumMsGc += ms[i];
                }
                else sumMsNoGc += ms[i];
            }

            var sorted = (double[])ms.Clone();
            Array.Sort(sorted);

            ctx.Record(name, "batch size", BatchSize.ToString());
            ctx.Record(name, "sample frames", n.ToString());
            ctx.Record(name, "frame alloc bytes avg", (sumBytes / (double)n).ToString("F0", inv));
            ctx.Record(name, "frame alloc bytes max", maxBytes.ToString());
            ctx.Record(name, "frame alloc count avg", (sumCounts / (double)n).ToString("F1", inv));
            ctx.Record(name, "work ms avg", (sumMs / n).ToString("F3", inv));
            ctx.Record(name, "work ms p50", sorted[n / 2].ToString("F3", inv));
            ctx.Record(name, "work ms p99", sorted[(int)(n * 0.99)].ToString("F3", inv));
            ctx.Record(name, "work ms max", maxMs.ToString("F3", inv));
            ctx.Record(name, "GC count during sample", gcTotal.ToString());
            ctx.Record(name, "frames with GC inside work", framesWithGc.ToString());
            ctx.Record(name, "work ms avg (frames with GC)", framesWithGc > 0 ? (sumMsGc / framesWithGc).ToString("F3", inv) : "-");
            ctx.Record(name, "work ms avg (frames without GC)", (sumMsNoGc / Math.Max(1, n - framesWithGc)).ToString("F3", inv));
            ctx.Record(name, "GC used start/end bytes", $"{usedStart}/{usedEnd}");

            // 프레임별 원본 (GC 주기·스파이크 위치 확인용)
            for (var i = 0; i < n; i++)
                ctx.Record(name, $"frame {i:D3}", $"{bytes[i]}|{ms[i].ToString("F3", inv)}|{gcPerFrame[i]}");
        }
    }
}
