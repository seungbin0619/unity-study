using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    // 객체마다 하는 일은 모두 같다: _value += UpdateCostLab.Dt (정적 필드 읽기 + 필드 쓰기)

    /// <summary>A: 엔진이 스크립트마다 Update를 부른다.</summary>
    public sealed class UpdateTicker : MonoBehaviour
    {
        float _value;
        void Update() => _value += UpdateCostLab.Dt;
    }

    /// <summary>A0: 빈 Update. 정의만 있어도 엔진이 부르는가?</summary>
    public sealed class EmptyUpdateTicker : MonoBehaviour
    {
        void Update() { }
    }

    /// <summary>N: Update 없음. 컴포넌트가 존재하는 것만으로 비용이 드는가?</summary>
    public sealed class NoUpdateTicker : MonoBehaviour
    {
        public float Value;
    }

    /// <summary>B2: Update 없는 MonoBehaviour를 매니저가 직접 Tick (매니지드 → 매니지드 호출).</summary>
    public sealed class ManualTicker : MonoBehaviour
    {
        float _value;
        public void Tick() => _value += UpdateCostLab.Dt;
    }

    /// <summary>B·C: 일반 C# 객체.</summary>
    public sealed class PlainTicker
    {
        float _value;
        public void Tick() => _value += UpdateCostLab.Dt;
    }

    /// <summary>B·B2: 매니저 하나의 Update에서 배열을 for로 돈다.</summary>
    public sealed class TickManager : MonoBehaviour
    {
        public PlainTicker[] Plain;
        public ManualTicker[] Behaviours;

        void Update()
        {
            var plain = Plain;
            if (plain != null)
                for (var i = 0; i < plain.Length; i++)
                    plain[i].Tick();

            var behaviours = Behaviours;
            if (behaviours != null)
                for (var i = 0; i < behaviours.Length; i++)
                    behaviours[i].Tick();
        }
    }
}
