using System;
using UnityEngine;

namespace Labs.SerializationLabs
{
    /// <summary>자기 타입 필드를 가진 인라인 클래스 (확인 질문: null인 next를 직렬화기가 어떻게 다루나).</summary>
    [Serializable]
    public class Node
    {
        public int value;
        public Node next;
    }

    /// <summary>간접 순환: CycA → CycB → CycA.</summary>
    [Serializable]
    public class CycA
    {
        public int value;
        public CycB b;
    }

    [Serializable]
    public class CycB
    {
        public int value;
        public CycA a;
    }

    /// <summary>컬렉션을 통한 순환 (트리 구조).</summary>
    [Serializable]
    public class TreeNode
    {
        public int value;
        public System.Collections.Generic.List<TreeNode> children;
    }

    [AddComponentMenu("")]
    public sealed class NodeHolder : MonoBehaviour
    {
        public Node head;
        public CycA cyc;
        public TreeNode tree;
    }
}
