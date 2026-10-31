using System.Collections.Generic;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 02-L02 측정 대상. 람다가 있는 분기를 실행하지 않을 때(use = false)의 할당을 본다.
    /// </summary>
    public sealed class ClosureTargets
    {
        readonly List<int> _list = new List<int> { 1, 2, 3, 4, 5 };
        int _target = 3;
        public long Sink;

        // A: 매개변수를 캡처
        public void CaptureParameter(int t, bool use)
        {
            if (use) Sink += _list.FindIndex(x => x == t);
        }

        // B: 메서드 맨 위에서 선언한 지역 변수를 캡처
        public void CaptureTopLocal(bool use)
        {
            int t = _target;
            if (use) Sink += _list.FindIndex(x => x == t);
        }

        // C: 분기 안에서 선언한 지역 변수를 캡처
        public void CaptureBranchLocal(bool use)
        {
            if (use)
            {
                int t = _target;
                Sink += _list.FindIndex(x => x == t);
            }
        }

        // D: this만 캡처 (필드를 읽음)
        public void CaptureThisOnly(bool use)
        {
            if (use) Sink += _list.FindIndex(x => x == _target);
        }
    }
}
