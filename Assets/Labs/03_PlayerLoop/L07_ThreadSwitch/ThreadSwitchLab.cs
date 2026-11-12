using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// await 종류별로 다음 줄이 실행되는 스레드·프레임·PlayerLoop 위치를 기록한다.
    /// 워커 스레드에서는 Unity API(Time.frameCount 등)를 부르지 않는다 — 기록은 미리 잡은 배열에 하고, 출력은 끝난 뒤 메인에서.
    /// </summary>
    [Preserve, Lab("03-L07")]
    public sealed class ThreadSwitchLab : ILab
    {
        const int Rounds = 5;
        const int StepsPerRound = 8; // start + A..G

        struct Sample
        {
            public string Label;
            public int ThreadId;
            public bool IsMain;
            public bool IsThreadPool;
            public string Context;
            public int Frame;       // 메인일 때만, 아니면 -1
            public string Where;    // 메인일 때만
            public double Ms;
        }

        static int s_mainThreadId;
        static readonly Sample[] s_samples = new Sample[Rounds * StepsPerRound];
        static int s_count;
        static readonly Stopwatch s_sw = new Stopwatch();

        public async Awaitable Run(LabContext ctx)
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            LoopMarkers.Install();
            try
            {
                await Awaitable.NextFrameAsync();
                for (var r = 0; r < Rounds; r++)
                {
                    await Sequence();
                    // 다음 라운드 전에 메인으로 확실히 복귀하고 한 프레임 쉰다
                    await Awaitable.MainThreadAsync();
                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                // 여기는 메인 (MainThreadAsync 뒤)
                LoopMarkers.Uninstall();
            }

            ctx.Record("-", "main thread id", s_mainThreadId.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < s_count; i++)
            {
                var s = s_samples[i];
                var cond = "round " + (i / StepsPerRound + 1) + " " + s.Label;
                ctx.Record(cond, "thread", s.IsMain ? "main" : (s.IsThreadPool ? "worker(pool)" : "other"));
                ctx.Record(cond, "thread id", s.ThreadId.ToString(CultureInfo.InvariantCulture));
                ctx.Record(cond, "SynchronizationContext", s.Context);
                ctx.Record(cond, "frame", s.Frame < 0 ? "-" : s.Frame.ToString(CultureInfo.InvariantCulture));
                ctx.Record(cond, "where", s.Where ?? "-");
                ctx.Record(cond, "ms since round start", s.Ms.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        static async Task Sequence()
        {
            s_sw.Restart();
            Mark("0 start");

            await Task.Run(Work);
            Mark("A after await Task.Run");

            await Task.Run(Work).ConfigureAwait(false);
            Mark("B after Task.Run ConfigureAwait(false)");

            await Task.Delay(10);
            Mark("C after Task.Delay (from B's thread)");

            await Awaitable.MainThreadAsync();
            Mark("D after Awaitable.MainThreadAsync");

            await Task.Delay(10);
            Mark("E after Task.Delay (from main)");

            await Awaitable.BackgroundThreadAsync();
            Mark("F after Awaitable.BackgroundThreadAsync");

            await Awaitable.MainThreadAsync();
            Mark("G after Awaitable.MainThreadAsync");
        }

        static void Work()
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalMilliseconds < 3) { } // LAB: 의도적 — 순수 계산 흉내
        }

        static void Mark(string label)
        {
            var t = Thread.CurrentThread;
            var isMain = t.ManagedThreadId == s_mainThreadId;
            var sc = SynchronizationContext.Current;
            s_samples[s_count++] = new Sample
            {
                Label = label,
                ThreadId = t.ManagedThreadId,
                IsMain = isMain,
                IsThreadPool = t.IsThreadPoolThread,
                Context = sc == null ? "null" : sc.GetType().Name,
                Frame = isMain ? Time.frameCount : -1,
                Where = isMain ? LoopMarkers.Where : null,
                Ms = s_sw.Elapsed.TotalMilliseconds,
            };
        }
    }
}
