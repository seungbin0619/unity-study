using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 프레임마다 그 프레임에 돈 FixedUpdate 횟수와 시간 값을 기록한다.
    /// FixedUpdate 단계는 Update 단계보다 앞이므로, Update에서 읽은 횟수 = 이번 프레임의 FixedUpdate 횟수.
    /// UpdateSleepMs: Update에서 잠들어 프레임을 늘린다. FixedSleepMs: FixedUpdate 한 번마다 잠든다(물리 스텝이 비쌀 때).
    /// </summary>
    public sealed class FixedCounter : MonoBehaviour
    {
        public struct Sample
        {
            public int FixedCount;
            public float DeltaTime;
            public float UnscaledDeltaTime;
            public double RealMs;
            public float FixedTimeFirst;
            public float FixedTimeLast;
        }

        public int UpdateSleepMs;
        public int FixedSleepMs;
        public bool Recording;
        public readonly List<Sample> Samples = new List<Sample>();

        int _fixedCount;
        float _fixedFirst, _fixedLast;
        readonly Stopwatch _sw = Stopwatch.StartNew();
        double _lastUpdateMs;

        void FixedUpdate()
        {
            // FixedUpdate 안의 Time.time은 fixedTime을 돌려준다
            if (_fixedCount == 0) _fixedFirst = Time.time;
            _fixedLast = Time.time;
            _fixedCount++;
            if (FixedSleepMs > 0)
                Thread.Sleep(FixedSleepMs); // LAB: 의도적 — 비싼 물리 스텝 흉내
        }

        void Update()
        {
            var now = _sw.Elapsed.TotalMilliseconds;
            if (Recording)
            {
                Samples.Add(new Sample
                {
                    FixedCount = _fixedCount,
                    DeltaTime = Time.deltaTime,
                    UnscaledDeltaTime = Time.unscaledDeltaTime,
                    RealMs = now - _lastUpdateMs,
                    FixedTimeFirst = _fixedFirst,
                    FixedTimeLast = _fixedLast,
                });
            }
            _lastUpdateMs = now;
            _fixedCount = 0;
            if (UpdateSleepMs > 0)
                Thread.Sleep(UpdateSleepMs); // LAB: 의도적 — 긴 프레임 흉내
        }
    }
}
