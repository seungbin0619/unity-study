using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// StartTrial()을 부르면 다음 Update에서 프로브를 한꺼번에 시작하고,
    /// 각 프로브가 재개되는 순간의 프레임·LoopMarkers.Where·스레드를 기록한다.
    /// 콜백(Update/LateUpdate/FixedUpdate)도 시행 시작 프레임 근처에서 위치를 기록해 마커가 맞는지 대조한다.
    /// </summary>
    public sealed class ResumeProbe : MonoBehaviour
    {
        public struct Hit
        {
            public int Trial;
            public string Probe;
            public int StartFrame;
            public int Frame;
            public string Where;
            public int Thread;
        }

        public readonly List<Hit> Hits = new List<Hit>();

        int _trial;
        int _startFrame = -1;
        bool _pending;
        int _fixedLogged;
        bool _lateLogged;

        public void StartTrial(int trial)
        {
            _trial = trial;
            _pending = true;
        }

        void Add(string probe) => Hits.Add(new Hit
        {
            Trial = _trial,
            Probe = probe,
            StartFrame = _startFrame,
            Frame = Time.frameCount,
            Where = LoopMarkers.Where,
            Thread = Thread.CurrentThread.ManagedThreadId,
        });

        void FixedUpdate()
        {
            // 시행 시작 이후 첫 FixedUpdate 2번만 기록
            if (_startFrame >= 0 && _fixedLogged < 2)
            {
                _fixedLogged++;
                Add("callback FixedUpdate");
            }
        }

        void Update()
        {
            if (!_pending)
                return;
            _pending = false;
            _startFrame = Time.frameCount;
            _fixedLogged = 0;
            _lateLogged = false;
            Add("callback Update (start)");

            StartCoroutine(CoNull());
            StartCoroutine(CoWaitForSeconds());
            StartCoroutine(CoWaitForFixedUpdate());
            StartCoroutine(CoWaitForEndOfFrame());
            _ = AwNextFrame();
            _ = AwFixedUpdate();
            _ = AwEndOfFrame();
            _ = AwWaitForSeconds();
            _ = TaskYield();
        }

        void LateUpdate()
        {
            if (_startFrame == Time.frameCount && !_lateLogged)
            {
                _lateLogged = true;
                Add("callback LateUpdate");
            }
        }

        IEnumerator CoNull()
        {
            Add("A coroutine first segment");
            yield return null;
            Add("A yield return null");
        }

        IEnumerator CoWaitForSeconds()
        {
            yield return new WaitForSeconds(0.01f);
            Add("B WaitForSeconds(0.01)");
        }

        IEnumerator CoWaitForFixedUpdate()
        {
            yield return new WaitForFixedUpdate();
            Add("C WaitForFixedUpdate");
        }

        IEnumerator CoWaitForEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            Add("D WaitForEndOfFrame");
        }

        async Awaitable AwNextFrame()
        {
            Add("E awaitable first segment");
            await Awaitable.NextFrameAsync();
            Add("E Awaitable.NextFrameAsync");
        }

        async Awaitable AwFixedUpdate()
        {
            await Awaitable.FixedUpdateAsync();
            Add("F Awaitable.FixedUpdateAsync");
        }

        async Awaitable AwEndOfFrame()
        {
            await Awaitable.EndOfFrameAsync();
            Add("G Awaitable.EndOfFrameAsync");
        }

        async Task TaskYield()
        {
            await Task.Yield();
            Add("H await Task.Yield()");
        }

        async Awaitable AwWaitForSeconds()
        {
            await Awaitable.WaitForSecondsAsync(0.01f);
            Add("I Awaitable.WaitForSecondsAsync(0.01)");
        }
    }
}
