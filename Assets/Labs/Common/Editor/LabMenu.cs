using UnityEditor;

namespace Labs.Editor
{
    /// <summary>에디터 메뉴에서 실습을 실행한다. 실행마다 플레이 모드에 새로 진입한다.</summary>
    static class LabMenu
    {
        [MenuItem("Labs/01/L01 Fake Null")]
        static void Run01L01() => Run("01-L01");

        [MenuItem("Labs/01/L02 New vs AddComponent")]
        static void Run01L02() => Run("01-L02");

        [MenuItem("Labs/01/L06 Read Bindings")]
        static void Run01L06() => Run("01-L06");

        public static void Run(string labId)
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogWarning("[Lab] 플레이 모드를 종료한 뒤 다시 실행하세요.");
                return;
            }
            SessionState.SetString(LabRunner.EditorLabIdKey, labId);
            EditorApplication.EnterPlaymode();
        }
    }
}
