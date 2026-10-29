using UnityEngine;
using UnityEngine.Scripting;
using static Labs.LabEval;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// Destroy 직후(같은 프레임)와 1프레임 뒤에 null 관련 연산자 6개를 평가한다.
    /// 예외가 나면 결과 대신 예외 타입을 기록한다.
    /// </summary>
    [Preserve, Lab("01-L01")]
    public sealed class FakeNullLab : ILab
    {
        public async Awaitable Run(LabContext ctx)
        {
            var go = new GameObject("Target");
            var fallback = new GameObject("Fallback");
            Object.Destroy(go);

            Evaluate(ctx, "A_SameFrame", go, fallback);
            await Awaitable.NextFrameAsync();
            Evaluate(ctx, "B_NextFrame", go, fallback);

            Object.Destroy(fallback);
        }

        static void Evaluate(LabContext ctx, string condition, GameObject go, GameObject fallback)
        {
            ctx.Record(condition, "1 go == null", Eval(() => go == null));
            ctx.Record(condition, "2 go is null", Eval(() => go is null));
            ctx.Record(condition, "3 ReferenceEquals(go, null)", Eval(() => ReferenceEquals(go, null)));
            ctx.Record(condition, "4 (bool)go", Eval(() => (bool)go));
#pragma warning disable UNT0008 // LAB: 의도적 — Unity 객체에 ?. 사용
            ctx.Record(condition, "5 go?.name", Eval(() => go?.name));
#pragma warning restore UNT0008
#pragma warning disable UNT0007 // LAB: 의도적 — Unity 객체에 ?? 사용
            ctx.Record(condition, "6 (go ?? fallback).name", Eval(() => (go ?? fallback).name));
#pragma warning restore UNT0007
        }
    }
}
