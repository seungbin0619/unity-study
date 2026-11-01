using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 같은 원소 Count개를 서로 다른 배치로 살려 둔 채 GC.Collect() 시간을 잰다.
    /// 0 기준선, 1 List&lt;class&gt;, 2 struct[], 3a 참조 필드 struct[] (같은 문자열 하나), 3b 참조 필드 struct[] (원소마다 다른 문자열).
    /// 순서 효과(힙이 커지는 것)를 보려고 마지막에 기준선을 한 번 더 잰다.
    /// 원소 수는 플레이어 인자 -count N으로 바꿀 수 있다 (기본 10만).
    /// </summary>
    [Preserve, Lab("02-L05")]
    public sealed class MarkCostLab : ILab
    {
        const int DefaultCount = 100_000;
        const int WarmupCollects = 3;
        const int SampleCollects = 31;
        const int SettleFrames = 5;

        // 측정 중 살아 있어야 하므로 필드에 보관한다
        List<EnemyClass> _list;
        EnemyStruct[] _structs;
        EnemyStructRef[] _structRefs;

        readonly double[] _samples = new double[SampleCollects];

        public async Awaitable Run(LabContext ctx)
        {
            var count = ReadCount();
            using var metrics = new LabMetrics();
            ctx.Record("-", "metrics valid", metrics.Valid.ToString().ToLowerInvariant());
            ctx.Record("-", "count", count.ToString(CultureInfo.InvariantCulture));

            await Measure(ctx, metrics, "0 baseline", () => { });

            await Measure(ctx, metrics, "1 List<EnemyClass>", () =>
            {
                _list = new List<EnemyClass>(count);
                for (var i = 0; i < count; i++) _list.Add(new EnemyClass { Hp = i }); // LAB: 의도적
            });
            _list = null;

            await Measure(ctx, metrics, "2 EnemyStruct[]", () =>
            {
                _structs = new EnemyStruct[count]; // LAB: 의도적
                for (var i = 0; i < count; i++) _structs[i].Hp = i;
            });
            _structs = null;

            await Measure(ctx, metrics, "3a EnemyStructRef[] shared string", () =>
            {
                const string shared = "Enemy";
                _structRefs = new EnemyStructRef[count]; // LAB: 의도적
                for (var i = 0; i < count; i++) { _structRefs[i].Hp = i; _structRefs[i].Name = shared; }
            });
            _structRefs = null;

            await Measure(ctx, metrics, "3b EnemyStructRef[] distinct strings", () =>
            {
                _structRefs = new EnemyStructRef[count]; // LAB: 의도적
                for (var i = 0; i < count; i++) { _structRefs[i].Hp = i; _structRefs[i].Name = "Enemy_" + i; } // LAB: 의도적
            });
            _structRefs = null;

            await Measure(ctx, metrics, "0' baseline again", () => { });
        }

        static int ReadCount()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-count");
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var n) && n > 0) return n;
            return DefaultCount;
        }

        async Awaitable Measure(LabContext ctx, LabMetrics metrics, string condition, Action build)
        {
            // 직전 경우의 데이터를 치운 상태에서 시작
            GC.Collect();
            await Settle();
            var usedBefore = metrics.GcUsedBytes;

            build();
            for (var i = 0; i < WarmupCollects; i++) GC.Collect();
            await Settle();
            var usedAfter = metrics.GcUsedBytes;

            for (var i = 0; i < SampleCollects; i++)
            {
                var start = Stopwatch.GetTimestamp();
                GC.Collect();
                _samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }

            for (var i = 0; i < SampleCollects; i++)
                ctx.Record(condition, "collect ms #" + i.ToString("D2", CultureInfo.InvariantCulture), Ms(_samples[i]));

            Array.Sort(_samples);
            ctx.Record(condition, "collect ms min", Ms(_samples[0]));
            ctx.Record(condition, "collect ms median", Ms(_samples[SampleCollects / 2]));
            ctx.Record(condition, "collect ms max", Ms(_samples[SampleCollects - 1]));
            ctx.Record(condition, "GC Used delta MB", Mb(usedAfter - usedBefore));
            ctx.Record(condition, "GC Used MB", Mb(usedAfter));
            ctx.Record(condition, "GC Reserved MB", Mb(metrics.GcReservedBytes));
        }

        static async Awaitable Settle()
        {
            for (var i = 0; i < SettleFrames; i++) await Awaitable.NextFrameAsync();
        }

        static string Ms(double ms) => ms.ToString("F3", CultureInfo.InvariantCulture);
        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
    }
}
