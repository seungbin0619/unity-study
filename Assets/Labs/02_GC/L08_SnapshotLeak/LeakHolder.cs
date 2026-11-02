using System;
using UnityEngine;

namespace Labs.GarbageCollection
{
    /// <summary>02-L08 정적 이벤트. 구독자는 이 델리게이트가 붙잡는다.</summary>
    public static class LeakEvents
    {
        public static event Action Tick;
        public static int SubscriberCount => Tick?.GetInvocationList().Length ?? 0;
    }

    /// <summary>
    /// 매니지드 버퍼(256 KB)와 런타임에 만든 Texture2D(512×512 RGBA32)를 들고, Awake에서 정적 이벤트를 구독한다.
    /// Unsubscribe가 true일 때만 OnDestroy에서 구독을 해제한다.
    /// </summary>
    public sealed class LeakHolder : MonoBehaviour
    {
        public bool Unsubscribe;

        readonly byte[] _buffer = new byte[256 * 1024]; // LAB: 의도적
        Texture2D _texture;

        public byte[] Buffer => _buffer;
        public Texture2D Texture => _texture;

        void Awake()
        {
            _texture = new Texture2D(512, 512, TextureFormat.RGBA32, false) { name = name + "_tex" }; // LAB: 의도적 (Destroy하지 않음)
            LeakEvents.Tick += OnTick;
        }

        void OnDestroy()
        {
            if (Unsubscribe) LeakEvents.Tick -= OnTick;
            // Leaky 그룹은 해제하지 않는다 — LAB: 의도적
        }

        void OnTick() { }
    }
}
