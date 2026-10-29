using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 01-L03용 컴포넌트. 생성자와 Awake가 불릴 때 현재 단계·스레드를 기록한다.
    /// 생성자는 메인 스레드가 아닐 수 있으므로 Unity API를 쓰지 않고 스레드 안전한 큐에만 쓴다.
    /// </summary>
    public sealed class CtorProbe : MonoBehaviour
    {
        public readonly struct Event
        {
            public readonly long Seq;
            public readonly string Phase;
            public readonly string Kind;
            public readonly int ThreadId;
            public readonly string ThreadName;

            public Event(long seq, string phase, string kind, int threadId, string threadName)
            {
                Seq = seq;
                Phase = phase;
                Kind = kind;
                ThreadId = threadId;
                ThreadName = threadName;
            }
        }

        public static readonly ConcurrentQueue<Event> Events = new ConcurrentQueue<Event>();
        public static volatile string Phase = "none";
        static long s_Seq;

        public CtorProbe() => Log("ctor");

        void Awake() => Log("Awake");

        static void Log(string kind)
        {
            var thread = Thread.CurrentThread;
            Events.Enqueue(new Event(Interlocked.Increment(ref s_Seq), Phase, kind, thread.ManagedThreadId, thread.Name));
        }
    }
}
