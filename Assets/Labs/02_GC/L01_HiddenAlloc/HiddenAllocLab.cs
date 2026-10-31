using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// AllocTargets의 케이스 A~K를 프레임마다 N번 호출하고 호출당 GC 할당을 잰다.
    /// 프레임 F에서 실행하고 F+1에서 F의 "GC Allocated In Frame"을 읽는다.
    /// 케이스마다 첫 워밍업 프레임의 할당도 따로 남긴다 (첫 호출에만 생기는 캐시·정적 초기화 확인용).
    /// </summary>
    [Preserve, Lab("02-L01")]
    public sealed class HiddenAllocLab : ILab
    {
        const int WarmupFrames = 3;
        const int SampleFrames = 10;
        const int Iterations = 100;

        readonly struct Case
        {
            public readonly string Name;
            public readonly Action Body;

            public Case(string name, Action body)
            {
                Name = name;
                Body = body;
            }
        }

        long _sink; // 0b 보정 케이스의 결과를 버리지 않게 한다

        public async Awaitable Run(LabContext ctx)
        {
            var t = new AllocTargets();
            var cases = new[]
            {
                new Case("0a baseline (empty)", () => { }),
                new Case("0b calibration new byte[16]", () => _sink += new byte[16].Length), // LAB: 의도적
                new Case("A boxing", t.Boxing), // LAB: 의도적 (이하 A~K)
                new Case("B interpolation", t.Interpolation),
                new Case("C capturing lambda", t.CapturingLambda),
                new Case("D non-capturing lambda", t.NonCapturingLambda),
                new Case("E static method group", t.StaticMethodGroup),
                new Case("F foreach List<int>", t.ForeachList),
                new Case("G foreach IEnumerable<int>", t.ForeachInterface),
                new Case("H LINQ Count(pred)", t.LinqCount),
                new Case("I new WaitForSeconds", t.NewWaitForSeconds),
                new Case("J params (3 args)", t.ParamsThree),
                new Case("K params (0 args)", t.ParamsZero),
            };

            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());

            foreach (var c in cases)
            {
                var bytes = new long[SampleFrames];
                var counts = new long[SampleFrames];
                long firstFrameBytes = 0, firstFrameCount = 0;

                for (var frame = -WarmupFrames; frame < SampleFrames; frame++)
                {
                    await Awaitable.NextFrameAsync(); // 프레임 F 시작
                    for (var i = 0; i < Iterations; i++) c.Body();

                    await Awaitable.NextFrameAsync(); // 프레임 F+1: F의 카운터를 읽는다
                    if (frame == -WarmupFrames)
                    {
                        firstFrameBytes = metrics.LastFrameGcAllocBytes;
                        firstFrameCount = metrics.LastFrameGcAllocCount;
                    }
                    if (frame < 0) continue;
                    bytes[frame] = metrics.LastFrameGcAllocBytes;
                    counts[frame] = metrics.LastFrameGcAllocCount;
                }

                Summarize(ctx, c, bytes, counts, firstFrameBytes, firstFrameCount);
            }

            ctx.Record("-", "sink", (_sink + t.Sink).ToString());
        }

        static void Summarize(LabContext ctx, Case c, long[] bytes, long[] counts, long firstBytes, long firstCount)
        {
            long sumBytes = 0, maxBytes = 0, minBytes = long.MaxValue, sumCounts = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                sumBytes += bytes[i];
                maxBytes = Math.Max(maxBytes, bytes[i]);
                minBytes = Math.Min(minBytes, bytes[i]);
                sumCounts += counts[i];
            }

            var n = bytes.Length;
            var inv = CultureInfo.InvariantCulture;
            ctx.Record(c.Name, "iterations per frame", Iterations.ToString());
            ctx.Record(c.Name, "frame alloc bytes min/max", $"{minBytes}/{maxBytes}");
            ctx.Record(c.Name, "alloc bytes per call", (sumBytes / (double)n / Iterations).ToString("F1", inv));
            ctx.Record(c.Name, "alloc count per call", (sumCounts / (double)n / Iterations).ToString("F2", inv));
            ctx.Record(c.Name, "first frame alloc bytes/count", $"{firstBytes}/{firstCount}");
        }
    }
}
