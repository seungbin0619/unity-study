using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Labs.NativeManaged
{
    /// <summary>
    /// 타입 인자만 다른 List&lt;T&gt;.Add 호출을 메서드 하나씩에 담는다.
    /// il2cppOutput에서 이 메서드 이름으로 호출하는 Add 함수를 찾아, 본문이 몇 벌인지 센다.
    /// </summary>
    public static class GenericTargets
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FillInt(List<int> list, int n)
        {
            list.Clear();
            for (var i = 0; i < n; i++)
                list.Add(i);
            return list.Count;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FillFloat(List<float> list, int n)
        {
            list.Clear();
            for (var i = 0; i < n; i++)
                list.Add(i);
            return list.Count;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FillVector3(List<Vector3> list, int n)
        {
            list.Clear();
            var v = Vector3.one;
            for (var i = 0; i < n; i++)
                list.Add(v);
            return list.Count;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FillString(List<string> list, string item, int n)
        {
            list.Clear();
            for (var i = 0; i < n; i++)
                list.Add(item);
            return list.Count;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int FillGameObject(List<GameObject> list, GameObject item, int n)
        {
            list.Clear();
            for (var i = 0; i < n; i++)
                list.Add(item);
            return list.Count;
        }
    }
}
