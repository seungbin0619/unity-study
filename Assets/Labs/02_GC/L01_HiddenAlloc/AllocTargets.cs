using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Labs.GarbageCollection
{
    /// <summary>
    /// 02-L01 측정 대상. 케이스마다 메서드 하나이고, 결과는 _sink에 더해 최적화로 사라지지 않게 한다.
    /// </summary>
    public sealed class AllocTargets
    {
        readonly List<int> _list = new List<int> { 1, 2, 3, 4, 5 };
        int _hp = 100;
        int _target = 3;
        public long Sink;

        // A
        public void Boxing()
        {
            object o = _hp;
            Sink += o.GetHashCode();
        }

        // B
        public void Interpolation()
        {
            string s = $"HP: {_hp}";
            Sink += s.Length;
        }

        // C
        public void CapturingLambda()
        {
            int t = _target;
            Sink += _list.FindIndex(x => x == t);
        }

        // D
        public void NonCapturingLambda()
        {
            Sink += _list.FindIndex(x => x == 3);
        }

        // E
        public void StaticMethodGroup()
        {
            Sink += _list.FindIndex(IsThree);
        }

        static bool IsThree(int x) => x == 3;

        // F
        public void ForeachList()
        {
            foreach (var x in _list) Sink += x;
        }

        // G
        public void ForeachInterface()
        {
            IEnumerable<int> e = _list;
            foreach (var x in e) Sink += x;
        }

        // H
        public void LinqCount()
        {
            Sink += _list.Count(x => x > 2);
        }

        // I
        public void NewWaitForSeconds()
        {
            var w = new WaitForSeconds(1f);
            Sink += w.GetHashCode();
        }

        // J
        public void ParamsThree()
        {
            Sink += Sum(1, 2, 3);
        }

        // K
        public void ParamsZero()
        {
            Sink += Sum();
        }

        static int Sum(params int[] xs)
        {
            var total = 0;
            for (var i = 0; i < xs.Length; i++) total += xs[i];
            return total;
        }

        // --- 추가: 메서드 그룹 대처법 비교 ---

        // L
        public void InstanceMethodGroup()
        {
            Sink += _list.FindIndex(IsMatch);
        }

        // M
        public void LambdaWrapsInstance()
        {
            Sink += _list.FindIndex(x => IsMatch(x));
        }

        // N
        public void LambdaWrapsInstanceNoField()
        {
            Sink += _list.FindIndex(x => IsThreeInstance(x));
        }

        bool IsMatch(int x) => x == _target;

#pragma warning disable CA1822 // LAB: 의도적 — 인스턴스 상태를 안 읽는 인스턴스 메서드
        bool IsThreeInstance(int x) => x == 3;
#pragma warning restore CA1822

        // O
        static readonly Predicate<int> s_isThree = IsThree;

        public void CachedStaticDelegate()
        {
            Sink += _list.FindIndex(s_isThree);
        }

        // P
        public void StructPredicate()
        {
            Sink += FindIndex(_list, new IsThreePred());
        }

        interface IIntPredicate
        {
            bool Invoke(int x);
        }

        struct IsThreePred : IIntPredicate
        {
            public bool Invoke(int x) => x == 3;
        }

        static int FindIndex<TPred>(List<int> list, TPred pred) where TPred : struct, IIntPredicate
        {
            for (var i = 0; i < list.Count; i++)
                if (pred.Invoke(list[i])) return i;
            return -1;
        }
    }
}
