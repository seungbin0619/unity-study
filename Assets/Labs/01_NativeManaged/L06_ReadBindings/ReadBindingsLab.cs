using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 실행 중인 UnityEngine.CoreModule에서 TryThrowEditorNullExceptionObject의 IL을 꺼내
    /// 크기와 ldstr 문자열을 기록한다. 에디터와 플레이어의 DLL이 다른지 확인하는 용도.
    /// </summary>
    [Preserve, Lab("01-L06")]
    public sealed class ReadBindingsLab : ILab
    {
        const string Condition = "TryThrowEditorNullExceptionObject";
        const byte OpLdstr = 0x72;
        const int UserStringTable = 0x70;

        public async Awaitable Run(LabContext ctx)
        {
            var module = typeof(Object).Assembly.ManifestModule;
            ctx.Record("CoreModule", "module", module.Name);

            var helper = typeof(Object).GetNestedType("MarshalledUnityObject", BindingFlags.NonPublic);
            var method = helper?.GetMethod(Condition, BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Object), typeof(string) }, null);
            ctx.Record(Condition, "method found", (method != null).ToString().ToLowerInvariant());

            var il = method?.GetMethodBody()?.GetILAsByteArray();
            if (il != null)
            {
                ctx.Record(Condition, "IL bytes", il.Length.ToString());
                RecordStrings(ctx, module, il);
            }

            await Awaitable.NextFrameAsync();
        }

        // ldstr 뒤 4바이트가 user string 토큰(0x70xxxxxx)이고 실제로 풀리는 것만 문자열로 인정한다
        static void RecordStrings(LabContext ctx, Module module, byte[] il)
        {
            var count = 0;
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != OpLdstr) continue;
                var token = BitConverter.ToInt32(il, i + 1);
                if ((token >> 24) != UserStringTable) continue;

                string value;
                try { value = module.ResolveString(token); }
                catch (ArgumentException) { continue; }

                var firstLine = value.Split('\n')[0];
                ctx.Record(Condition, $"ldstr #{count++}", "\"" + firstLine + "\"");
                i += 4;
            }
            ctx.Record(Condition, "ldstr count", count.ToString());
        }
    }
}
