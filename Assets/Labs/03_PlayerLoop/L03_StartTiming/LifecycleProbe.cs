using System.Collections.Generic;
using UnityEngine;

namespace Labs.PlayerLoopLabs
{
    /// <summary>
    /// 생성된 뒤 Awake/OnEnable/Start와 첫 FixedUpdate/Update/LateUpdate가 불리는 프레임·위치를 기록한다.
    /// 어느 생성 지점에서 만들어졌는지는 Instantiate 직전에 Spawner가 정적 필드 NextLabel에 적어 둔다
    /// (Awake가 Instantiate 안에서 불릴 수도 있으므로 생성 뒤에 필드를 넣는 방식은 쓸 수 없다).
    /// </summary>
    public sealed class LifecycleProbe : MonoBehaviour
    {
        public struct Event
        {
            public string Label;
            public string Name;
            public int Frame;
            public string Where;
        }

        public static readonly List<Event> Events = new List<Event>();
        public static string NextLabel = "template";
        public static bool InsideInstantiate;

        string _label;
        bool _fixed, _update, _late;

        void Add(string name) => Events.Add(new Event
        {
            Label = _label,
            Name = name,
            Frame = Time.frameCount,
            Where = InsideInstantiate ? "(inside Instantiate)" : LoopMarkers.Where,
        });

        void Awake()
        {
            _label = NextLabel;
            Add("Awake");
        }

        void OnEnable() => Add("OnEnable");
        void Start() => Add("Start");

        void FixedUpdate()
        {
            if (_fixed) return;
            _fixed = true;
            Add("first FixedUpdate");
        }

        void Update()
        {
            if (_update) return;
            _update = true;
            Add("first Update");
        }

        void LateUpdate()
        {
            if (_late) return;
            _late = true;
            Add("first LateUpdate");
        }
    }
}
