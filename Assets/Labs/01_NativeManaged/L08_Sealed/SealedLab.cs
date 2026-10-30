using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.NativeManaged
{
    /// <summary>
    /// SealedTargets를 호출해 스트리핑을 막고, 정적 타입별 Speak() 호출 시간을 잰다.
    /// 케이스 순서를 표본마다 돌려서 측정 순서에 따른 치우침을 줄인다.
    /// 생성 코드 자체는 빌드 후 il2cppOutput에서 읽는다.
    /// </summary>
    [Preserve, Lab("01-L08")]
    public sealed class SealedLab : ILab
    {
        const int CallsPerSample = 10_000_000;
        const int WarmupSamples = 3;
        const int Samples = 20;

        long _sink; // 결과를 버리지 않게 해서 호출이 최적화로 사라지지 않게 한다

        public async Awaitable Run(LabContext ctx)
        {
            var dog = new Dog();
            var sealedDog = new SealedDog();
            var wolf = new Wolf();

            ctx.Record("A CallAnimal(Dog)", "result", SealedTargets.CallAnimal(dog).ToString());
            ctx.Record("B CallDog", "result", SealedTargets.CallDog(dog).ToString());
            ctx.Record("C CallSealedDog", "result", SealedTargets.CallSealedDog(sealedDog).ToString());
            ctx.Record("D CallWolf", "result", SealedTargets.CallWolf(wolf).ToString());

            var fox = new Fox();
            var openFox = new OpenFox();
            ctx.Record("E CallFox", "result", SealedTargets.CallFox(fox).ToString());
            ctx.Record("F CallOpenFox", "result", SealedTargets.CallOpenFox(openFox).ToString());

            var names = new[] { "A Animal (Dog instance)", "B Dog", "C SealedDog", "D Wolf", "E Fox", "F OpenFox" };
            var bodies = new Func<int, long>[]
            {
                n => SealedTargets.LoopAnimal(dog, n),
                n => SealedTargets.LoopDog(dog, n),
                n => SealedTargets.LoopSealedDog(sealedDog, n),
                n => SealedTargets.LoopWolf(wolf, n),
                n => SealedTargets.LoopFox(fox, n),
                n => SealedTargets.LoopOpenFox(openFox, n),
            };

            var ns = new double[names.Length, Samples];
            var stopwatch = new Stopwatch();

            for (var s = -WarmupSamples; s < Samples; s++)
            {
                for (var k = 0; k < names.Length; k++)
                {
                    var c = (k + Math.Max(s, 0)) % names.Length; // 표본마다 시작 케이스를 돌린다
                    stopwatch.Restart();
                    _sink += bodies[c](CallsPerSample);
                    stopwatch.Stop();
                    if (s >= 0)
                        ns[c, s] = stopwatch.Elapsed.TotalMilliseconds * 1e6 / CallsPerSample;
                }
            }

            for (var c = 0; c < names.Length; c++)
            {
                double sum = 0, min = double.MaxValue, max = 0;
                for (var s = 0; s < Samples; s++)
                {
                    sum += ns[c, s];
                    min = Math.Min(min, ns[c, s]);
                    max = Math.Max(max, ns[c, s]);
                }
                ctx.Record(names[c], "ns/call avg", (sum / Samples).ToString("F3", CultureInfo.InvariantCulture));
                ctx.Record(names[c], "ns/call min", min.ToString("F3", CultureInfo.InvariantCulture));
                ctx.Record(names[c], "ns/call max", max.ToString("F3", CultureInfo.InvariantCulture));
            }
            ctx.Record("-", "sink", _sink.ToString());

            await Awaitable.NextFrameAsync();
        }
    }
}
