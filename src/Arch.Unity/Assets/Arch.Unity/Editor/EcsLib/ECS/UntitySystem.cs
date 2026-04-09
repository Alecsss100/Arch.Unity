using Arch.Core;
using Arch.System;
using Arch.Unity.Toolkit;

namespace L.ECS
{
    public abstract class UnitySystem : BaseSystem<World, SystemState>
    {
        public int Order {get; set;}

        protected UnitySystem(World world, int order) : base(world)
        {
            Order = order;
        }
    }
}