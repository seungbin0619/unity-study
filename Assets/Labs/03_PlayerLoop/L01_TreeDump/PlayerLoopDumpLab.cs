using System.Globalization;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 기본 트리(GetDefaultPlayerLoop)와 현재 트리(GetCurrentPlayerLoop)를 노드마다 한 행씩 덤프한다.
    /// condition = 트리 종류("default"/"current"), metric = 깊이·순번이 붙은 경로, value = 노드 종류.
    /// 노드 종류: native(updateFunction), managed(updateDelegate), group(둘 다 없음, 자식만).
    /// 현재 트리는 패키지가 끼운 시스템이 반영되도록 몇 프레임 기다린 뒤 읽는다.
    /// </summary>
    [Preserve, Lab("03-L01")]
    public sealed class PlayerLoopDumpLab : ILab
    {
        const int SettleFrames = 10;

        public async Awaitable Run(LabContext ctx)
        {
            for (var i = 0; i < SettleFrames; i++)
                await Awaitable.NextFrameAsync();

            Dump(ctx, "default", PlayerLoop.GetDefaultPlayerLoop());
            Dump(ctx, "current", PlayerLoop.GetCurrentPlayerLoop());
        }

        static void Dump(LabContext ctx, string tree, PlayerLoopSystem root)
        {
            var phases = root.subSystemList ?? new PlayerLoopSystem[0];
            var counts = new int[3]; // native, managed, group
            for (var i = 0; i < phases.Length; i++)
                Walk(ctx, tree, phases[i], i.ToString("00", CultureInfo.InvariantCulture), counts);

            ctx.Record(tree, "phases", phases.Length.ToString(CultureInfo.InvariantCulture));
            ctx.Record(tree, "native", counts[0].ToString(CultureInfo.InvariantCulture));
            ctx.Record(tree, "managed", counts[1].ToString(CultureInfo.InvariantCulture));
            ctx.Record(tree, "group", counts[2].ToString(CultureInfo.InvariantCulture));
            Debug.Log($"[03-L01] {tree}: phases {phases.Length}, native {counts[0]}, managed {counts[1]}, group {counts[2]}");
        }

        static void Walk(LabContext ctx, string tree, PlayerLoopSystem node, string index, int[] counts)
        {
            string kind;
            if (node.updateFunction != System.IntPtr.Zero) { kind = "native"; counts[0]++; }
            else if (node.updateDelegate != null) { kind = "managed"; counts[1]++; }
            else { kind = "group"; counts[2]++; }

            var name = node.type != null ? node.type.FullName : "(null)";
            ctx.Record(tree, $"node {index} {name}", kind);

            var children = node.subSystemList;
            if (children == null)
                return;
            for (var i = 0; i < children.Length; i++)
                Walk(ctx, tree, children[i], index + "." + i.ToString("00", CultureInfo.InvariantCulture), counts);
        }
    }
}
