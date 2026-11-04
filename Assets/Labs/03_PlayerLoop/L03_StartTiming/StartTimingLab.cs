using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 03-L02의 LoopMarkers를 끼운 상태에서 Spawner를 만들고, 생성 지점별로 Instantiate한 객체의
    /// Awake/OnEnable/Start/첫 FixedUpdate/첫 Update/첫 LateUpdate가 생성 프레임 기준 몇 프레임 뒤, 어느 하위 시스템에서 불리는지 기록한다.
    /// condition = 생성 지점, metric = 이벤트, value = "+Δframe | 위치". 템플릿 자신의 이벤트는 기록하지 않는다.
    /// </summary>
    [Preserve, Lab("03-L03")]
    public sealed class StartTimingLab : ILab
    {
        const int Warmup = 10;
        const int Wait = 60;

        static readonly string[] Labels =
        {
            "Spawner.Awake", "Spawner.Start", "Spawner.FixedUpdate", "Spawner.Update", "Spawner.LateUpdate",
            "coroutine after yield null", "after await Task.Yield()", "coroutine after WaitForEndOfFrame",
        };

        static readonly string[] Names =
        {
            "Awake", "OnEnable", "Start", "first FixedUpdate", "first Update", "first LateUpdate",
        };

        public async Awaitable Run(LabContext ctx)
        {
            ctx.Record("-", "Application.isBatchMode", Application.isBatchMode.ToString().ToLowerInvariant());
            LifecycleProbe.Events.Clear();
            Spawner.SpawnFrames.Clear();

            // 비활성 템플릿을 복제하면 복제본도 비활성이라 SetActive 경로가 섞인다.
            // 그래서 활성 템플릿을 쓰고, 템플릿 자신의 기록은 "template" 라벨로 걸러낸다.
            LifecycleProbe.NextLabel = "template";
            var template = new GameObject("ProbeTemplate");
            template.AddComponent<LifecycleProbe>();
            Spawner.Template = template;

            LoopMarkers.Install();
            GameObject spawner = null;
            try
            {
                for (var i = 0; i < Warmup; i++)
                    await Awaitable.NextFrameAsync();
                spawner = new GameObject("Spawner");
                spawner.AddComponent<Spawner>();
                var begin = Time.realtimeSinceStartupAsDouble;
                for (var i = 0; i < Wait; i++)
                    await Awaitable.NextFrameAsync();
                var ms = (Time.realtimeSinceStartupAsDouble - begin) * 1000.0;
                ctx.Record("-", $"wall ms for {Wait} frames", ms.ToString("F2", CultureInfo.InvariantCulture));
            }
            finally
            {
                LoopMarkers.Uninstall();
            }

            foreach (var label in Labels)
            {
                if (!Spawner.SpawnFrames.TryGetValue(label, out var spawnFrame))
                {
                    ctx.Record(label, "spawn", "not spawned");
                    continue;
                }
                foreach (var name in Names)
                {
                    var value = "not called";
                    foreach (var e in LifecycleProbe.Events)
                    {
                        if (e.Label == label && e.Name == name)
                        {
                            value = $"+{e.Frame - spawnFrame} | {e.Where}";
                            break;
                        }
                    }
                    ctx.Record(label, name, value);
                }
            }

            Object.Destroy(spawner);
            Object.Destroy(template);
            Debug.Log($"[03-L03] events {LifecycleProbe.Events.Count}");
        }
    }
}
