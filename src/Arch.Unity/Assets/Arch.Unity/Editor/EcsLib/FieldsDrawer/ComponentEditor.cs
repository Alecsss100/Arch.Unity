using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using L;

#if UNITY_EDITOR
/// <summary>
/// A custom floating inspector window.
/// </summary>
public class InspectorPopup : EditorWindow
{
    [SerializeField] private UnityEngine.Object _target;
    private Editor _mainEditor;
    private readonly List<Editor> _componentEditors = new List<Editor>();
    private readonly Dictionary<UnityEngine.Object, bool> _foldoutStates = new Dictionary<UnityEngine.Object, bool>();
    private Vector2 _scrollPosition;

    /// <summary>
    /// Opens a new inspector window for the given target object.
    /// </summary>
    public static void Open(UnityEngine.Object target)
    {
        var window = CreateInstance<InspectorPopup>();
        window.titleContent = new GUIContent(target.name);
        window._target = target;
        // When OnEnable is called, _target will be set, and we can create the editor.
        window.Show();
    }

    private void OnEnable()
    {
        // OnEnable вызывается при перезагрузке скриптов. Если _target был сериализован, мы можем воссоздать редактор.
        CreateTargetEditor();
    }

    private void OnGUI()
    {
        // Если редактор не был создан в OnEnable (например, при первом открытии), создаем его сейчас.
        if (_target != null && _mainEditor == null && _componentEditors.Count == 0)
        {
            CreateTargetEditor();
        }

        if (_target == null)
        {
            EditorGUILayout.LabelField("No object selected or object was destroyed.");
            // Закрываем окно, только если оно в фокусе и цель потеряна, чтобы избежать закрытия при перезагрузке скриптов.
            if (_target == null && focusedWindow == this) Close();
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_mainEditor != null)
        {
            // Отрисовываем главный редактор (для ScriptableObject)
            _mainEditor.OnInspectorGUI();
        }

        // Если это GameObject, отрисовываем редакторы для всех его компонентов.
        if (_target is GameObject)
        {
            foreach (var componentEditor in _componentEditors)
            {
                if (componentEditor != null && componentEditor.target != null)
                {
                    EditorGUILayout.Separator();

                    // Получаем или инициализируем состояние сворачивания для компонента
                    if (!_foldoutStates.ContainsKey(componentEditor.target))
                    {
                        _foldoutStates[componentEditor.target] = true;
                    }

                    bool foldout = _foldoutStates[componentEditor.target];
                    // Отрисовываем стандартный заголовок инспектора для компонента
                    foldout = EditorGUILayout.InspectorTitlebar(foldout, componentEditor.target);
                    _foldoutStates[componentEditor.target] = foldout;

                    // Если заголовок развернут, отрисовываем содержимое инспектора компонента
                    if (foldout)
                    {
                        componentEditor.OnInspectorGUI();
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void OnDestroy()
    {
        if (_mainEditor != null)
        {
            DestroyImmediate(_mainEditor);
        }
        foreach (var componentEditor in _componentEditors)
        {
            if (componentEditor != null)
            {
                DestroyImmediate(componentEditor);
            }
        }
    }

    private void CreateTargetEditor()
    {
        if (_target == null) return;
        
        // Перед созданием нового редактора, уничтожаем старый, если он существует.
        if (_mainEditor != null)
        {
            DestroyImmediate(_mainEditor);
            _mainEditor = null;
        }
        foreach (var componentEditor in _componentEditors)
        {
            if (componentEditor != null) DestroyImmediate(componentEditor);
        }
        _componentEditors.Clear();
        _foldoutStates.Clear();

        if (_target is GameObject go)
        {
            // Для GameObject мы не создаем "главный" редактор, так как его поведение в кастомных окнах нестабильно.
            // Вместо этого мы просто создаем редакторы для каждого компонента по отдельности.
            // Это обеспечивает одинаковое и предсказуемое поведение и для префабов, и для объектов со сцены.
            foreach (var component in go.GetComponents<Component>())
            {
                if (component != null) // Защита от "битых" скриптов
                {
                    _componentEditors.Add(Editor.CreateEditor(component));
                }
            }
        }
        else // Для всех остальных ассетов (например, ScriptableObject)
        {
            _mainEditor = Editor.CreateEditor(_target);
        }
    }
}

[CustomPropertyDrawer(typeof(L.ECS.ComponentField))]
public class ComponentEditor : PropertyDrawer
{
    /// <summary>
    /// Кастомный JsonConverter для правильной сериализации и десериализации UnityEngine.Object в редакторе.
    /// Он не сохраняет сам объект, а записывает его индекс в отдельный список ObjectReferences.
    /// </summary>
    public class UnityObjectConverter : JsonConverter
    {
        private readonly SerializedProperty _objectReferencesProp;

        public UnityObjectConverter(SerializedProperty objectReferencesProp)
        {
            _objectReferencesProp = objectReferencesProp;
        }

        public override bool CanConvert(Type objectType) => typeof(UnityEngine.Object).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var obj = (UnityEngine.Object)value;
            if (obj == null)
            {
                writer.WriteNull();
                return;
            }

            int index = -1;
            for (int i = 0; i < _objectReferencesProp.arraySize; i++)
            {
                if (_objectReferencesProp.GetArrayElementAtIndex(i).objectReferenceValue == obj)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                index = _objectReferencesProp.arraySize;
                _objectReferencesProp.InsertArrayElementAtIndex(index);
                _objectReferencesProp.GetArrayElementAtIndex(index).objectReferenceValue = obj;
            }

            writer.WriteValue(index);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException($"Unexpected token type: {reader.TokenType}. Expected Integer.");
            
            int index = Convert.ToInt32(reader.Value);

            if (index < 0 || index >= _objectReferencesProp.arraySize)
            {
                Debug.LogWarning($"[ComponentEditor] Found an invalid object reference index '{index}'. The list has {_objectReferencesProp.arraySize} elements. This can happen if data is out of sync. Returning null.");
                return null;
            }
            return _objectReferencesProp.GetArrayElementAtIndex(index).objectReferenceValue;
        }
    }

    // Статические кэши для повышения производительности
    private static readonly List<Type> _componentTypes;
    private static readonly string[] _componentTypeNames;
    private static readonly Dictionary<string, object> _instanceCache = new Dictionary<string, object>();
    private static readonly List<string> _addressableKeys;
    private static readonly string[] _addressableDisplayNames;
    private static bool _isDirty;

    static ComponentEditor()
    {
        _componentTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsValueType && !type.IsEnum && !type.IsPrimitive && type.IsPublic && !type.IsGenericType && !type.IsAbstract
                           && (type.Namespace?.Split('.').FirstOrDefault() == "L" || type.Namespace?.Split('.').FirstOrDefault() == "F"))
            .OrderBy(type => type.FullName)
            .ToList();

        var displayNames = new List<string> { "Select a component type..." };
        displayNames.AddRange(_componentTypes.Select(t => t.FullName.Replace('.', '/')));
        _componentTypeNames = displayNames.ToArray();

        // Собираем ключи Addressables для кастомного дропдауна
        _addressableKeys = new List<string> { "" }; // "None"
        var addressableDisplay = new List<string> { "None" };

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
                    _addressableKeys.Add(key);
                    // Создаем красивое имя для отображения, например "Prefabs/Player"
                    addressableDisplay.Add($"{groupClass.Name}/{field.Name}");
                }
            }
        }
        _addressableDisplayNames = addressableDisplay.ToArray();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        var componentTypeProp = property.FindPropertyRelative("ComponentType");

        if (!string.IsNullOrEmpty(componentTypeProp.stringValue) && property.isExpanded)
        {
            object instance = GetInstance(property);
            if (instance != null)
            {
                height += GetFieldsHeight(instance, property.propertyPath);
            }
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        _isDirty = false;

        var componentTypeProp = property.FindPropertyRelative("ComponentType");
        var componentDataProp = property.FindPropertyRelative("ComponentData");

        var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        if (string.IsNullOrEmpty(componentTypeProp.stringValue))
        {
            DrawUninitializedComponentSelector(lineRect, property);
        }
        else
        {
            var foldoutRect = new Rect(lineRect.x, lineRect.y, lineRect.width - 70, lineRect.height);
            var buttonRect = new Rect(foldoutRect.xMax + 5, lineRect.y, 65, lineRect.height);

            string componentName = Type.GetType(componentTypeProp.stringValue)?.Name ?? "Invalid Component";
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, componentName, true);

            if (GUI.Button(buttonRect, "Remove"))
            {
                componentTypeProp.stringValue = string.Empty;
                componentDataProp.stringValue = "{}";
                string cacheKey = $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
                _instanceCache.Remove(cacheKey);
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI(); // Выход, чтобы инспектор корректно перерисовался
            }

            if (property.isExpanded)
            {
                object instance = GetInstance(property);
                if (instance == null)
                {
                    var errorRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight * 2);
                    EditorGUI.HelpBox(errorRect, $"Component type '{componentTypeProp.stringValue}' not found or failed to deserialize.", MessageType.Error);
                }
                else
                {
                    var contentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.height);
                    EditorGUI.indentLevel++;
                    DrawFields(contentRect, instance, property.propertyPath, property);
                    EditorGUI.indentLevel--;
                }
            }
        }

        if (_isDirty)
        {
            object instance = GetInstance(property);
            if (instance != null)
            {
                var objectRefsProp = property.FindPropertyRelative("ObjectReferences");
                objectRefsProp.ClearArray();

                var settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter> { new UnityObjectConverter(objectRefsProp) }
                };

                componentDataProp.stringValue = JsonConvert.SerializeObject(instance, settings);
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUI.EndProperty();
    }

    private object GetInstance(SerializedProperty property)
    {
        // Ключ для кэша должен быть глобально уникальным, а не только в пределах одного объекта.
        string path = $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
        var componentTypeProp = property.FindPropertyRelative("ComponentType");
        var componentDataProp = property.FindPropertyRelative("ComponentData");

        if (_instanceCache.TryGetValue(path, out object cachedInstance) && cachedInstance.GetType().AssemblyQualifiedName == componentTypeProp.stringValue)
        {
            return cachedInstance;
        }

        Type type = Type.GetType(componentTypeProp.stringValue);
        if (type != null)
        {
            try
            {
                var objectRefsProp = property.FindPropertyRelative("ObjectReferences");
                var settings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter> { new UnityObjectConverter(objectRefsProp) }
                };
                object instance = JsonConvert.DeserializeObject(componentDataProp.stringValue, type, settings);
                _instanceCache[path] = instance;
                return instance;
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка десериализации компонента {type.Name}: {e.Message}");
                /* Deserialization failed, will show error */
            }
        }

        _instanceCache.Remove(path);
        return null;
    }

    private static void DrawUninitializedComponentSelector(Rect position, SerializedProperty property)
    {
        var componentTypeProp = property.FindPropertyRelative("ComponentType");
        var componentDataProp = property.FindPropertyRelative("ComponentData");

        int newSelectedIndex = EditorGUI.Popup(position, 0, _componentTypeNames);

        if (newSelectedIndex > 0)
        {
            var selectedType = _componentTypes[newSelectedIndex - 1];
            var newInstance = Activator.CreateInstance(selectedType);

            componentTypeProp.stringValue = newInstance.GetType().AssemblyQualifiedName;
            // Сериализуем пустой объект, чтобы инициализировать данные
            var objectRefsProp = property.FindPropertyRelative("ObjectReferences");
            objectRefsProp.ClearArray();
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Converters = new List<JsonConverter> { new UnityObjectConverter(objectRefsProp) }
            };
            componentDataProp.stringValue = JsonConvert.SerializeObject(newInstance, settings);

            property.serializedObject.ApplyModifiedProperties();
        }
    }

    private static float GetFieldsHeight(object obj, string path)
    {
        if (obj == null) return 0;
        float height = 0;
        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            height += GetFieldHeight(field.FieldType, field.GetValue(obj), path + "." + field.Name);
        }
        return height;
    }

    private static void DrawFields(Rect position, object container, string path, SerializedProperty property)
    {
        if (container == null) return;
        var fields = container.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var currentRect = new Rect(position.x, position.y, position.width, 0);

        foreach (var field in fields)
        {
            string fieldPath = path + "." + field.Name;
            object value = field.GetValue(container);
            float fieldHeight = GetFieldHeight(field.FieldType, value, fieldPath);
            currentRect.height = fieldHeight;

            object newValue = DrawField(currentRect, ObjectNames.NicifyVariableName(field.Name), value, field.FieldType, fieldPath, property, field);

            if (!Equals(value, newValue))
            {
                field.SetValue(container, newValue);
                _isDirty = true;
            }
            currentRect.y += fieldHeight;
        }
    }

    private static float GetFieldHeight(Type type, object value, string path)
    {
        if (type.IsArray) return GetArrayHeight((Array)value, type, path);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return GetListHeight((System.Collections.IList)value, type, path);
        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum) return GetStructHeight(value, type, path);
        return EditorGUIUtility.singleLineHeight;
    }

    private static object DrawField(Rect position, string label, object value, Type type, string path, SerializedProperty property, FieldInfo field)
    {
        // Проверяем наличие атрибута [AddressableKey]
        if (field != null && field.IsDefined(typeof(AddressableKeyAttribute), false))
        {
            if (type == typeof(string))
            {
                string currentValue = (string)value ?? "";
                int currentIndex = _addressableKeys.IndexOf(currentValue);
                if (currentIndex == -1) currentIndex = 0; // Если ключ не найден, сбрасываем на "None"

                int newIndex = EditorGUI.Popup(position, label, currentIndex, _addressableDisplayNames);

                if (newIndex != currentIndex)
                {
                    return _addressableKeys[newIndex];
                }
                return currentValue;
            }
        }

        if (type == typeof(int)) return EditorGUI.IntField(position, label, (int)(value ?? 0));
        if (type == typeof(float)) return EditorGUI.FloatField(position, label, (float)(value ?? 0f));
        if (type == typeof(string)) return EditorGUI.TextField(position, label, (string)value);
        if (type == typeof(bool)) return EditorGUI.Toggle(position, label, (bool)(value ?? false));
        if (type == typeof(Vector2)) return EditorGUI.Vector2Field(position, label, (Vector2)(value ?? Vector2.zero));
        if (type == typeof(Vector3)) return EditorGUI.Vector3Field(position, label, (Vector3)(value ?? Vector3.zero));
        if (type == typeof(Color)) return EditorGUI.ColorField(position, label, (Color)(value ?? Color.white));
        if (type.IsEnum) return EditorGUI.EnumPopup(position, label, (Enum)(value ?? Activator.CreateInstance(type)));
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            var objValue = (UnityEngine.Object)value;
            var fieldRect = position;
            
            if (objValue != null)
            {
                fieldRect.width -= 24;
                var buttonRect = new Rect(fieldRect.xMax + 4, position.y, 20, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, "->"))
                {
                    InspectInNewWindow(objValue);
                }
            }
            bool allowSceneObjects = property.serializedObject.targetObject is MonoBehaviour;
            return EditorGUI.ObjectField(fieldRect, label, objValue, type, allowSceneObjects);
        }
        if (type.IsArray) return DrawArray(position, label, (Array)value, type, path, property);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return DrawList(position, label, (System.Collections.IList)value, type, path, property);
        if (type.IsValueType && !type.IsPrimitive) return DrawStruct(position, label, value ?? Activator.CreateInstance(type), type, path, property);

        EditorGUI.LabelField(position, label, $"Unsupported type: {type.Name}");
        return value;
    }

    private static float GetStructHeight(object structValue, Type type, string path)
    {
        bool isExpanded = SessionState.GetBool(path, true);
        float height = EditorGUIUtility.singleLineHeight;
        if (isExpanded) height += GetFieldsHeight(structValue, path);
        return height;
    }

    private static object DrawStruct(Rect position, string label, object structValue, Type type, string path, SerializedProperty property)
    {
        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        bool isExpanded = SessionState.GetBool(path, true);
        isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, label, true);
        SessionState.SetBool(path, isExpanded);

        if (isExpanded)
        {
            EditorGUI.indentLevel++;
            var contentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.height);
            DrawFields(contentRect, structValue, path, property);
            EditorGUI.indentLevel--;
        }
        return structValue;
    }

    private static float GetListHeight(System.Collections.IList list, Type listType, string path)
    {
        bool isExpanded = SessionState.GetBool(path, false);
        float height = EditorGUIUtility.singleLineHeight;
        if (isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight; // For Size field
            if (list != null)
            {
                var elementType = listType.GetGenericArguments()[0];
                for (int i = 0; i < list.Count; i++)
                {
                    height += GetFieldHeight(elementType, list[i], path + $"[{i}]");
                }
            }
        }
        return height;
    }

    private static object DrawList(Rect position, string label, System.Collections.IList list, Type listType, string path, SerializedProperty property)
    {
        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        bool isExpanded = SessionState.GetBool(path, false);
        isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, $"{label} ({list?.Count ?? 0})", true);
        SessionState.SetBool(path, isExpanded);

        if (!isExpanded) return list;

        EditorGUI.indentLevel++;
        var currentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
        
        int newSize = EditorGUI.IntField(currentRect, "Size", list?.Count ?? 0);
        if (list == null && newSize > 0)
        {
            list = (System.Collections.IList)Activator.CreateInstance(listType);
            _isDirty = true;
        }

        if (list != null && newSize != list.Count)
        {
            var elementType = listType.GetGenericArguments()[0];
            while (newSize > list.Count) list.Add(elementType.IsValueType ? Activator.CreateInstance(elementType) : null);
            while (newSize < list.Count) list.RemoveAt(list.Count - 1);
            _isDirty = true;
        }
        currentRect.y += EditorGUIUtility.singleLineHeight;

        if (list != null)
        {
            var elementType = listType.GetGenericArguments()[0];
            for (int i = 0; i < list.Count; i++)
            {
                string elementPath = path + $"[{i}]";
                float fieldHeight = GetFieldHeight(elementType, list[i], elementPath);
                currentRect.height = fieldHeight;
                object newValue = DrawField(currentRect, $"Element {i}", list[i], elementType, elementPath, property, null);
                if (!Equals(list[i], newValue))
                {
                    list[i] = newValue;
                    _isDirty = true;
                }
                currentRect.y += fieldHeight;
            }
        }
        
        EditorGUI.indentLevel--;
        return list;
    }

    // Аналогичные методы для Array
    private static float GetArrayHeight(Array array, Type arrayType, string path) => GetListHeight(array, arrayType, path);
    private static object DrawArray(Rect position, string label, Array array, Type arrayType, string path, SerializedProperty property)
    {
        var list = DrawList(position, label, array, arrayType, path, property) as System.Collections.IList;
        if (list is Array) return list; // No change

        // If size changed, a List might be returned, convert it back to array
        if (list != null)
        {
            var elementType = arrayType.GetElementType();
            Array newArray = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(newArray, 0);
            return newArray;
        }
        return null;
    }
    
    /// <summary>
    /// Открывает новое окно инспектора, заблокированное на указанном объекте.
    /// </summary>
    private static void InspectInNewWindow(UnityEngine.Object target)
    {
        // Открываем наше кастомное плавающее окно инспектора.
        InspectorPopup.Open(target);
    }
}

#endif