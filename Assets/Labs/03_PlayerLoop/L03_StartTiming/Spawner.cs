using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 생성 지점마다 템플릿을 한 번씩 Instantiate하고 그 프레임을 SpawnFrames에 남긴다.
    /// 생성 지점: 자신의 Awake·Start·첫 FixedUpdate·첫 Update·첫 LateUpdate,
    /// Start에서 시작한 코루틴(yield return null 뒤, WaitForEndOfFrame 뒤), 첫 Update에서 시작한 await Task.Yield() 뒤.
    /// </summary>
    public sealed class Spawner : MonoBehaviour
    {
        public static GameObject Template;
        public static readonly Dictionary<string, int> SpawnFrames = new Dictionary<string, int>();

        bool _fixed, _update, _late;

        static void Spawn(string label)
        {
            SpawnFrames[label] = Time.frameCount;
            LifecycleProbe.NextLabel = label;
            LifecycleProbe.InsideInstantiate = true;
            Instantiate(Template);
            LifecycleProbe.InsideInstantiate = false;
        }

        void Awake() => Spawn("Spawner.Awake");

        void Start()
        {
            Spawn("Spawner.Start");
            StartCoroutine(AfterYieldNull());
            StartCoroutine(AfterEndOfFrame());
        }

        void FixedUpdate()
        {
            if (_fixed) return;
            _fixed = true;
            Spawn("Spawner.FixedUpdate");
        }

        void Update()
        {
            if (_update) return;
            _update = true;
            Spawn("Spawner.Update");
            _ = AfterTaskYield();
        }

        void LateUpdate()
        {
            if (_late) return;
            _late = true;
            Spawn("Spawner.LateUpdate");
        }

        IEnumerator AfterYieldNull()
        {
            yield return null;
            Spawn("coroutine after yield null");
        }

        IEnumerator AfterEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            Spawn("coroutine after WaitForEndOfFrame");
        }

        async Task AfterTaskYield()
        {
            await Task.Yield();
            Spawn("after await Task.Yield()");
        }
    }
}
