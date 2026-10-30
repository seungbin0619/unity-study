using System.Runtime.CompilerServices;

namespace Labs.NativeManaged
{
    /// <summary>IL2CPP 생성 코드에서 호출 방식을 비교할 대상. 메서드 이름으로 il2cppOutput을 검색한다.</summary>
    public class Animal
    {
        public virtual int Speak() => 1;
    }

    // 봉인하지 않은 파생 클래스
    public class Dog : Animal
    {
        public override int Speak() => 2;
    }

    // 클래스 전체를 봉인
    public sealed class SealedDog : Animal
    {
        public override int Speak() => 3;
    }

    // 클래스는 열어 두고 override 메서드만 봉인
    public class Wolf : Animal
    {
        public sealed override int Speak() => 4;
    }

    public static class SealedTargets
    {
        // (A) 기반 타입으로 호출
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallAnimal(Animal a) => a.Speak();

        // (B) 봉인하지 않은 파생 타입으로 호출
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallDog(Dog d) => d.Speak();

        // (C) sealed 클래스 타입으로 호출
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallSealedDog(SealedDog d) => d.Speak();

        // (D) sealed override 메서드를 가진 타입으로 호출
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallWolf(Wolf w) => w.Speak();

        // 시간 측정용: 같은 호출을 루프 안에서 반복한다
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopAnimal(Animal a, int n) { long s = 0; for (var i = 0; i < n; i++) s += a.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopDog(Dog d, int n) { long s = 0; for (var i = 0; i < n; i++) s += d.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopSealedDog(SealedDog d, int n) { long s = 0; for (var i = 0; i < n; i++) s += d.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopWolf(Wolf w, int n) { long s = 0; for (var i = 0; i < n; i++) s += w.Speak(); return s; }
    }
}
