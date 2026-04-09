using System;
using System.Collections.Generic;
using L;
using UnityEngine;
using VContainer;

namespace Arch.Unity.Conversion.Custom
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class Entity : MonoBehaviour, IComponentConverter
    {
        [SerializeField] EntityConversionOptions options;
        [SerializeField] L.ECS.TypeField parent;
        [SerializeField] List<L.ECS.TypeField> aspects;
        [SerializeField] List<L.ECS.ComponentField> components;

        void Awake()
        {
            var entity = EntityConversion.Convert(gameObject, options);
        }

        public void Convert(IEntityConverter converter)
        {
            if (parent.Type != null && parent.Type.IsAssignableFrom(typeof(BaseEntity)))
            {
                var entity = (BaseEntity)Activator.CreateInstance(parent.Type);
                var components = entity.GetComponents();

                foreach (var component in components)
                {
                    Debug.Log(component);
                }
            }

            foreach (var aspect in aspects)
            {
                Debug.Log(aspect + " " + aspect.Type + " " + typeof(IAspect).IsAssignableFrom(aspect.Type));
                if (aspect.Type != null && typeof(IAspect).IsAssignableFrom(aspect.Type))
                {
                    Debug.Log("Aspect");
                    var aspect_obj = (IAspect)Activator.CreateInstance(aspect.Type);
                    foreach (var component in aspect_obj.GetComponentTypeList())
                    {
                        converter.AddComponent(component.GetType(), component);
                    }
                }
            }

            foreach (var component in components)
            {
                var instance = component.GetInstance();

                if (instance != null)
                {
                    converter.AddComponent(component.GetType(), instance);
                }
            }
        }
    }
}