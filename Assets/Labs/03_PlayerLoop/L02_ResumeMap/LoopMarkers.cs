using UnityEngine.LowLevel;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 현재 PlayerLoop의 모든 하위 시스템 앞에 마커 시스템을 끼운다.
    /// 마커는 Where에 "단계/하위 시스템" 이름을 적기만 한다. 따라서 어떤 코드가 실행될 때 Where를 읽으면
    /// "마지막으로 시작한 하위 시스템", 즉 그 코드를 부른 하위 시스템 안이라는 뜻이 된다.
    /// 각 단계 끝에도 "단계/(end)" 마커를 둬서 마지막 하위 시스템 뒤의 일과 구분한다.
    /// </summary>
    public static class LoopMarkers
    {
        public static string Where = "(before loop)";

        static PlayerLoopSystem s_original;
        static bool s_installed;

        public static void Install()
        {
            if (s_installed)
                return;
            s_original = PlayerLoop.GetCurrentPlayerLoop();
            var root = s_original;
            var phases = (PlayerLoopSystem[])root.subSystemList.Clone();
            for (var p = 0; p < phases.Length; p++)
            {
                // struct 복사본: loopConditionFunction(FixedUpdate 반복)은 그대로 유지된다
                var phase = phases[p];
                var phaseName = phase.type != null ? phase.type.Name : "(null)";
                var children = phase.subSystemList ?? new PlayerLoopSystem[0];
                var wrapped = new PlayerLoopSystem[children.Length * 2 + 1];
                for (var c = 0; c < children.Length; c++)
                {
                    var childName = children[c].type != null ? children[c].type.Name : "(null)";
                    wrapped[c * 2] = Marker(phaseName + "/" + childName);
                    wrapped[c * 2 + 1] = children[c];
                }
                wrapped[children.Length * 2] = Marker(phaseName + "/(end)");
                phase.subSystemList = wrapped;
                phases[p] = phase;
            }
            root.subSystemList = phases;
            PlayerLoop.SetPlayerLoop(root);
            s_installed = true;
        }

        public static void Uninstall()
        {
            if (!s_installed)
                return;
            PlayerLoop.SetPlayerLoop(s_original);
            s_installed = false;
            Where = "(before loop)";
        }

        struct Mark { }

        static PlayerLoopSystem Marker(string name) => new PlayerLoopSystem
        {
            type = typeof(Mark),
            updateDelegate = () => Where = name,
        };
    }
}
