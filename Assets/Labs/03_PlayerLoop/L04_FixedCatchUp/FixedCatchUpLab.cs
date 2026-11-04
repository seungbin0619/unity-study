using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 프레임 길이(Update에서 Sleep)와 물리 스텝 비용(FixedUpdate에서 Sleep), maximumDeltaTime을 바꿔 가며
    /// 프레임당 FixedUpdate 횟수, Time.deltaTime, 실제 프레임 시간, 게임 시간/실제 시간 비율을 잰다.
    /// 조건마다 워밍업 5프레임 뒤 Frames 프레임을 기록하고, 프레임별 횟수 수열도 남긴다.
    /// </summary>
    [Preserve, Lab("03-L04")]
    public sealed class FixedCatchUpLab : ILab
    {
        struct Condition
        {
            public string Name;
            public int UpdateSleepMs;
            public int FixedSleepMs;
            public float MaxDelta;
            public int Frames;
        }

        const int Warmup = 5;

        public async Awaitable Run(LabContext ctx)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var originalMaxDelta = Time.maximumDeltaTime;
            ctx.Record("-", "Application.isBatchMode", Application.isBatchMode.ToString().ToLowerInvariant());
            ctx.Record("-", "fixedDeltaTime", F(Time.fixedDeltaTime));
            ctx.Record("-", "maximumDeltaTime (default)", F(originalMaxDelta));

            var conditions = new[]
            {
                new Condition { Name = "a frame 0ms", UpdateSleepMs = 0, MaxDelta = originalMaxDelta, Frames = 60 },
                new Condition { Name = "b frame 10ms", UpdateSleepMs = 10, MaxDelta = originalMaxDelta, Frames = 60 },
                new Condition { Name = "c frame 50ms", UpdateSleepMs = 50, MaxDelta = originalMaxDelta, Frames = 30 },
                new Condition { Name = "d frame 100ms", UpdateSleepMs = 100, MaxDelta = originalMaxDelta, Frames = 20 },
                new Condition { Name = "e frame 500ms", UpdateSleepMs = 500, MaxDelta = originalMaxDelta, Frames = 10 },
                new Condition { Name = "f frame 500ms, maximumDeltaTime 1.0", UpdateSleepMs = 500, MaxDelta = 1f, Frames = 10 },
                new Condition { Name = "g FixedUpdate 30ms each", FixedSleepMs = 30, MaxDelta = originalMaxDelta, Frames = 20 },
            };

            var go = new GameObject("FixedCounter");
            var counter = go.AddComponent<FixedCounter>();
            try
            {
                foreach (var c in conditions)
                {
                    Time.maximumDeltaTime = c.MaxDelta;
                    counter.UpdateSleepMs = c.UpdateSleepMs;
                    counter.FixedSleepMs = c.FixedSleepMs;
                    for (var i = 0; i < Warmup; i++)
                        await Awaitable.NextFrameAsync();
                    counter.Samples.Clear();
                    counter.Recording = true;
                    while (counter.Samples.Count < c.Frames)
                        await Awaitable.NextFrameAsync();
                    counter.Recording = false;
                    Summarize(ctx, c.Name, counter);
                }
            }
            finally
            {
                Time.maximumDeltaTime = originalMaxDelta;
                Object.Destroy(go);
            }
        }

        static void Summarize(LabContext ctx, string name, FixedCounter counter)
        {
            int min = int.MaxValue, max = 0, sum = 0;
            double realSum = 0, deltaSum = 0;
            var seq = new StringBuilder();
            foreach (var s in counter.Samples)
            {
                min = Mathf.Min(min, s.FixedCount);
                max = Mathf.Max(max, s.FixedCount);
                sum += s.FixedCount;
                realSum += s.RealMs;
                deltaSum += s.DeltaTime;
                seq.Append(s.FixedCount).Append(' ');
            }
            var n = counter.Samples.Count;
            var last = counter.Samples[n - 1];
            ctx.Record(name, "frames", n.ToString(CultureInfo.InvariantCulture));
            ctx.Record(name, "fixed per frame min", min.ToString(CultureInfo.InvariantCulture));
            ctx.Record(name, "fixed per frame avg", F(sum / (float)n));
            ctx.Record(name, "fixed per frame max", max.ToString(CultureInfo.InvariantCulture));
            ctx.Record(name, "fixed per frame sequence", seq.ToString().TrimEnd());
            ctx.Record(name, "real frame ms avg", F((float)(realSum / n)));
            ctx.Record(name, "Time.deltaTime avg", F((float)(deltaSum / n)));
            ctx.Record(name, "Time.unscaledDeltaTime last", F(last.UnscaledDeltaTime));
            // 게임 시간이 실제 시간을 따라가는 비율: 1이면 정상, 1보다 작으면 슬로모션
            ctx.Record(name, "game/real time ratio", F((float)(deltaSum * 1000.0 / realSum)));
            ctx.Record(name, "fixedTime span in last frame",
                last.FixedCount >= 2 ? F(last.FixedTimeLast - last.FixedTimeFirst) : "-");
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
