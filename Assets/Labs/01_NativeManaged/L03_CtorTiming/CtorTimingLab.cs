using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 생성 경로 4가지(AddComponent, Instantiate, LoadScene, LoadSceneAsync)에서
    /// CtorProbe의 생성자·Awake 호출 순서와 스레드를 기록한다.
    /// </summary>
    [Preserve, Lab("01-L03")]
    public sealed class CtorTimingLab : ILab
    {
        const string ScenePath = "Assets/Labs/01_NativeManaged/L03_CtorTiming/L03_Scene.unity";

        int _mainThreadId;

        public async Awaitable Run(LabContext ctx)
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Drain(ctx, null); // 이전에 쌓인 이벤트(에디터에서 씬을 만들 때 등)는 버린다

            CtorProbe.Phase = "1_AddComponent";
            var host = new GameObject("ProbeHost");
            host.AddComponent<CtorProbe>();
            Drain(ctx, "1_AddComponent");

            CtorProbe.Phase = "2_Instantiate";
            var clone = Object.Instantiate(host);
            Drain(ctx, "2_Instantiate");

            CtorProbe.Phase = "3_LoadScene";
            LoadScene(async: false);
            await Awaitable.NextFrameAsync(); // 동기 LoadScene은 다음 프레임에 로드가 끝난다
            await Awaitable.NextFrameAsync();
            Drain(ctx, "3_LoadScene");
            await UnloadLabScene();

            CtorProbe.Phase = "4_LoadSceneAsync";
            var op = LoadScene(async: true);
            await op;
            await Awaitable.NextFrameAsync();
            Drain(ctx, "4_LoadSceneAsync");
            await UnloadLabScene();

            CtorProbe.Phase = "none";
            Object.Destroy(host);
            Object.Destroy(clone);
        }

        static AsyncOperation LoadScene(bool async)
        {
            var parameters = new LoadSceneParameters(LoadSceneMode.Additive);
#if UNITY_EDITOR
            // 에디터에서는 Build Settings에 없는 씬을 경로로 로드한다
            if (async)
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, parameters);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, parameters);
            return null;
#else
            if (async)
                return SceneManager.LoadSceneAsync(ScenePath, parameters);
            SceneManager.LoadScene(ScenePath, parameters);
            return null;
#endif
        }

        static async Awaitable UnloadLabScene()
        {
            CtorProbe.Phase = "unload";
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded)
                await SceneManager.UnloadSceneAsync(scene);
        }

        // phase가 null이면 기록하지 않고 버린다
        void Drain(LabContext ctx, string phase)
        {
            var ctorCount = 0;
            var awakeCount = 0;
            var order = "";
            while (CtorProbe.Events.TryDequeue(out var e))
            {
                if (phase == null) continue;

                var thread = e.ThreadId == _mainThreadId ? "main" : $"other(id={e.ThreadId},name={e.ThreadName ?? "null"})";
                ctx.Record(phase, $"event seq={e.Seq}", $"{e.Kind} [{e.Phase}] {thread}");
                if (e.Kind == "ctor") ctorCount++; else awakeCount++;
                order += (order.Length == 0 ? "" : " > ") + e.Kind + (e.ThreadId == _mainThreadId ? "(main)" : "(other)");
            }
            if (phase == null) return;

            ctx.Record(phase, "ctor count", ctorCount.ToString());
            ctx.Record(phase, "Awake count", awakeCount.ToString());
            ctx.Record(phase, "order", order.Length == 0 ? "none" : order);
        }
    }
}
