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

    // 가설 검증용: IL의 호출 대상이 수신 타입 자신의 메서드가 되도록 인터페이스를 구현한다.
    // (sealed class에는 새 virtual 메서드를 선언할 수 없어서(CS0549) 이 방식으로 IL 대상을 바꾼다)
    public interface ISpeaker
    {
        int Speak();
    }

    // 암시적 구현 — IL에서 Speak는 virtual final(IsFinal=True)
    public class Fox : ISpeaker
    {
        public int Speak() => 5;
    }

    // virtual로 구현 — IL에서 Speak는 virtual, final 아님
    public class OpenFox : ISpeaker
    {
        public virtual int Speak() => 6;
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

        // (E) final 메서드를 그 클래스 타입으로 호출 — IL: callvirt Fox::Speak
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallFox(Fox f) => f.Speak();

        // (F) final이 아닌 virtual 메서드를 그 클래스 타입으로 호출 — IL: callvirt OpenFox::Speak
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CallOpenFox(OpenFox f) => f.Speak();

        // 시간 측정용: 같은 호출을 루프 안에서 반복한다
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopAnimal(Animal a, int n) { long s = 0; for (var i = 0; i < n; i++) s += a.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopDog(Dog d, int n) { long s = 0; for (var i = 0; i < n; i++) s += d.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopSealedDog(SealedDog d, int n) { long s = 0; for (var i = 0; i < n; i++) s += d.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopWolf(Wolf w, int n) { long s = 0; for (var i = 0; i < n; i++) s += w.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopFox(Fox f, int n) { long s = 0; for (var i = 0; i < n; i++) s += f.Speak(); return s; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long LoopOpenFox(OpenFox f, int n) { long s = 0; for (var i = 0; i < n; i++) s += f.Speak(); return s; }
    }
}
