using Arch.Core;
using Arch.System;
using Arch.Unity.Toolkit;

namespace L.ECS
{
    public abstract class UnityAlwaysWorkSystem : BaseSystem<World, SystemState>
    {
        public int Order {get; set;}

        protected UnityAlwaysWorkSystem(World world, int order) : base(world)
        {
            Order = order;
        }
    }
}