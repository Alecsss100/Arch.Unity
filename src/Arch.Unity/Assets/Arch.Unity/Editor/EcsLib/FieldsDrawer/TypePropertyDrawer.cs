using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using L;
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(L.ECS.TypeField))]
public class TypePropertyDrawer : PropertyDrawer
{
    private static List<Type> _types;
    private static string[] _typeDisplayNames;

    static TypePropertyDrawer()
    {
        // Находим и кэшируем все релевантные типы из пространства имен "F"
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("F") && !t.IsInterface && !t.IsAbstract && !t.IsGenericTypeDefinition)
            .OrderBy(t => t.FullName)
            .ToList();

        var displayNames = new List<string> { "None" };
        displayNames.AddRange(_types.Select(t => t.FullName.Replace('.', '/')));
        _typeDisplayNames = displayNames.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var typeNameProp = property.FindPropertyRelative("_typeName");

        int currentIndex = 0; // По умолчанию "None"
        if (!string.IsNullOrEmpty(typeNameProp.stringValue))
        {
            var currentType = Type.GetType(typeNameProp.stringValue);
            if (currentType != null)
            {
                // Находим индекс в нашем кэшированном списке. +1, так как "None" на 0-й позиции.
                int foundIndex = _types.IndexOf(currentType);
                if (foundIndex != -1)
                {
                    currentIndex = foundIndex + 1;
                }
            }
        }

        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        int newIndex = EditorGUI.Popup(position, currentIndex, _typeDisplayNames);

        if (newIndex != currentIndex)
        {
            typeNameProp.stringValue = (newIndex == 0) ? null : _types[newIndex - 1].AssemblyQualifiedName;
        }

        EditorGUI.EndProperty();
    }
}
#endif