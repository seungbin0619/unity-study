using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 1부: 대기자 100개를 동시에 돌려 반복당 할당. 2부: 1000개 시작 프레임의 호출당 할당 (5라운드).
    /// 3부: 같은 async 메서드 8회 vs 서로 다른 async 메서드 8개 (순차).
    /// 프레임 카운터는 직전 프레임 값이므로, 프레임 F의 할당은 F+1에서 읽는다.
    /// </summary>
    [Preserve, Lab("03-L06")]
    public sealed class WaitAllocLab : ILab
    {
        const int Waiters = 100;
        const int WarmupFrames = 30;
        const int MinWindowFrames = 120;
        const int MinWindowIterations = 3000;
        const int StartCount = 1000;
        const int StartRounds = 5;

        enum Wait { None, CoNull, CoZero, CoZeroGeneric, CoNewWait, CoCachedWait, AwNextFrame, TaskYield }
        enum Start { Coroutine, Awaitable, Task }

        public async Awaitable Run(LabContext ctx)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var go = new GameObject("WaitHost");
            var host = go.AddComponent<WaitHost>();
            using var metrics = new LabMetrics();
            if (!metrics.Valid)
                throw new InvalidOperationException("ProfilerRecorder 무효 — Development Build에서 실행할 것");

            try
            {
                // 1부
                foreach (Wait w in Enum.GetValues(typeof(Wait)))
                    await MeasurePerIteration(ctx, host, metrics, w);

                // 2부
                foreach (Start s in Enum.GetValues(typeof(Start)))
                    await MeasurePerStart(ctx, host, metrics, s);

                // 3부: 순서 효과를 피하려고 서로 다른 메서드를 먼저
                await MeasureHypothesis(ctx, metrics, different: true);
                await MeasureHypothesis(ctx, metrics, different: false);
            }
            finally
            {
                WaitHost.Running = false;
                Object.Destroy(go);
            }
        }

        static async Awaitable MeasurePerIteration(LabContext ctx, WaitHost host, LabMetrics m, Wait kind)
        {
            var name = "1 per-iteration " + kind;
            WaitHost.Running = true;
            WaitHost.Counting = false;
            for (var i = 0; i < Waiters && kind != Wait.None; i++)
                StartWaiter(host, kind);

            for (var i = 0; i < WarmupFrames; i++)
                await Awaitable.NextFrameAsync();
            if (kind == Wait.CoNewWait || kind == Wait.CoCachedWait)
                await Awaitable.WaitForSecondsAsync(0.1f); // 대기자마다 몇 번은 돌게

            WaitHost.Iterations = 0;
            WaitHost.Counting = true;
            await Awaitable.NextFrameAsync(); // 카운트를 켠 프레임은 할당 합계에서 뺀다
            long bytes = 0, count = 0;
            var frames = 0;
            var start = Time.realtimeSinceStartupAsDouble;
            while (frames < MinWindowFrames || (kind != Wait.None && WaitHost.Iterations < MinWindowIterations))
            {
                await Awaitable.NextFrameAsync();
                bytes += m.LastFrameGcAllocBytes;
                count += m.LastFrameGcAllocCount;
                frames++;
            }
            WaitHost.Counting = false;
            var iterations = WaitHost.Iterations;
            var seconds = Time.realtimeSinceStartupAsDouble - start;

            ctx.Record(name, "frames", I(frames));
            ctx.Record(name, "seconds", F(seconds));
            ctx.Record(name, "iterations", I(iterations));
            ctx.Record(name, "GC bytes total", I(bytes));
            ctx.Record(name, "GC alloc count total", I(count));
            ctx.Record(name, "GC bytes per frame", F(bytes / (double)frames));
            ctx.Record(name, "GC bytes per iteration", iterations > 0 ? F(bytes / (double)iterations) : "-");
            ctx.Record(name, "GC allocs per iteration", iterations > 0 ? F(count / (double)iterations) : "-");

            // 정리: 루프 종료 → 남은 코루틴 중지
            WaitHost.Running = false;
            await Awaitable.WaitForSecondsAsync(0.05f);
            host.StopAllCoroutines();
            for (var i = 0; i < 5; i++)
                await Awaitable.NextFrameAsync();
            GC.Collect();
            await Awaitable.NextFrameAsync();
        }

        static void StartWaiter(WaitHost host, Wait kind)
        {
            switch (kind)
            {
                case Wait.CoNull: host.StartCoroutine(host.CoNull()); break;
                case Wait.CoZero: host.StartCoroutine(host.CoZero()); break;
                case Wait.CoZeroGeneric: host.StartCoroutine(host.CoZeroGeneric()); break;
                case Wait.CoNewWait: host.StartCoroutine(host.CoNewWait()); break;
                case Wait.CoCachedWait: host.StartCoroutine(host.CoCachedWait()); break;
                case Wait.AwNextFrame: _ = WaitHost.AwNextFrame(); break;
                case Wait.TaskYield: _ = WaitHost.TaskYield(); break;
            }
        }

        static async Awaitable MeasurePerStart(LabContext ctx, WaitHost host, LabMetrics m, Start kind)
        {
            var name = "2 per-start " + kind;
            for (var round = 1; round <= StartRounds; round++)
            {
                await Awaitable.NextFrameAsync();
                // 측정 프레임: 시작만 한다 (각자 다음 재개 지점에서 끝남)
                for (var i = 0; i < StartCount; i++)
                {
                    switch (kind)
                    {
                        case Start.Coroutine: host.StartCoroutine(host.CoOnce()); break;
                        case Start.Awaitable: _ = WaitHost.AwOnce(); break;
                        case Start.Task: _ = WaitHost.TaskOnce(); break;
                    }
                }
                await Awaitable.NextFrameAsync();
                var bytes = m.LastFrameGcAllocBytes;
                var count = m.LastFrameGcAllocCount;
                ctx.Record(name, "round " + round + " bytes per start", F(bytes / (double)StartCount));
                ctx.Record(name, "round " + round + " allocs per start", F(count / (double)StartCount));
                // 모두 끝날 때까지
                for (var i = 0; i < 5; i++)
                    await Awaitable.NextFrameAsync();
            }
            GC.Collect();
            await Awaitable.NextFrameAsync();
        }

        static async Awaitable MeasureHypothesis(LabContext ctx, LabMetrics m, bool different)
        {
            var name = different ? "3 different methods x1 each" : "3 same method x8";
            for (var i = 0; i < 8; i++)
            {
                await Awaitable.NextFrameAsync();
                var a = different ? WaitHost.CallDifferent(i) : WaitHost.AwSame();
                await Awaitable.NextFrameAsync();
                ctx.Record(name, "call " + (i + 1) + " bytes", I(m.LastFrameGcAllocBytes));
                ctx.Record(name, "call " + (i + 1) + " allocs", I(m.LastFrameGcAllocCount));
                for (var f = 0; f < 3; f++)
                    await Awaitable.NextFrameAsync();
                // a는 이미 끝났다. 끝난 Awaitable을 다시 await하지 않는다 (풀로 돌아갔을 수 있음)
                _ = a;
            }
        }

        static string I(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
