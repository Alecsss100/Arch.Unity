using Arch.Core;
using Arch.System;
using Arch.Unity.Toolkit;

namespace L.ECS
{
    public abstract class UnityFixedSystem : BaseSystem<World, SystemState>
    {
        public int Order {get; set;}

        protected UnityFixedSystem(World world, int order) : base(world)
        {
            Order = order;
        }
    }
}