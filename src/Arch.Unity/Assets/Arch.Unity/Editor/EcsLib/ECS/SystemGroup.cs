using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.Unity.Toolkit;

namespace L.ECS
{
    public class SystemGroup<T> : Group<T>
    {
        public SystemGroup(string name, World world, List<BaseSystem<World, SystemState>> systems) : base(name)
        {
            foreach (var system in systems) 
            {
                Add((ISystem<T>)system);
            }
        }
    }
}