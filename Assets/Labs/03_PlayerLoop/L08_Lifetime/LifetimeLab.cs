using System;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 한 프레임마다 도는 루프 7종을 MonoBehaviour 하나(Host)에 걸어 두고,
    /// Destroy / SetActive(false) / enabled = false 뒤에 각 루프가 계속 도는지 센다.
    /// 루프는 Time.frameCount만 기록한다 (AwTouch만 의도적으로 transform을 읽는다).
    /// </summary>
    [Preserve, Lab("03-L08")]
    public sealed class LifetimeLab : ILab
    {
        const int FramesBeforeOp = 3;
        const int FramesAfterOp = 5;      // op → restore
        const int FramesAfterRestore = 5; // restore → stop
        const int FramesAfterStop = 3;

        enum Op { Destroy, SetActiveFalse, EnabledFalse }

        public async Awaitable Run(LabContext ctx)
        {
            var otherGo = new GameObject("L08 OtherHost");
            var other = otherGo.AddComponent<LifetimeOtherHost>();
            try
            {
                await Awaitable.NextFrameAsync();
                foreach (Op op in Enum.GetValues(typeof(Op)))
                    await RunScenario(ctx, op, other);
            }
            finally
            {
                UnityEngine.Object.Destroy(otherGo);
            }
        }

        static async Awaitable RunScenario(LabContext ctx, Op op, LifetimeOtherHost other)
        {
            var go = new GameObject("L08 Host " + op);
            var host = go.AddComponent<LifetimeHost>();
            var probes = host.Begin(other);

            for (var i = 0; i < FramesBeforeOp; i++)
                await Awaitable.NextFrameAsync();

            var opFrame = Time.frameCount;
            foreach (var p in probes) p.AtOp = p.Count;
            switch (op)
            {
                case Op.Destroy: UnityEngine.Object.Destroy(go); break;
                case Op.SetActiveFalse: go.SetActive(false); break;
                case Op.EnabledFalse: host.enabled = false; break;
            }

            for (var i = 0; i < FramesAfterOp; i++)
                await Awaitable.NextFrameAsync();

            var restoreFrame = Time.frameCount;
            foreach (var p in probes) p.AtRestore = p.Count;
            switch (op)
            {
                case Op.SetActiveFalse: go.SetActive(true); break;
                case Op.EnabledFalse: host.enabled = true; break;
            }

            for (var i = 0; i < FramesAfterRestore; i++)
                await Awaitable.NextFrameAsync();

            foreach (var p in probes) { p.AtStop = p.Count; p.Stop = true; }
            for (var i = 0; i < FramesAfterStop; i++)
                await Awaitable.NextFrameAsync();

            if (go != null) UnityEngine.Object.Destroy(go);
            await Awaitable.NextFrameAsync();

            var cond = op.ToString();
            ctx.Record(cond, "op frame", opFrame.ToString(CultureInfo.InvariantCulture));
            ctx.Record(cond, "restore frame", restoreFrame.ToString(CultureInfo.InvariantCulture));
            foreach (var p in probes)
            {
                var c = cond + " " + p.Name;
                ctx.Record(c, "ticks before op", p.AtOp.ToString(CultureInfo.InvariantCulture));
                ctx.Record(c, "ticks op->restore", (p.AtRestore - p.AtOp).ToString(CultureInfo.InvariantCulture));
                ctx.Record(c, "ticks restore->stop", (p.AtStop - p.AtRestore).ToString(CultureInfo.InvariantCulture));
                ctx.Record(c, "last tick frame - op frame", (p.LastFrame - opFrame).ToString(CultureInfo.InvariantCulture));
                ctx.Record(c, "end", p.EndReason);
                ctx.Record(c, "end frame - op frame", p.EndFrame < 0 ? "-" : (p.EndFrame - opFrame).ToString(CultureInfo.InvariantCulture));
                ctx.Record(c, "finally ran", p.FinallyRan ? "yes" : "no");
            }
        }
    }

    /// <summary>루프 하나의 기록. 매니지드 객체라 Host가 파괴돼도 읽을 수 있다.</summary>
    public sealed class LoopProbe
    {
        public readonly string Name;
        public int Count, LastFrame = -1, EndFrame = -1;
        public int AtOp, AtRestore, AtStop;
        public bool Stop, FinallyRan;
        public string EndReason = "still suspended";

        public LoopProbe(string name) => Name = name;

        public void Tick() { Count++; LastFrame = Time.frameCount; }

        public void End(string reason)
        {
            EndReason = reason;
            EndFrame = Time.frameCount;
        }
    }

    [Preserve]
    public sealed class LifetimeOtherHost : MonoBehaviour { }

    [Preserve]
    public sealed class LifetimeHost : MonoBehaviour
    {
        public LoopProbe[] Begin(LifetimeOtherHost other)
        {
            var co = new LoopProbe("Co");
            var coOther = new LoopProbe("CoOnOtherHost");
            var aw = new LoopProbe("Aw");
            var task = new LoopProbe("Task");
            var awTok = new LoopProbe("AwToken");
            var taskTok = new LoopProbe("TaskToken");
            var awTouch = new LoopProbe("AwTouchTransform");

            var token = destroyCancellationToken; // 파괴 뒤에는 읽지 않는다

            StartCoroutine(CoLoop(co));
            other.StartCoroutine(CoLoop(coOther)); // 이 Host의 이터레이터를 다른 MonoBehaviour가 돌린다
            _ = AwLoop(aw, default);
            _ = TaskLoop(task, default);
            _ = AwLoop(awTok, token);
            _ = TaskLoop(taskTok, token);
            _ = AwTouchLoop(awTouch);
            return new[] { co, coOther, aw, task, awTok, taskTok, awTouch };
        }

        IEnumerator CoLoop(LoopProbe p)
        {
            try
            {
                while (!p.Stop)
                {
                    p.Tick();
                    yield return null;
                }
                p.End("stopped by flag");
            }
            finally
            {
                p.FinallyRan = true; // Unity가 코루틴을 버릴 때 Dispose()를 부르는지 확인
            }
        }

        static async Awaitable AwLoop(LoopProbe p, CancellationToken ct)
        {
            try
            {
                while (!p.Stop)
                {
                    p.Tick();
                    await Awaitable.NextFrameAsync(ct);
                }
                p.End("stopped by flag");
            }
            catch (Exception e)
            {
                p.End(e.GetType().Name);
            }
            finally
            {
                p.FinallyRan = true;
            }
        }

        static async Task TaskLoop(LoopProbe p, CancellationToken ct)
        {
            try
            {
                while (!p.Stop)
                {
                    p.Tick();
                    await Task.Yield();
                    ct.ThrowIfCancellationRequested(); // Task.Yield는 토큰을 받지 않으므로 직접 확인
                }
                p.End("stopped by flag");
            }
            catch (Exception e)
            {
                p.End(e.GetType().Name);
            }
            finally
            {
                p.FinallyRan = true;
            }
        }

        async Awaitable AwTouchLoop(LoopProbe p)
        {
            try
            {
                while (!p.Stop)
                {
                    p.Tick();
                    _ = transform.position; // LAB: 의도적 — 파괴 뒤 접근 예외 확인
                    await Awaitable.NextFrameAsync();
                }
                p.End("stopped by flag");
            }
            catch (Exception e)
            {
                p.End(e.GetType().Name);
            }
            finally
            {
                p.FinallyRan = true;
            }
        }
    }
}
