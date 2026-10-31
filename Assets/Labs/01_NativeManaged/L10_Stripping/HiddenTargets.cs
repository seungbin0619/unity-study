namespace Labs.NativeManaged
{
    // 아래 클래스들은 어디서도 직접 생성·호출하지 않는다. StrippingLab이 리플렉션으로만 찾는다.
    // UnityLinker가 무엇을 남기는지 보려고 일부러 [Preserve]를 붙이지 않은 것도 있다.

    // (A) 상수 문자열로 찾는다
    public class HiddenLiteral
    {
        public int Ping() => 1;
    }

    // (B) 런타임에 조합한 문자열로 찾는다
    public class HiddenDynamic
    {
        public int Ping() => 2;
    }

    // (C) (B)와 같지만 [Preserve]로 보존
    [UnityEngine.Scripting.Preserve]
    public class HiddenPreserved
    {
        public int Ping() => 3;
    }

    // (D) (B)와 같지만 link.xml로 보존
    public class HiddenLinkXml
    {
        public int Ping() => 4;
    }

    // (E) 타입은 typeof로 정적 참조하지만, 생성자와 Ping은 리플렉션으로만 쓴다
    public class HiddenTypeOnly
    {
        public int Ping() => 5;
    }
}
