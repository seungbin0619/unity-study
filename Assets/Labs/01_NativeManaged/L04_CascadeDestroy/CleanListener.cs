using UnityEngine;

namespace Labs.NativeManaged
{
    /// <summary>OnEnable에서 구독하고 OnDisable에서 해제하는 리스너.</summary>
    public sealed class CleanListener : MonoBehaviour
    {
        public static string LastTickResult = "not called";
        public static bool OnDestroyCalled;

        void OnEnable() => GameEvents.Tick += OnTick;

        void OnDisable() => GameEvents.Tick -= OnTick;

        void OnTick() => LastTickResult = LabEval.Eval(() => name);

        void OnDestroy() => OnDestroyCalled = true;
    }
}
