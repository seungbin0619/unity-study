using UnityEngine;

namespace Labs.GarbageCollection
{
    /// <summary>02-L03에서 생성·풀링하는 객체에 붙는 빈 컴포넌트. 생명주기 콜백이 불리는 비용을 포함시키려고 둔다.</summary>
    public sealed class PooledProbe : MonoBehaviour
    {
        public int EnableCount;

        void OnEnable() => EnableCount++;
    }
}
