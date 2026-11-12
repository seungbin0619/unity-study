using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 03-L06 대기 방식별 루프. Running이 false가 되면 다음 재개 때 루프를 빠져나간다.
    /// Iterations는 Counting일 때만 센다 (측정 구간 안에서 할당 없음: 정적 int 증가).
    /// </summary>
    public sealed class WaitHost : MonoBehaviour
    {
        public static bool Running;
        public static bool Counting;
        public static int Iterations;

        readonly WaitForSeconds _cached = new WaitForSeconds(0.01f);

        static void Tick()
        {
            if (Counting) Iterations++;
        }

        // ---- 1부: 반복당 ----

        public IEnumerator CoNull()
        {
            while (Running) { Tick(); yield return null; }
        }

        public IEnumerator CoZero()
        {
            while (Running) { Tick(); yield return 0; } // LAB: 의도적 — int를 object로 반환
        }

        // 확인 질문: Current가 int 필드인 제네릭 이터레이터. 엔진은 비제네릭 IEnumerator로 받는다
        public IEnumerator<int> CoZeroGeneric()
        {
            while (Running) { Tick(); yield return 0; }
        }

        public IEnumerator CoNewWait()
        {
            while (Running) { Tick(); yield return new WaitForSeconds(0.01f); } // LAB: 의도적
        }

        public IEnumerator CoCachedWait()
        {
            while (Running) { Tick(); yield return _cached; }
        }

        public static async Awaitable AwNextFrame()
        {
            while (Running) { Tick(); await Awaitable.NextFrameAsync(); }
        }

        public static async Task TaskYield()
        {
            while (Running) { Tick(); await Task.Yield(); }
        }

        // ---- 2부: 시작당 (한 번 대기하고 끝남) ----

        public IEnumerator CoOnce()
        {
            yield return null;
        }

        public static async Awaitable AwOnce()
        {
            await Awaitable.NextFrameAsync();
        }

        public static async Task TaskOnce()
        {
            await Task.Yield();
        }

        // ---- 3부: 같은 메서드 반복 vs 서로 다른 메서드 (상태 머신 타입이 메서드마다 다름) ----

        public static async Awaitable AwSame() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD0() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD1() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD2() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD3() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD4() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD5() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD6() { await Awaitable.NextFrameAsync(); }
        public static async Awaitable AwD7() { await Awaitable.NextFrameAsync(); }

        public static Awaitable CallDifferent(int i)
        {
            switch (i)
            {
                case 0: return AwD0();
                case 1: return AwD1();
                case 2: return AwD2();
                case 3: return AwD3();
                case 4: return AwD4();
                case 5: return AwD5();
                case 6: return AwD6();
                default: return AwD7();
            }
        }
    }
}
