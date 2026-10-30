using UnityEngine;

namespace Labs.NativeManaged
{
    // Unity 블로그 "IL2CPP optimizations: Devirtualization"(2016, Unity 5.3.5)의 예제를 그대로 옮긴 것.
    // 차이: 블로그의 Cow는 sealed 전/후를 따로 빌드했고, 여기서는 한 빌드에서 비교하려고
    // 봉인하지 않은 OpenCow를 "sealed 전" 대조군으로 함께 둔다.
    public abstract class FarmAnimal
    {
        public abstract string Speak();
    }

    public sealed class Cow : FarmAnimal
    {
        public override string Speak()
        {
            return "Moo";
        }
    }

    public class Pig : FarmAnimal
    {
        public override string Speak()
        {
            return "Oink";
        }
    }

    public class OpenCow : FarmAnimal
    {
        public override string Speak()
        {
            return "Moo";
        }
    }

    public class Farm : MonoBehaviour
    {
        void Start()
        {
            FarmAnimal[] animals = new FarmAnimal[] { new Cow(), new Pig() };
            foreach (var animal in animals)
                Debug.LogFormat("Some animal says '{0}'", animal.Speak());

            var cow = new Cow();
            Debug.LogFormat("The cow says '{0}'", cow.Speak());

            var openCow = new OpenCow();
            Debug.LogFormat("The open cow says '{0}'", openCow.Speak());
        }
    }
}
