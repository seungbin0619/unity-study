using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.NativeManaged
{
    /// <summary>
    /// CheckTargets를 실제로 호출해 스트리핑되지 않게 하고, 두 가지를 기록한다.
    /// 1) null 체인에서 나는 예외 타입 2) SumArray와 검사를 끈 SumArrayUnchecked의 원소당 시간.
    /// 생성 코드 자체는 빌드 후 il2cppOutput에서 읽는다.
    /// </summary>
    [Preserve, Lab("01-L07")]
    public sealed class NullCheckLab : ILab
    {
        const int Length = 1_000_000;
        const int CallsPerSample = 10;
        const int WarmupSamples = 3;
        const int Samples = 20;

        long _sink; // 결과를 버리지 않게 해서 호출이 최적화로 사라지지 않게 한다

        public async Awaitable Run(LabContext ctx)
        {
            var chain = new ChainNode { Value = 1, Next = new ChainNode { Value = 2, Next = new ChainNode { Value = 3 } } };
            ctx.Record("A ReadChain", "result", CheckTargets.ReadChain(chain).ToString());
            ctx.Record("A ReadChain(Next == null)", "exception", LabEval.Eval(() => CheckTargets.ReadChain(new ChainNode()))); // LAB: 의도적
            ctx.Record("C DoubleValue", "result", chain.DoubleValue().ToString());

            var array = new int[Length];
            for (var i = 0; i < Length; i++)
                array[i] = i & 0xFF;

            Measure(ctx, "B SumArray", CheckTargets.SumArray, array);
            Measure(ctx, "D SumArrayUnchecked", CheckTargets.SumArrayUnchecked, array);
            ctx.Record("-", "sink", _sink.ToString());

            await Awaitable.NextFrameAsync();
        }

        void Measure(LabContext ctx, string condition, Func<int[], int> body, int[] array)
        {
            var stopwatch = new Stopwatch();
            var ns = new double[Samples];

            for (var s = -WarmupSamples; s < Samples; s++)
            {
                stopwatch.Restart();
                for (var c = 0; c < CallsPerSample; c++)
                    _sink += body(array);
                stopwatch.Stop();

                if (s >= 0)
                    ns[s] = stopwatch.Elapsed.TotalMilliseconds * 1e6 / ((double)Length * CallsPerSample);
            }

            double sum = 0, min = double.MaxValue, max = 0;
            foreach (var v in ns)
            {
                sum += v;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            ctx.Record(condition, "ns/element avg", (sum / Samples).ToString("F3", CultureInfo.InvariantCulture));
            ctx.Record(condition, "ns/element min", min.ToString("F3", CultureInfo.InvariantCulture));
            ctx.Record(condition, "ns/element max", max.ToString("F3", CultureInfo.InvariantCulture));
        }
    }
}
