using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 경계를 넘는 호출 3쌍의 호출당 GC 할당과 시간을 잰다.
    /// 프레임 F에서 한 케이스를 N번 실행하고, 프레임 F+1에서 F의 "GC Allocated In Frame"을 읽는다.
    /// 0a(빈 루프)는 프레임 자체의 잡음, 0b(new byte[1024])는 측정 경로가 맞는지 보는 보정용이다.
    /// </summary>
    [Preserve, Lab("01-L05")]
    public sealed class BoundaryCostLab : ILab
    {
        const int WarmupFrames = 3;
        const int SampleFrames = 10;
        const int VertexCount = 1000;

        readonly struct Case
        {
            public readonly string Name;
            public readonly int Iterations;
            public readonly Action<int> Body;

            public Case(string name, int iterations, Action<int> body)
            {
                Name = name;
                Iterations = iterations;
                Body = body;
            }
        }

        GameObject _go;
        BoxCollider _cachedCollider;
        Mesh _mesh;
        readonly List<Vector3> _list = new List<Vector3>(VertexCount);
        long _sink; // 결과를 버리지 않게 해서 루프가 최적화로 사라지지 않게 한다

        public async Awaitable Run(LabContext ctx)
        {
            _go = new GameObject("Target") { tag = "Player" };
            _cachedCollider = _go.AddComponent<BoxCollider>();
            _mesh = new Mesh { vertices = new Vector3[VertexCount] };

            var cases = new[]
            {
                new Case("0a baseline (empty loop)", 1000, n => { for (var i = 0; i < n; i++) _sink++; }),
                new Case("0b calibration new byte[1024]", 100, n => { for (var i = 0; i < n; i++) _sink += new byte[1024].Length; }), // LAB: 의도적
                new Case("1a GetComponent<BoxCollider>", 1000, n => { for (var i = 0; i < n; i++) if (!ReferenceEquals(_go.GetComponent<BoxCollider>(), null)) _sink++; }),
                new Case("1b cached field", 1000, n => { for (var i = 0; i < n; i++) if (!ReferenceEquals(_cachedCollider, null)) _sink++; }),
                new Case("1c GetComponent<Rigidbody> (missing)", 1000, n => { for (var i = 0; i < n; i++) if (!ReferenceEquals(_go.GetComponent<Rigidbody>(), null)) _sink++; }),
                new Case("2a mesh.vertices", 100, n => { for (var i = 0; i < n; i++) _sink += _mesh.vertices.Length; }), // LAB: 의도적
                new Case("2b mesh.GetVertices(list)", 100, n => { for (var i = 0; i < n; i++) { _mesh.GetVertices(_list); _sink += _list.Count; } }),
                new Case("3a go.tag == \"Player\"", 1000, n => { for (var i = 0; i < n; i++) if (_go.tag == "Player") _sink++; }), // LAB: 의도적
                new Case("3b go.CompareTag(\"Player\")", 1000, n => { for (var i = 0; i < n; i++) if (_go.CompareTag("Player")) _sink++; }),
            };

            // 없는 컴포넌트를 조회했을 때 돌려받는 값의 정체 (측정 구간 밖)
#pragma warning disable UNT0008 // LAB: 의도적 — Unity 객체에 ?. 사용
            ctx.Record("1c returned value", "is null", LabEval.Eval(() => _go.GetComponent<Rigidbody>() is null));
            ctx.Record("1c returned value", "== null", LabEval.Eval(() => _go.GetComponent<Rigidbody>() == null));
            ctx.Record("1c returned value", "?.mass", LabEval.Eval(() => _go.GetComponent<Rigidbody>()?.mass));
#pragma warning restore UNT0008

            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());
            var stopwatch = new Stopwatch();

            foreach (var c in cases)
            {
                var bytes = new long[SampleFrames];
                var counts = new long[SampleFrames];
                var ns = new double[SampleFrames];

                for (var frame = -WarmupFrames; frame < SampleFrames; frame++)
                {
                    await Awaitable.NextFrameAsync(); // 프레임 F 시작
                    stopwatch.Restart();
                    c.Body(c.Iterations);
                    stopwatch.Stop();
                    var elapsedNs = stopwatch.Elapsed.TotalMilliseconds * 1e6;

                    await Awaitable.NextFrameAsync(); // 프레임 F+1: F의 카운터를 읽는다
                    if (frame < 0) continue;
                    bytes[frame] = metrics.LastFrameGcAllocBytes;
                    counts[frame] = metrics.LastFrameGcAllocCount;
                    ns[frame] = elapsedNs / c.Iterations;
                }

                Summarize(ctx, c, bytes, counts, ns);
            }

            ctx.Record("-", "sink", _sink.ToString());
            Object.Destroy(_mesh);
            Object.Destroy(_go);
        }

        static void Summarize(LabContext ctx, Case c, long[] bytes, long[] counts, double[] ns)
        {
            long sumBytes = 0, maxBytes = 0, minBytes = long.MaxValue, sumCounts = 0;
            double sumNs = 0, minNs = double.MaxValue;
            for (var i = 0; i < bytes.Length; i++)
            {
                sumBytes += bytes[i];
                maxBytes = Math.Max(maxBytes, bytes[i]);
                minBytes = Math.Min(minBytes, bytes[i]);
                sumCounts += counts[i];
                sumNs += ns[i];
                minNs = Math.Min(minNs, ns[i]);
            }

            var n = bytes.Length;
            var inv = CultureInfo.InvariantCulture;
            ctx.Record(c.Name, "iterations per frame", c.Iterations.ToString());
            ctx.Record(c.Name, "frame alloc bytes avg", (sumBytes / (double)n).ToString("F0", inv));
            ctx.Record(c.Name, "frame alloc bytes min/max", $"{minBytes}/{maxBytes}");
            ctx.Record(c.Name, "frame alloc count avg", (sumCounts / (double)n).ToString("F1", inv));
            ctx.Record(c.Name, "alloc bytes per call", (sumBytes / (double)n / c.Iterations).ToString("F1", inv));
            ctx.Record(c.Name, "ns per call avg", (sumNs / n).ToString("F1", inv));
            ctx.Record(c.Name, "ns per call min", minNs.ToString("F1", inv));
        }
    }
}
