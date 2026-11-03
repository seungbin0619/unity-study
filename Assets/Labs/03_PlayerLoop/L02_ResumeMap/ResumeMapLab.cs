using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 모든 하위 시스템 앞에 마커를 끼운 뒤(LoopMarkers), Update에서 코루틴·Awaitable·Task 프로브를 동시에 시작해
    /// 각각이 어느 하위 시스템 안에서, 몇 프레임 뒤에, 어느 스레드에서 재개되는지 기록한다.
    /// 시행마다 Timeout 프레임을 기다리고, 그때까지 재개되지 않은 프로브는 "not resumed"로 남긴다.
    /// condition = 프로브, metric = "trial N", value = "+Δframe | 단계/하위 시스템 | main 또는 thread id".
    /// </summary>
    [Preserve, Lab("03-L02")]
    public sealed class ResumeMapLab : ILab
    {
        const int Trials = 3;
        const int Warmup = 10;
        const int Timeout = 60;

        static readonly string[] Probes =
        {
            "callback Update (start)", "callback LateUpdate", "callback FixedUpdate",
            "A coroutine first segment", "A yield return null", "B WaitForSeconds(0.01)",
            "C WaitForFixedUpdate", "D WaitForEndOfFrame",
            "E awaitable first segment", "E Awaitable.NextFrameAsync", "F Awaitable.FixedUpdateAsync",
            "G Awaitable.EndOfFrameAsync", "H await Task.Yield()", "I Awaitable.WaitForSecondsAsync(0.01)",
        };

        public async Awaitable Run(LabContext ctx)
        {
            ctx.Record("-", "Application.isBatchMode", Application.isBatchMode.ToString().ToLowerInvariant());
            ctx.Record("-", "graphicsDeviceType", SystemInfo.graphicsDeviceType.ToString());
            ctx.Record("-", "fixedDeltaTime", Time.fixedDeltaTime.ToString(CultureInfo.InvariantCulture));

            var mainThread = Thread.CurrentThread.ManagedThreadId;
            LoopMarkers.Install();
            var go = new GameObject("ResumeProbe");
            var probe = go.AddComponent<ResumeProbe>();
            try
            {
                for (var i = 0; i < Warmup; i++)
                    await Awaitable.NextFrameAsync();

                for (var t = 1; t <= Trials; t++)
                {
                    probe.StartTrial(t);
                    var begin = Time.realtimeSinceStartupAsDouble;
                    for (var i = 0; i < Timeout; i++)
                        await Awaitable.NextFrameAsync();
                    // 대기 시간이 프레임 수가 아니라 시간으로 정해지는 프로브(B·C·F·I)를 해석하려고 남긴다
                    var ms = (Time.realtimeSinceStartupAsDouble - begin) * 1000.0;
                    ctx.Record("-", $"trial {t} wall ms for {Timeout} frames", ms.ToString("F2", CultureInfo.InvariantCulture));
                }
            }
            finally
            {
                LoopMarkers.Uninstall();
            }

            for (var t = 1; t <= Trials; t++)
            {
                foreach (var name in Probes)
                {
                    var found = false;
                    foreach (var h in probe.Hits)
                    {
                        if (h.Trial != t || h.Probe != name)
                            continue;
                        found = true;
                        var thread = h.Thread == mainThread ? "main" : "thread " + h.Thread;
                        ctx.Record(name, "trial " + t, $"+{h.Frame - h.StartFrame} | {h.Where} | {thread}");
                    }
                    if (!found)
                        ctx.Record(name, "trial " + t, "not resumed");
                }
            }
            Object.Destroy(go);
            Debug.Log($"[03-L02] hits {probe.Hits.Count}");
        }
    }
}
