using System;
using UnityEngine;

namespace Labs.SerializationLabs
{
    /// <summary>InlineHolder와 같은 필드를 [SerializeReference]로 선언한 비교군.</summary>
    [AddComponentMenu("")]
    public sealed class RefHolder : MonoBehaviour
    {
        [SerializeReference] public Item a, b, c, d;

        [NonSerialized] public int nonSerialized;
    }
}
