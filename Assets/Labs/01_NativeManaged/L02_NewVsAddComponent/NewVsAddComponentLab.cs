using UnityEngine;
using UnityEngine.Scripting;
using static Labs.LabEval;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// new로 만든 MonoBehaviour(a)와 AddComponent로 만든 것(b)을 비교한다.
    /// 생성 줄의 콘솔 출력을 잡고, 두 객체에 null 관련 식 3개를 평가한다.
    /// </summary>
    [Preserve, Lab("01-L02")]
    public sealed class NewVsAddComponentLab : ILab
    {
        public async Awaitable Run(LabContext ctx)
        {
            var go = new GameObject("Host");
            ProbeBehaviour a = null, b = null;

            ctx.Record("a_new", "0 console", CaptureLog(() => a = new ProbeBehaviour())); // LAB: 의도적
            ctx.Record("b_AddComponent", "0 console", CaptureLog(() => b = go.AddComponent<ProbeBehaviour>()));

            Evaluate(ctx, "a_new", a);
            Evaluate(ctx, "b_AddComponent", b);

            Object.Destroy(go);
            await Awaitable.NextFrameAsync();
        }

        static void Evaluate(LabContext ctx, string condition, ProbeBehaviour x)
        {
            ctx.Record(condition, "1 x == null", Eval(() => x == null));
            ctx.Record(condition, "2 x is null", Eval(() => x is null));
#pragma warning disable UNT0008 // LAB: 의도적 — Unity 객체에 ?. 사용
            ctx.Record(condition, "3 x?.name", Eval(() => x?.name));
#pragma warning restore UNT0008
        }
    }
}
