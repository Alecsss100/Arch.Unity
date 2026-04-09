using Arch.Core;

namespace Arch.Unity.Conversion
{
    public interface IEntityConverter
    {
        void AddComponent(Type type, object component);
        void AddComponent<T>(T component);
        Entity Convert(World world);
    }
}
