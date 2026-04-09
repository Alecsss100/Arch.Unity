using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if UNITY_EDITOR

/// <summary>
/// Кастомный PropertyDrawer, который отображает строковое поле с атрибутом [AddressableKey]
/// в виде выпадающего списка со всеми ключами из сгенерированного класса A.AddressableKeys.
/// </summary>
[CustomPropertyDrawer(typeof(AddressableKeyAttribute))]
public class AddressableKeyDrawer : PropertyDrawer
{
    private static List<string> _keys;
    private static string[] _displayNames;

    static AddressableKeyDrawer()
    {
        _keys = new List<string> { "" }; // "None"
        var display = new List<string> { "None" };

        var keysClass = Type.GetType("A.AddressableKeys, Assembly-CSharp");
        if (keysClass != null)
        {
            var groupClasses = keysClass.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
            foreach (var groupClass in groupClasses.OrderBy(t => t.Name))
            {
                var fields = groupClass.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                       .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));
                foreach (var field in fields.OrderBy(f => f.Name))
                {
                    string key = (string)field.GetValue(null);
                    _keys.Add(key);
                    // Создаем красивое имя для отображения, например "Prefabs/Player"
                    display.Add($"{groupClass.Name}/{field.Name}");
                }
            }
        }
        _displayNames = display.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [AddressableKey] with strings only.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        int currentIndex = _keys.IndexOf(property.stringValue);
        if (currentIndex == -1) currentIndex = 0; // Если ключ не найден (например, был удален), сбрасываем на "None"

        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, _displayNames);

        if (newIndex != currentIndex)
        {
            property.stringValue = _keys[newIndex];
        }

        EditorGUI.EndProperty();
    }
}
#endif