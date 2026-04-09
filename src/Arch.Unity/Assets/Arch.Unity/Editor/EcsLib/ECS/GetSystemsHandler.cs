using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arch.Core;
using Arch.System;
using Arch.Unity.Toolkit;
using VContainer;

class GetSystemsHandler
{
    public static List<T> ResolveTypes<T>(IObjectResolver resolver) where T : BaseSystem<World, SystemState>
    {
        var inheritTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(T)))
            .Select(x=>(T)resolver.Resolve(x))
            .ToList();
        
        return inheritTypes;
    }

    public static List<System.Type> GetTypes<T>() where T : BaseSystem<World, SystemState>
    {
        var inheritTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(T)))
            .ToList();

        return inheritTypes;
    }
}