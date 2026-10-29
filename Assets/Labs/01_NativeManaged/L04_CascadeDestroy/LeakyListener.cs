using UnityEngine;

namespace Labs.NativeManaged
{
    /// <summary>OnEnable에서 구독하고 해제하지 않는 리스너 (LAB: 의도적 누수).</summary>
    public sealed class LeakyListener : MonoBehaviour
    {
        public static string LastTickResult = "not called";
        public static bool OnDestroyCalled;

        void OnEnable() => GameEvents.Tick += OnTick;

        void OnTick() => LastTickResult = LabEval.Eval(() => name);

        void OnDestroy() => OnDestroyCalled = true;
    }
}
