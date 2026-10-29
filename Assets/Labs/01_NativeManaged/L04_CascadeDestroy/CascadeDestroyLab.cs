using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;
using static Labs.LabEval;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 부모만 Destroy했을 때 자식 컴포넌트를 캐싱한 참조와 정적 이벤트 구독이 어떻게 되는지 본다.
    /// (a) LeakyListener: 구독 유지, (b) CleanListener: OnDisable에서 해제.
    /// 마지막에 WeakReference로 매니지드 객체가 GC로 회수되는지 확인한다.
    /// </summary>
    [Preserve, Lab("01-L04")]
    public sealed class CascadeDestroyLab : ILab
    {
        public async Awaitable Run(LabContext ctx)
        {
            GameEvents.Clear();

            var leaky = new Case<LeakyListener>("a_Leaky");
            var clean = new Case<CleanListener>("b_Clean");
            leaky.Create();
            clean.Create();
            ctx.Record("-", "subscribers after create", GameEvents.SubscriberCount.ToString());

            Object.Destroy(leaky.Parent);
            Object.Destroy(clean.Parent);
            leaky.RecordNullChecks(ctx, "1 same frame");
            clean.RecordNullChecks(ctx, "1 same frame");

            await Awaitable.NextFrameAsync();
            leaky.RecordNullChecks(ctx, "2 next frame");
            clean.RecordNullChecks(ctx, "2 next frame");
            ctx.Record("a_Leaky", "OnDestroy called", LeakyListener.OnDestroyCalled.ToString().ToLowerInvariant());
            ctx.Record("b_Clean", "OnDestroy called", CleanListener.OnDestroyCalled.ToString().ToLowerInvariant());
            ctx.Record("-", "subscribers after destroy", GameEvents.SubscriberCount.ToString());

            GameEvents.Raise();
            ctx.Record("a_Leaky", "3 OnTick name", LeakyListener.LastTickResult);
            ctx.Record("b_Clean", "3 OnTick name", CleanListener.LastTickResult);

            // 4: 강한 참조를 모두 버리고 GC. 이벤트가 진짜 루트인지 보려고 Clear 후에도 한 번 더 본다.
            var leakyWeak = leaky.Release();
            var cleanWeak = clean.Release();
            await Awaitable.NextFrameAsync(); // 스택에 남은 참조가 보수적 스캔에 걸리지 않게 프레임을 넘긴다
            FullCollect();
            ctx.Record("a_Leaky", "4 alive after GC (subscribed)", leakyWeak.IsAlive.ToString().ToLowerInvariant());
            ctx.Record("b_Clean", "4 alive after GC", cleanWeak.IsAlive.ToString().ToLowerInvariant());

            GameEvents.Clear();
            await Awaitable.NextFrameAsync();
            FullCollect();
            ctx.Record("a_Leaky", "4 alive after GC (event cleared)", leakyWeak.IsAlive.ToString().ToLowerInvariant());
        }

        static void FullCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        sealed class Case<T> where T : MonoBehaviour
        {
            readonly string _name;
            public GameObject Parent;
            GameObject _child;
            T _cached;

            public Case(string name) => _name = name;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Create()
            {
                Parent = new GameObject("Parent");
                _child = new GameObject("Child");
                _child.transform.SetParent(Parent.transform);
                _cached = _child.AddComponent<T>();
            }

            public void RecordNullChecks(LabContext ctx, string when)
            {
                ctx.Record(_name, when + " cached == null", Eval(() => _cached == null));
                ctx.Record(_name, when + " child == null", Eval(() => _child == null));
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public WeakReference Release()
            {
                var weak = new WeakReference(_cached);
                _cached = null;
                _child = null;
                Parent = null;
                return weak;
            }
        }
    }
}
