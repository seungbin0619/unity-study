using System;
using UnityEngine;

namespace Labs
{
    /// <summary>식 평가 결과를 CSV에 쓸 문자열로 바꾸는 도우미. 측정 구간 밖에서만 쓴다.</summary>
    public static class LabEval
    {
        /// <summary>값은 소문자/따옴표 문자열로, 예외는 예외 타입 이름으로 돌려준다.</summary>
        public static string Eval<T>(Func<T> expr)
        {
            try
            {
                object value = expr();
                if (value is null) return "null";
                if (value is string s) return "\"" + s + "\"";
                return value.ToString().ToLowerInvariant();
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }

        /// <summary>action 실행 중 처음 찍힌 콘솔 메시지를 "LogType: 첫 줄" 형태로 돌려준다. 없으면 "none".</summary>
        public static string CaptureLog(Action action)
        {
            string captured = null;
            void Handler(string message, string stackTrace, LogType type)
            {
                if (captured != null) return;
                var newline = message.IndexOf('\n');
                captured = type + ": " + (newline < 0 ? message : message.Substring(0, newline)).Trim();
            }

            Application.logMessageReceived += Handler;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= Handler;
            }
            return captured ?? "none";
        }
    }
}
