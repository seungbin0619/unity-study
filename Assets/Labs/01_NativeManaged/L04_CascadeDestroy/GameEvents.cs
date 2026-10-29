using System;

namespace Labs.NativeManaged
{
    /// <summary>01-L04용 정적 이벤트. 구독자가 해제하지 않으면 델리게이트가 구독자를 계속 붙잡는다.</summary>
    public static class GameEvents
    {
        public static event Action Tick;

        public static void Raise() => Tick?.Invoke();

        public static int SubscriberCount => Tick?.GetInvocationList().Length ?? 0;

        public static void Clear() => Tick = null;
    }
}
