using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Labs.NativeManaged
{
    /// <summary>IL2CPP 생성 코드에서 검사 코드를 셀 대상. 메서드 이름으로 il2cppOutput을 검색한다.</summary>
    public sealed class ChainNode
    {
        public int Value;
        public ChainNode Next;

        // (C) this의 필드를 두 번 읽는다
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int DoubleValue() => Value + Value;
    }

    public static class CheckTargets
    {
        // (A) 참조를 두 번 따라간 뒤 필드를 읽는다
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ReadChain(ChainNode n) => n.Next.Next.Value;

        // (B) 배열 길이와 원소를 읽는 루프
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int SumArray(int[] a)
        {
            var sum = 0;
            for (var i = 0; i < a.Length; i++)
                sum += a[i];
            return sum;
        }

        // (E) 개발자가 직접 null을 검사한 뒤 B와 같은 루프. Il2CppSetOption은 없음
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int SumArrayGuarded(int[] a)
        {
            if (a == null) return 0;
            var sum = 0;
            for (var i = 0; i < a.Length; i++)
                sum += a[i];
            return sum;
        }

        // (D) B와 같은 코드에서 검사만 끈 버전
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int SumArrayUnchecked(int[] a)
        {
            var sum = 0;
            for (var i = 0; i < a.Length; i++)
                sum += a[i];
            return sum;
        }
    }
}
