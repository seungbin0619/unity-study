using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// ClosureTargets의 A~D를 use = false / true로 프레임마다 N번 호출하고 호출당 GC 할당을 잰다.
    /// 프레임 F에서 실행하고 F+1에서 F의 "GC Allocated In Frame"을 읽는다 (02-L01과 같은 방식).
    /// </summary>
    [Preserve, Lab("02-L02")]
    public sealed class ClosureTimingLab : ILab
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
            var t = new ClosureTargets();
            var arg = 3;
            var off = false;
            var on = true;
            var cases = new[]
            {
                new Case("0a baseline (empty)", () => { }),
                new Case("0b calibration new byte[16]", () => _sink += new byte[16].Length), // LAB: 의도적
                new Case("A capture parameter, use=false", () => t.CaptureParameter(arg, off)), // LAB: 의도적 (이하 A~D)
                new Case("B capture top local, use=false", () => t.CaptureTopLocal(off)),
                new Case("C capture branch local, use=false", () => t.CaptureBranchLocal(off)),
                new Case("D capture this only, use=false", () => t.CaptureThisOnly(off)),
                new Case("A capture parameter, use=true", () => t.CaptureParameter(arg, on)),
                new Case("B capture top local, use=true", () => t.CaptureTopLocal(on)),
                new Case("C capture branch local, use=true", () => t.CaptureBranchLocal(on)),
                new Case("D capture this only, use=true", () => t.CaptureThisOnly(on)),
            };

            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());

            foreach (var c in cases)
            {
                var bytes = new long[SampleFrames];
                var counts = new long[SampleFrames];

                for (var frame = -WarmupFrames; frame < SampleFrames; frame++)
                {
                    await Awaitable.NextFrameAsync(); // 프레임 F 시작
                    for (var i = 0; i < Iterations; i++) c.Body();

                    await Awaitable.NextFrameAsync(); // 프레임 F+1: F의 카운터를 읽는다
                    if (frame < 0) continue;
                    bytes[frame] = metrics.LastFrameGcAllocBytes;
                    counts[frame] = metrics.LastFrameGcAllocCount;
                }

                Summarize(ctx, c, bytes, counts);
            }

            ctx.Record("-", "sink", (_sink + t.Sink).ToString());
        }

        static void Summarize(LabContext ctx, Case c, long[] bytes, long[] counts)
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
        }
    }
}
