using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.NativeManaged
{
    /// <summary>
    /// List&lt;T&gt;.Add를 타입 5개로 호출해 인스턴스가 빌드에 들어가게 하고, Add 1회 시간을 잰다.
    /// 용량을 미리 잡아 두어 측정 구간에서 배열 재할당이 일어나지 않게 한다.
    /// 생성 코드 자체는 빌드 후 il2cppOutput에서 읽는다.
    /// </summary>
    [Preserve, Lab("01-L09")]
    public sealed class GenericSharingLab : ILab
    {
        const int AddsPerSample = 1_000_000;
        const int WarmupSamples = 3;
        const int Samples = 20;

        long _sink; // 결과를 버리지 않게 해서 호출이 최적화로 사라지지 않게 한다

        public async Awaitable Run(LabContext ctx)
        {
            var ints = new List<int>(AddsPerSample);
            var floats = new List<float>(AddsPerSample);
            var vectors = new List<Vector3>(AddsPerSample);
            var strings = new List<string>(AddsPerSample);
            var objects = new List<GameObject>(AddsPerSample);
            var text = "lab";
            var go = new GameObject("L09 item");

            var names = new[] { "int", "float", "Vector3", "string", "GameObject" };
            var bodies = new Func<int, int>[]
            {
                n => GenericTargets.FillInt(ints, n),
                n => GenericTargets.FillFloat(floats, n),
                n => GenericTargets.FillVector3(vectors, n),
                n => GenericTargets.FillString(strings, text, n),
                n => GenericTargets.FillGameObject(objects, go, n),
            };

            var ns = new double[names.Length, Samples];
            var stopwatch = new Stopwatch();

            for (var s = -WarmupSamples; s < Samples; s++)
            {
                for (var k = 0; k < names.Length; k++)
                {
                    var c = (k + Math.Max(s, 0)) % names.Length; // 표본마다 시작 케이스를 돌린다
                    stopwatch.Restart();
                    _sink += bodies[c](AddsPerSample);
                    stopwatch.Stop();
                    if (s >= 0)
                        ns[c, s] = stopwatch.Elapsed.TotalMilliseconds * 1e6 / AddsPerSample;
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
                ctx.Record(names[c], "ns/add avg", (sum / Samples).ToString("F3", CultureInfo.InvariantCulture));
                ctx.Record(names[c], "ns/add min", min.ToString("F3", CultureInfo.InvariantCulture));
                ctx.Record(names[c], "ns/add max", max.ToString("F3", CultureInfo.InvariantCulture));
            }
            ctx.Record("-", "sink", _sink.ToString());

            UnityEngine.Object.Destroy(go);
            await Awaitable.NextFrameAsync();
        }
    }
}
