using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 03-L08 심화: Destroy 프레임 안에서 OnDisable / OnDestroy / 토큰 콜백 / AwToken catch / TaskToken catch가
    /// 어떤 순서로, PlayerLoop 어디서, 누구의 호출 스택 안에서 일어나는지 기록한다.
    /// </summary>
    [Preserve, Lab("03-L08b")]
    public sealed class TokenOrderLab : ILab
    {
        public struct Ev
        {
            public int Seq, Frame;
            public string What, Where, Extra, Stack;
        }

        static readonly Ev[] s_events = new Ev[64];
        static int s_count;

        public static void Log(string what, string extra = "", bool stack = false)
        {
            if (s_count >= s_events.Length) return;
            s_events[s_count] = new Ev
            {
                Seq = s_count,
                Frame = Time.frameCount,
                What = what,
                Where = LoopMarkers.Where,
                Extra = extra,
                Stack = stack ? ShortStack() : "",
            };
            s_count++;
        }

        static string ShortStack()
        {
            // 위에서부터 메서드 이름만. Log/ShortStack 자신은 건너뛴다
            var st = new StackTrace(2, false);
            var sb = new StringBuilder();
            var n = Math.Min(st.FrameCount, 14);
            for (var i = 0; i < n; i++)
            {
                var m = st.GetFrame(i)?.GetMethod();
                if (m == null) continue;
                if (sb.Length > 0) sb.Append(" <- ");
                sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name);
            }
            return sb.ToString();
        }

        public async Awaitable Run(LabContext ctx)
        {
            LoopMarkers.Install();
            try
            {
                await Awaitable.NextFrameAsync();

                var go = new GameObject("L08b Host");
                var host = go.AddComponent<TokenOrderHost>();
                host.Begin();

                for (var i = 0; i < 3; i++)
                    await Awaitable.NextFrameAsync();

                Log("driver: Destroy(gameObject) called");
                UnityEngine.Object.Destroy(go);
                Log("driver: returned from Destroy", "host == null: " + (host == null));

                for (var i = 0; i < 3; i++)
                {
                    await Awaitable.NextFrameAsync();
                    Log("driver: next frame " + (i + 1));
                }
            }
            finally
            {
                LoopMarkers.Uninstall();
            }

            var baseFrame = -1;
            for (var i = 0; i < s_count; i++)
                if (s_events[i].What.StartsWith("driver: Destroy", StringComparison.Ordinal)) { baseFrame = s_events[i].Frame; break; }

            for (var i = 0; i < s_count; i++)
            {
                var e = s_events[i];
                var cond = e.Seq.ToString("00", CultureInfo.InvariantCulture) + " " + e.What;
                ctx.Record(cond, "frame - destroy frame", (e.Frame - baseFrame).ToString(CultureInfo.InvariantCulture));
                ctx.Record(cond, "where", e.Where);
                if (e.Extra.Length > 0) ctx.Record(cond, "extra", e.Extra);
                if (e.Stack.Length > 0) ctx.Record(cond, "stack", e.Stack.Replace(',', ';'));
            }
        }
    }

    [Preserve]
    public sealed class TokenOrderHost : MonoBehaviour
    {
        CancellationToken _token;

        public void Begin()
        {
            _token = destroyCancellationToken;
            _token.Register(() => TokenOrderLab.Log("token.Register callback", "this == null: " + (this == null), stack: true));
            _ = AwTokenLoop();
            _ = TaskTokenLoop();
        }

        void OnDisable() =>
            TokenOrderLab.Log("OnDisable", "token cancelled: " + _token.IsCancellationRequested);

        void OnDestroy() =>
            TokenOrderLab.Log("OnDestroy", "token cancelled: " + _token.IsCancellationRequested + ", this == null: " + (this == null));

        async Awaitable AwTokenLoop()
        {
            try
            {
                while (true)
                    await Awaitable.NextFrameAsync(_token);
            }
            catch (OperationCanceledException)
            {
                TokenOrderLab.Log("AwToken catch", "", stack: true);
            }
        }

        async Task TaskTokenLoop()
        {
            try
            {
                while (true)
                {
                    await Task.Yield();
                    _token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                TokenOrderLab.Log("TaskToken catch", "", stack: true);
            }
        }
    }
}
