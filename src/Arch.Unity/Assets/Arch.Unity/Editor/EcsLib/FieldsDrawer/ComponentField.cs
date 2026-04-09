using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace L.ECS
{
    /// <summary>
    /// A serializable container for an ECS component (struct),
    /// allowing it to be stored and edited in the Unity Inspector.
    /// </summary>
    [System.Serializable]
    public class ComponentField
    {
        [SerializeField, HideInInspector] public string ComponentType;
        [SerializeField, HideInInspector] public string ComponentData = "{}";
        [SerializeField, HideInInspector] public List<UnityEngine.Object> ObjectReferences = new List<UnityEngine.Object>();
        [System.NonSerialized] public object Instance;

        public ComponentField()
        {
            
        }

        public ComponentField(Type type)
        {
            ComponentType = type.AssemblyQualifiedName;
        }

        public Type GetType()
        {
            return Type.GetType(ComponentType);
        }

        public object GetInstance()
        {
            if (Instance == null && !string.IsNullOrEmpty(ComponentType))
            {
                Type type = Type.GetType(ComponentType);
                if (type != null)
                {
                    var settings = new JsonSerializerSettings {
                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                        Converters = new List<JsonConverter> { new L.ECS.RuntimeUnityObjectConverter(ObjectReferences) }
                    };
                    Instance = JsonConvert.DeserializeObject(ComponentData, type, settings);
                }
            }

            return Instance;
        }
    }

    /// <summary>
    /// Конвертер для десериализации ссылок на UnityEngine.Object во время выполнения игры.
    /// </summary>
    public class RuntimeUnityObjectConverter : JsonConverter
    {
        private readonly List<UnityEngine.Object> _references;
        public RuntimeUnityObjectConverter(List<UnityEngine.Object> references) { _references = references; }
        public override bool CanConvert(Type objectType) => typeof(UnityEngine.Object).IsAssignableFrom(objectType);
        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.Integer) return null;
            int index = Convert.ToInt32(reader.Value);
            if (index < 0 || _references == null || index >= _references.Count) return null;
            return _references[index];
        }
    }
}