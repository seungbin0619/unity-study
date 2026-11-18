using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Labs.PlayerLoopLabs
{
    /// <summary>커스텀 시스템 노드의 타입 표식.</summary>
    public struct LeakySystem { }

    /// <summary>
    /// 플레이 진입마다 Update 단계 끝에 LeakySystem을 끼운다. 정리는 조건에 따라 다르다.
    /// 03-L09가 켜져 있을 때(SessionState ModeKey ≥ 0)만 동작하므로 다른 실습·평소 플레이에는 영향이 없다.
    /// </summary>
    public static class SystemLeak
    {
        public const string ModeKey = "Labs.03-L09.Mode";   // 조건 번호, -1 = 꺼짐
        public const string EntryKey = "Labs.03-L09.Entry"; // 조건 안의 진입 순번 (0부터)
        public const string ProbeTypeKey = "Labs.03-L09.ProbeType";
        public const string ProbeNameKey = "Labs.03-L09.ProbeName";
        public const string ProbeUpdateKey = "Labs.03-L09.ProbeUpdate";

        public const int ModeReloadOn = 0;
        public const int ModeReloadOff = 1;
        public const int ModeCleanupSubsystem = 2;
        public const int ModeCleanupExiting = 3;
        public const int ModeCount = 4;

        public static readonly string[] ModeNames =
        {
            "A reload on, no cleanup",
            "B reload off, no cleanup",
            "C reload off, SubsystemRegistration cleanup",
            "D reload off, ExitingPlayMode cleanup",
        };

        /// <summary>Tick 호출 수. 도메인 리로드가 꺼져 있으면 진입을 넘어 남지만, 실습이 측정 직전에 0으로 둔다.</summary>
        public static int Ticks;

        static void Tick() => Ticks++;

        public static int Mode
        {
            get
            {
#if UNITY_EDITOR
                return SessionState.GetInt(ModeKey, -1);
#else
                return -1;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSubsystemRegistration()
        {
#if UNITY_EDITOR
            var mode = Mode;
            if (mode < 0)
                return;
            // 이번 진입에서 아무것도 하기 전에 남아 있는 것을 센다
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            SessionState.SetInt(ProbeTypeKey, CountByType(loop));
            SessionState.SetInt(ProbeNameKey, CountByName(loop));
            SessionState.SetInt(ProbeUpdateKey, UpdateChildCount(loop));

            if (mode == ModeCleanupSubsystem)
                RemoveAll();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
#if UNITY_EDITOR
            var mode = Mode;
            if (mode < 0)
                return;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var phases = loop.subSystemList;
            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != typeof(UnityEngine.PlayerLoop.Update))
                    continue;
                var list = new List<PlayerLoopSystem>(phases[i].subSystemList)
                {
                    new PlayerLoopSystem { type = typeof(LeakySystem), updateDelegate = Tick },
                };
                phases[i].subSystemList = list.ToArray();
            }
            PlayerLoop.SetPlayerLoop(loop);

            if (mode == ModeCleanupExiting)
            {
                // 도메인 리로드가 꺼져 있으면 static 이벤트 구독도 쌓이므로 먼저 뺀다
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
#endif
        }

#if UNITY_EDITOR
        public static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RemoveAll();
        }
#endif

        public static void RemoveAll()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (Strip(ref loop))
                PlayerLoop.SetPlayerLoop(loop);
        }

        static bool Strip(ref PlayerLoopSystem node)
        {
            var children = node.subSystemList;
            if (children == null)
                return false;
            var changed = false;
            var kept = new List<PlayerLoopSystem>(children.Length);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child.type == typeof(LeakySystem))
                {
                    changed = true;
                    continue;
                }
                changed |= Strip(ref child);
                kept.Add(child);
            }
            if (changed)
                node.subSystemList = kept.ToArray();
            return changed;
        }

        public static int CountByType(PlayerLoopSystem node)
        {
            var n = node.type == typeof(LeakySystem) ? 1 : 0;
            if (node.subSystemList != null)
                foreach (var child in node.subSystemList)
                    n += CountByType(child);
            return n;
        }

        /// <summary>도메인 리로드 뒤 옛 어셈블리의 Type이 남아 있으면 타입 비교로는 안 잡히므로 이름으로도 센다.</summary>
        public static int CountByName(PlayerLoopSystem node)
        {
            var n = node.type != null && node.type.Name == nameof(LeakySystem) ? 1 : 0;
            if (node.subSystemList != null)
                foreach (var child in node.subSystemList)
                    n += CountByName(child);
            return n;
        }

        public static int UpdateChildCount(PlayerLoopSystem root)
        {
            foreach (var phase in root.subSystemList)
                if (phase.type == typeof(UnityEngine.PlayerLoop.Update))
                    return phase.subSystemList?.Length ?? 0;
            return -1;
        }
    }

    /// <summary>진입마다 한 번 실행된다. 노드 수와 실제 Tick 호출 수를 기록한다.</summary>
    [Preserve, Lab("03-L09")]
    public sealed class SystemLeakLab : ILab
    {
        const int Frames = 10;

        public async Awaitable Run(LabContext ctx)
        {
#if UNITY_EDITOR
            var mode = SystemLeak.Mode;
            if (mode < 0)
            {
                Debug.LogWarning("[03-L09] 메뉴 Labs/03/L09 System Leak로 실행하세요 (조건 4개 × 진입 3회를 자동으로 돕니다).");
                return;
            }
            var entry = SessionState.GetInt(SystemLeak.EntryKey, 0);
            var cond = SystemLeak.ModeNames[mode];
            var tag = "entry " + (entry + 1).ToString(CultureInfo.InvariantCulture);

            var current = PlayerLoop.GetCurrentPlayerLoop();
            var byType = SystemLeak.CountByType(current);
            var byName = SystemLeak.CountByName(current);
            var updateNow = SystemLeak.UpdateChildCount(current);
            var updateDefault = SystemLeak.UpdateChildCount(PlayerLoop.GetDefaultPlayerLoop());

            SystemLeak.Ticks = 0;
            for (var i = 0; i < Frames; i++)
                await Awaitable.NextFrameAsync();
            var ticks = SystemLeak.Ticks;

            var inv = CultureInfo.InvariantCulture;
            ctx.Record(cond, tag + " enterPlayModeOptions", EditorSettings.enterPlayModeOptions.ToString());
            ctx.Record(cond, tag + " leftover at SubsystemRegistration (type)", SessionState.GetInt(SystemLeak.ProbeTypeKey, -1).ToString(inv));
            ctx.Record(cond, tag + " leftover at SubsystemRegistration (name)", SessionState.GetInt(SystemLeak.ProbeNameKey, -1).ToString(inv));
            ctx.Record(cond, tag + " Update children at SubsystemRegistration", SessionState.GetInt(SystemLeak.ProbeUpdateKey, -1).ToString(inv));
            ctx.Record(cond, tag + " LeakySystem nodes (type)", byType.ToString(inv));
            ctx.Record(cond, tag + " LeakySystem nodes (name)", byName.ToString(inv));
            ctx.Record(cond, tag + " Update children current", updateNow.ToString(inv));
            ctx.Record(cond, tag + " Update children default", updateDefault.ToString(inv));
            ctx.Record(cond, tag + " Tick calls per frame", (ticks / (double)Frames).ToString("0.##", inv));
            Debug.Log($"[03-L09] {cond} {tag}: nodes {byType}/{byName}, ticks/frame {ticks / (double)Frames:0.##}");
#else
            ctx.Record("-", "skipped", "editor only");
            await Awaitable.NextFrameAsync();
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 조건 4개 × 진입 3회를 자동으로 돈다. 상태는 SessionState에 둔다 (도메인 리로드를 넘어 남도록).
    /// 진입은 LabRunner가 실습을 마치고 ExitPlaymode하면, EnteredEditMode에서 다음 진입을 건다.
    /// </summary>
    public static class SystemLeakDriver
    {
        const string ActiveKey = "Labs.03-L09.Active";
        const string OrigEnabledKey = "Labs.03-L09.OrigEnabled";
        const string OrigOptionsKey = "Labs.03-L09.OrigOptions";
        public const int EntriesPerCondition = 3;

        static bool Active => SessionState.GetBool(ActiveKey, false);

        /// <summary>LabMenu가 첫 진입 직전에 부른다.</summary>
        public static void Prepare()
        {
            SessionState.SetBool(OrigEnabledKey, EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(OrigOptionsKey, (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(SystemLeak.EntryKey, 0);
            BeginCondition(0);
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnChanged;
            EditorApplication.playModeStateChanged += OnChanged;
        }

        static void OnChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && Active)
                EditorApplication.delayCall += Advance;
        }

        static void Advance()
        {
            if (!Active)
                return;
            var entry = SessionState.GetInt(SystemLeak.EntryKey, 0) + 1;
            var mode = SystemLeak.Mode;
            if (entry >= EntriesPerCondition)
            {
                Debug.Log($"[03-L09] {SystemLeak.ModeNames[mode]} 종료 후 편집 모드: LeakySystem {SystemLeak.CountByType(PlayerLoop.GetCurrentPlayerLoop())}개");
                mode++;
                entry = 0;
                if (mode >= SystemLeak.ModeCount)
                {
                    Finish();
                    return;
                }
                BeginCondition(mode);
            }
            SessionState.SetInt(SystemLeak.EntryKey, entry);
            Enter();
        }

        static void BeginCondition(int mode)
        {
            // 조건마다 깨끗한 트리에서 시작
            SystemLeak.RemoveAll();
            EditorApplication.playModeStateChanged -= SystemLeak.OnPlayModeChanged;
            SessionState.SetInt(SystemLeak.ModeKey, mode);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = mode == SystemLeak.ModeReloadOn
                ? EnterPlayModeOptions.None
                : EnterPlayModeOptions.DisableDomainReload;
        }

        static void Enter()
        {
            SessionState.SetString(LabRunner.EditorLabIdKey, "03-L09");
            EditorApplication.EnterPlaymode();
        }

        static void Finish()
        {
            SystemLeak.RemoveAll();
            EditorApplication.playModeStateChanged -= SystemLeak.OnPlayModeChanged;
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(OrigEnabledKey, true);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(OrigOptionsKey, 0);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(SystemLeak.ModeKey);
            SessionState.EraseInt(SystemLeak.EntryKey);
            Debug.Log($"[03-L09] 완료. Enter Play Mode Options 복원: enabled {EditorSettings.enterPlayModeOptionsEnabled}, {EditorSettings.enterPlayModeOptions}");
        }
    }
#endif
}
