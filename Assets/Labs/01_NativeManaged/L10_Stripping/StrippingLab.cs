using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 리플렉션으로만 쓰는 클래스를 찾아 생성하고 Ping()을 호출한다.
    /// 단계별로 어디서 실패하는지(타입 / 생성 / 메서드 / 호출) 기록한다.
    /// </summary>
    [Preserve, Lab("01-L10")]
    public sealed class StrippingLab : ILab
    {
        const string Ns = "Labs.NativeManaged";

        public async Awaitable Run(LabContext ctx)
        {
            // (A) 이름 전체가 상수 문자열
            Probe(ctx, "A literal", Type.GetType("Labs.NativeManaged.HiddenLiteral, Labs"), "Ping");

            // (B)~(D) 이름을 실행 중에 조합한다. 코드 어디에도 완성된 타입 이름 문자열이 없다
            Probe(ctx, "B dynamic", Type.GetType(Compose("Dynamic")), Compose2("Pi", "ng"));
            Probe(ctx, "C dynamic + [Preserve]", Type.GetType(Compose("Preserved")), Compose2("Pi", "ng"));
            Probe(ctx, "D dynamic + link.xml", Type.GetType(Compose("LinkXml")), Compose2("Pi", "ng"));

            // (E) 타입은 정적으로 참조, 생성자와 메서드는 리플렉션으로만
            Probe(ctx, "E typeof only", typeof(HiddenTypeOnly), Compose2("Pi", "ng"));

            await Awaitable.NextFrameAsync();
        }

        static string Compose(string suffix) => string.Join(".", new[] { "Labs", "NativeManaged", "Hidden" + suffix }) + ", Labs";

        static string Compose2(string a, string b) => a + b;

        static void Probe(LabContext ctx, string name, Type type, string methodName)
        {
            ctx.Record(name, "type", type == null ? "null" : "found");
            if (type == null)
                return;

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
                ctx.Record(name, "create", "ok");
            }
            catch (Exception e)
            {
                ctx.Record(name, "create", e.GetType().Name);
                return;
            }

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            ctx.Record(name, "method", method == null ? "null" : "found");
            if (method == null)
                return;

            try
            {
                ctx.Record(name, "invoke", method.Invoke(instance, null).ToString());
            }
            catch (Exception e)
            {
                ctx.Record(name, "invoke", e.GetType().Name);
            }
        }
    }
}
