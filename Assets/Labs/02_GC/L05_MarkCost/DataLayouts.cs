using UnityEngine;

namespace Labs.GarbageCollection
{
    /// <summary>설계 1: 원소마다 힙 객체. 필드에 참조가 없다.</summary>
    public sealed class EnemyClass
    {
        public Vector3 Position;
        public int Hp;
    }

    /// <summary>설계 2: 값 타입, 참조 필드 없음.</summary>
    public struct EnemyStruct
    {
        public Vector3 Position;
        public int Hp;
    }

    /// <summary>설계 3: 값 타입이지만 참조 필드 하나를 가진다.</summary>
    public struct EnemyStructRef
    {
        public Vector3 Position;
        public int Hp;
        public string Name;
    }
}
