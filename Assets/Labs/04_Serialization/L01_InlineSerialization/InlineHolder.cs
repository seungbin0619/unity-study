using System;
using UnityEngine;

namespace Labs.SerializationLabs
{
    [Serializable]
    public class Item
    {
        public int value;
    }

    [Serializable]
    public class Special : Item
    {
        public int bonus;
    }

    /// <summary>일반 [Serializable] 클래스 필드 4개. 선언 타입은 모두 Item.</summary>
    [AddComponentMenu("")]
    public sealed class InlineHolder : MonoBehaviour
    {
        public Item a, b, c, d;

        /// <summary>도메인 리로드 확인용: 직렬화되지 않는 필드.</summary>
        [NonSerialized] public int nonSerialized;
    }
}
