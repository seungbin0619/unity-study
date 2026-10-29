using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Labs.Editor
{
    /// <summary>에디터 메뉴에서 실습을 실행한다. 실행마다 Main 씬을 열고 플레이 모드에 새로 진입한다.</summary>
    static class LabMenu
    {
        const string MainScene = "Assets/Main.unity";

        [MenuItem("Labs/01/L01 Fake Null")]
        static void Run01L01() => Run("01-L01");

        [MenuItem("Labs/01/L02 New vs AddComponent")]
        static void Run01L02() => Run("01-L02");

        [MenuItem("Labs/01/L03 Ctor Timing")]
        static void Run01L03() => Run("01-L03");

        [MenuItem("Labs/01/L06 Read Bindings")]
        static void Run01L06() => Run("01-L06");

        public static void Run(string labId)
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogWarning("[Lab] 플레이 모드를 종료한 뒤 다시 실행하세요.");
                return;
            }
            // 모든 실습은 Main 씬 하나만 연 상태에서 시작한다 (기준선을 실습마다 같게)
            var active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount != 1 || active.path != MainScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            }

            SessionState.SetString(LabRunner.EditorLabIdKey, labId);
            EditorApplication.EnterPlaymode();
        }
    }
}
